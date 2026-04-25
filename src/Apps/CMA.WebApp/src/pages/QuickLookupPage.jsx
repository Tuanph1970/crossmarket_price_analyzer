import { useState, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { SearchX, Plus, Trash2, Loader2, ExternalLink } from 'lucide-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import PageContainer from '@/components/layout/PageContainer';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { productApi } from '@/api/productApi';
import { CONFIDENCE_COLORS } from '@/lib/constants';

function isListingPage(url) {
  try {
    const { hostname, pathname } = new URL(url);
    if (hostname.includes('cigarpage.com')) {
      const segments = pathname.split('/').filter(Boolean);
      return segments.length > 1;
    }
    if (hostname.includes('amazon.com')) {
      if (pathname.includes('/dp/') || pathname.includes('/gp/product/')) return false;
      const segments = pathname.split('/').filter(Boolean);
      return segments.includes('s') || segments.includes('b') || segments.includes('zgbs');
    }
    return false;
  } catch {
    return false;
  }
}

function VnMatchRow({ match }) {
  return (
    <div className="flex items-center justify-between py-2.5 border-b border-border last:border-0">
      <div className="flex-1 min-w-0">
        <div className="text-sm font-medium text-text-primary truncate">{match.name}</div>
        <div className="flex gap-4 mt-0.5 text-xs text-text-muted font-mono">
          {match.brandName && <span>{match.brandName}</span>}
          {match.latestPriceVnd && (
            <span>{Number(match.latestPriceVnd).toLocaleString('vi-VN')} ₫</span>
          )}
        </div>
      </div>
      <div className="ml-4 shrink-0 text-right">
        <div className="text-sm font-bold font-mono text-text-primary">{match.matchScore?.toFixed(1) ?? '—'}</div>
        <span className={`text-xs px-2 py-0.5 rounded-full ${
          CONFIDENCE_COLORS[match.matchConfidenceLevel] ?? CONFIDENCE_COLORS.Low
        }`}>
          {match.matchConfidenceLevel ?? '—'}
        </span>
      </div>
    </div>
  );
}

function StatusIcon({ status }) {
  if (status === 'pending') return <Loader2 className="w-3.5 h-3.5 animate-spin text-text-muted shrink-0" />;
  if (status === 'success') return <span className="w-3.5 h-3.5 shrink-0 text-success font-bold text-sm leading-none">✓</span>;
  return <span className="w-3.5 h-3.5 shrink-0 text-danger font-bold text-sm leading-none">✗</span>;
}

function ProductLookupResult({ url, status, result, error }) {
  const { t } = useTranslation();
  return (
    <div className="border border-border rounded-xl overflow-hidden bg-surface">
      <div className="flex items-center gap-3 px-4 py-2.5 bg-surface-secondary border-b border-border">
        <StatusIcon status={status} />
        <span className="text-xs text-text-muted truncate font-mono">{url}</span>
      </div>

      <div className="p-4 space-y-4">
        {status === 'pending' && (
          <p className="text-sm text-text-muted">{t('quickLookup.analyzing', 'Analyzing…')}</p>
        )}
        {status === 'error' && (
          <div className="bg-danger/8 border border-danger/25 text-danger rounded-lg p-3 text-sm">
            {error}
          </div>
        )}
        {status === 'success' && result && (
          <>
            {result.scrapedProduct && (
              <div className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
                <div>
                  <span className="text-text-muted text-xs uppercase tracking-wider">Name </span>
                  <span className="font-medium text-text-primary">{result.scrapedProduct.name}</span>
                </div>
                <div>
                  <span className="text-text-muted text-xs uppercase tracking-wider">Source </span>
                  <span className="text-text-primary">{result.scrapedProduct.source}</span>
                </div>
                {result.scrapedProduct.brand && (
                  <div>
                    <span className="text-text-muted text-xs uppercase tracking-wider">Brand </span>
                    <span className="text-text-primary">{result.scrapedProduct.brand}</span>
                  </div>
                )}
                <div>
                  <span className="text-text-muted text-xs uppercase tracking-wider">Price </span>
                  <span className="font-bold font-mono text-primary">
                    {Number(result.scrapedProduct.price).toLocaleString('en-US', {
                      style: 'currency',
                      currency: result.scrapedProduct.currency ?? 'USD',
                    })}
                  </span>
                </div>
                {result.exchangeRate && (
                  <div>
                    <span className="text-text-muted text-xs uppercase tracking-wider">Rate </span>
                    <span className="font-mono text-text-primary">
                      {Number(result.exchangeRate).toLocaleString('vi-VN')} ₫/USD
                    </span>
                  </div>
                )}
              </div>
            )}
            {(result.vnMatches ?? []).length > 0 && (
              <div>
                <p className="text-xs font-semibold text-text-muted uppercase tracking-widest mb-2">
                  Vietnam Matches
                </p>
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

function ListingResult({ url, status, result, error }) {
  return (
    <div className="border border-border rounded-xl overflow-hidden bg-surface">
      <div className="flex items-center gap-3 px-4 py-2.5 bg-surface-secondary border-b border-border">
        <StatusIcon status={status} />
        <div className="flex-1 min-w-0">
          <span className="text-xs text-text-muted truncate block font-mono">{url}</span>
          {status === 'success' && result && (
            <span className="text-xs text-success font-medium">{result.totalFound} products found</span>
          )}
        </div>
      </div>

      <div className="p-4">
        {status === 'pending' && (
          <p className="text-sm text-text-muted">
            Fetching listing page and scraping each product — this may take a minute…
          </p>
        )}
        {status === 'error' && (
          <div className="bg-danger/8 border border-danger/25 text-danger rounded-lg p-3 text-sm">{error}</div>
        )}
        {status === 'success' && result && (
          <div className="divide-y divide-border">
            {(result.products ?? []).map((p, i) => (
              <div key={p.sourceUrl ?? i} className="flex items-center justify-between py-3">
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-medium text-text-primary truncate">{p.name}</div>
                  <div className="flex gap-3 mt-0.5 text-xs text-text-muted font-mono">
                    {p.brand && <span>{p.brand}</span>}
                    {p.sku && <span>SKU: {p.sku}</span>}
                  </div>
                </div>
                <div className="flex items-center gap-4 ml-4 shrink-0">
                  <span className="text-sm font-bold font-mono text-primary">
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
  const queryClient = useQueryClient();
  const [inputUrl, setInputUrl] = useState('');
  const [results, setResults] = useState({});
  const inputRef = useRef(null);

  const { data: savedUrls = [] } = useQuery({
    queryKey: ['analysis-urls'],
    queryFn: async () => {
      const res = await productApi.getAnalysisUrls();
      return (res?.data ?? []).map((u) => ({ id: u.id, url: u.url, type: u.urlType }));
    },
  });

  const addUrlMutation = useMutation({
    mutationFn: ({ url, type }) => productApi.saveAnalysisUrl(url, type),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analysis-urls'] }),
  });

  const deleteUrlMutation = useMutation({
    mutationFn: (id) => productApi.deleteAnalysisUrl(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analysis-urls'] }),
  });

  const clearUrlsMutation = useMutation({
    mutationFn: () => productApi.clearAnalysisUrls(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analysis-urls'] }),
  });

  const quickLookupMutation  = useMutation({ mutationFn: (data) => productApi.quickLookup(data) });
  const scrapeListingMutation = useMutation({ mutationFn: (data) => productApi.scrapeListing(data) });

  const handleAddUrl = async () => {
    const trimmed = inputUrl.trim();
    if (!trimmed) return;
    if (savedUrls.some((e) => e.url === trimmed)) {
      setInputUrl(''); inputRef.current?.focus(); return;
    }
    const type = isListingPage(trimmed) ? 'listing' : 'product';
    await addUrlMutation.mutateAsync({ url: trimmed, type });
    setInputUrl(''); inputRef.current?.focus();
  };

  const handleRemoveUrl = async (item) => {
    await deleteUrlMutation.mutateAsync(item.id);
    setResults((prev) => { const n = { ...prev }; delete n[item.url]; return n; });
  };

  const handleAnalyzeAll = async () => {
    if (savedUrls.length === 0) return;
    setResults(Object.fromEntries(savedUrls.map(({ url }) => [url, { status: 'pending' }])));

    for (const { url, type } of savedUrls) {
      try {
        if (type === 'listing') {
          const res = await scrapeListingMutation.mutateAsync({ pageUrl: url, maxProducts: 20 });
          setResults((prev) => ({ ...prev, [url]: { status: 'success', result: res?.data ?? res ?? null } }));
        } else {
          const res = await quickLookupMutation.mutateAsync({
            url, vnNameFilter: null, maxVnMatches: 5, minMatchScore: 40,
          });
          setResults((prev) => ({ ...prev, [url]: { status: 'success', result: res?.data ?? res ?? null } }));
        }
      } catch (err) {
        const error = err?.response?.data?.message || err?.response?.data?.error || t('quickLookup.error', 'Failed to analyze URL');
        setResults((prev) => ({ ...prev, [url]: { status: 'error', error } }));
      }
    }
  };

  const isAnalyzing = Object.values(results).some((r) => r.status === 'pending');
  const hasResults  = Object.keys(results).length > 0;

  return (
    <ErrorBoundary>
      <PageContainer>
        <h1 className="font-display text-2xl font-bold text-text-primary mb-6">
          {t('quickLookup.title', 'Quick Lookup')}
        </h1>

        <div className="max-w-2xl space-y-3 mb-6">
          {/* URL input */}
          <div className="flex gap-2">
            <Input
              ref={inputRef}
              placeholder="Paste a product or category URL (Amazon, Walmart, cigarpage.com)…"
              value={inputUrl}
              onChange={(e) => setInputUrl(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleAddUrl()}
              className="flex-1 font-mono text-xs"
            />
            <Button variant="outline" size="md" onClick={handleAddUrl} disabled={!inputUrl.trim()}>
              <Plus className="w-4 h-4 mr-1" />
              Add
            </Button>
          </div>

          {/* URL list */}
          {savedUrls.length > 0 && (
            <div className="rounded-xl border border-border divide-y divide-border bg-surface overflow-hidden">
              {savedUrls.map((item) => (
                <div key={item.id} className="flex items-center gap-3 px-4 py-2.5 hover:bg-surface-raised transition-colors">
                  <span className={`text-xs px-2 py-0.5 rounded-full shrink-0 font-medium border ${
                    item.type === 'listing'
                      ? 'bg-primary/15 text-primary border-primary/20'
                      : 'bg-gold/15 text-gold border-gold/20'
                  }`}>
                    {item.type === 'listing' ? 'Listing' : 'Product'}
                  </span>
                  <span className="flex-1 text-xs text-text-muted truncate font-mono">{item.url}</span>
                  <button
                    onClick={() => handleRemoveUrl(item)}
                    className="shrink-0 text-text-subtle hover:text-danger transition-colors"
                    aria-label="Remove URL"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Analyze button */}
          {savedUrls.length > 0 && (
            <Button onClick={handleAnalyzeAll} disabled={isAnalyzing} className="w-full">
              {isAnalyzing
                ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" />Analyzing…</>
                : `Analyze ${savedUrls.length} URL${savedUrls.length > 1 ? 's' : ''}`}
            </Button>
          )}
        </div>

        {/* Results */}
        {hasResults && (
          <div className="space-y-4">
            {savedUrls.map((item) => {
              const r = results[item.url];
              if (!r) return null;
              return item.type === 'listing'
                ? <ListingResult key={item.url} url={item.url} status={r.status} result={r.result} error={r.error} />
                : <ProductLookupResult key={item.url} url={item.url} status={r.status} result={r.result} error={r.error} />;
            })}
          </div>
        )}

        {/* Empty state */}
        {!hasResults && savedUrls.length === 0 && (
          <EmptyState
            icon={SearchX}
            title="Quick Lookup"
            description="Add one or more URLs. Category pages (e.g. cigarpage.com/samplers/…) will automatically scrape all products on the page."
          />
        )}
      </PageContainer>
    </ErrorBoundary>
  );
}
