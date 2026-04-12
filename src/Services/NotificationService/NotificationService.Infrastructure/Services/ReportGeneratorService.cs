using Microsoft.Extensions.Logging;

namespace NotificationService.Infrastructure.Services;

/// <summary>
/// P4-B08: Scheduled report generation.
/// Produces opportunity summary PDFs using QuestPDF.
/// Scheduled by NotificationService.Worker via Quartz.NET.
/// TODO: Replace mock with real QuestPDF PDF generation.
/// </summary>
public sealed class ReportGeneratorService : IReportGeneratorService
{
    private readonly ILogger<ReportGeneratorService> _logger;

    public ReportGeneratorService(ILogger<ReportGeneratorService> logger) => _logger = logger;

    /// <summary>
    /// Generates a multi-page PDF report of top opportunities.
    /// TODO (v2): Replace mock byte array with real QuestPDF document.
    /// </summary>
    public async Task<byte[]> GenerateOpportunityReportAsync(
        OpportunityReportRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating {Format} report '{Title}' for {Count} opportunities (period {From:d}–{To:d})",
            request.Format, request.Title, request.Opportunities.Count,
            request.PeriodFrom, request.PeriodTo);

        // TODO (v2): Real QuestPDF implementation:
        // using var doc = Document.Create(container =>
        // {
        //     container.Page(page =>
        //     {
        //         page.Size(PageSizes.A4);
        //         page.Content().Text($"{request.Title}\n{request.PeriodFrom:d} – {request.PeriodTo:d}");
        //         page.Content().Table(table => ...); // top opportunities
        //     });
        // });
        // return doc.GeneratePdf();

        await Task.Delay(100, ct); // simulate PDF generation

        // Return a minimal PDF stub (valid header for browser download)
        return PdfStub(request.Title, request.Opportunities.Count);
    }

    /// <summary>
    /// Generates a CSV export of opportunities.
    /// </summary>
    public Task<byte[]> GenerateOpportunityCsvAsync(
        OpportunityReportRequest request,
        CancellationToken ct = default)
    {
        var lines = new List<string>
        {
            "MatchId,CompositeScore,MarginPct,DemandScore,CompetitionScore,StabilityScore,ConfidenceScore,LandedCostVnd,RetailVnd",
        };

        foreach (var opp in request.Opportunities)
        {
            lines.Add($"{opp.MatchId},{opp.CompositeScore:F2},{opp.ProfitMarginPct:F2}," +
                      $"{opp.DemandScore:F2},{opp.CompetitionScore:F2},{opp.PriceStabilityScore:F2}," +
                      $"{opp.MatchConfidenceScore:F2},{opp.LandedCostVnd},{opp.VietnamRetailVnd}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines));
        return Task.FromResult(bytes);
    }

    private static byte[] PdfStub(string title, int count)
    {
        // Minimal valid PDF 1.4
        var content = $"%PDF-1.4\n" +
            $"1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            $"2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
            $"/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n" +
            $"4 0 obj\n<< /Length 100 >>\nstream\n" +
            $"BT\n" +
            $"/F1 16 Tf\n100 700 Td\n({title} - {count} opportunities) Tj\n" +
            $"ET\nendstream\nendobj\n" +
            $"5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n" +
            $"xref\n0 6\n0000000000 65535 f\n" +
            $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n0\n%%EOF";

        return System.Text.Encoding.ASCII.GetBytes(content);
    }
}

public record OpportunityReportRequest(
    string Title,
    string Format, // "pdf" or "csv"
    IReadOnlyList<OpportunitySummaryDto> Opportunities,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    Guid? UserId = null  // null = all users
);

public record OpportunitySummaryDto(
    Guid MatchId,
    string UsProductName,
    string VnProductName,
    decimal CompositeScore,
    decimal ProfitMarginPct,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    decimal LandedCostVnd,
    decimal VietnamRetailVnd
);

public interface IReportGeneratorService
{
    Task<byte[]> GenerateOpportunityReportAsync(OpportunityReportRequest request, CancellationToken ct = default);
    Task<byte[]> GenerateOpportunityCsvAsync(OpportunityReportRequest request, CancellationToken ct = default);
}
