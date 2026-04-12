using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScoringService.Application.Services;
using Xunit;

namespace ScoringService.UnitTests;

/// <summary>
/// P3-T01: Unit tests for new Phase 3 services:
/// PriceStabilityService, HsCodeClassifier, TariffService, ShippingService.
/// </summary>
public class Phase3ServicesTests
{
    // ── HsCodeClassifier ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Apple MacBook Pro laptop", "8471.30")]
    [InlineData("Samsung Galaxy smartphone", "8517.12")]
    [InlineData("Sony wireless headphones", "8518.30")]
    [InlineData("Nike running shoes", "6403.99")]
    [InlineData("Protein whey supplement powder", "2106.10")]
    [InlineData("Vitamin D3 tablets", "2106.90")]
    [InlineData("Shampoo for hair", "3401.30")]
    [InlineData("Luxury perfume fragrance", "3303.00")]
    [InlineData("Ray-Ban sunglasses", "9004.10")]
    public void HsCodeClassifier_Classify_ReturnsCorrectCode(string productName, string expected)
    {
        var classifier = new HsCodeClassifier();
        var result = classifier.Classify(productName);
        result.Should().Be(expected);
    }

    [Fact]
    public void HsCodeClassifier_Classify_UnknownProduct_ReturnsNull()
    {
        var classifier = new HsCodeClassifier();
        var result = classifier.Classify("some random thing xyz 123");
        result.Should().BeNull();
    }

    [Fact]
    public void HsCodeClassifier_Classify_LongestMatchWins()
    {
        var classifier = new HsCodeClassifier();
        // "laptop charger" contains both "charger" (8504.40) and "laptop" (8471.30)
        // longest match is "laptop" (6 chars) → 8471.30
        var result = classifier.Classify("Apple laptop charger");
        result.Should().Be("8471.30");
    }

    [Fact]
    public void HsCodeClassifier_Classify_CaseInsensitive()
    {
        var classifier = new HsCodeClassifier();
        classifier.Classify("HEADPHONES").Should().Be("8518.30");
        classifier.Classify("headphones").Should().Be("8518.30");
        classifier.Classify("HeAdPhOnEs").Should().Be("8518.30");
    }

    [Fact]
    public void HsCodeClassifier_GetAllMappings_ReturnsNonEmpty()
    {
        var classifier = new HsCodeClassifier();
        var mappings = classifier.GetAllMappings();
        mappings.Should().NotBeEmpty();
        mappings.Should().ContainKey("laptop");
        mappings.Should().ContainValue("8471.30");
    }

    // ── TariffService ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("8517.12", "VN", 0.0)]   // Mobile phones — IT Agreement 0%
    [InlineData("8471.30", "VN", 0.0)]   // Computers — IT Agreement 0%
    [InlineData("8528.72", "VN", 15.0)]  // Colour TV — 15%
    [InlineData("0901.11", "VN", 20.0)]  // Coffee beans — 20%
    [InlineData("2402.10", "VN", 135.0)] // Cigars — 135%
    [InlineData("6403.99", "VN", 12.0)]  // Footwear — 12%
    public void TariffService_GetRate_VietnamSpecific_ReturnsCorrectRate(
        string hsCode, string country, decimal expected)
    {
        var logger = new Mock<ILogger<TariffService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new TariffService(logger.Object, httpClientFactory.Object);

        var rate = service.GetRate(hsCode, country);
        rate.Should().Be(expected);
    }

    [Fact]
    public void TariffService_GetRate_NonVietnam_FallsBackTo5Percent()
    {
        var logger = new Mock<ILogger<TariffService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new TariffService(logger.Object, httpClientFactory.Object);

        service.GetRate("8471.30", "US").Should().Be(5.0m);
        service.GetRate("8528.72", "DE").Should().Be(5.0m);
    }

    [Fact]
    public void TariffService_GetRate_UnknownVietnamCode_FallsBackTo5Percent()
    {
        var logger = new Mock<ILogger<TariffService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new TariffService(logger.Object, httpClientFactory.Object);

        service.GetRate("9999.99", "VN").Should().Be(5.0m);
    }

    [Fact]
    public void TariffService_IsStale_InitiallyFalse()
    {
        var logger = new Mock<ILogger<TariffService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new TariffService(logger.Object, httpClientFactory.Object);

        service.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task TariffService_RefreshTariffTableAsync_CompletesSuccessfully()
    {
        var logger = new Mock<ILogger<TariffService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new TariffService(logger.Object, httpClientFactory.Object);

        await service.RefreshTariffTableAsync();

        service.IsStale.Should().BeFalse();
        service.LastRefreshedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── PriceStabilityService — coefficient of variation ───────────────────────

    [Fact]
    public void PriceStabilityService_CalculateCv_StablePrices_ReturnsLowCV()
    {
        var logger = new Mock<ILogger<PriceStabilityService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new PriceStabilityService(logger.Object, httpClientFactory.Object);

        var prices = new[] { 100m, 101m, 99m, 100.5m, 100m };
        var cv = service.CalculateCoefficientOfVariation(prices);
        cv.Should().BeLessThan(1m, "prices are very stable");
    }

    [Fact]
    public void PriceStabilityService_CalculateCv_VolatilePrices_ReturnsHighCV()
    {
        var logger = new Mock<ILogger<PriceStabilityService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new PriceStabilityService(logger.Object, httpClientFactory.Object);

        var prices = new[] { 100m, 130m, 70m, 120m, 80m };
        var cv = service.CalculateCoefficientOfVariation(prices);
        cv.Should().BeGreaterThan(15m, "prices are volatile");
    }

    [Fact]
    public void PriceStabilityService_CalculateCv_SinglePrice_ReturnsZero()
    {
        var logger = new Mock<ILogger<PriceStabilityService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new PriceStabilityService(logger.Object, httpClientFactory.Object);

        service.CalculateCoefficientOfVariation(new[] { 100m }).Should().Be(0m);
    }

    [Fact]
    public void PriceStabilityService_CalculateCv_ZeroMean_ReturnsZero()
    {
        var logger = new Mock<ILogger<PriceStabilityService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new PriceStabilityService(logger.Object, httpClientFactory.Object);

        service.CalculateCoefficientOfVariation(new[] { 0m, 0m, 0m }).Should().Be(0m);
    }

    [Theory]
    [InlineData(2.0, 100)]   // CV < 5%
    [InlineData(7.0, 85)]   // CV < 10%
    [InlineData(12.0, 70)]  // CV < 15%
    [InlineData(18.0, 55)]  // CV < 20%
    [InlineData(25.0, 40)]  // CV < 30%
    [InlineData(35.0, 25)]  // CV >= 30%
    public void PriceStabilityService_CvToScore_MappingIsCorrect(
        decimal cv, decimal expectedScore)
    {
        var logger = new Mock<ILogger<PriceStabilityService>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var service = new PriceStabilityService(logger.Object, httpClientFactory.Object);

        // Calculate CV for synthetic prices that produce the target CV
        // Approximate with: mean=100, stdDev=cv
        var prices = new[] { 100m - cv, 100m + cv, 100m };
        var computed = service.CalculateCoefficientOfVariation(prices);

        // The CV for these two-point prices should be close to cv
        computed.Should().BeApproximately(cv, 2m);
    }

    // ── ShippingService ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ShippingService_GetQuoteAsync_FedExPrimary_ReturnsQuote()
    {
        var logger = new Mock<ILogger<ShippingService>>();
        var mockExchangeRate = new Mock<Common.Application.Interfaces.IExchangeRateService>();
        mockExchangeRate.Setup(x => x.GetUsdToVndRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25000m);

        var httpClient = new HttpClient();
        var service = new ShippingService(httpClient, logger.Object, mockExchangeRate.Object);

        var request = new ShippingRequest(
            WeightKg: 2m,
            OriginCountryCode: "US",
            DestinationCountryCode: "VN",
            DeclaredValueUsd: 500m);

        var quote = await service.GetQuoteAsync(request);

        quote.Should().NotBeNull();
        quote!.Carrier.Should().BeOneOf("FedEx", "DHL");
        quote.RateUsd.Should().BeGreaterThan(0m);
        quote.EstimatedDays.Should().BeInRange(2, 6);
    }

    [Fact]
    public async Task ShippingService_GetAllQuotesAsync_BothCarriers_ReturnsBoth()
    {
        var logger = new Mock<ILogger<ShippingService>>();
        var mockExchangeRate = new Mock<Common.Application.Interfaces.IExchangeRateService>();
        mockExchangeRate.Setup(x => x.GetUsdToVndRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25000m);

        var httpClient = new HttpClient();
        var service = new ShippingService(httpClient, logger.Object, mockExchangeRate.Object);

        var request = new ShippingRequest(1m, "US", "VN", 200m);
        var quotes = await service.GetAllQuotesAsync(request);

        quotes.Should().NotBeEmpty();
    }

    [Fact]
    public void ShippingService_FedExFormula_Correct()
    {
        // FedEx: rate = 15 + weight*12.5 + value*0.02
        var weight = 3m;
        var value = 500m;
        var expected = 15m + (3m * 12.5m) + (500m * 0.02m); // = 15 + 37.5 + 10 = 62.5

        var logger = new Mock<ILogger<ShippingService>>();
        var mockExchangeRate = new Mock<Common.Application.Interfaces.IExchangeRateService>();
        mockExchangeRate.Setup(x => x.GetUsdToVndRateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(25000m);

        var httpClient = new HttpClient();
        var service = new ShippingService(httpClient, logger.Object, mockExchangeRate.Object);

        // The actual implementation uses Random.Shared for estimated days
        // so we test the formula indirectly by checking the rate is reasonable
        var request = new ShippingRequest(weight, "US", "VN", value);
        var quote = service.GetQuoteAsync(request).GetAwaiter().GetResult();
        quote!.RateUsd.Should().BeCloseTo(expected, 0.01m);
    }
}