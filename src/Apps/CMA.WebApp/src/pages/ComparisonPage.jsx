import { useTranslation } from 'react-i18next';
import { useParams, Link } from 'react-router-dom';
import PageContainer from '@/components/layout/PageContainer';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { useScoreBreakdown } from '@/hooks/useScores';
import { useMatch } from '@/hooks/useMatches';
import { useScores } from '@/hooks/useScores';

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

const FACTOR_LABELS = {
  ProfitMargin: 'profitMargin',
  Demand: 'demand',
  Competition: 'competition',
  Stability: 'stability',
  Confidence: 'confidence',
};

export default function ComparisonPage() {
  const { t } = useTranslation();
  const { matchId } = useParams();
  const { data: breakdown } = useScoreBreakdown(matchId);
  const { data: match } = useMatch(matchId);
  const { data: scoresData } = useScores({ pageSize: 100 });

  const getLabel = (key) => {
    const i18nKey = FACTOR_LABELS[key] ?? key.toLowerCase();
    return t(`comparison.${i18nKey}`, i18nKey);
  };

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('comparison.title')}</h1>

      {matchId ? (
        breakdown ? (
          <div className="grid md:grid-cols-2 gap-6">
            {/* Composite Score Card */}
            <Card className="p-6">
              <div className="flex justify-between items-center mb-4">
                <h2 className="text-lg font-semibold">{t('comparison.compositeScore')}</h2>
                <span className="text-3xl font-bold text-primary">
                  {breakdown.compositeScore?.toFixed(1) ?? '—'}
                </span>
              </div>
              <div className="space-y-1">
                {(breakdown.factors ?? []).map((f) => (
                  <ScoreFactorRow
                    key={f.factorKey ?? f.name}
                    label={getLabel(f.factorKey ?? f.name)}
                    raw={f.rawScore}
                    weight={f.weight}
                    normalized={f.normalizedScore ?? f.normalized}
                    weighted={f.weightedScore ?? f.weighted}
                  />
                ))}
              </div>
            </Card>

            {/* Landed Cost Breakdown Card */}
            <Card className="p-6">
              <h2 className="text-lg font-semibold mb-4">{t('comparison.landedCost')}</h2>
              <div className="space-y-2 text-sm">
                {breakdown.costBreakdown ? (
                  <>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.usPurchase')}</span>
                      <span>{breakdown.costBreakdown.usPurchasePriceVnd?.toLocaleString('vi-VN')} VND</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.cif')}</span>
                      <span>{breakdown.costBreakdown.cifVnd?.toLocaleString('vi-VN')} VND</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.importDuty')}</span>
                      <span>{breakdown.costBreakdown.importDutyVnd?.toLocaleString('vi-VN')} VND</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.vat')}</span>
                      <span>{breakdown.costBreakdown.vatVnd?.toLocaleString('vi-VN')} VND</span>
                    </div>
                    <div className="flex justify-between font-bold border-t pt-2 mt-2">
                      <span>{t('comparison.totalLanded')}</span>
                      <span>{breakdown.costBreakdown.totalLandedCostVnd?.toLocaleString('vi-VN')} VND</span>
                    </div>
                  </>
                ) : (
                  /* Show landed cost derived from score-level fields */
                  <>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.totalLanded')}</span>
                      <span>{Number(breakdown.landedCostVnd ?? 0).toLocaleString('vi-VN')} VND</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.profitMargin', 'Margin')}</span>
                      <span>{breakdown.profitMarginPct?.toFixed(1) ?? '—'}%</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-text-muted">{t('comparison.retailPrice', 'Retail Price')}</span>
                      <span>{Number(breakdown.vietnamRetailVnd ?? 0).toLocaleString('vi-VN')} VND</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-text-muted">Price Difference</span>
                      <span>{Number(breakdown.priceDifferenceVnd ?? 0).toLocaleString('vi-VN')} VND</span>
                    </div>
                  </>
                )}
              </div>
            </Card>

            {/* Match Info */}
            {match && (
              <Card className="p-6 md:col-span-2">
                <h2 className="text-lg font-semibold mb-3">Match Details</h2>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                  <div>
                    <span className="text-text-muted">Status:</span>{' '}
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                      match.status === 'Confirmed' ? 'bg-green-100 text-green-800' :
                      match.status === 'Rejected' ? 'bg-red-100 text-red-800' :
                      'bg-yellow-100 text-yellow-800'
                    }`}>{match.status}</span>
                  </div>
                  <div><span className="text-text-muted">Confidence:</span> {match.confidenceLevel ?? '—'}</div>
                  <div><span className="text-text-muted">Match Score:</span> {match.confidenceScore?.toFixed(1) ?? '—'}</div>
                  <div><span className="text-text-muted">Created:</span> {match.createdAt ? new Date(match.createdAt).toLocaleDateString() : '—'}</div>
                </div>
              </Card>
            )}
          </div>
        ) : (
          <p className="text-text-muted">{t('comparison.noData')}</p>
        )
      ) : (
        <div className="text-center py-16 space-y-4">
          <p className="text-text-muted text-lg">{t('comparison.enterPrompt')}</p>
          {scoresData?.items?.length > 0 && (
            <div className="max-w-md mx-auto space-y-2">
              <p className="text-sm text-text-muted">Recent matches:</p>
              <div className="space-y-1">
                {(scoresData.items.slice(0, 5) || []).map((score) => (
                  <Link
                    key={score.matchId}
                    to={`/compare/${score.matchId}`}
                    className="block p-2 border border-border rounded hover:bg-bg-secondary text-sm text-text-primary transition-colors"
                  >
                    Match #{score.matchId?.slice(0, 8)} — Score: {score.compositeScore?.toFixed(1) ?? '—'}
                  </Link>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </PageContainer>
  );
}
