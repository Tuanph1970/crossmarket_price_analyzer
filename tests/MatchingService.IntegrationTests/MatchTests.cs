using Common.Domain.Enums;
using FluentAssertions;
using MatchingService.Application.Persistence;
using MatchingService.Application.Services;
using MatchingService.Domain.Entities;
using Xunit;

namespace MatchingService.IntegrationTests;

public class MatchTests : IClassFixture<MatchingServiceTestFixture>
{
    private readonly MatchingServiceTestFixture _fixture;

    public MatchTests(MatchingServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FuzzyMatchingService_ComputeScore_SameBrand_HighScore()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var fuzzyService = scope.ServiceProvider.GetRequiredService<FuzzyMatchingService>();

        // Act
        var score = fuzzyService.ComputeMatchScore(
            usProductName: "Apple iPhone 15 Pro 256GB",
            vnProductName: "iPhone 15 Pro 256GB Apple",
            usBrand: "Apple",
            vnBrand: "Apple");

        // Assert
        score.Should().BeGreaterThan(80m,
            "identical brand names with similar product names should produce a score > 80");
    }

    [Fact]
    public async Task FuzzyMatchingService_ComputeScore_DifferentBrand_LowScore()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var fuzzyService = scope.ServiceProvider.GetRequiredService<FuzzyMatchingService>();

        // Act
        var score = fuzzyService.ComputeMatchScore(
            usProductName: "Samsung Galaxy S24 Ultra",
            vnProductName: "Apple iPhone 15 Pro",
            usBrand: "Samsung",
            vnBrand: "Apple");

        // Assert
        score.Should().BeLessThan(50m,
            "completely different brands and product lines should produce a score < 50");
    }

    [Fact]
    public async Task FuzzyMatchingService_ComputeScore_NoMatch_ReturnsZero()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var fuzzyService = scope.ServiceProvider.GetRequiredService<FuzzyMatchingService>();

        // Act
        var score = fuzzyService.ComputeMatchScore(
            usProductName: "",
            vnProductName: "iPhone 15",
            usBrand: null,
            vnBrand: "Apple");

        // Assert
        score.Should().Be(0m, "empty US product name should yield a score of 0");
    }

    [Fact]
    public async Task ProductMatchRepository_Create_Confirm_Reject()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductMatchRepository>();

        var usProductId = Guid.NewGuid();
        var vnProductId = Guid.NewGuid();

        var match = ProductMatch.Create(
            usProductId: usProductId,
            vnProductId: vnProductId,
            confidenceScore: 75m,
            status: MatchStatus.Pending);

        // Act — Create
        await repo.AddAsync(match);
        var created = await repo.GetByIdAsync(match.Id);

        // Assert — Created state
        created.Should().NotBeNull();
        created!.Status.Should().Be(MatchStatus.Pending);
        created.ConfirmedBy.Should().BeNull();
        created.ConfirmedAt.Should().BeNull();
        created.Confirmations.Should().BeEmpty();

        // Act — Confirm
        created.Confirm("user-001", "Verified manually");
        await repo.UpdateAsync(created);
        var confirmed = await repo.GetByIdAsync(created.Id);

        // Assert — Confirmed state
        confirmed.Should().NotBeNull();
        confirmed!.Status.Should().Be(MatchStatus.Confirmed);
        confirmed.ConfirmedBy.Should().Be("user-001");
        confirmed.ConfirmedAt.Should().NotBeNull();
        confirmed.Confirmations.Should().HaveCount(1);
        confirmed.Confirmations.First().Action.Should().Be(ConfirmAction.Confirmed);

        // Act — Reject (create a new pending match)
        var matchToReject = ProductMatch.Create(
            usProductId: Guid.NewGuid(),
            vnProductId: Guid.NewGuid(),
            confidenceScore: 30m,
            status: MatchStatus.Pending);

        await repo.AddAsync(matchToReject);
        matchToReject.Reject("user-002", "Wrong product pairing");
        await repo.UpdateAsync(matchToReject);
        var rejected = await repo.GetByIdAsync(matchToReject.Id);

        // Assert — Rejected state
        rejected.Should().NotBeNull();
        rejected!.Status.Should().Be(MatchStatus.Rejected);
        rejected.ConfirmedBy.Should().Be("user-002");
        rejected.Confirmations.Should().HaveCount(1);
        rejected.Confirmations.First().Action.Should().Be(ConfirmAction.Rejected);
    }

    [Fact]
    public async Task ProductMatchRepository_GetByStatus_FiltersCorrectly()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductMatchRepository>();

        var usId1 = Guid.NewGuid();
        var vnId1 = Guid.NewGuid();
        var usId2 = Guid.NewGuid();
        var vnId2 = Guid.NewGuid();

        var pendingMatch = ProductMatch.Create(usId1, vnId1, 60m, MatchStatus.Pending);
        var confirmedMatch = ProductMatch.Create(usId2, vnId2, 80m, MatchStatus.Pending);

        await repo.AddAsync(pendingMatch);
        await repo.AddAsync(confirmedMatch);
        confirmedMatch.Confirm("admin");
        await repo.UpdateAsync(confirmedMatch);

        // Act
        var pending = await repo.GetByStatusAsync(MatchStatus.Pending);
        var confirmed = await repo.GetByStatusAsync(MatchStatus.Confirmed);

        // Assert
        pending.Select(m => m.Id).Should().Contain(pendingMatch.Id);
        confirmed.Select(m => m.Id).Should().Contain(confirmedMatch.Id);
    }

    [Fact]
    public async Task ProductMatchRepository_GetByUsProductId_ReturnsCorrectMatches()
    {
        // Arrange
        await using var scope = _fixture.WebAppFactory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductMatchRepository>();

        var usId = Guid.NewGuid();
        var vnId1 = Guid.NewGuid();
        var vnId2 = Guid.NewGuid();

        var match1 = ProductMatch.Create(usId, vnId1, 90m);
        var match2 = ProductMatch.Create(usId, vnId2, 70m);

        await repo.AddAsync(match1);
        await repo.AddAsync(match2);

        // Act
        var results = await repo.GetByUsProductIdAsync(usId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(m => m.VnProductId == vnId1);
        results.Should().Contain(m => m.VnProductId == vnId2);
        results.First().ConfidenceScore.Should().BeGreaterOrEqualTo(results.Last().ConfidenceScore);
    }
}
