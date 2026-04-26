using Common.Domain.Scraping;
using Microsoft.Extensions.Logging;
using ProductService.Application.Commands;
using ProductService.Application.DTOs;
using ProductService.Application.Services;

namespace ProductService.Application.Handlers;

public sealed class ScrapeListingCommandHandler
    : MediatR.IRequestHandler<ScrapeListingCommand, ScrapeListingResultDto>
{
    private readonly IEnumerable<IProductScraper> _scrapers;
    private readonly IProductService _productService;
    private readonly ILogger<ScrapeListingCommandHandler> _logger;

    public ScrapeListingCommandHandler(
        IEnumerable<IProductScraper> scrapers,
        IProductService productService,
        ILogger<ScrapeListingCommandHandler> logger)
    {
        _scrapers = scrapers;
        _productService = productService;
        _logger = logger;
    }

    public async Task<ScrapeListingResultDto> Handle(ScrapeListingCommand cmd, CancellationToken ct)
    {
        var scraper = _scrapers.FirstOrDefault(s => s.CanHandle(cmd.PageUrl))
            ?? throw new InvalidOperationException(
                $"No scraper registered for URL: {cmd.PageUrl}. Supported: amazon.com, amazon.co.uk, cigarpage.com");

        // Single-fetch approach: parse all products directly from the listing page
        var products = await scraper.ScrapeListingDirectAsync(cmd.PageUrl, cmd.MaxProducts, ct);

        if (products.Count == 0)
            throw new InvalidOperationException(
                $"No products found on page: {cmd.PageUrl}. " +
                "Make sure this is a category/listing page (e.g. cigarpage.com/samplers/...).");

        _logger.LogInformation("ScrapeListingCommand: extracted {Count} products from {PageUrl}",
            products.Count, cmd.PageUrl);

        var dtos = products.Select(p => new ScrapedProductDto(
            p.Name, p.Brand, p.Sku, p.Price, p.Currency, p.SourceUrl, p.Source
        )).ToList();

        // Persist all scraped products to ProductService DB (enables price history + future matching)
        foreach (var p in products)
        {
            try
            {
                await _productService.UpsertFromScrapeAsync(
                    p.Name, p.Brand, p.Sku,
                    p.Price, p.Currency, p.QuantityPerUnit,
                    p.SellerName, p.SellerRating, p.SalesVolume,
                    p.SourceUrl, p.Source, null, null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScrapeListingCommand: failed to persist product '{Name}'", p.Name);
            }
        }

        return new ScrapeListingResultDto(cmd.PageUrl, dtos, dtos.Count, DateTime.UtcNow);
    }
}
