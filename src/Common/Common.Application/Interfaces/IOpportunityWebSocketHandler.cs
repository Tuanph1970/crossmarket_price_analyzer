using System.Net.WebSockets;

namespace Common.Application.Interfaces;

/// <summary>
/// Public interface for broadcasting opportunity score updates over WebSocket.
/// Implemented by <see cref="OpportunityWebSocketHandler"/> in ScoringService.Infrastructure.
/// </summary>
public interface IOpportunityWebSocketHandler
{
    /// <summary>Broadcasts a typed payload to all connected clients.</summary>
    Task BroadcastAsync<T>(OpportunityBroadcast<T> message, CancellationToken ct = default);

    /// <summary>Registers a new WebSocket connection and returns its connection ID.</summary>
    Guid AddConnection(WebSocket socket);

    /// <summary>Removes a connection by ID.</summary>
    void RemoveConnection(Guid id);

    /// <summary>Returns the state of every tracked connection.</summary>
    IReadOnlyDictionary<Guid, WebSocketState> GetConnectionStates();

    /// <summary>Returns the total number of tracked connections.</summary>
    int ConnectionCount { get; }
}

/// <summary>
/// Typed broadcast envelope sent over WebSocket to all connected clients.
/// </summary>
/// <typeparam name="T">Payload type — OpportunitySnapshotDto or ScoreDeltaDto</typeparam>
public record OpportunityBroadcast<T>(
    string Type,
    T Payload,
    DateTime TimestampUtc
);

/// <summary>
/// Full top-20 snapshot sent on initial connect and after recalculation.
/// </summary>
public record OpportunitySnapshotDto(
    IReadOnlyList<SnapshotItemDto> TopOpportunities,
    DateTime GeneratedAt
);

/// <summary>
/// Single opportunity item within an opportunity snapshot.
/// </summary>
public record SnapshotItemDto(
    Guid MatchId,
    decimal CompositeScore,
    decimal ProfitMarginPct,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    decimal LandedCostVnd,
    decimal VietnamRetailVnd
);

/// <summary>
/// Delta update payload sent when a single score changes.
/// </summary>
public record ScoreDeltaDto(
    Guid MatchId,
    decimal OldScore,
    decimal NewScore,
    decimal CompositeScore,
    decimal ProfitMarginPct
);
