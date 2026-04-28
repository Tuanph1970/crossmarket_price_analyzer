# TODO — VN Scraper Work (resume from coffee shop)

Session checkpoint: 2026-04-28. Commit `b4fc1bbd` on `main` (local, not pushed).

## Hypothesis to verify first

Shopee + Lazada both refused to serve product data from this network's egress IP (Shopee returned `error 90309999`, Lazada served an SPA shell with no product grid). **Likely cause: home/office firewall or geo/IP reputation, not a bug in the scrapers.** Test from a different network before any more code changes.

## Quick smoke test on the new network

```bash
docker compose up -d scraping-worker
docker logs -f cma-scraping-worker
```

The worker will fire `VnProductScrapingJob` 60 seconds after startup (TEMP trigger — see below). Watch for:

- ✅ `Lazada SMOKE-TEST 'cigar' → N products. First 3: ...` — Lazada works, no further action needed
- ⚠️ `Lazada 'cigar': no product grid. URL=... Title='...'` — still blocked; check the URL field for captcha redirect

Then test Shopee by temporarily swapping the smoke-test method call (see `VnProductScrapingJob.cs` Execute method).

## TEMP scaffolding to revert when scrapers work

Search for `TEMP` in these files:

### `src/Services/ScrapingService/ScrapingService.Worker/JobsConfiguration.cs` (line ~25)
Restore daily cron:
```csharp
quartz.AddTrigger(t => t
    .ForJob("VnProductScrapingJob")
    .WithIdentity("VnProductScrapingJob-trigger")
    .WithCronSchedule("0 0 3 * * ?"));
```

### `src/Services/ScrapingService/ScrapingService.Worker/Jobs/VnProductScrapingJob.cs` (Execute method)
Restore the proper multi-source flow:
```csharp
public async Task Execute(IJobExecutionContext context)
{
    var startedAt = DateTime.UtcNow;
    _logger.LogInformation("VnProductScrapingJob started");

    var total = 0;
    total += await ScrapeTikiAsync(context.CancellationToken);
    total += await ScrapeShopeeAsync(context.CancellationToken);
    total += await ScrapeLazadaAsync(context.CancellationToken);  // need to add this method

    _logger.LogInformation(
        "VnProductScrapingJob completed: {Total} products in {Duration:F1}s",
        total, (DateTime.UtcNow - startedAt).TotalSeconds);
}
```

`ScrapeLazadaAsync` doesn't exist yet — mirror `ScrapeShopeeAsync` (same shape, replace `_shopeeClient` → `_lazadaClient`, `ShopeeKeywords` → `LazadaKeywords`, `SaveShopeeProductAsync` → write a `SaveLazadaProductAsync` helper that maps `LazadaProduct` to the upsert payload).

## Unrelated bugs that surfaced during this session

These block end-to-end verification but aren't on the scraper critical path:

1. **`POST /api/products/upsert-from-scrape` returns 400 on enum field**
   - `ProductService.Api/Program.cs` doesn't register `JsonStringEnumConverter`, so it expects integer source codes.
   - Worker sends `"source": "Tiki"` (string), API expects `"source": 4` or similar.
   - Fix: add `.Configure<JsonOptions>(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))` in ProductService Program.cs OR change worker to send `(int)p.Source`.

2. **`LazadaApiClient` constructor still has unregistered `IExchangeRateService`**
   - Wait, this was fixed when I rewrote it — it now takes only `(HttpClient, ILogger)`. Verify before commit.

3. **Worker Dockerfile already correct** — no further work needed. Image weight ~+400 MB due to Chromium.

## Other untracked items in working tree (decide what to do)

```
amazon-analyzing.png             # screenshots from earlier UI testing
amazon-result.png                # → probably delete (already in chat history)
amazon-url-entered.png
analyze-loading.png
analyze-pending.png
analyze-result.png
app-current.png
cigarpage-result.png
flaresolverr-analyzing.png
quick-lookup-filled.png
quick-lookup.png
scripts/seed-demo-data.py        # pre-existing — keep or commit if useful
src/.../Domain/Entities/AnalysisUrl.cs   # orphan from abandoned server-persistence path; safe to delete
```

## Stash to dispose of

```bash
git stash list
# stash@{0}: On main: wip-local-changes-pre-pull
```

This is the abandoned **server-side analysis-URL persistence** WIP from earlier — the upstream `326fc33d` commit chose localStorage instead, so this stash is dead code. `git stash drop stash@{0}` when convinced you don't need it.

## Fallback plan if Shopee still blocked on open network

Three options, in order of effort:

1. **Shopee Affiliate API** (https://affiliate.shopee.vn) — easier approval than Open Platform, designed for price-comparison sites. ~1-2 weeks for approval, then ~2 days to swap implementation.
2. **Vietnam-region residential proxy** — wire through `IRotatingProxyService` (stub already in `Common.Application.Interfaces`). Cost: ~$50-200/month.
3. **Shopee Open Platform partner status** — full marketplace API. Requires registered business entity. ~4 weeks approval, may not approve for this use case.
