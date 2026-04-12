import { useState, useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Download, TrendingUp } from 'lucide-react';
import PageContainer from '@/components/layout/PageContainer';
import { MetricCard } from '@/components/ui/MetricCard';
import { Button } from '@/components/ui/Button';
import { FilterBar } from '@/components/shared/FilterBar';
import { OpportunityCard } from '@/components/shared/OpportunityCard';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { useDashboardMetrics } from '@/hooks/useOpportunities';
import { useScores } from '@/hooks/useScores';
import { useFilterStore } from '@/store/filterStore';
import { keepPreviousData } from '@tanstack/react-query';

export default function DashboardPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const filters = useFilterStore();

  const { data: metrics, isLoading: metricsLoading } = useDashboardMetrics({
    minMargin: filters.minMargin || undefined,
  });

  const { data: scoresData, isLoading: scoresLoading } = useScores(
    { page, pageSize: 20, minMargin: filters.minMargin || undefined },
    { placeholderData: keepPreviousData }
  );

  const items = scoresData?.items ?? [];

  // ARIA live region: announce count changes
  const [announcement, setAnnouncement] = useState('');
  useEffect(() => {
    if (!scoresLoading && items.length > 0) {
      setAnnouncement(
        `${items.length} ${items.length === 1 ? 'opportunity' : 'opportunities'} displayed`
      );
    } else if (!scoresLoading && items.length === 0) {
      setAnnouncement('No opportunities found. Adjust your filters.');
    }
  }, [items.length, scoresLoading]);

  const handleExportCSV = useCallback(() => {
    const rows = [
      ['MatchId', 'CompositeScore', 'ProfitMargin%', 'Demand', 'Competition', 'Stability', 'Confidence', 'LandedCostVND', 'RetailVND'],
      ...items.map(s => [
        s.matchId ?? '', s.compositeScore ?? 0, s.profitMarginPct ?? 0,
        s.demandScore ?? 0, s.competitionScore ?? 0, s.priceStabilityScore ?? 0,
        s.matchConfidenceScore ?? 0, s.landedCostVnd ?? 0, s.vietnamRetailVnd ?? 0,
      ]),
    ];
    const csv = rows.map(r => r.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href = url;
    a.download = `opportunities-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click(); URL.revokeObjectURL(url);
  }, [items]);

  const handlePrev = useCallback(() => setPage(p => p - 1), []);
  const handleNext = useCallback(() => setPage(p => p + 1), []);

  return (
    <ErrorBoundary>
    <PageContainer>
      {/* Skip-to-content link */}
      <a href="#main-opportunities" className="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-50 focus:px-4 focus:py-2 focus:bg-primary focus:text-white focus:rounded">
        Skip to opportunities
      </a>

      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('dashboard.title')}</h1>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4 mb-8">
        <MetricCard
          data-testid="metric-total"
          label={t('dashboard.totalOpportunities')}
          value={metricsLoading ? '—' : metrics?.total ?? 0}
        />
        <MetricCard
          data-testid="metric-avg-margin"
          label={t('dashboard.avgMargin')}
          value={metricsLoading ? '—' : `${metrics?.avgMargin ?? 0}%`}
          variant="primary"
        />
        <MetricCard
          data-testid="metric-avg-demand"
          label={t('dashboard.avgDemand')}
          value={metricsLoading ? '—' : `${metrics?.avgDemand ?? 0}`}
          variant="warning"
        />
        <MetricCard
          data-testid="metric-high-opps"
          label={t('dashboard.highOpportunities')}
          value={metricsLoading ? '—' : metrics?.highOpps ?? 0}
          variant="success"
        />
      </div>

      {/* Filter Bar */}
      <div className="flex flex-wrap gap-3 mb-6">
        <FilterBar />
        <Button
          variant="outline"
          onClick={handleExportCSV}
          aria-label="Export opportunities to CSV"
        >
          <Download className="w-4 h-4 mr-1.5 inline" aria-hidden="true" />
          {t('common.export', 'Export CSV')}
        </Button>
      </div>

      {/* ARIA live region for dynamic content updates */}
      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {announcement}
      </div>

      {/* Opportunity Cards */}
      <div
        id="main-opportunities"
        className="space-y-4"
        aria-label="Opportunity results"
        aria-busy={scoresLoading}
      >
        {scoresLoading && items.length === 0 ? (
          <p className="text-text-muted" role="status" aria-label="Loading opportunities">
            {t('dashboard.loading', 'Loading opportunities...')}
          </p>
        ) : items.length === 0 ? (
          <EmptyState
            icon={TrendingUp}
            title={t('dashboard.noOpportunities', 'No opportunities found matching your filters.')}
            description={t('dashboard.noResults', 'Try adjusting the filters or check back later.')}
            action={filters.resetFilters}
            actionLabel={t('dashboard.filterBar.resetFilters', 'Reset Filters')}
          />
        ) : items.map((score) => (
          <OpportunityCard key={score.id ?? score.matchId} score={score} />
        ))}
      </div>

      {/* Pagination */}
      {scoresData?.totalCount > 20 && (
        <nav aria-label="Pagination" className="flex justify-center gap-2 mt-6">
          <Button
            data-testid="btn-prev"
            variant="outline"
            disabled={page === 1}
            onClick={handlePrev}
            aria-label="Previous page"
          >
            {t('common.previous', 'Previous')}
          </Button>
          <span className="px-4 py-2 text-sm text-text-muted" aria-current="page">
            {t('dashboard.page', 'Page')} {page}
          </span>
          <Button
            data-testid="btn-next"
            variant="outline"
            onClick={handleNext}
            aria-label="Next page"
          >
            {t('common.next', 'Next')}
          </Button>
        </nav>
      )}
    </PageContainer>
    </ErrorBoundary>
  );
}
