import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import PageContainer from '@/components/layout/PageContainer';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { useUpsertFromScrape } from '@/hooks/useProducts';

export default function QuickLookupPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [url, setUrl] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  const upsertMutation = useUpsertFromScrape({
    onSuccess: (data) => {
      setResult(data?.data ?? data);
      setError(null);
    },
    onError: (err) => {
      setError(err?.response?.data?.message || 'Failed to analyze URL');
      setResult(null);
    },
  });

  const handleAnalyze = () => {
    if (!url.trim()) return;
    setError(null);
    setResult(null);

    const urlLower = url.toLowerCase();
    let source = 'Amazon';
    if (urlLower.includes('walmart.com')) source = 'Walmart';
    else if (urlLower.includes('cigarpage')) source = 'CigarPage';
    else if (urlLower.includes('shopee')) source = 'Shopee';
    else if (urlLower.includes('lazada')) source = 'Lazada';

    let name = 'Product from URL';
    try {
      const u = new URL(url);
      const slug = decodeURIComponent(u.pathname.split('/').filter(Boolean).pop() || '');
      name = slug.replace(/-/g, ' ').replace(/_/g, ' ') || name;
    } catch { /* use default */ }

    upsertMutation.mutate({
      Name: name,
      Brand: null,
      Sku: null,
      Price: 0,
      Currency: 'USD',
      QuantityPerUnit: 1,
      SellerName: null,
      SellerRating: null,
      SalesVolume: null,
      SourceUrl: url,
      Source: source,
      HsCode: null,
      CategoryName: null,
    });
  };

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('quickLookup.title', 'Quick Lookup')}</h1>
      <div className="max-w-2xl space-y-6">
        <div className="flex gap-3">
          <Input
            placeholder="Paste a product URL (Amazon, Walmart, cigarpage.com, Shopee)..."
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleAnalyze()}
            className="flex-1"
          />
          <Button onClick={handleAnalyze} disabled={upsertMutation.isPending}>
            {upsertMutation.isPending ? 'Analyzing...' : 'Analyze'}
          </Button>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-4">{error}</div>
        )}

        {result && (
          <div className="bg-bg-secondary border border-border rounded-lg p-6 space-y-4">
            <h2 className="text-lg font-semibold text-text-primary">{result.name || 'Product Analyzed'}</h2>
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div><span className="text-text-muted">Source:</span> {result.source}</div>
              <div><span className="text-text-muted">Price:</span> {result.latestSnapshot?.price ?? '—'}</div>
              <div><span className="text-text-muted">Currency:</span> {result.latestSnapshot?.currency ?? '—'}</div>
              <div><span className="text-text-muted">Seller:</span> {result.latestSnapshot?.sellerName ?? '—'}</div>
            </div>
            {result.id && (
              <div className="pt-4 border-t border-border">
                <Button variant="outline" onClick={() => navigate(`/compare/${result.id}`)}>
                  View Full Comparison
                </Button>
              </div>
            )}
          </div>
        )}
      </div>
    </PageContainer>
  );
}