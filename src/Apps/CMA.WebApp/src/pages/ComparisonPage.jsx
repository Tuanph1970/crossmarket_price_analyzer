import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import PageContainer from '@/components/layout/PageContainer';
import { Card } from '@/components/ui/Card';
import { useScoreBreakdown } from '@/hooks/useScores';
import { useMatch } from '@/hooks/useMatches';

function ScoreFactorRow({ label, raw, weight, normalized, weighted }) {
  const pct = Math.round(normalized ?? 0);
  const color = pct >= 80 ? 'bg-green-500' : pct >= 60 ? 'bg-yellow-500' : 'bg-red-500';
  return (
    <div className="flex items-center gap-3 py-1">
      <span className="text-sm text-text-muted w-24">{label}</span>
      <div className="flex-1 bg-gray-200 rounded-full h-2">
        <div className={`${color} h-2 rounded-full`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs w-8 text-right">{pct}</span>
      <span className="text-xs text-text-muted w-16 text-right">×{weight}%</span>
      <span className="text-sm font-mono w-12 text-right">{weighted?.toFixed(1) ?? '0'}</span>
    </div>
  );
}

export default function ComparisonPage() {
  const { t } = useTranslation();
  const [matchId, setMatchId] = useState('');
  const { data: breakdown } = useScoreBreakdown(matchId);

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('comparison.title')}</h1>

      <div className="max-w-xl mb-6 flex gap-3">
        <input
          className="flex-1 border border-border rounded-lg px-4 py-2 bg-bg-primary text-text-primary"
          placeholder="Enter match ID to compare..."
          value={matchId}
          onChange={(e) => setMatchId(e.target.value)}
        />
      </div>

      {breakdown ? (
        <div className="grid md:grid-cols-2 gap-6">
          <Card className="p-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-lg font-semibold">Composite Score</h2>
              <span className="text-3xl font-bold text-primary">
                {breakdown.compositeScore?.toFixed(1) ?? '—'}
              </span>
            </div>
            <div className="space-y-1">
              {(breakdown.factors ?? []).map((f) => (
                <ScoreFactorRow key={f.name} {...f} />
              ))}
            </div>
          </Card>

          <Card className="p-6">
            <h2 className="text-lg font-semibold mb-4">Landed Cost Breakdown</h2>
            <div className="space-y-2 text-sm">
              {breakdown.landedCost ? (
                <>
                  <div className="flex justify-between">
                    <span className="text-text-muted">US Purchase</span>
                    <span>{breakdown.landedCost.usPurchasePriceUsd?.toLocaleString()} USD</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">Exchange Rate</span>
                    <span>{breakdown.landedCost.exchangeRate}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">CIF</span>
                    <span>{breakdown.landedCost.cifVnd?.toLocaleString()} VND</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">Import Duty (5%)</span>
                    <span>{breakdown.landedCost.importDutyVnd?.toLocaleString()} VND</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-text-muted">VAT (10%)</span>
                    <span>{breakdown.landedCost.vatVnd?.toLocaleString()} VND</span>
                  </div>
                  <div className="flex justify-between font-bold border-t pt-2 mt-2">
                    <span>Total Landed Cost</span>
                    <span>{breakdown.landedCost.totalCostVnd?.toLocaleString()} VND</span>
                  </div>
                </>
              ) : (
                <p className="text-text-muted">No landed cost data.</p>
              )}
            </div>
          </Card>
        </div>
      ) : matchId ? (
        <p className="text-text-muted">No score data found for this match.</p>
      ) : (
        <p className="text-text-muted">Enter a match ID above to view the score breakdown.</p>
      )}
    </PageContainer>
  );
}