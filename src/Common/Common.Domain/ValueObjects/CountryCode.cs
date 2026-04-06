namespace Common.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing an ISO 3166-1 alpha-2 country code.
/// </summary>
public sealed record CountryCode
{
    public string Code { get; init; } = string.Empty;

    public CountryCode() { }

    public CountryCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Country code cannot be empty.", nameof(code));

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length != 2)
            throw new ArgumentException(
                "Country code must be a 2-letter ISO code (e.g., 'US', 'VN').", nameof(code));

        Code = normalized;
    }

    public static CountryCode US => new("US");
    public static CountryCode VN => new("VN");

    public override string ToString() => Code;
}
