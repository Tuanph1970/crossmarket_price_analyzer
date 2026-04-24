import { useState, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { SearchX, Plus, Trash2, Loader2, ExternalLink } from 'lucide-react';
import { useMutation } from '@tanstack/react-query';
import PageContainer from '@/components/layout/PageContainer';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { productApi } from '@/api/productApi';
import { CONFIDENCE_COLORS } from '@/lib/constants';

// A cigarpage URL is a listing page when its path has more than one segment
// e.g. /samplers/best-selling-cigar-samplers.html  →  listing
//      /arturo-fuente-opus-x.html                  →  product
function isListingPage(url) {
  try {
    const { hostname, pathname } = new URL(url);
    if (!hostname.includes('cigarpage.com')) return false;
    const segments = pathname.split('/').filter(Boolean);
    return segments.length > 1;
  } catch {
    return false;
  }
}

function VnMatchRow({ match }) {
  return (
    <div className="flex items-center justify-between py-2 border-b border-border last:border-0">
      <div className="flex-1 min-w-0">
        <div className="text-sm font-medium text-text-primary truncate">{match.name}</div>
        <div className="flex gap-4 mt-0.5 text-xs text-text-muted">
          {match.brandName && <span>{match.brandName}</span>}
          {match.latestPriceVnd && (
            <span>{Number(match.latestPriceVnd).toLocaleString('vi-VN')} VND</span>
          )}
        </div>
      </div>
      <div className="ml-4 shrink-0 text-right">
        <div className="text-sm font-semibold">{match.matchScore?.toFixed(1) ?? '—'}</div>
        <span className={`text-xs px-2 py-0.5 rounded-full ${
          CONFIDENCE_COLORS[match.matchConfidenceLevel] ?? CONFIDENCE_COLORS.Low
        }`}>
          {match.matchConfidenceLevel ?? '—'}
        </span>
      </div>
    </div>
  );
}

// Result block for a single quick-lookup (product URL)
function ProductLookupResult({ url, status, result, error }) {
  const { t } = useTranslation();
  return (
    <div className="border border-border rounded-xl overflow-hidden">
      <div className="flex items-center gap-3 px-4 py-3 bg-surface-secondary border-b border-border">
        {status === 'pending' && <Loader2 className="w-4 h-4 animate-spin text-text-muted shrink-0" />}
        {status === 'success' && <span className="w-4 h-4 shrink-0 text-green-500 font-bold">✓</span>}
        {status === 'error'   && <span className="w-4 h-4 shrink-0 text-red-500 font-bold">✗</span>}
        <span className="text-xs text-text-muted truncate">{url}</span>
      </div>

      <div className="p-4 space-y-4">
        {status === 'pending' && <p className="text-sm text-text-muted">{t('quickLookup.analyzing', 'Analyzing...')}</p>}
        {status === 'error'   && <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{error}</div>}
        {status === 'success' && result && (
          <>
            {result.scrapedProduct && (
              <div className="grid grid-cols-2 gap-x-8 gap-y-1 text-sm">
                <div><span className="text-text-muted">Name: </span><span className="font-medium">{result.scrapedProduct.name}</span></div>
                <div><span className="text-text-muted">Source: </span><span>{result.scrapedProduct.source}</span></div>
                {result.scrapedProduct.brand && <div><span className="text-text-muted">Brand: </span><span>{result.scrapedProduct.brand}</span></div>}
                <div>
                  <span className="text-text-muted">Price: </span>
                  <span className="font-semibold">
                    {Number(result.scrapedProduct.price).toLocaleString('en-US', { style: 'currency', currency: result.scrapedProduct.currency ?? 'USD' })}
                  </span>
                </div>
                {result.exchangeRate && <div><span className="text-text-muted">Rate: </span><span>{Number(result.exchangeRate).toLocaleString('vi-VN')} VND/USD</span></div>}
              </div>
            )}
            {(result.vnMatches ?? []).length > 0 && (
              <div>
                <p className="text-xs font-semibold text-text-muted uppercase tracking-wide mb-2">Vietnam Matches</p>
                {result.vnMatches.map((m) => <VnMatchRow key={m.productId} match={m} />)}
              </div>
            )}
            {(result.vnMatches ?? []).length === 0 && result.scrapedProduct && (
              <p className="text-sm text-text-muted">No Vietnam matches found above score threshold.</p>
            )}
          </>
        )}
      </div>
    </div>
  );
}

// Result block for a listing-page scrape (returns many products)
function ListingResult({ url, status, result, error }) {
  const { t } = useTranslation();
  return (
    <div className="border border-border rounded-xl overflow-hidden">
      <div className="flex items-center gap-3 px-4 py-3 bg-surface-secondary border-b border-border">
        {status === 'pending' && <Loader2 className="w-4 h-4 animate-spin text-text-muted shrink-0" />}
        {status === 'success' && <span className="w-4 h-4 shrink-0 text-green-500 font-bold">✓</span>}
        {status === 'error'   && <span className="w-4 h-4 shrink-0 text-red-500 font-bold">✗</span>}
        <div className="flex-1 min-w-0">
          <span className="text-xs text-text-muted truncate block">{url}</span>
          {status === 'success' && result && (
            <span className="text-xs text-green-600 font-medium">{result.totalFound} products found</span>
          )}
        </div>
      </div>

      <div className="p-4">
        {status === 'pending' && (
          <p className="text-sm text-text-muted">
            Fetching listing page and scraping each product — this may take a minute...
          </p>
        )}
        {status === 'error' && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{error}</div>
        )}
        {status === 'success' && result && (
          <div className="divide-y divide-border">
            {(result.products ?? []).map((p, i) => (
              <div key={p.sourceUrl ?? i} className="flex items-center justify-between py-3">
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium text-text-primary truncate">{p.name}</div>
                  <div className="flex gap-3 mt-0.5 text-xs text-text-muted">
                    {p.brand && <span>{p.brand}</span>}
                    {p.sku && <span>SKU: {p.sku}</span>}
                  </div>
                </div>
                <div className="flex items-center gap-4 ml-4 shrink-0">
                  <span className="text-sm font-semibold text-text-primary">
                    {Number(p.price).toLocaleString('en-US', { style: 'currency', currency: p.currency ?? 'USD' })}
                  </span>
                  <a
                    href={p.sourceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-text-muted hover:text-primary transition-colors"
                    title="Open product page"
                  >
                    <ExternalLink className="w-4 h-4" />
                  </a>
                </div>
              </div>
            ))}
            {(result.products ?? []).length === 0 && (
              <p className="text-sm text-text-muted py-2">No products could be scraped from this page.</p>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default function QuickLookupPage() {
  const { t } = useTranslation();
  const [inputUrl, setInputUrl] = useState('');
  const [urlList, setUrlList] = useState([]);       // { url, type: 'product'|'listing' }[]
  const [results, setResults] = useState({});       // url → { status, result, error }
  const inputRef = useRef(null);

  const quickLookupMutation = useMutation({ mutationFn: (data) => productApi.quickLookup(data) });
  const scrapeListingMutation = useMutation({ mutationFn: (data) => productApi.scrapeListing(data) });

  const handleAddUrl = () => {
    const trimmed = inputUrl.trim();
    if (!trimmed) return;
    if (urlList.some((e) => e.url === trimmed)) {
      setInputUrl('');
      inputRef.current?.focus();
      return;
    }
    const type = isListingPage(trimmed) ? 'listing' : 'product';
    setUrlList((prev) => [...prev, { url: trimmed, type }]);
    setInputUrl('');
    inputRef.current?.focus();
  };

  const handleRemoveUrl = (url) => {
    setUrlList((prev) => prev.filter((e) => e.url !== url));
    setResults((prev) => { const n = { ...prev }; delete n[url]; return n; });
  };

  const handleAnalyzeAll = async () => {
    if (urlList.length === 0) return;

    // Mark all pending
    setResults(Object.fromEntries(urlList.map(({ url }) => [url, { status: 'pending' }])));

    for (const { url, type } of urlList) {
      try {
        let data;
        if (type === 'listing') {
          const res = await scrapeListingMutation.mutateAsync({ pageUrl: url, maxProducts: 15 });
          data = res?.data ?? res ?? null;
          setResults((prev) => ({ ...prev, [url]: { status: 'success', result: data } }));
        } else {
          const res = await quickLookupMutation.mutateAsync({
            url,
            vnNameFilter: null,
            maxVnMatches: 5,
            minMatchScore: 40,
          });
          data = res?.data ?? res ?? null;
          setResults((prev) => ({ ...prev, [url]: { status: 'success', result: data } }));
        }
      } catch (err) {
        const error = err?.response?.data?.message || err?.response?.data?.error || t('quickLookup.error', 'Failed to analyze URL');
        setResults((prev) => ({ ...prev, [url]: { status: 'error', error } }));
      }
    }
  };

  const isAnalyzing = Object.values(results).some((r) => r.status === 'pending');
  const hasResults = Object.keys(results).length > 0;

  return (
    <ErrorBoundary>
      <PageContainer>
        <h1 className="text-2xl font-bold text-text-primary mb-6">
          {t('quickLookup.title', 'Quick Lookup')}
        </h1>

        {/* Input row */}
        <div className="max-w-2xl space-y-4 mb-6">
          <div className="flex gap-3">
            <Input
              ref={inputRef}
              placeholder="Paste a product or category URL (Amazon, Walmart, cigarpage.com)..."
              value={inputUrl}
              onChange={(e) => setInputUrl(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleAddUrl()}
              className="flex-1"
            />
            <Button variant="outline" onClick={handleAddUrl} disabled={!inputUrl.trim()}>
              <Plus className="w-4 h-4 mr-1" />
              Add URL
            </Button>
          </div>

          {/* URL list */}
          {urlList.length > 0 && (
            <div className="rounded-xl border border-border divide-y divide-border">
              {urlList.map(({ url, type }) => (
                <div key={url} className="flex items-center gap-3 px-4 py-2.5">
                  <span className={`text-xs px-2 py-0.5 rounded-full shrink-0 font-medium ${
                    type === 'listing'
                      ? 'bg-purple-100 text-purple-700'
                      : 'bg-blue-100 text-blue-700'
                  }`}>
                    {type === 'listing' ? 'Listing' : 'Product'}
                  </span>
                  <span className="flex-1 text-sm text-text-primary truncate">{url}</span>
                  <button
                    onClick={() => handleRemoveUrl(url)}
                    className="shrink-0 text-text-muted hover:text-red-500 transition-colors"
                    aria-label="Remove URL"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Analyze button */}
          {urlList.length > 0 && (
            <Button onClick={handleAnalyzeAll} disabled={isAnalyzing} className="w-full">
              {isAnalyzing
                ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" />Analyzing...</>
                : `Analyze ${urlList.length} URL${urlList.length > 1 ? 's' : ''}`}
            </Button>
          )}
        </div>

        {/* Results */}
        {hasResults && (
          <div className="space-y-4">
            {urlList.map(({ url, type }) => {
              const r = results[url];
              if (!r) return null;
              return type === 'listing'
                ? <ListingResult key={url} url={url} status={r.status} result={r.result} error={r.error} />
                : <ProductLookupResult key={url} url={url} status={r.status} result={r.result} error={r.error} />;
            })}
          </div>
        )}

        {/* Empty state */}
        {!hasResults && urlList.length === 0 && (
          <EmptyState
            icon={SearchX}
            title="Quick Lookup"
            description="Add one or more URLs. Category pages (e.g. cigarpage.com/samplers/...) will automatically scrape all products on the page."
          />
        )}
      </PageContainer>
    </ErrorBoundary>
  );
}
