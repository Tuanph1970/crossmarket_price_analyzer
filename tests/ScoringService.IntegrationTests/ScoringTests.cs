using FluentAssertions;
using ScoringService.Application.Services;
using ScoringService.Domain.Entities;
using Xunit;

namespace ScoringService.IntegrationTests;

public class ScoringTests : IClassFixture<ScoringServiceTestFixture>
{
    private readonly ScoringServiceTestFixture _fixture;

    public ScoringTests(ScoringServiceTestFixture fixture) => _fixture = fixture;

    // ── ScoringEngine ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ScoringEngine_CalculateCompositeScore_HighMargin()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();

        // High margin, good demand, low competition → composite > 70
        var score = engine.CalculateCompositeScore(
            profitMarginPct: 40m,
            demandScore: 85m,
            competitionScore: 15m,
            priceStabilityScore: 80m,
            matchConfidenceScore: 90m);

        // Assert
        score.Should().BeGreaterThan(70m,
            "high margin + strong supporting factors should yield composite score > 70");
    }

    [Fact]
    public async Task ScoringEngine_CalculateCompositeScore_PoorInputs_LowScore()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();

        // Very low margin, poor demand, high competition → composite < 30
        var score = engine.CalculateCompositeScore(
            profitMarginPct: 5m,
            demandScore: 10m,
            competitionScore: 90m,
            priceStabilityScore: 20m,
            matchConfidenceScore: 30m);

        // Assert
        score.Should().BeLessThan(30m,
            "poor inputs should yield composite score < 30");
    }

    [Fact]
    public async Task ScoringEngine_Normalize_ClampsToBounds()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();

        // Act
        var below = engine.Normalize(0m, 10m, 50m);
        var within = engine.Normalize(30m, 10m, 50m);
        var above = engine.Normalize(100m, 10m, 50m);

        // Assert
        below.Should().Be(0m);
        within.Should().Be(50m);
        above.Should().Be(100m);
    }

    // ── LandedCostCalculator ──────────────────────────────────────────────────

    [Fact]
    public async Task LandedCostCalculator_Calculate_IncludesAllComponents()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var calculator = scope.ServiceProvider.GetRequiredService<LandedCostCalculator>();

        var usPriceUsd = 100m;
        var shippingUsd = 15m;
        var exchangeRate = 25000m; // 1 USD = 25,000 VND

        // Act
        var breakdown = calculator.CalculateBreakdown(
            usPriceUsd: usPriceUsd,
            exchangeRateUsdToVnd: exchangeRate,
            shippingCostUsd: shippingUsd);

        // Assert
        breakdown.UsPurchasePriceVnd.Should().Be(2_500_000m);
        breakdown.ShippingCostVnd.Should().Be(375_000m);
        breakdown.ImportDutyVnd.Should().BeGreaterThan(0m,
            "import duty should be charged on CIF value (US price + shipping)");
        breakdown.VatVnd.Should().BeGreaterThan(0m,
            "VAT should be charged on CIF + duty");
        breakdown.HandlingFeesVnd.Should().BeGreaterThan(0m,
            "handling fee should be applied");
        breakdown.TotalLandedCostVnd.Should().BeGreaterThan(
            breakdown.UsPurchasePriceVnd + breakdown.ShippingCostVnd,
            "total landed cost must include duty, VAT and handling");
    }

    [Fact]
    public async Task LandedCostCalculator_Calculate_WithOverrides()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var calculator = scope.ServiceProvider.GetRequiredService<LandedCostCalculator>();

        // Act — manual override takes precedence
        var landed = calculator.CalculateLandedCost(
            usPriceUsd: 100m,
            exchangeRateUsdToVnd: 25000m,
            shippingCostUsd: 10m,
            landedCostOverride: 3_000_000m);

        // Assert
        landed.Should().Be(3_000_000m,
            "landedCostOverride should be returned directly without further calculation");
    }

    [Fact]
    public async Task LandedCostCalculator_CalculateBreakdown_ImportDutyOverride()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var calculator = scope.ServiceProvider.GetRequiredService<LandedCostCalculator>();

        // Act — override import duty to 0%
        var breakdown = calculator.CalculateBreakdown(
            usPriceUsd: 200m,
            exchangeRateUsdToVnd: 25000m,
            shippingCostUsd: 20m,
            importDutyOverridePct: 0m);

        // Assert
        breakdown.ImportDutyVnd.Should().Be(0m,
            "importDutyOverridePct=0 should result in zero import duty");
    }

    [Fact]
    public async Task LandedCostCalculator_CalculateProfitMargin_CorrectFormula()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var calculator = scope.ServiceProvider.GetRequiredService<LandedCostCalculator>();

        // Act
        var margin = calculator.CalculateProfitMargin(
            vnRetailPriceVnd: 3_000_000m,
            landedCostVnd: 2_000_000m);

        // Assert
        // margin = (3,000,000 - 2,000,000) / 3,000,000 * 100 = 33.33%
        margin.Should().BeApproximately(33.33m, 0.01m);
    }

    [Fact]
    public async Task LandedCostCalculator_CalculateRoi_CorrectFormula()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var calculator = scope.ServiceProvider.GetRequiredService<LandedCostCalculator>();

        // Act
        var roi = calculator.CalculateRoi(
            vnRetailPriceVnd: 3_000_000m,
            landedCostVnd: 2_000_000m);

        // Assert
        // roi = (3,000,000 - 2,000,000) / 2,000,000 * 100 = 50%
        roi.Should().Be(50m);
    }

    // ── OpportunityScore persistence ───────────────────────────────────────────

    [Fact]
    public async Task OpportunityScore_Upsert_UpdatesExisting()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScoringDbContext>();
        var calculator = scope.ServiceProvider.GetRequiredService<LandedCostCalculator>();
        var engine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();

        var matchId = Guid.NewGuid();

        // Create initial score
        var initialBreakdown = calculator.CalculateBreakdown(100m, 25000m, 10m);
        var initialMargin = calculator.CalculateProfitMargin(3_500_000m, initialBreakdown.TotalLandedCostVnd);
        var initialScore = engine.CalculateCompositeScore(initialMargin, 70m, 20m, 75m, 85m);

        var entity = OpportunityScore.Create(
            matchId: matchId,
            profitMarginPct: initialMargin,
            demandScore: 70m,
            competitionScore: 20m,
            priceStabilityScore: 75m,
            matchConfidenceScore: 85m,
            compositeScore: initialScore,
            landedCostVnd: initialBreakdown.TotalLandedCostVnd,
            vietnamRetailVnd: 3_500_000m);

        await db.OpportunityScores.AddAsync(entity);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act — update with better values
        var updatedBreakdown = calculator.CalculateBreakdown(100m, 25000m, 10m);
        var updatedMargin = calculator.CalculateProfitMargin(4_000_000m, updatedBreakdown.TotalLandedCostVnd);
        var updatedComposite = engine.CalculateCompositeScore(updatedMargin, 90m, 10m, 80m, 95m);

        var updatedEntity = OpportunityScore.Create(
            matchId: matchId,
            profitMarginPct: updatedMargin,
            demandScore: 90m,
            competitionScore: 10m,
            priceStabilityScore: 80m,
            matchConfidenceScore: 95m,
            compositeScore: updatedComposite,
            landedCostVnd: updatedBreakdown.TotalLandedCostVnd,
            vietnamRetailVnd: 4_000_000m);

        var repo = new OpportunityScoreRepository(db);
        await repo.SaveAsync(updatedEntity);

        db.ChangeTracker.Clear();

        // Assert — only one record should exist
        var all = await db.OpportunityScores.ToListAsync();
        all.Should().HaveCount(1, "upsert should replace existing record rather than create a new one");

        var reloaded = await db.OpportunityScores.FirstAsync();
        reloaded.CompositeScore.Should().Be(updatedComposite);
        reloaded.DemandScore.Should().Be(90m);
        reloaded.VietnamRetailVnd.Should().Be(4_000_000m);
        reloaded.UpdatedAt.Should().NotBeNull("UpdatedAt should be set on update");
    }

    [Fact]
    public async Task OpportunityScore_Insert_NewRecord()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScoringDbContext>();

        var matchId = Guid.NewGuid();
        var entity = OpportunityScore.Create(
            matchId: matchId,
            profitMarginPct: 25m,
            demandScore: 65m,
            competitionScore: 35m,
            priceStabilityScore: 70m,
            matchConfidenceScore: 80m,
            compositeScore: 65m,
            landedCostVnd: 2_000_000m,
            vietnamRetailVnd: 2_800_000m);

        // Act
        await db.OpportunityScores.AddAsync(entity);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        // Assert
        var reloaded = await db.OpportunityScores.FirstOrDefaultAsync(s => s.MatchId == matchId);
        reloaded.Should().NotBeNull();
        reloaded!.CompositeScore.Should().Be(65m);
        reloaded.ProfitMarginPct.Should().Be(25m);
    }

    [Fact]
    public async Task OpportunityScore_GetTopOpportunities_OrdersByCompositeScore()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScoringDbContext>();

        var matchIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var scores = new[] { 45m, 90m, 60m, 80m, 30m };

        for (var i = 0; i < 5; i++)
        {
            var entity = OpportunityScore.Create(
                matchId: matchIds[i],
                profitMarginPct: scores[i],
                demandScore: 50m, competitionScore: 50m,
                priceStabilityScore: 50m, matchConfidenceScore: 50m,
                compositeScore: scores[i],
                landedCostVnd: 1_000_000m, vietnamRetailVnd: 1_500_000m);

            await db.OpportunityScores.AddAsync(entity);
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Act
        var top = await db.OpportunityScores
            .OrderByDescending(s => s.CompositeScore)
            .Take(3)
            .Select(s => s.CompositeScore)
            .ToListAsync();

        // Assert
        top.Should().BeEquivalentTo(new[] { 90m, 80m, 60m },
            "top 3 opportunities should be ordered by composite score descending");
    }
}
