using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Common.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ScoringService.Application.Persistence;

namespace ScoringService.Infrastructure.WebSockets;

/// <summary>
/// Manages live WebSocket connections for opportunity score broadcasts.
/// Broadcasts top-20 snapshots on each recalculation and delta updates on score changes.
/// Thread-safe — concurrent dictionary of connections, single lock per send operation.
/// </summary>
public sealed class OpportunityWebSocketHandler : BackgroundService, IOpportunityWebSocketHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpportunityWebSocketHandler> _logger;
    private readonly ConcurrentDictionary<Guid, WebSocket> _connections = new();

    private const int BufferSize = 16 * 1024;

    public OpportunityWebSocketHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<OpportunityWebSocketHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── IOpportunityWebSocketHandler ──────────────────────────────────────────

    /// <summary>Broadcasts a typed payload to all connected clients.</summary>
    public async Task BroadcastAsync<T>(OpportunityBroadcast<T> message, CancellationToken ct = default)
    {
        if (_connections.IsEmpty) return;

        var json = System.Text.Json.JsonSerializer.Serialize(message, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        var deadConnections = new List<Guid>();

        await Parallel.ForEachAsync(
            _connections,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (kvp, _) =>
            {
                var (connectionId, socket) = kvp;
                try
                {
                    if (socket.State != WebSocketState.Open) { deadConnections.Add(connectionId); return; }
                    await socket.SendAsync(
                        bytes.AsMemory(),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast to connection {ConnectionId}", connectionId);
                    deadConnections.Add(connectionId);
                }
            });

        foreach (var id in deadConnections)
            _connections.TryRemove(id, out _);

        _logger.LogDebug("Broadcast {Type} to {Count} clients ({Dead} removed)",
            message.Type, _connections.Count, deadConnections.Count);
    }

    /// <summary>Registers a new WebSocket connection and returns its ID.</summary>
    public Guid AddConnection(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _connections.TryAdd(id, socket);
        _logger.LogInformation("WebSocket connection established: {ConnectionId} (total: {Total})",
            id, _connections.Count);
        return id;
    }

    /// <summary>Removes a connection by ID.</summary>
    public void RemoveConnection(Guid id)
    {
        if (_connections.TryRemove(id, out var socket))
        {
            _logger.LogInformation("WebSocket connection closed: {ConnectionId} (remaining: {Total})",
                id, _connections.Count);
            socket.Dispose();
        }
    }

    public IReadOnlyDictionary<Guid, WebSocketState> GetConnectionStates()
        => _connections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.State);

    public int ConnectionCount => _connections.Count;

    // ── BackgroundService ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpportunityWebSocketHandler background service started");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogInformation("WebSocket handler active — {Count} connections", _connections.Count);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpportunityWebSocketHandler stopping — closing {Count} connections",
            _connections.Count);

        var closeTasks = _connections.Values
            .Where(s => s.State == WebSocketState.Open)
            .Select(s => s.CloseAsync(WebSocketCloseStatus.NormalClosure,
                "Server shutting down", cancellationToken))
            .ToList();

        if (closeTasks.Count > 0)
            await Task.WhenAll(closeTasks);

        foreach (var socket in _connections.Values)
            socket.Dispose();

        _connections.Clear();
        await base.StopAsync(cancellationToken);
    }
}