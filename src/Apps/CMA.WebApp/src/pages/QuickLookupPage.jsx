import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { SearchX } from 'lucide-react';
import PageContainer from '@/components/layout/PageContainer';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Card';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { useQuickLookup } from '@/hooks/useProducts';
import { CONFIDENCE_COLORS } from '@/lib/constants';

function VnMatchRow({ match, onViewComparison }) {
  const { t } = useTranslation();
  return (
    <div className="flex items-center justify-between py-3 border-b border-border last:border-0">
      <div className="flex-1 min-w-0">
        <div className="font-medium text-text-primary truncate">{match.name}</div>
        <div className="flex gap-4 mt-1 text-xs text-text-muted">
          {match.brandName && <span>Brand: {match.brandName}</span>}
          {match.latestPriceVnd && (
            <span>Retail: {Number(match.latestPriceVnd).toLocaleString('vi-VN')} VND</span>
          )}
        </div>
      </div>
      <div className="flex items-center gap-3 ml-4 shrink-0">
        <div className="text-right">
          <div className="text-sm font-semibold">{match.matchScore?.toFixed(1) ?? '—'}</div>
          <span className={`text-xs px-2 py-0.5 rounded-full ${
            CONFIDENCE_COLORS[match.matchConfidenceLevel] ?? CONFIDENCE_COLORS.Low
          }`}>
            {match.matchConfidenceLevel ?? '—'}
          </span>
        </div>
        {match.productId && (
          <Button variant="outline" size="sm" onClick={() => onViewComparison(match.productId)}>
            {t('quickLookup.viewComparison', 'View')}
          </Button>
        )}
      </div>
    </div>
  );
}

function ScoreCard({ score }) {
  const { t } = useTranslation();
  return (
    <Card className="p-4 flex-1 min-w-[200px]">
      <div className="flex justify-between items-center mb-2">
        <span className="text-sm text-text-muted">{score.matchId ? `#${score.matchId.slice(0, 8)}` : '—'}</span>
        <span className={`text-lg font-bold ${
          (score.compositeScore ?? 0) >= 80 ? 'text-green-600' :
          (score.compositeScore ?? 0) >= 60 ? 'text-yellow-600' : 'text-red-600'
        }`}>
          {score.compositeScore?.toFixed(1) ?? '—'}
        </span>
      </div>
      <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-text-muted">
        <span>{t('comparison.profitMargin', 'Margin')}:</span>
        <span>{score.profitMarginPct?.toFixed(1) ?? '—'}%</span>
        <span>{t('comparison.demand', 'Demand')}:</span>
        <span>{score.demandScore ?? '—'}</span>
        <span>{t('comparison.competition', 'Competition')}:</span>
        <span>{score.competitionScore ?? '—'}</span>
        <span>{t('comparison.stability', 'Stability')}:</span>
        <span>{score.priceStabilityScore ?? '—'}</span>
      </div>
    </Card>
  );
}

export default function QuickLookupPage() {
  const { t } = useTranslation();
  const [url, setUrl] = useState('');
  const [error, setError] = useState(null);

  const lookupMutation = useQuickLookup({
    onError: (err) => {
      setError(err?.response?.data?.message || t('quickLookup.error', 'Failed to analyze URL'));
    },
  });

  const result = lookupMutation.data?.data ?? lookupMutation.data ?? null;

  const handleAnalyze = () => {
    if (!url.trim()) return;
    setError(null);
    lookupMutation.reset();
    lookupMutation.mutate({
      url: url.trim(),
      vnNameFilter: null,
      maxVnMatches: 5,
      minMatchScore: 40,
    });
  };

  return (
    <ErrorBoundary>
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">
        {t('quickLookup.title', 'Quick Lookup')}
      </h1>

      {/* URL Input */}
      <div className="max-w-2xl space-y-4 mb-8">
        <div className="flex gap-3">
          <Input
            placeholder={t('quickLookup.placeholder', 'Paste a product URL (Amazon, Walmart, cigarpage.com, Shopee)...')}
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleAnalyze()}
            className="flex-1"
          />
          <Button
            onClick={handleAnalyze}
            disabled={lookupMutation.isPending || !url.trim()}
          >
            {lookupMutation.isPending
              ? (t('quickLookup.analyzing', 'Analyzing...'))
              : (t('quickLookup.analyze', 'Analyze'))}
          </Button>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-4 text-sm">
            {error}
          </div>
        )}
      </div>

      {/* Results */}
      {result && (
        <div className="space-y-6">
          {/* Scraped Product */}
          {result.scrapedProduct && (
            <Card className="p-6">
              <h2 className="text-lg font-semibold text-text-primary mb-4">
                {t('quickLookup.scrapedProduct', 'Scraped Product')}
              </h2>
              <div className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
                <div>
                  <span className="text-text-muted">Name: </span>
                  <span className="text-text-primary">{result.scrapedProduct.name}</span>
                </div>
                <div>
                  <span className="text-text-muted">Source: </span>
                  <span className="text-text-primary">{result.scrapedProduct.source}</span>
                </div>
                {result.scrapedProduct.brand && (
                  <div>
                    <span className="text-text-muted">Brand: </span>
                    <span className="text-text-primary">{result.scrapedProduct.brand}</span>
                  </div>
                )}
                {result.scrapedProduct.sku && (
                  <div>
                    <span className="text-text-muted">SKU: </span>
                    <span className="text-text-primary">{result.scrapedProduct.sku}</span>
                  </div>
                )}
                <div>
                  <span className="text-text-muted">Price: </span>
                  <span className="text-text-primary font-semibold">
                    {Number(result.scrapedProduct.price).toLocaleString('en-US', {
                      style: 'currency', currency: result.scrapedProduct.currency ?? 'USD',
                    })}
                  </span>
                </div>
                <div>
                  <span className="text-text-muted">URL: </span>
                  <a
                    href={result.scrapedProduct.sourceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-blue-600 hover:underline text-xs truncate max-w-[200px] inline-block align-bottom"
                  >
                    {result.scrapedProduct.sourceUrl}
                  </a>
                </div>
              </div>

              {/* Exchange Rate */}
              <div className="mt-3 pt-3 border-t border-border text-sm">
                <span className="text-text-muted">{t('quickLookup.exchangeRate', 'Exchange Rate')}: </span>
                <span className="text-text-primary font-medium">
                  {Number(result.exchangeRate ?? 0).toLocaleString('vi-VN')} VND/USD
                </span>
              </div>
            </Card>
          )}

          {/* Vietnam Matches */}
          {(result.vnMatches ?? []).length > 0 && (
            <Card className="p-6">
              <h2 className="text-lg font-semibold text-text-primary mb-2">
                {t('quickLookup.vnMatches', 'Vietnam Matches')}
              </h2>
              <p className="text-xs text-text-muted mb-4">
                Min match score: 40%
              </p>
              <div>
                {result.vnMatches.map((match) => (
                  <VnMatchRow key={match.productId} match={match} onViewComparison={(id) => {}} />
                ))}
              </div>
            </Card>
          )}

          {/* Scores */}
          {(result.scores ?? []).length > 0 && (
            <div>
              <h2 className="text-lg font-semibold text-text-primary mb-3">
                {t('quickLookup.scores', 'Opportunity Scores')}
              </h2>
              <div className="flex flex-wrap gap-4">
                {result.scores.map((score, i) => (
                  <ScoreCard key={score.matchId ?? i} score={score} />
                ))}
              </div>
            </div>
          )}

          {/* No matches fallback */}
          {(result.vnMatches ?? []).length === 0 && result.scrapedProduct && (
            <Card className="p-6">
              <EmptyState
                icon={SearchX}
                title={t('quickLookup.noResults', 'No results found.')}
                description={t('quickLookup.results.noMatches', 'No Vietnam matches found above the score threshold.')}
              />
            </Card>
          )}
        </div>
      )}

      {/* Empty state — no input yet */}
      {!result && !lookupMutation.isPending && (
        <EmptyState
          icon={SearchX}
          title={t('quickLookup.title', 'Quick Lookup')}
          description={t('quickLookup.placeholder', 'Paste a product URL to analyze it.')}
        />
      )}
    </PageContainer>
    </ErrorBoundary>
  );
}
