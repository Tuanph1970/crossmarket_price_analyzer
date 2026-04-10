using FluentAssertions;
using ScoringService.Application.Services;
using Xunit;

namespace ScoringService.UnitTests;

public class LandedCostCalculatorTests
{
    private readonly LandedCostCalculator _sut;

    // Default rates from LandedCostCalculator:
    // ImportDuty = 5%, VAT = 10%, Handling = $3, ExchangeRate = 25000
    private const decimal DefaultDuty = 5.0m;
    private const decimal DefaultVat = 10.0m;
    private const decimal DefaultHandlingUsd = 3.0m;
    private const decimal DefaultRate = 25000m;

    public LandedCostCalculatorTests()
    {
        _sut = new LandedCostCalculator();
    }

    // ── CalculateLandedCost ────────────────────────────────────────────────────

    [Fact]
    public void CalculateLandedCost_WithDefaults_ReturnsCorrectTotal()
    {
        // $100 * 25000 = 2,500,000 VND (purchase)
        // $10 * 25000 = 250,000 VND (shipping)
        // CIF = 2,750,000
        // Duty = 2,750,000 * 5% = 137,500
        // VAT = (2,750,000 + 137,500) * 10% = 288,750
        // Handling = $3 * 25000 = 75,000
        // Total = 2,750,000 + 137,500 + 288,750 + 75,000 = 3,251,250
        var total = _sut.CalculateLandedCost(100m, DefaultRate, 10m);

        total.Should().Be(3_251_250m);
    }

    [Fact]
    public void CalculateLandedCost_ZeroShipping_CalculatesCorrectly()
    {
        // CIF = 2,500,000
        // Duty = 2,500,000 * 5% = 125,000
        // VAT = 2,625,000 * 10% = 262,500
        // Handling = 75,000
        // Total = 2,962,500
        var total = _sut.CalculateLandedCost(100m, DefaultRate, 0m);
        total.Should().Be(2_962_500m);
    }

    [Fact]
    public void CalculateLandedCost_ZeroExchangeRate_UsesDefaultRate()
    {
        var withZeroRate = _sut.CalculateLandedCost(100m, 0m, 10m);
        var withDefaultRate = _sut.CalculateLandedCost(100m, DefaultRate, 10m);

        withZeroRate.Should().Be(withDefaultRate);
    }

    // ── CalculateBreakdown ─────────────────────────────────────────────────────

    [Fact]
    public void CalculateBreakdown_ReturnsFullBreakdown()
    {
        var breakdown = _sut.CalculateBreakdown(100m, DefaultRate, 10m);

        breakdown.UsPurchasePriceUsd.Should().Be(100m);
        breakdown.UsPurchasePriceVnd.Should().Be(2_500_000m);
        breakdown.ShippingCostUsd.Should().Be(10m);
        breakdown.ShippingCostVnd.Should().Be(250_000m);
        breakdown.ImportDutyVnd.Should().Be(137_500m);
        breakdown.VatVnd.Should().Be(288_750m);
        breakdown.HandlingFeesVnd.Should().Be(75_000m);
        breakdown.TotalLandedCostVnd.Should().Be(3_251_250m);
    }

    [Fact]
    public void CalculateBreakdown_ComponentsSumToTotal()
    {
        var breakdown = _sut.CalculateBreakdown(200m, DefaultRate, 15m);

        // Total = purchaseVnd + shippingVnd + duty + vat + handling
        var expectedTotal = breakdown.UsPurchasePriceVnd
                          + breakdown.ShippingCostVnd
                          + breakdown.ImportDutyVnd
                          + breakdown.VatVnd
                          + breakdown.HandlingFeesVnd;

        breakdown.TotalLandedCostVnd.Should().Be(expectedTotal);
    }

    [Theory]
    [InlineData(5.0, 10.0, 0, 5.0, 10.0, 0)]   // all defaults except handling=0 → use 0
    [InlineData(10.0, 5.0, 0, 10.0, 5.0, 0)]   // swapped duty/VAT
    public void CalculateBreakdown_CustomRates_UsedInsteadOfDefaults(
        decimal dutyRate, decimal vatRate, decimal handlingUsd,
        decimal expectedDuty, decimal expectedVat, decimal expectedHandling)
    {
        // With rate 25000:
        // purchase = 100*25000 = 2,500,000, shipping = 0
        // CIF = 2,500,000
        // duty = CIF * expectedDuty% = 2,500,000 * expectedDuty/100
        // vat = (CIF + duty) * expectedVat%
        // handling = expectedHandling * 25000

        var breakdown = _sut.CalculateBreakdown(100m, DefaultRate, 0m, dutyRate, vatRate, handlingUsd);

        breakdown.ImportDutyVnd.Should().Be(2_500_000m * expectedDuty / 100m);
        breakdown.VatVnd.Should().Be((2_500_000m + breakdown.ImportDutyVnd) * expectedVat / 100m);
        breakdown.HandlingFeesVnd.Should().Be(expectedHandling * DefaultRate);
    }

    [Fact]
    public void CalculateBreakdown_CIFIsPurchasePlusShipping()
    {
        var breakdown = _sut.CalculateBreakdown(100m, DefaultRate, 50m);

        // CIF = purchaseVnd + shippingVnd = 2,500,000 + 1,250,000 = 3,750,000
        breakdown.UsPurchasePriceVnd.Should().Be(2_500_000m);
        breakdown.ShippingCostVnd.Should().Be(1_250_000m);

        // Duty = CIF * 5% = 187,500
        breakdown.ImportDutyVnd.Should().Be(3_750_000m * 0.05m);
    }

    [Fact]
    public void CalculateBreakdown_VATAppliedOnCIFPlusDuty()
    {
        // Manually verify: VAT base = CIF + Duty
        var breakdown = _sut.CalculateBreakdown(100m, DefaultRate, 0m);

        var cif = breakdown.UsPurchasePriceVnd + breakdown.ShippingCostVnd; // 2,500,000
        var expectedVat = (cif + breakdown.ImportDutyVnd) * (DefaultVat / 100m);

        breakdown.VatVnd.Should().Be(expectedVat);
    }

    // ── CalculateProfitMargin ─────────────────────────────────────────────────

    [Fact]
    public void CalculateProfitMargin_PositiveMargin_ReturnsCorrectPercentage()
    {
        // vnRetailPrice = 4,000,000, landedCost = 3,251,250
        // margin = (4,000,000 - 3,251,250) / 4,000,000 * 100 = 18.72%
        var margin = _sut.CalculateProfitMargin(4_000_000m, 3_251_250m);

        margin.Should().BeGreaterThan(18m);
        margin.Should().BeLessThan(19m);
    }

    [Fact]
    public void CalculateProfitMargin_ZeroMargin_ReturnsZero()
    {
        var margin = _sut.CalculateProfitMargin(3_251_250m, 3_251_250m);
        margin.Should().Be(0m);
    }

    [Fact]
    public void CalculateProfitMargin_LandedCostExceedsRetail_ReturnsZero()
    {
        var margin = _sut.CalculateProfitMargin(1_000_000m, 3_000_000m);
        margin.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CalculateProfitMargin_InvalidRetailPrice_ReturnsZero(decimal retailPrice)
    {
        var margin = _sut.CalculateProfitMargin(retailPrice, 1_000_000m);
        margin.Should().Be(0m);
    }

    [Fact]
    public void CalculateProfitMargin_FullProfit_Returns100()
    {
        // When landed cost is 0
        var margin = _sut.CalculateProfitMargin(1_000_000m, 0m);
        margin.Should().Be(100m);
    }

    // ── CalculateRoi ───────────────────────────────────────────────────────────

    [Fact]
    public void CalculateRoi_PositiveROI_ReturnsCorrectPercentage()
    {
        // vnRetailPrice = 4,000,000, landedCost = 3,251,250
        // roi = (4,000,000 - 3,251,250) / 3,251,250 * 100
        var roi = _sut.CalculateRoi(4_000_000m, 3_251_250m);

        roi.Should().BeGreaterThan(22m);
        roi.Should().BeLessThan(24m);
    }

    [Fact]
    public void CalculateRoi_ZeroCost_ReturnsZero()
    {
        var roi = _sut.CalculateRoi(1_000_000m, 0m);
        roi.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CalculateRoi_InvalidLandedCost_ReturnsZero(decimal landedCost)
    {
        var roi = _sut.CalculateRoi(1_000_000m, landedCost);
        roi.Should().Be(0m);
    }

    [Fact]
    public void CalculateRoi_LandedCostExceedsRetail_ReturnsZero()
    {
        var roi = _sut.CalculateRoi(1_000_000m, 3_000_000m);
        roi.Should().Be(0m);
    }

    // ── ROI vs Margin comparison ───────────────────────────────────────────────

    [Fact]
    public void CalculateRoi_AlwaysGreaterThanMargin_WhenLandedCostLessThanRetail()
    {
        var margin = _sut.CalculateProfitMargin(4_000_000m, 3_000_000m);
        var roi = _sut.CalculateRoi(4_000_000m, 3_000_000m);

        // ROI = profit/cost, Margin = profit/revenue, and cost < revenue, so ROI > margin
        roi.Should().BeGreaterThan(margin);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateBreakdown_SmallAmounts_HandlesCorrectly()
    {
        // $0.01 item with $0 shipping
        var breakdown = _sut.CalculateBreakdown(0.01m, DefaultRate, 0m);

        breakdown.UsPurchasePriceVnd.Should().Be(250m);
        breakdown.ImportDutyVnd.Should().BeGreaterOrEqualTo(0m);
        breakdown.TotalLandedCostVnd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculateBreakdown_LargeAmounts_HandlesCorrectly()
    {
        // $10,000 item
        var breakdown = _sut.CalculateBreakdown(10_000m, DefaultRate, 500m);

        breakdown.UsPurchasePriceVnd.Should().Be(250_000_000m);
        breakdown.TotalLandedCostVnd.Should().BeGreaterThan(breakdown.UsPurchasePriceVnd);
    }
}
