using ProductService.Application.DTOs;

namespace ProductService.Application.Commands;

/// <summary>
/// Scrapes a category/listing page, extracts individual product URLs, then scrapes each one.
/// Usage: POST /api/products/scrape-listing { PageUrl: "https://www.cigarpage.com/samplers/...", MaxProducts: 15 }
/// </summary>
public record ScrapeListingCommand(
    string PageUrl,
    int MaxProducts = 15
) : MediatR.IRequest<ScrapeListingResultDto>;
