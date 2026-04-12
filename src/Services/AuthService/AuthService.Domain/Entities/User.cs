using Common.Domain.Entities;

namespace AuthService.Domain.Entities;

/// <summary>
/// Represents a CrossMarket platform user.
/// Owns alert subscriptions, watchlists, and personal preferences.
/// Multi-tenancy field: each user sees only their own data.
/// </summary>
public sealed class User : AuditableEntity<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public bool IsEmailConfirmed { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? SuspendedAt { get; private set; }

    // Refresh token
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static User Create(string email, string passwordHash, string fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            IsEmailConfirmed = false,
            IsActive = true,
        };
    }

    // ── Behaviour ───────────────────────────────────────────────────────────

    public void SetRefreshToken(string token, DateTime expiresAt)
    {
        RefreshToken = token;
        RefreshTokenExpiresAt = expiresAt;
    }

    public void ClearRefreshToken() => RefreshToken = null;

    public void ConfirmEmail() => IsEmailConfirmed = true;

    public void Suspend()
    {
        IsActive = false;
        SuspendedAt = DateTime.UtcNow;
        ClearRefreshToken();
    }

    public void Reactivate()
    {
        IsActive = true;
        SuspendedAt = null;
    }
}