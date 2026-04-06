import { useState } from 'react';
import PageContainer from '@/components/layout/PageContainer';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';

export default function QuickLookupPage() {
  const [url, setUrl] = useState('');

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">Quick Lookup</h1>
      <div className="max-w-xl space-y-4">
        <Input
          placeholder="Paste a product URL (Amazon, Walmart, cigarpage.com)..."
          value={url}
          onChange={(e) => setUrl(e.target.value)}
        />
        <Button>Analyze</Button>
        <p className="text-text-muted text-sm">Quick Lookup — full implementation in Phase 1.</p>
      </div>
    </PageContainer>
  );
}
