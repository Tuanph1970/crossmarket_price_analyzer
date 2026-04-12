namespace CrossMarket.SharedKernel;

/// <summary>
/// BCrypt password hasher — thread-safe, uses work factor 12.
/// </summary>
public sealed class PasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>Hashes a plain-text password using BCrypt with work factor 12.</summary>
    public string HashPassword(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        return BCrypt.Net.BCrypt.HashPassword(plainText, WorkFactor);
    }

    /// <summary>Verifies a plain-text password against a BCrypt hash. Always returns false on null input.</summary>
    public bool VerifyPassword(string plainText, string hash)
    {
        if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(hash))
            return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(plainText, hash);
        }
        catch
        {
            return false;
        }
    }
}
