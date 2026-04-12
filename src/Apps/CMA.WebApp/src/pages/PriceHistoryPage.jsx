import { useState, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend,
} from 'recharts';
import { Download, TrendingUp } from 'lucide-react';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardHeader, CardContent } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Skeleton } from '@/components/ui/Skeleton';
import { usePriceHistory } from '@/hooks/useProducts';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { CONFIDENCE_COLORS } from '@/lib/constants';

const DEFAULT_FROM = () => {
  const d = new Date();
  d.setDate(d.getDate() - 30);
  return d.toISOString().slice(0, 10);
};
const DEFAULT_TO = () => new Date().toISOString().slice(0, 10);

function toVnd(usdPrice, exchangeRate = 25000) {
  return usdPrice * exchangeRate;
}

function downloadCsv(snapshots, productName) {
  const header = ['Date', 'US Price (USD)', 'VN Price (VND)', 'Seller', 'Source'];
  const rows = snapshots.map((s) => [
    new Date(s.scrapedAt).toLocaleDateString(),
    s.price ?? '',
    s.vietnamPriceVnd ?? '',
    s.seller ?? '',
    s.source ?? '',
  ]);
  const csv = [header, ...rows].map((r) => r.join(',')).join('\n');
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `price-history-${productName ?? 'product'}-${new Date().toISOString().slice(0, 10)}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

function LatestSnapshotCard({ snapshot, exchangeRate }) {
  const { t } = useTranslation();
  if (!snapshot) return null;
  return (
    <Card className="p-5">
      <div className="flex justify-between items-start mb-3">
        <div>
          <p className="text-sm text-text-muted">{t('priceHistory.latestSnapshot', 'Latest Snapshot')}</p>
          <p className="text-xs text-text-muted mt-0.5">
            {new Date(snapshot.scrapedAt).toLocaleDateString()}
          </p>
        </div>
        <Badge variant="primary">{snapshot.source ?? '—'}</Badge>
      </div>
      <div className="grid grid-cols-2 gap-4 text-sm">
        <div>
          <p className="text-text-muted text-xs">{t('priceHistory.usPrice', 'US Price')}</p>
          <p className="text-lg font-semibold text-text-primary">
            {Number(snapshot.price ?? 0).toLocaleString('en-US', { style: 'currency', currency: 'USD' })}
          </p>
        </div>
        <div>
          <p className="text-text-muted text-xs">{t('priceHistory.vnPrice', 'VN Price (est.)')}</p>
          <p className="text-lg font-semibold text-text-primary">
            {toVnd(snapshot.price, exchangeRate).toLocaleString('vi-VN')} VND
          </p>
        </div>
      </div>
      {snapshot.seller && (
        <p className="text-xs text-text-muted mt-3 pt-3 border-t border-border">
          {t('priceHistory.seller', 'Seller')}: {snapshot.seller}
        </p>
      )}
    </Card>
  );
}

function ChartSkeleton() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-[300px] w-full rounded-lg" />
      <div className="flex gap-2 justify-center">
        <Skeleton className="h-4 w-24" />
        <Skeleton className="h-4 w-24" />
      </div>
    </div>
  );
}

export default function PriceHistoryPage() {
  const { t } = useTranslation();
  const { productId } = useParams();
  const [from, setFrom] = useState(DEFAULT_FROM);
  const [to, setTo] = useState(DEFAULT_TO);
  const [limit] = useState(30);

  const { data, isLoading, isError, refetch } = usePriceHistory(productId, { from, to, limit });

  const snapshots = data?.snapshots ?? [];
  const latest = snapshots[0] ?? null;
  const exchangeRate = data?.exchangeRate ?? 25000;

  const chartData = useMemo(
    () =>
      [...snapshots].reverse().map((s) => ({
        date: new Date(s.scrapedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
        usPrice: s.price,
        vnPrice: s.vietnamPriceVnd ?? toVnd(s.price, exchangeRate),
      })),
    [snapshots, exchangeRate]
  );

  const handleExport = () => downloadCsv(snapshots, data?.productName ?? productId);

  return (
    <ErrorBoundary onReset={refetch}>
      <PageContainer>
        <div className="flex flex-wrap justify-between items-center gap-4 mb-6">
          <h1 className="text-2xl font-bold text-text-primary">
            {t('priceHistory.title', 'Price History')} — {data?.productName ?? productId}
          </h1>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={handleExport} disabled={snapshots.length === 0}>
              <Download className="w-4 h-4 mr-1.5 inline" />
              {t('priceHistory.export', 'Export CSV')}
            </Button>
          </div>
        </div>

        {/* Date Filters */}
        <Card className="p-4 mb-6">
          <div className="flex flex-wrap gap-4 items-end">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-text-muted">{t('priceHistory.from', 'From')}</label>
              <input
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
                className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary/50"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-text-muted">{t('priceHistory.to', 'To')}</label>
              <input
                type="date"
                value={to}
                onChange={(e) => setTo(e.target.value)}
                className="px-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary/50"
              />
            </div>
          </div>
        </Card>

        {/* Main Chart */}
        {isLoading ? (
          <ChartSkeleton />
        ) : isError ? (
          <div className="py-8">
            <EmptyState
              icon={TrendingUp}
              title={t('common.error', 'An error occurred')}
              description={t('priceHistory.errorLoading', 'Could not load price history.')}
              action={refetch}
              actionLabel={t('common.retry', 'Retry')}
            />
          </div>
        ) : chartData.length === 0 ? (
          <Card className="p-6">
            <EmptyState
              icon={TrendingUp}
              title={t('priceHistory.noData', 'No price history available')}
              description={t('priceHistory.noDataDesc', 'There is no price history data for this product in the selected date range.')}
            />
          </Card>
        ) : (
          <>
            <Card className="p-6 mb-6">
              <ResponsiveContainer width="100%" height={320}>
                <LineChart data={chartData} margin={{ top: 4, right: 16, bottom: 0, left: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                  <XAxis dataKey="date" fontSize={12} tickLine={false} axisLine={false} />
                  <YAxis
                    fontSize={12}
                    tickLine={false}
                    axisLine={false}
                    tickFormatter={(v) => v.toLocaleString()}
                    width={80}
                  />
                  <Tooltip
                    formatter={(value, name) => [
                      value?.toLocaleString() ?? '—',
                      name === 'usPrice'
                        ? t('priceHistory.usPrice', 'US Price')
                        : t('priceHistory.vnPrice', 'VN Price'),
                    ]}
                    labelFormatter={(label) => new Date(label).toLocaleDateString()}
                  />
                  <Legend
                    formatter={(value) =>
                      value === 'usPrice'
                        ? t('priceHistory.usPrice', 'US Price')
                        : t('priceHistory.vnPrice', 'VN Price')
                    }
                  />
                  <Line
                    type="monotone"
                    dataKey="usPrice"
                    stroke="#2563eb"
                    strokeWidth={2}
                    dot={false}
                    name="usPrice"
                  />
                  <Line
                    type="monotone"
                    dataKey="vnPrice"
                    stroke="#dc2626"
                    strokeWidth={2}
                    dot={false}
                    name="vnPrice"
                  />
                </LineChart>
              </ResponsiveContainer>

              <div className="mt-4 text-sm text-text-muted text-center">
                {t('priceHistory.dataPoints', '{{count}} data points', { count: chartData.length })}
              </div>
            </Card>

            {/* Confidence + Latest Snapshot */}
            <div className="grid md:grid-cols-2 gap-6">
              {latest && (
                <LatestSnapshotCard snapshot={latest} exchangeRate={exchangeRate} />
              )}
              {data?.confidenceLevel && (
                <Card className="p-5">
                  <p className="text-sm text-text-muted mb-2">{t('priceHistory.confidence', 'Confidence Level')}</p>
                  <Badge
                    variant={
                      data.confidenceLevel === 'High'
                        ? 'success'
                        : data.confidenceLevel === 'Medium'
                        ? 'warning'
                        : 'danger'
                    }
                    className="text-sm px-3 py-1"
                  >
                    {data.confidenceLevel}
                  </Badge>
                  <p className="text-xs text-text-muted mt-3">
                    {t('priceHistory.confidenceDesc', 'Based on data completeness and price variance.')}
                  </p>
                </Card>
              )}
            </div>
          </>
        )}
      </PageContainer>
    </ErrorBoundary>
  );
}
