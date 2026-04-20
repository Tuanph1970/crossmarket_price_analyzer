namespace Common.Application.Interfaces;

/// <summary>
/// Calculates fully-loaded landed cost for US products imported to Vietnam.
/// </summary>
public interface ILandedCostCalculator
{
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
    decimal CalculateLandedCost(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null,
        decimal? landedCostOverride = null,
        decimal? importDutyOverridePct = null);

    /// <summary>
    /// Calculate full landed cost breakdown.
    /// </summary>
    LandedCostBreakdown CalculateBreakdown(
        decimal usPriceUsd,
        decimal exchangeRateUsdToVnd,
        decimal shippingCostUsd,
        decimal? importDutyRatePct = null,
        decimal? vatRatePct = null,
        decimal? handlingFeesUsd = null,
        decimal? landedCostOverride = null,
        decimal? importDutyOverridePct = null);

    /// <summary>
    /// Calculate profit margin percentage.
    /// margin = (vnRetailPrice - landedCost) / vnRetailPrice * 100
    /// </summary>
    decimal CalculateProfitMargin(decimal vnRetailPriceVnd, decimal landedCostVnd);

    /// <summary>
    /// Calculate ROI percentage.
    /// roi = (vnRetailPrice - landedCost) / landedCost * 100
    /// </summary>
    decimal CalculateRoi(decimal vnRetailPriceVnd, decimal landedCostVnd);
}
