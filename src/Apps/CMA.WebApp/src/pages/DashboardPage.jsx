import { useState, useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Download, TrendingUp, Wifi, WifiOff } from 'lucide-react';
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
import { useRealtimeOpportunities } from '@/hooks/useRealtimeOpportunities';
import { MarginBarChart } from '@/components/shared/MarginBarChart';
import { ScoreRadarChart } from '@/components/shared/ScoreRadarChart';
import { scoringApi } from '@/api/scoringApi';
import { keepPreviousData } from '@tanstack/react-query';

export default function DashboardPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const [selectedOpp, setSelectedOpp] = useState(null);
  const [isExporting, setIsExporting] = useState(false);
  const filters = useFilterStore();

  // P3-F02: Merge real-time WebSocket updates into the scores cache
  const { isConnected } = useRealtimeOpportunities(20);

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

  // P3-F06: Excel export
  const handleExportExcel = useCallback(async () => {
    setIsExporting(true);
    try {
      const response = await scoringApi.exportToExcel({
        title: `CrossMarket Export ${new Date().toISOString().slice(0, 10)}`,
        limit: 0,
      });
      const blob = new Blob([response.data], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `cma-opportunities-${Date.now()}.xlsx`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Excel export failed:', err);
    } finally {
      setIsExporting(false);
    }
  }, []);

  const handlePrev = useCallback(() => setPage(p => Math.max(1, p - 1)), []);
  const handleNext = useCallback(() => setPage(p => p + 1), []);

  return (
    <ErrorBoundary>
    <PageContainer>
      {/* Skip-to-content link */}
      <a href="#main-opportunities" className="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-50 focus:px-4 focus:py-2 focus:bg-primary focus:text-white focus:rounded">
        Skip to opportunities
      </a>

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-text-primary">{t('dashboard.title')}</h1>
        {/* P3-F01: Real-time connection indicator */}
        <div
          className="flex items-center gap-1.5 text-sm text-text-muted"
          aria-live="polite"
          aria-label={isConnected ? 'Real-time updates connected' : 'Real-time updates disconnected'}
        >
          {isConnected
            ? <Wifi className="w-4 h-4 text-green-500" aria-hidden="true" />
            : <WifiOff className="w-4 h-4 text-text-muted" aria-hidden="true" />}
          <span className="sr-only">{isConnected ? 'Connected' : 'Disconnected'}</span>
          <span className="hidden sm:inline">{isConnected ? 'Live' : 'Polling'}</span>
        </div>
      </div>

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

      {/* P3-F04 & P3-F05: Charts — show only when there is data */}
      {items.length > 0 && (
        <div className="grid gap-6 lg:grid-cols-2 mb-8">
          <div className="bg-bg-secondary border border-border rounded-lg p-4">
            <h2 className="text-sm font-semibold text-text-primary mb-3">
              {t('dashboard.marginChart.title', 'Top 10 by Profit Margin')}
            </h2>
            <MarginBarChart scores={items} />
          </div>
          <div className="bg-bg-secondary border border-border rounded-lg p-4">
            <h2 className="text-sm font-semibold text-text-primary mb-3">
              {t('dashboard.radarChart.title', 'Score Breakdown —')}{' '}
              {selectedOpp
                ? `#${selectedOpp.matchId?.slice(0, 8)}`
                : t('dashboard.radarChart.select', 'select an opportunity below')}
            </h2>
            <ScoreRadarChart score={selectedOpp ?? items[0]} />
          </div>
        </div>
      )}

      {/* Filter Bar */}
      <div className="flex flex-wrap gap-3 mb-6">
        <FilterBar />
        {/* P3-F06: Replaced CSV with Excel export */}
        <Button
          variant="outline"
          onClick={handleExportExcel}
          disabled={isExporting}
          aria-label={isExporting ? 'Exporting to Excel' : 'Export opportunities to Excel'}
        >
          <Download className="w-4 h-4 mr-1.5 inline" aria-hidden="true" />
          {isExporting
            ? t('dashboard.exporting', 'Exporting…')
            : t('dashboard.exportExcel', 'Export Excel')}
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
          <div
            key={score.id ?? score.matchId}
            role="button"
            tabIndex={0}
            onClick={() => setSelectedOpp(score)}
            onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && setSelectedOpp(score)}
            aria-pressed={selectedOpp?.matchId === score.matchId}
          >
            <OpportunityCard score={score} />
          </div>
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
