namespace ScoringService.Application.Services;

/// <summary>
/// Calculates fully-loaded landed cost for US products imported to Vietnam.
/// Formula: LandedCost = US_Price_VND + Shipping + Import_Duty + VAT + Handling
/// </summary>
public class LandedCostCalculator
{
    // Vietnam default rates
    private const decimal DefaultImportDutyRatePct = 5.0m;  // Standard import duty %
    private const decimal DefaultVatRatePct = 10.0m;        // Vietnam standard VAT %
    private const decimal DefaultHandlingFeeUsd = 3.0m;     // Flat handling fee per shipment
    private const decimal DefaultExchangeRate = 25000m;       // Default USD→VND rate

    public record LandedCostBreakdown(
        decimal UsPurchasePriceUsd,
        decimal UsPurchasePriceVnd,
        decimal ShippingCostUsd,
        decimal ShippingCostVnd,
        decimal ImportDutyVnd,
        decimal VatVnd,
        decimal HandlingFeesVnd,
        decimal TotalLandedCostVnd
    );

    /// <summary>
    /// Calculate total landed cost in VND.
    /// </summary>
    public decimal CalculateLandedCost(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null)
    {
        var breakdown = CalculateBreakdown(
            usPriceUsd, exchangeRateUsdToVnd, shippingCostUsd,
            importDutyRatePct, vatRatePct, handlingFeesUsd);
        return breakdown.TotalLandedCostVnd;
    }

    /// <summary>
    /// Calculate full landed cost breakdown.
    /// </summary>
    public LandedCostBreakdown CalculateBreakdown(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null)
    {
        var dutyRate = importDutyRatePct ?? DefaultImportDutyRatePct;
        var vatRate = vatRatePct ?? DefaultVatRatePct;
        var handling = handlingFeesUsd ?? DefaultHandlingFeeUsd;

        var rate = exchangeRateUsdToVnd > 0 ? exchangeRateUsdToVnd : DefaultExchangeRate;

        // Step 1: Convert USD prices to VND
        var usdPriceVnd = usPriceUsd * rate;
        var shippingVnd = shippingCostUsd * rate;
        var handlingVnd = handling * rate;

        // Step 2: Import duty is applied to CIF value (US price + shipping)
        var cifValue = usdPriceVnd + shippingVnd;
        var importDuty = cifValue * (dutyRate / 100m);

        // Step 3: VAT is applied to CIF + duty
        var vat = (cifValue + importDuty) * (vatRate / 100m);

        // Step 4: Total landed cost
        var total = cifValue + importDuty + vat + handlingVnd;

        return new LandedCostBreakdown(
            UsPurchasePriceUsd: usPriceUsd,
            UsPurchasePriceVnd: usdPriceVnd,
            ShippingCostUsd: shippingCostUsd,
            ShippingCostVnd: shippingVnd,
            ImportDutyVnd: importDuty,
            VatVnd: vat,
            HandlingFeesVnd: handlingVnd,
            TotalLandedCostVnd: total
        );
    }

    /// <summary>
    /// Calculate profit margin percentage.
    /// margin = (vnRetailPrice - landedCost) / vnRetailPrice * 100
    /// </summary>
    public decimal CalculateProfitMargin(decimal vnRetailPriceVnd, decimal landedCostVnd)
    {
        if (vnRetailPriceVnd <= 0) return 0;
        return Math.Max(0, (vnRetailPriceVnd - landedCostVnd) / vnRetailPriceVnd * 100m);
    }

    /// <summary>
    /// Calculate ROI percentage.
    /// roi = (vnRetailPrice - landedCost) / landedCost * 100
    /// </summary>
    public decimal CalculateRoi(decimal vnRetailPriceVnd, decimal landedCostVnd)
    {
        if (landedCostVnd <= 0) return 0;
        return Math.Max(0, (vnRetailPriceVnd - landedCostVnd) / landedCostVnd * 100m);
    }
}
