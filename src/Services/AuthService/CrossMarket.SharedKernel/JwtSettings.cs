namespace CrossMarket.SharedKernel;

/// <summary>
/// JWT token settings — bound from appsettings.json "Jwt" section.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionKey = "Jwt";

    public string Issuer { get; set; } = "CrossMarketAnalyzer";
    public string Audience { get; set; } = "CrossMarketUsers";
    public string SecretKey { get; set; } = string.Empty; // Set via env var — REQUIRED
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
