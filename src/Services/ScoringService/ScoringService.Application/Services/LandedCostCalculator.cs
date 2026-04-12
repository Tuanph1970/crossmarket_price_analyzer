namespace ScoringService.Application.Services;

/// <summary>
/// Calculates fully-loaded landed cost for US products imported to Vietnam.
///
/// Formula (when no override is provided):
///   LandedCost = US_Price_VND + Shipping + Import_Duty + VAT + Handling
///
/// If <paramref name="landedCostOverride"/> is set, that value is returned directly,
/// bypassing the full calculation (useful for manual overrides).
/// If <paramref name="importDutyOverridePct"/> is set, it replaces the default rate
/// regardless of any lookup table.
/// </summary>
public class LandedCostCalculator
{
    // Vietnam default rates
    private const decimal DefaultImportDutyRatePct = 5.0m;  // Standard import duty %
    private const decimal DefaultVatRatePct = 10.0m;        // Vietnam standard VAT %
    private const decimal DefaultHandlingFeeUsd = 3.0m;     // Flat handling fee per shipment
    private const decimal DefaultExchangeRate = 25000m;     // Default USD→VND rate

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
    ///
    /// <param name="landedCostOverride">
    ///   When set, returned directly — skips all calculation (manual override mode).
    /// </param>
    /// <param name="importDutyOverridePct">
    ///   When set, replaces <see cref="DefaultImportDutyRatePct"/> for this call.
    /// </param>
    /// </summary>
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
        // P2-B06: manual override — bypass calculator
        if (landedCostOverride.HasValue)
            return landedCostOverride.Value;

        var breakdown = CalculateBreakdown(
            usPriceUsd, exchangeRateUsdToVnd, shippingCostUsd,
            importDutyRatePct, vatRatePct, handlingFeesUsd,
            landedCostOverride, importDutyOverridePct);
        return breakdown.TotalLandedCostVnd;
    }

    /// <summary>
    /// Calculate full landed cost breakdown.
    ///
    /// <param name="importDutyOverridePct">
    ///   When set, overrides the duty rate for this calculation regardless of any lookup table.
    /// </param>
    /// </summary>
    public LandedCostBreakdown CalculateBreakdown(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null,
        decimal? landedCostOverride = null,
        decimal? importDutyOverridePct = null)
    {
        // P2-B06: manual override — return a synthetic breakdown
        if (landedCostOverride.HasValue)
        {
            return new LandedCostBreakdown(
                UsPurchasePriceUsd: usPriceUsd,
                UsPurchasePriceVnd: usPriceUsd * exchangeRateUsdToVnd,
                ShippingCostUsd: shippingCostUsd,
                ShippingCostVnd: shippingCostUsd * exchangeRateUsdToVnd,
                ImportDutyVnd: 0,
                VatVnd: 0,
                HandlingFeesVnd: 0,
                TotalLandedCostVnd: landedCostOverride.Value
            );
        }

        // P2-B06: honour import-duty override, falling back to the provided rate then defaults
        var dutyRate = importDutyOverridePct ?? importDutyRatePct ?? DefaultImportDutyRatePct;
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