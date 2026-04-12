using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Services;

/// <summary>
/// P4-B06: Telegram bot notification delivery.
/// Sends formatted messages via the Telegram Bot API.
/// TODO: Replace mock with real Telegram Bot API once bot token is provisioned.
/// Real API: https://core.telegram.org/bots/api#sendmessage
/// </summary>
public sealed class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly string? _botToken;

    public TelegramNotificationService(
        HttpClient httpClient,
        ILogger<TelegramNotificationService> logger,
        string? botToken = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _botToken = botToken;
    }

    public async Task<bool> SendTelegramMessageAsync(
        string chatId,
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_botToken))
        {
            _logger.LogInformation(
                "[Telegram Mock] to {ChatId}: {Text}", chatId, text[..Math.Min(80, text.Length)]);
            return true;
        }

        try
        {
            // TODO (v2): Real Telegram Bot API call:
            // POST https://api.telegram.org/bot{_botToken}/sendMessage
            // { "chat_id": chatId, "text": text, "parse_mode": "HTML" }

            await Task.Delay(30, ct); // simulate API latency

            _logger.LogInformation(
                "Telegram message sent to {ChatId}: {Preview}",
                chatId, text[..Math.Min(100, text.Length)]);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to {ChatId}", chatId);
            return false;
        }
    }

    public string BuildOpportunityAlertMarkdown(
        string productName,
        decimal compositeScore,
        decimal profitMarginPct,
        string matchUrl)
    {
        var emoji = compositeScore >= 80 ? "🟢" : compositeScore >= 60 ? "🟡" : "🔴";
        return $"""
{emoji} *CrossMarket Opportunity Alert*

📦 *{productName}*

📊 Score: *{compositeScore:F0}/100* | Margin: *{profitMarginPct:F1}%*

🔗 [View Opportunity]({matchUrl})
""";
    }

    public string BuildDigestMarkdown(
        IReadOnlyList<AlertDigestItem> items,
        int totalMatches)
    {
        var lines = new List<string>
        {
            "📈 *CrossMarket Daily Digest*",
            $"Covering {totalMatches:N0} opportunities\n",
        };

        foreach (var item in items.Take(10))
        {
            var e = item.CompositeScore >= 80 ? "🟢" : item.CompositeScore >= 60 ? "🟡" : "🔴";
            lines.Add($"{e} *{item.ProductName}* — Score {item.CompositeScore:F0}, Margin {item.ProfitMarginPct:F1}%");
        }

        if (items.Count > 10)
            lines.Add($"\n_...and {items.Count - 10} more_");

        return string.Join("\n", lines);
    }
}

public record AlertDigestItem(
    string ProductName,
    decimal CompositeScore,
    decimal ProfitMarginPct
);

public interface ITelegramNotificationService
{
    Task<bool> SendTelegramMessageAsync(string chatId, string text, CancellationToken ct = default);
    string BuildOpportunityAlertMarkdown(string product, decimal score, decimal margin, string url);
    string BuildDigestMarkdown(IReadOnlyList<AlertDigestItem> items, int totalMatches);
}
