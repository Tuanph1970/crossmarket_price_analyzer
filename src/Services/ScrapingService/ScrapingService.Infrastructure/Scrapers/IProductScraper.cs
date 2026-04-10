// This file intentionally left minimal.
// IProductScraper lives in Common.Domain.Scraping.
// ScrapedProduct lives in Common.Domain.Scraping.
// Both are used directly by scraper implementations in this project.
// The local duplicate was removed to avoid shadowing.
// If you need to re-add a local interface, ensure it doesn't conflict with Common.Domain.Scraping.IProductScraper.