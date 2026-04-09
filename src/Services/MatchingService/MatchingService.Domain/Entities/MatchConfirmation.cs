using Common.Domain.Entities;
using Common.Domain.Enums;

namespace MatchingService.Domain.Entities;

public class MatchConfirmation : BaseEntity<Guid>
{
    public Guid MatchId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ConfirmAction Action { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Factory method — creates a new MatchConfirmation with auto-generated Id.
    /// </summary>
    public static MatchConfirmation Create(
        Guid matchId, string userId, ConfirmAction action, string? notes = null)
    {
        return new MatchConfirmation
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            UserId = userId,
            Action = action,
            Notes = notes
        };
    }
}

public enum ConfirmAction
{
    Confirmed,
    Rejected
}