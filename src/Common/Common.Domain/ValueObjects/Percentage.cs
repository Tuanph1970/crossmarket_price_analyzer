namespace Common.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a percentage (0–100 scale).
/// </summary>
public sealed record Percentage
{
    /// <summary>Value on the 0–100 scale (e.g., 25.5 means 25.5%).</summary>
    public decimal Value { get; init; }

    public Percentage() { }

    public Percentage(decimal value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentException("Percentage must be between 0 and 100.", nameof(value));

        Value = Math.Round(value, 4);
    }

    /// <summary>Returns the value on a 0–1 decimal scale (e.g., 25.5% → 0.255).</summary>
    public decimal ToDecimal() => Value / 100m;

    /// <summary>Creates a Percentage from a decimal value on the 0–1 scale.</summary>
    public static Percentage FromDecimal(decimal decimalValue)
        => new(decimalValue * 100m);

    public static Percentage Zero => new(0);
    public static Percentage Hundred => new(100);

    public override string ToString() => $"{Value:N2}%";
}
