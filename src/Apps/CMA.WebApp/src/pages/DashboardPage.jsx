import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/ui/MetricCard';
import PageContainer from '@/components/layout/PageContainer';
import { useDashboardMetrics } from '@/hooks/useOpportunities';
import { useScores } from '@/hooks/useScores';
import { useFilterStore } from '@/store/filterStore';
import { Button } from '@/components/ui/Button';
import { Select } from '@/components/ui/Select';

export default function DashboardPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const filters = useFilterStore();
  const { data: metrics, isLoading: metricsLoading } = useDashboardMetrics({ minMargin: filters.minMargin || undefined });
  const { data: scoresData, isLoading: scoresLoading } = useScores({ page, pageSize: 20, minMargin: filters.minMargin || undefined });

  const items = scoresData?.items ?? [];

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('dashboard.title')}</h1>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4 mb-8">
        <MetricCard
          label={t('dashboard.totalOpportunities')}
          value={metricsLoading ? '—' : metrics?.total ?? 0}
        />
        <MetricCard
          label={t('dashboard.avgMargin')}
          value={metricsLoading ? '—' : `${metrics?.avgMargin ?? 0}%`}
          variant="primary"
        />
        <MetricCard
          label={t('dashboard.avgDemand')}
          value={metricsLoading ? '—' : `${metrics?.avgDemand ?? 0}`}
          variant="warning"
        />
        <MetricCard
          label={t('dashboard.highOpportunities')}
          value={metricsLoading ? '—' : metrics?.highOpps ?? 0}
          variant="success"
        />
      </div>

      {/* Filter Bar */}
      <div className="flex flex-wrap gap-3 mb-6">
        <Select
          placeholder="Min Margin %"
          value={filters.minMargin}
          onChange={(e) => filters.setMinMargin(Number(e.target.value))}
          options={[
            { value: 0, label: 'Any Margin' },
            { value: 10, label: '≥ 10%' },
            { value: 20, label: '≥ 20%' },
            { value: 30, label: '≥ 30%' },
          ]}
        />
        <Button variant="outline" onClick={() => filters.resetFilters()}>Reset Filters</Button>
      </div>

      {/* Opportunity Cards */}
      <div className="space-y-4">
        {scoresLoading && items.length === 0 ? (
          <p className="text-text-muted">Loading opportunities...</p>
        ) : items.length === 0 ? (
          <p className="text-text-muted">No opportunities found. Adjust your filters.</p>
        ) : items.map((score) => (
          <div key={score.id ?? score.matchId} className="bg-bg-secondary border border-border rounded-lg p-4 hover:shadow-md transition-shadow cursor-pointer">
            <div className="flex justify-between items-start">
              <div>
                <div className="font-semibold text-text-primary">Match #{score.matchId?.slice(0, 8)}</div>
                <div className="text-sm text-text-muted">Landed: {score.landedCostVnd?.toLocaleString('vi-VN')} VND · Retail: {score.vietnamRetailVnd?.toLocaleString('vi-VN')} VND</div>
              </div>
              <div className={`px-3 py-1 rounded-full text-sm font-bold ${
                (score.compositeScore ?? 0) >= 80 ? 'bg-green-100 text-green-800' :
                (score.compositeScore ?? 0) >= 60 ? 'bg-yellow-100 text-yellow-800' :
                'bg-red-100 text-red-800'
              }`}>
                {score.compositeScore ?? 0}
              </div>
            </div>
            <div className="mt-2 grid grid-cols-5 gap-2 text-xs">
              <div><span className="text-text-muted">Margin:</span> {score.profitMarginPct?.toFixed(1)}%</div>
              <div><span className="text-text-muted">Demand:</span> {score.demandScore}</div>
              <div><span className="text-text-muted">Competition:</span> {score.competitionScore}</div>
              <div><span className="text-text-muted">Stability:</span> {score.priceStabilityScore}</div>
              <div><span className="text-text-muted">Confidence:</span> {score.matchConfidenceScore}</div>
            </div>
          </div>
        ))}
      </div>

      {/* Pagination */}
      {scoresData?.totalCount > 20 && (
        <div className="flex justify-center gap-2 mt-6">
          <Button variant="outline" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
          <span className="px-4 py-2 text-sm text-text-muted">Page {page}</span>
          <Button variant="outline" onClick={() => setPage(p => p + 1)}>Next</Button>
        </div>
      )}
    </PageContainer>
  );
}