using FluentAssertions;
using ScoringService.Application.Services;
using Xunit;

namespace ScoringService.UnitTests;

public class ScoringEngineTests
{
    private readonly ScoringEngine _sut;

    public ScoringEngineTests()
    {
        _sut = new ScoringEngine();
    }

    // ── Default weights ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultWeights_ShouldSumTo100()
    {
        var total = ScoringEngine.DefaultWeights.Values.Sum();
        total.Should().Be(100m);
    }

    [Fact]
    public void DefaultWeights_ShouldHaveCorrectKeys()
    {
        var keys = ScoringEngine.DefaultWeights.Keys.ToHashSet();
        keys.Should().Contain("ProfitMargin");
        keys.Should().Contain("Demand");
        keys.Should().Contain("Competition");
        keys.Should().Contain("Stability");
        keys.Should().Contain("Confidence");
    }

    [Theory]
    [InlineData("ProfitMargin", 40)]
    [InlineData("Demand", 25)]
    [InlineData("Competition", 20)]
    [InlineData("Stability", 10)]
    [InlineData("Confidence", 5)]
    public void DefaultWeights_ShouldHaveCorrectValues(string key, decimal expected)
    {
        ScoringEngine.DefaultWeights[key].Should().Be(expected);
    }

    // ── Composite score calculation ───────────────────────────────────────────

    [Fact]
    public void CalculateCompositeScore_PerfectInputs_Returns100()
    {
        // Perfect margin (≥50% → normalized to 100)
        // Max demand (100) + No competition (0 → 100) + Perfect stability + Perfect confidence
        var score = _sut.CalculateCompositeScore(
            profitMarginPct: 50m,
            demandScore: 100m,
            competitionScore: 0m,     // inverted: 100 - 0 = 100
            priceStabilityScore: 100m,
            matchConfidenceScore: 100m);

        score.Should().Be(100m);
    }

    [Fact]
    public void CalculateCompositeScore_ZeroInputs_ReturnsZero()
    {
        var score = _sut.CalculateCompositeScore(
            profitMarginPct: 0m,
            demandScore: 0m,
            competitionScore: 100m,   // inverted: 100 - 100 = 0
            priceStabilityScore: 0m,
            matchConfidenceScore: 0m);

        score.Should().Be(0m);
    }

    [Fact]
    public void CalculateCompositeScore_MidRangeInputs_ReturnsCorrectMidScore()
    {
        // Margin normalized: 25/50 * 100 = 50
        // 50 * 0.40 + 50 * 0.25 + 50 * 0.20 + 50 * 0.10 + 50 * 0.05
        // = 20 + 12.5 + 10 + 5 + 2.5 = 50
        var score = _sut.CalculateCompositeScore(
            profitMarginPct: 25m,     // 50% normalized
            demandScore: 50m,
            competitionScore: 50m,    // inverted: 50
            priceStabilityScore: 50m,
            matchConfidenceScore: 50m);

        score.Should().Be(50m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CalculateCompositeScore_NegativeMargin_TreatedAsZero(decimal margin)
    {
        // Normalized: (0-0)/(50-0)*100 = 0
        var score = _sut.CalculateCompositeScore(
            profitMarginPct: margin,
            demandScore: 100m,
            competitionScore: 0m,
            priceStabilityScore: 100m,
            matchConfidenceScore: 100m);

        // Only non-zero contributions: demand(100)*0.25 + competition(100)*0.20 + stability(100)*0.10 + confidence(100)*0.05
        // = 25 + 20 + 10 + 5 = 60
        score.Should().Be(60m);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 100)]
    [InlineData(25, 50)]
    [InlineData(100, 100)]
    [InlineData(-10, 0)]
    public void CalculateCompositeScore_MarginClampedCorrectly(decimal inputMargin, decimal expectedNormalized)
    {
        // All other factors at 0, competition at 100 (so inverted = 0)
        var score = _sut.CalculateCompositeScore(
            profitMarginPct: inputMargin,
            demandScore: 0m,
            competitionScore: 100m,
            priceStabilityScore: 0m,
            matchConfidenceScore: 0m);

        // Expected = normalized_margin * 0.40
        var expected = expectedNormalized * 0.40m;
        score.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 50)]
    [InlineData(100, 0)]
    public void CalculateCompositeScore_CompetitionInverted(decimal competitionScore, decimal expectedContribution)
    {
        // ProfitMargin = 50 (normalized 100), competition is the variable
        // Expected = 100*0.40 + competition_adjusted*0.20 = 40 + (100-competition)*0.20
        var score = _sut.CalculateCompositeScore(
            profitMarginPct: 50m,
            demandScore: 0m,
            competitionScore: competitionScore,
            priceStabilityScore: 0m,
            matchConfidenceScore: 0m);

        var expected = 40m + expectedContribution * 0.20m;
        score.Should().Be(expected);
    }

    [Fact]
    public void CalculateCompositeScore_HighCompetition_LowersScore()
    {
        var noCompetition = _sut.CalculateCompositeScore(25m, 50m, 0m, 50m, 50m);
        var highCompetition = _sut.CalculateCompositeScore(25m, 50m, 100m, 50m, 50m);

        noCompetition.Should().BeGreaterThan(highCompetition);
    }

    [Fact]
    public void CalculateCompositeScore_CustomWeights_OverridesDefaults()
    {
        var customWeights = new Dictionary<string, decimal>
        {
            { "ProfitMargin", 60m },
            { "Demand", 20m },
            { "Competition", 10m },
            { "Stability", 5m },
            { "Confidence", 5m }
        };

        // All inputs at 50, competition at 0
        var defaultScore = _sut.CalculateCompositeScore(50m, 50m, 0m, 50m, 50m);
        var customScore = _sut.CalculateCompositeScore(50m, 50m, 0m, 50m, 50m, customWeights);

        customScore.Should().NotBe(defaultScore);
        // Default: 100*0.40 + 50*0.25 + 100*0.20 + 50*0.10 + 50*0.05 = 40+12.5+20+5+2.5 = 80
        // Custom: 100*0.60 + 50*0.20 + 100*0.10 + 50*0.05 + 50*0.05 = 60+10+10+2.5+2.5 = 85
        customScore.Should().Be(85m);
    }

    [Fact]
    public void CalculateCompositeScore_AlwaysReturnsRounded2Decimal()
    {
        var score = _sut.CalculateCompositeScore(33.333m, 66.666m, 11.111m, 22.222m, 44.444m);
        score.Should().Be(Math.Round(score, 2));
        score.Should().BeGreaterOrEqualTo(0m);
        score.Should().BeLessOrEqualTo(100m);
    }

    // ── Normalize ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 100, 0)]
    [InlineData(50, 0, 100, 50)]
    [InlineData(100, 0, 100, 100)]
    [InlineData(25, 0, 50, 50)]
    [InlineData(50, 0, 50, 100)]
    [InlineData(10, 0, 50, 20)]
    public void Normalize_WithinRange_CalculatesCorrectly(decimal value, decimal min, decimal max, decimal expected)
    {
        var result = _sut.Normalize(value, min, max);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-10, 0, 100, 0)]
    [InlineData(150, 0, 100, 100)]
    [InlineData(-100, 0, 50, 0)]
    [InlineData(200, 0, 50, 100)]
    public void Normalize_OutsideRange_ClampedToBounds(decimal value, decimal min, decimal max, decimal expected)
    {
        var result = _sut.Normalize(value, min, max);
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_MinEqualsMax_Returns50()
    {
        var result = _sut.Normalize(50m, 50m, 50m);
        result.Should().Be(50m);
    }

    [Fact]
    public void Normalize_VariousInputs_AlwaysInRange()
    {
        var inputs = new[] { -100m, -50m, 0m, 25m, 50m, 75m, 100m, 150m, 200m };
        foreach (var v in inputs)
        {
            var result = _sut.Normalize(v, 0m, 50m);
            result.Should().BeGreaterOrEqualTo(0m);
            result.Should().BeLessOrEqualTo(100m);
        }
    }
}
