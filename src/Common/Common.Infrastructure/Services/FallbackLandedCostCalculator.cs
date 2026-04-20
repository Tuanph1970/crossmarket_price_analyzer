using Common.Application.Interfaces;

using Microsoft.Extensions.Logging;

namespace Common.Infrastructure.Services;

/// <summary>
/// Stub landed-cost calculator used by ProductService when ScoringService is unavailable.
/// Applies simple default rates — real calculation is done by ScoringService.
/// </summary>
public sealed class FallbackLandedCostCalculator : ILandedCostCalculator
{

    private readonly ILogger<FallbackLandedCostCalculator> _logger;
    private const decimal DefaultImportDutyRatePct = 5.0m;
    private const decimal DefaultVatRatePct = 10.0m;
    private const decimal DefaultHandlingFeeUsd = 3.0m;
    private const decimal DefaultExchangeRate = 25000m;

    public FallbackLandedCostCalculator(ILogger<FallbackLandedCostCalculator> logger)
        => _logger = logger;

    public decimal CalculateLandedCost(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null,
        decimal? landedCostOverride = null,
        decimal? importDutyOverridePct = null)
    {
        if (landedCostOverride.HasValue) return landedCostOverride.Value;
        var breakdown = CalculateBreakdown(usPriceUsd, exchangeRateUsdToVnd, shippingCostUsd,
            importDutyRatePct, vatRatePct, handlingFeesUsd, landedCostOverride, importDutyOverridePct);
        return breakdown.TotalLandedCostVnd;
    }

    public ILandedCostCalculator.LandedCostBreakdown CalculateBreakdown(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null,
        decimal? landedCostOverride = null,
        decimal? importDutyOverridePct = null)
    {
        if (landedCostOverride.HasValue)
        {
            return new ILandedCostCalculator.LandedCostBreakdown(
                UsPurchasePriceUsd: usPriceUsd,
                UsPurchasePriceVnd: usPriceUsd * exchangeRateUsdToVnd,
                ShippingCostUsd: shippingCostUsd,
                ShippingCostVnd: shippingCostUsd * exchangeRateUsdToVnd,
                ImportDutyVnd: 0, VatVnd: 0, HandlingFeesVnd: 0,
                TotalLandedCostVnd: landedCostOverride.Value);
        }

        var dutyRate = importDutyOverridePct ?? importDutyRatePct ?? DefaultImportDutyRatePct;
        var vatRate = vatRatePct ?? DefaultVatRatePct;
        var handling = handlingFeesUsd ?? DefaultHandlingFeeUsd;
        var rate = exchangeRateUsdToVnd > 0 ? exchangeRateUsdToVnd : DefaultExchangeRate;

        var usdPriceVnd = usPriceUsd * rate;
        var shippingVnd = shippingCostUsd * rate;
        var handlingVnd = handling * rate;
        var cifValue = usdPriceVnd + shippingVnd;
        var importDuty = cifValue * (dutyRate / 100m);
        var vat = (cifValue + importDuty) * (vatRate / 100m);
        var total = cifValue + importDuty + vat + handlingVnd;

        return new ILandedCostCalculator.LandedCostBreakdown(
            UsPurchasePriceUsd: usPriceUsd,
            UsPurchasePriceVnd: usdPriceVnd,
            ShippingCostUsd: shippingCostUsd,
            ShippingCostVnd: shippingVnd,
            ImportDutyVnd: importDuty,
            VatVnd: vat,
            HandlingFeesVnd: handlingVnd,
            TotalLandedCostVnd: total);
    }

    public decimal CalculateProfitMargin(decimal vnRetailPriceVnd, decimal landedCostVnd)
    {
        if (vnRetailPriceVnd <= 0) return 0;
        return Math.Max(0, (vnRetailPriceVnd - landedCostVnd) / vnRetailPriceVnd * 100m);
    }

    public decimal CalculateRoi(decimal vnRetailPriceVnd, decimal landedCostVnd)
    {
        if (landedCostVnd <= 0) return 0;
        return Math.Max(0, (vnRetailPriceVnd - landedCostVnd) / landedCostVnd * 100m);
    }
}
