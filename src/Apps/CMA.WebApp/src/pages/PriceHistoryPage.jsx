import { useParams } from 'react-router-dom';
import PageContainer from '@/components/layout/PageContainer';

export default function PriceHistoryPage() {
  const { productId } = useParams();

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">Price History</h1>
      <p className="text-text-muted">Price history chart for product {productId} — implementation in Phase 1.</p>
    </PageContainer>
  );
}
