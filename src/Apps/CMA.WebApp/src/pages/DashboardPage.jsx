import { useTranslation } from 'react-i18next';
import { MetricCard } from '@/components/ui/MetricCard';
import PageContainer from '@/components/layout/PageContainer';

export default function DashboardPage() {
  const { t } = useTranslation();

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('dashboard.title')}</h1>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4 mb-8">
        <MetricCard label={t('dashboard.totalOpportunities')} value="—" />
        <MetricCard label={t('dashboard.avgMargin')} value="—" variant="primary" />
        <MetricCard label={t('dashboard.avgDemand')} value="—" variant="warning" />
        <MetricCard label={t('dashboard.highOpportunities')} value="—" variant="success" />
      </div>

      <p className="text-text-muted">
        Dashboard implementation coming in Phase 1 — Opportunity cards, filters, and WebSocket updates will be built there.
      </p>
    </PageContainer>
  );
}
