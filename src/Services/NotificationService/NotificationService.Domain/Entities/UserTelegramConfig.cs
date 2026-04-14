namespace NotificationService.Domain.Entities;

/// <summary>
/// Stores Telegram chat ID and username for a user.
/// </summary>
public sealed class UserTelegramConfig : Common.Domain.Entities.BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string TelegramChatId { get; private set; } = string.Empty;
    public string? TelegramUsername { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserTelegramConfig() { } // EF Core

    public static UserTelegramConfig Create(Guid userId, string chatId, string? username = null)
    {
        return new UserTelegramConfig
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TelegramChatId = chatId,
            TelegramUsername = username,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
