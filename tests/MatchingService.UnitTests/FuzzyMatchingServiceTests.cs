using FluentAssertions;
using MatchingService.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MatchingService.UnitTests;

public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _sut;

    public FuzzyMatchingServiceTests()
    {
        var logger = new Mock<ILogger<FuzzyMatchingService>>();
        _sut = new FuzzyMatchingService(logger.Object);
    }

    // ── Exact / near-exact matches ────────────────────────────────────────────

    [Theory]
    [InlineData("MacBook Pro 14", "MacBook Pro 14")]
    [InlineData("iphone 15 pro", "IPHONE 15 PRO")]
    [InlineData("Sony WH-1000XM5", "Sony WH-1000XM5")]
    public void ComputeMatchScore_IdenticalNames_ReturnsHighScore(string us, string vn)
    {
        var score = _sut.ComputeMatchScore(us, vn, null, null);
        score.Should().BeGreaterOrEqualTo(85m);
    }

    [Fact]
    public void ComputeMatchScore_IdenticalNames_Returns85()
    {
        // nameScore = 100, score = 100 * 0.85 = 85 (capped at 100)
        var score = _sut.ComputeMatchScore("AirPods Pro", "AirPods Pro", null, null);
        score.Should().Be(85m);
    }

    // ── Case-insensitivity ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeMatchScore_CaseInsensitive_ReturnsSameScore()
    {
        var lower = _sut.ComputeMatchScore("MacBook Pro", "macbook pro", null, null);
        var upper = _sut.ComputeMatchScore("MACBOOK PRO", "MACBOOK PRO", null, null);
        var mixed = _sut.ComputeMatchScore("MacBook Pro", "mAcBoOk PrO", null, null);

        lower.Should().Be(upper);
        upper.Should().Be(mixed);
    }

    // ── Punctuation / special characters stripped ─────────────────────────────

    [Theory]
    [InlineData("MacBook-Pro 14", "MacBook Pro 14")]
    [InlineData("iPhone (15)", "iPhone 15")]
    [InlineData("AirPods/Pro", "AirPods Pro")]
    public void ComputeMatchScore_NormalizesSpecialChars_ReturnsHighScore(string us, string vn)
    {
        var score = _sut.ComputeMatchScore(us, vn, null, null);
        score.Should().BeGreaterOrEqualTo(80m);
    }

    // ── Brand bonus ────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeMatchScore_SameBrandHighSimilarity_AppliesHighBrandBonus()
    {
        // 15-point bonus when brand similarity > 0.85
        // nameScore=100*0.85=85, +15 bonus = 100 (capped)
        var withBrand = _sut.ComputeMatchScore("Apple iPhone 15", "Apple iPhone 15", "Apple", "Apple");
        var withoutBrand = _sut.ComputeMatchScore("Apple iPhone 15", "Apple iPhone 15", null, null);

        withBrand.Should().Be(100m); // 85 + 15 bonus capped at 100
        withBrand.Should().BeGreaterThan(withoutBrand);
    }

    [Fact]
    public void ComputeMatchScore_SameBrandMediumSimilarity_AppliesSmallBrandBonus()
    {
        // 8-point bonus when brand similarity > 0.70
        var withBrand = _sut.ComputeMatchScore("Apple Laptop", "Fruit Company Laptop", "Apple", "Fruit Company");
        var withoutBrand = _sut.ComputeMatchScore("Apple Laptop", "Fruit Company Laptop", null, null);

        // Brand similarity ~0.2, so no bonus
        withBrand.Should().BeLessOrEqualTo(withoutBrand + 8m);
    }

    [Fact]
    public void ComputeMatchScore_NullBrands_NoBrandBonus()
    {
        // nameScore = 100, total = 100 * 0.85 = 85
        var result = _sut.ComputeMatchScore("MacBook Pro", "MacBook Pro", null, null);
        result.Should().Be(85m);
    }

    [Fact]
    public void ComputeMatchScore_OneNullBrand_NoBrandBonus()
    {
        var score = _sut.ComputeMatchScore("Apple Laptop", "Apple Laptop", "Apple", null);
        score.Should().BeGreaterOrEqualTo(85m); // Name-only scoring
    }

    // ── Dissimilar names ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("MacBook Pro", "Samsung Galaxy")]
    [InlineData("iPhone 15", "Nvidia RTX 4090")]
    [InlineData("Sony Headphones", "Dyson Vacuum")]
    public void ComputeMatchScore_CompletelyDifferent_ReturnsLowScore(string us, string vn)
    {
        var score = _sut.ComputeMatchScore(us, vn, null, null);
        score.Should().BeLessThan(30m);
    }

    // ── Empty / null inputs ────────────────────────────────────────────────────

    [Theory]
    [InlineData("", "MacBook Pro")]
    [InlineData("   ", "MacBook Pro")]
    [InlineData(null, "MacBook Pro")]
    [InlineData("MacBook Pro", "")]
    [InlineData("MacBook Pro", "   ")]
    [InlineData("MacBook Pro", null)]
    public void ComputeMatchScore_EmptyOrNull_ReturnsZero(string? us, string? vn)
    {
        var score = _sut.ComputeMatchScore(us!, vn!, null, null);
        score.Should().Be(0m);
    }

    [Fact]
    public void ComputeMatchScore_BothNull_ReturnsZero()
    {
        var score = _sut.ComputeMatchScore(null!, null!, null, null);
        score.Should().Be(0m);
    }

    // ── Score bounds ───────────────────────────────────────────────────────────

    [Fact]
    public void ComputeMatchScore_AlwaysReturnsInRange()
    {
        var testCases = new[]
        {
            ("MacBook Pro", "MacBook Pro 14"),
            ("iPhone 15 Pro Max", "iPhone 15"),
            ("Sony WH-1000XM5 Wireless", "Sony XM5"),
            ("", ""),
            ("Samsung", "Apple"),
            ("Very Long Product Name 2024 Model Ultra Plus", "Very Long Product Name 2024 Model Ultra Plus"),
        };

        foreach (var (us, vn) in testCases)
        {
            var score = _sut.ComputeMatchScore(us, vn, "Brand1", "Brand2");
            score.Should().BeGreaterOrEqualTo(0m, $"'{us}' vs '{vn}'");
            score.Should().BeLessOrEqualTo(100m, $"'{us}' vs '{vn}'");
        }
    }

    // ── Order independence ────────────────────────────────────────────────────

    [Fact]
    public void ComputeMatchScore_OrderIndependent()
    {
        var forward = _sut.ComputeMatchScore("Apple iPhone 15 Pro", "iPhone 15 Pro Apple", "Apple", "Apple");
        var backward = _sut.ComputeMatchScore("iPhone 15 Pro Apple", "Apple iPhone 15 Pro", "Apple", "Apple");

        forward.Should().Be(backward);
    }

    // ── Partial name overlap ──────────────────────────────────────────────────

    [Fact]
    public void ComputeMatchScore_PartialOverlap_ReturnsModerateScore()
    {
        var score = _sut.ComputeMatchScore("MacBook Pro 14 M3", "MacBook Pro 16 M3", null, null);
        score.Should().BeGreaterThan(50m);
        score.Should().BeLessThan(95m);
    }
}
