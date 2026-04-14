using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ScoringService.Application.Persistence;

namespace ScoringService.Application.Services;

/// <summary>
/// Exports opportunity and price data to formatted Excel workbooks using EPPlus.
/// Creates multi-sheet workbooks with styled headers, auto-fitted columns, and frozen rows.
/// </summary>
public class ExcelExportService : IExcelExportService
{
    private readonly ScoringDbContext _db;
    private readonly ILogger<ExcelExportService> _logger;

    static ExcelExportService()
    {
        // Required for non-commercial use (EPPlus community license)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public ExcelExportService(ScoringDbContext db, ILogger<ExcelExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Exports opportunities to a multi-sheet Excel workbook.
    /// Sheet 1: Summary stats
    /// Sheet 2: Full opportunity data
    /// Sheet 3: Top 20 by composite score
    /// </summary>
    public async Task<byte[]> ExportOpportunitiesAsync(ExportRequest request, CancellationToken ct = default)
    {
        await Task.CompletedTask; // sync for now

        using var package = new ExcelPackage();
        var workbook = package.Workbook;

        // ── Sheet 1: Summary ─────────────────────────────────────────────────
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cells[1, 1].Value = "CrossMarket Price Analyzer — Opportunity Export";
        summarySheet.Cells[1, 1].Style.Font.Bold = true;
        summarySheet.Cells[1, 1].Style.Font.Size = 14;
        summarySheet.Cells[2, 1].Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        summarySheet.Cells[3, 1].Value = $"Title: {request.Title}";
        summarySheet.Cells[4, 1].Value = $"Total Records: {request.Rows.Count}";
        summarySheet.Cells[5, 1].Value = $"Average Margin %: {(request.Rows.Count > 0 ? request.Rows.Average(r => r.ProfitMarginPct) : 0m):F2}";

        // ── Sheet 2: Opportunities ───────────────────────────────────────────
        var oppSheet = workbook.Worksheets.Add("Opportunities");
        var headers = new[]
        {
            "#", "US Product", "VN Product", "US Price (USD)", "VN Price (VND)",
            "Landed Cost (VND)", "Retail (VND)", "Margin %", "ROI %",
            "Score", "Demand", "Competition", "Stability", "Confidence", "Calculated"
        };

        WriteHeaderRow(oppSheet, headers);

        for (int i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var r = i + 2;
            oppSheet.Cells[r, 1].Value = i + 1;
            oppSheet.Cells[r, 2].Value = row.UsProductName;
            oppSheet.Cells[r, 3].Value = row.VnProductName;
            oppSheet.Cells[r, 4].Value = row.UsPriceUsd ?? 0m;
            oppSheet.Cells[r, 5].Value = row.VnPriceVnd ?? 0m;
            oppSheet.Cells[r, 6].Value = row.LandedCostVnd ?? 0m;
            oppSheet.Cells[r, 7].Value = row.VietnamRetailVnd ?? 0m;
            oppSheet.Cells[r, 8].Value = row.ProfitMarginPct;
            oppSheet.Cells[r, 8].Style.Numberformat.Format = "0.00%";
            oppSheet.Cells[r, 9].Value = row.RoiPct;
            oppSheet.Cells[r, 9].Style.Numberformat.Format = "0.00%";
            oppSheet.Cells[r, 10].Value = row.CompositeScore;
            oppSheet.Cells[r, 11].Value = row.DemandScore;
            oppSheet.Cells[r, 12].Value = row.CompetitionScore;
            oppSheet.Cells[r, 13].Value = row.PriceStabilityScore;
            oppSheet.Cells[r, 14].Value = row.MatchConfidenceScore;
            oppSheet.Cells[r, 15].Value = row.CalculatedAt;
            oppSheet.Cells[r, 15].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
        }

        oppSheet.Cells[oppSheet.Dimension.Address].AutoFitColumns();
        oppSheet.View.FreezePanes(2, 1); // freeze header row

        // ── Sheet 3: Top 20 ──────────────────────────────────────────────────
        var topSheet = workbook.Worksheets.Add("Top 20");
        WriteHeaderRow(topSheet, headers);

        var top20 = request.Rows
            .OrderByDescending(r => r.CompositeScore)
            .Take(20)
            .ToList();

        for (int i = 0; i < top20.Count; i++)
        {
            var row = top20[i];
            var r = i + 2;
            topSheet.Cells[r, 1].Value = i + 1;
            topSheet.Cells[r, 2].Value = row.UsProductName;
            topSheet.Cells[r, 3].Value = row.VnProductName;
            topSheet.Cells[r, 4].Value = row.UsPriceUsd ?? 0m;
            topSheet.Cells[r, 5].Value = row.VnPriceVnd ?? 0m;
            topSheet.Cells[r, 6].Value = row.LandedCostVnd ?? 0m;
            topSheet.Cells[r, 7].Value = row.VietnamRetailVnd ?? 0m;
            topSheet.Cells[r, 8].Value = row.ProfitMarginPct;
            topSheet.Cells[r, 8].Style.Numberformat.Format = "0.00%";
            topSheet.Cells[r, 9].Value = row.RoiPct;
            topSheet.Cells[r, 9].Style.Numberformat.Format = "0.00%";
            topSheet.Cells[r, 10].Value = row.CompositeScore;
            topSheet.Cells[r, 11].Value = row.DemandScore;
            topSheet.Cells[r, 12].Value = row.CompetitionScore;
            topSheet.Cells[r, 13].Value = row.PriceStabilityScore;
            topSheet.Cells[r, 14].Value = row.MatchConfidenceScore;
            topSheet.Cells[r, 15].Value = row.CalculatedAt;
        }

        topSheet.Cells[topSheet.Dimension.Address].AutoFitColumns();
        topSheet.View.FreezePanes(2, 1);

        _logger.LogInformation("Excel export: {Count} opportunities → {Sheets} sheets",
            request.Rows.Count, workbook.Worksheets.Count);

        return package.GetAsByteArray();
    }

    public async Task<byte[]> ExportPriceHistoryAsync(Guid productId, CancellationToken ct = default)
    {
        using var package = new ExcelPackage();
        var workbook = package.Workbook;
        var sheet = workbook.Worksheets.Add("Price History");

        var headers = new[] { "Date", "Price (VND)", "Currency" };
        WriteHeaderRow(sheet, headers);

        sheet.Cells[1, 1].Value = $"Product ID: {productId}";
        sheet.Cells[1, 1].Style.Font.Bold = true;

        // TODO: Fetch from ProductService API if needed
        sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
        sheet.View.FreezePanes(2, 1);

        await Task.CompletedTask;
        return package.GetAsByteArray();
    }

    private static void WriteHeaderRow(ExcelWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.SetBackground(System.Drawing.Color.FromArgb(46, 80, 144));
            cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
    }
}

public interface IExcelExportService
{
    Task<byte[]> ExportOpportunitiesAsync(ExportRequest request, CancellationToken ct = default);
    Task<byte[]> ExportPriceHistoryAsync(Guid productId, CancellationToken ct = default);
}

public record ExportRequest(
    string Title,
    IReadOnlyList<OpportunityExportRow> Rows
);

public record OpportunityExportRow(
    string UsProductName,
    string VnProductName,
    decimal? UsPriceUsd,
    decimal? VnPriceVnd,
    decimal? LandedCostVnd,
    decimal? VietnamRetailVnd,
    decimal? ProfitMarginPct,
    decimal? RoiPct,
    decimal CompositeScore,
    decimal DemandScore,
    decimal CompetitionScore,
    decimal PriceStabilityScore,
    decimal MatchConfidenceScore,
    DateTime CalculatedAt
);
