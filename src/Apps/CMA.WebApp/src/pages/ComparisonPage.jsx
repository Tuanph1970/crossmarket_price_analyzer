import { useParams } from 'react-router-dom';
import PageContainer from '@/components/layout/PageContainer';

export default function ComparisonPage() {
  const { matchId } = useParams();

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">Product Comparison</h1>
      <p className="text-text-muted">Comparison page for match {matchId} — implementation in Phase 1.</p>
    </PageContainer>
  );
}
