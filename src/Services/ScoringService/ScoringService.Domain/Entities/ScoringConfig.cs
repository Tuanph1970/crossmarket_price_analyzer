using Common.Domain.Entities;

namespace ScoringService.Domain.Entities;

/// <summary>
/// Entity: stores configurable scoring factor weights.
/// </summary>
public class ScoringConfig : BaseEntity<Guid>
{
    public string FactorKey { get; set; } = string.Empty;
    public decimal Weight { get; set; }        // Default: sums to 100%
    public decimal MinThreshold { get; set; }
    public decimal MaxThreshold { get; set; }
    public bool IsActive { get; set; } = true;
}
