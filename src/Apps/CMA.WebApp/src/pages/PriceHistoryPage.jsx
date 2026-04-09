import { useParams } from 'react-router-dom';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import PageContainer from '@/components/layout/PageContainer';
import { usePriceHistory } from '@/hooks/useProducts';
import { Spinner } from '@/components/ui/Spinner';

export default function PriceHistoryPage() {
  const { productId } = useParams();
  const { data: history, isLoading } = usePriceHistory(productId, { pageSize: 100 });

  const snapshots = history?.snapshots ?? [];

  const chartData = snapshots
    .slice()
    .reverse()
    .map((s) => ({
      date: new Date(s.scrapedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
      price: s.price,
      volume: s.salesVolume ?? 0,
    }));

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">
        Price History — {history?.productName ?? productId}
      </h1>

      {isLoading ? (
        <div className="flex justify-center py-12"><Spinner size="lg" /></div>
      ) : chartData.length === 0 ? (
        <p className="text-text-muted">No price history available for this product.</p>
      ) : (
        <div className="bg-bg-secondary border border-border rounded-lg p-6">
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis dataKey="date" fontSize={12} tickLine={false} />
              <YAxis
                fontSize={12}
                tickLine={false}
                tickFormatter={(v) => v.toLocaleString()}
              />
              <Tooltip
                formatter={(value) => [
                  `${value.toLocaleString()} ${history?.currency ?? 'USD'}`,
                  'Price',
                ]}
              />
              <Line
                type="monotone"
                dataKey="price"
                stroke="#2563eb"
                strokeWidth={2}
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>

          <div className="mt-4 text-sm text-text-muted text-center">
            Showing {chartData.length} data points
          </div>
        </div>
      )}
    </PageContainer>
  );
}