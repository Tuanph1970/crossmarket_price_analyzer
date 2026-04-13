using System.Net.WebSockets;
using System.Text;
using Common.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using ScoringService.Infrastructure.WebSockets;

namespace ScoringService.Api.WebSockets;

/// <summary>
/// ASP.NET Core middleware that upgrades HTTP requests at /ws/opportunities
/// to WebSocket connections and hands them off to OpportunityWebSocketHandler.
/// Supports both binary and text frames; client sends nothing — server pushes only.
/// </summary>
public sealed class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(RequestDelegate next, ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOpportunityWebSocketHandler handler)
    {
        // Only intercept /ws/opportunities
        if (!context.WebSockets.IsWebSocketRequest ||
            context.Request.Path != "/ws/opportunities")
        {
            await _next(context);
            return;
        }

        WebSocket? socket = null;
        Guid connectionId;

        try
        {
            // Perform the WebSocket upgrade
            socket = await context.WebSockets.AcceptWebSocketAsync(subprotocol: null);
            connectionId = handler.AddConnection(socket);

            _logger.LogInformation(
                "WebSocket handshake complete for connection {ConnectionId} from {RemoteIp}",
                connectionId, context.Connection.RemoteIpAddress);

            // Send initial top-20 snapshot immediately on connect
            await SendInitialSnapshotAsync(context.RequestServices, handler, connectionId, CancellationToken.None);

            // Receive loop — client sends nothing, but we handle close frames gracefully
            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open)
            {
                try
                {
                    var receiveResult = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Close:
                            _logger.LogDebug(
                                "Client initiated close: {CloseStatus} {Description}",
                                receiveResult.CloseStatus, receiveResult.CloseStatusDescription);
                            goto ExitLoop;

                        case WebSocketMessageType.Text:
                        case WebSocketMessageType.Binary:
                            // Client should not send data — ignore and stay open
                            break;
                    }
                }
                catch (WebSocketException ex) when (
                    ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely ||
                    ex.WebSocketErrorCode == WebSocketError.InvalidState)
                {
                    _logger.LogDebug("WebSocket connection {ConnectionId} closed prematurely", connectionId);
                    goto ExitLoop;
                }
            }

        ExitLoop:
            _logger.LogInformation(
                "WebSocket connection {ConnectionId} ended (state: {State})",
                connectionId, socket?.State);

            // Clean handshake close
            if (socket?.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
                catch { /* best-effort */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error in WebSocket middleware for connection {ConnectionId}",
                connectionId);
        }
        finally
        {
            if (socket != null)
            {
                handler.RemoveConnection(connectionId);
                socket.Dispose();
            }
        }
    }

    /// <summary>
    /// Queries the current top-20 opportunities from the database and sends
    /// the initial snapshot to the newly connected client.
    /// </summary>
    private static async Task SendInitialSnapshotAsync(
        IServiceProvider services,
        IOpportunityWebSocketHandler handler,
        Guid connectionId,
        CancellationToken ct)
    {
        try
        {
            using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<ScoringService.Application.Persistence.ScoringDbContext>();

            var top20 = await db.OpportunityScores
                .OrderByDescending(s => s.CompositeScore)
                .Take(20)
                .Select(s => new SnapshotItemDto(
                    s.MatchId,
                    s.CompositeScore,
                    s.ProfitMarginPct,
                    s.DemandScore,
                    s.CompetitionScore,
                    s.PriceStabilityScore,
                    s.MatchConfidenceScore,
                    s.LandedCostVnd,
                    s.VietnamRetailVnd))
                .ToListAsync(ct);

            var snapshot = new OpportunitySnapshotDto(top20, DateTime.UtcNow);
            var broadcast = new OpportunityBroadcast<OpportunitySnapshotDto>(
                Type: "full_snapshot",
                Payload: snapshot,
                TimestampUtc: DateTime.UtcNow);

            await handler.BroadcastAsync(broadcast, ct);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<WebSocketMiddleware>>();
            logger.LogWarning(ex, "Failed to send initial snapshot to connection {ConnectionId}", connectionId);
        }
    }
}

/// <summary>
/// Extension methods to wire WebSocketMiddleware into the ASP.NET Core pipeline.
/// </summary>
public static class WebSocketMiddlewareExtensions
{
    /// <summary>
    /// Adds the WebSocket middleware to the pipeline, mapping /ws/opportunities.
    /// </summary>
    public static IApplicationBuilder UseOpportunityWebSockets(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WebSocketMiddleware>();
    }
}
