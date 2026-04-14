using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Services;

/// <summary>
/// P4-B05: SendGrid email delivery service.
/// Sends templated HTML emails for opportunity alerts.
/// TODO: Replace mock with real SendGrid API once credentials are provisioned.
/// Real SendGrid SDK: https://www.nuget.org/packages/SendGrid
/// </summary>
public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        HttpClient httpClient,
        ILogger<EmailNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendAlertEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            // TODO (v2): Replace with real SendGrid API:
            // var content = new SendGridMessage();
            // content.AddTo(to);
            // content.SetSubject(subject);
            // content.AddContent(MimeType.Html, htmlBody);
            // var response = await _sendGridClient.SendEmailAsync(content, ct);

            // Mock: simulate 50ms network latency
            await Task.Delay(50, ct);

            _logger.LogInformation(
                "Email sent to {To}: {Subject} ({HtmlLength} chars)",
                to, subject, htmlBody.Length);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
            return false;
        }
    }

    public string BuildOpportunityAlertHtml(
        string userName,
        string productName,
        decimal compositeScore,
        decimal profitMarginPct,
        string matchUrl)
    {
        return $"""
<!DOCTYPE html>
<html>
<head><meta charset="utf-8"><title>CrossMarket Opportunity Alert</title></head>
<body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
  <h2 style="color:#2E5090;">CrossMarket — Opportunity Alert</h2>
  <p>Hi {userName},</p>
  <p>A new opportunity matches your criteria:</p>
  <div style="background:#f5f5f5; border-radius:8px; padding:16px; margin:16px 0;">
    <p style="margin:0; font-size:18px; font-weight:bold;">{productName}</p>
    <p style="margin:8px 0 0;">Composite Score: <strong style="color:#16a34a;">{compositeScore:F0}/100</strong></p>
    <p style="margin:4px 0;">Profit Margin: <strong>{profitMarginPct:F1}%</strong></p>
  </div>
  <a href="{matchUrl}" style="display:inline-block; background:#2E5090; color:#fff; padding:12px 24px;
     border-radius:6px; text-decoration:none; font-weight:bold;">View Opportunity →</a>
  <p style="margin-top:24px; color:#888; font-size:12px;">
    You are receiving this because you have alert thresholds configured on CrossMarket.<br>
    <a href="{{{{unsubscribe_url}}}}">Manage alerts</a>
  </p>
</body>
</html>
""";
    }
}

public interface IEmailNotificationService
{
    Task<bool> SendAlertEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    string BuildOpportunityAlertHtml(string userName, string productName, decimal score, decimal margin, string matchUrl);
}
