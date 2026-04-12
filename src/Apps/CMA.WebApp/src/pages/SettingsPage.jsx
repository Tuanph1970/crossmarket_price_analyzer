import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Scale, RotateCcw, Save } from 'lucide-react';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardHeader, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Skeleton } from '@/components/ui/Skeleton';
import { useScoringConfig, useUpdateWeights } from '@/hooks/useScores';
import { useScoringStore } from '@/store/scoringStore';
import { SCORING_WEIGHTS } from '@/lib/constants';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';

const FACTOR_KEYS = ['profitMargin', 'demand', 'competition', 'stability', 'confidence'];

const FACTOR_ICONS = {
  profitMargin: '💰',
  demand: '📈',
  competition: '⚔️',
  stability: '📊',
  confidence: '🎯',
};

/** Preview composite score using sample data */
const SAMPLE_FACTORS = {
  profitMargin: { raw: 72, weight: 40 },
  demand: { raw: 85, weight: 25 },
  competition: { raw: 45, weight: 20 },
  stability: { raw: 68, weight: 10 },
  confidence: { raw: 90, weight: 5 },
};

function computePreviewScore(weights) {
  return Object.entries(SAMPLE_FACTORS).reduce((sum, [key, f]) => {
    const w = weights?.[key] ?? SCORING_WEIGHTS[key] ?? 0;
    return sum + f.raw * (w / 100);
  }, 0);
}

function SliderRow({ factorKey }) {
  const { t } = useTranslation();
  const store = useScoringStore();
  const val = store.weights?.[factorKey] ?? SCORING_WEIGHTS[factorKey] ?? 0;

  return (
    <div className="space-y-2">
      <div className="flex justify-between items-center">
        <label className="text-sm font-medium text-text-primary flex items-center gap-2">
          <span>{FACTOR_ICONS[factorKey]}</span>
          <span>{t(`settings.scoringWeights.${factorKey}`, factorKey)}</span>
        </label>
        <span className="text-sm font-mono font-bold text-primary">{val}%</span>
      </div>
      <input
        type="range"
        min={0}
        max={100}
        step={1}
        value={val}
        onChange={(e) => store.setWeight(factorKey, Number(e.target.value))}
        className="w-full accent-primary"
        aria-label={t(`settings.scoringWeights.${factorKey}`, factorKey)}
      />
      <div className="w-full bg-gray-100 rounded-full h-1.5">
        <div
          className="bg-primary h-1.5 rounded-full transition-all duration-200"
          style={{ width: `${val}%` }}
        />
      </div>
    </div>
  );
}

function WeightsSkeleton() {
  return (
    <Card className="p-6 space-y-6">
      {[...Array(5)].map((_, i) => (
        <div key={i} className="space-y-2">
          <div className="flex justify-between">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-4 w-12" />
          </div>
          <Skeleton className="h-2 w-full rounded-full" />
        </div>
      ))}
      <div className="pt-4 border-t flex justify-between">
        <Skeleton className="h-4 w-20" />
        <div className="flex gap-3">
          <Skeleton className="h-9 w-28" />
          <Skeleton className="h-9 w-28" />
        </div>
      </div>
    </Card>
  );
}

export default function SettingsPage() {
  const { t } = useTranslation();
  const { data: config, isLoading } = useScoringConfig();
  const updateMutation = useUpdateWeights();

  const total = Object.values(useScoringStore.getState().weights ?? SCORING_WEIGHTS).reduce(
    (a, b) => a + b, 0
  );
  const previewScore = computePreviewScore(useScoringStore.getState().weights);

  const handleSave = () => {
    const weights = useScoringStore.getState().weights;
    updateMutation.mutate(weights, {
      onSuccess: () => {
        toast.success(t('settings.saved', 'Settings saved successfully!'));
      },
      onError: (err) => {
        toast.error(
          t('common.error', 'Error'),
          { description: err?.response?.data?.message || t('settings.saveFailed', 'Could not save weights.') }
        );
      },
    });
  };

  const handleReset = () => {
    useScoringStore.getState().resetWeights();
    toast.info(t('settings.resetDone', 'Weights reset to defaults.'));
  };

  return (
    <ErrorBoundary>
      <PageContainer>
        <div className="flex flex-wrap justify-between items-center gap-4 mb-6">
          <h1 className="text-2xl font-bold text-text-primary">
            {t('settings.title', 'Settings')}
          </h1>
        </div>

        <div className="max-w-2xl space-y-6">
          {/* Scoring Weights */}
          {isLoading ? (
            <WeightsSkeleton />
          ) : (
            <Card className="p-6">
              <div className="flex items-center gap-2 mb-1">
                <Scale className="w-5 h-5 text-primary" />
                <h2 className="text-lg font-semibold text-text-primary">
                  {t('settings.scoringWeights.title', 'Scoring Weights')}
                </h2>
              </div>
              <p className="text-sm text-text-muted mb-6">
                {t('settings.scoringWeights.description', 'Adjust how each factor contributes to the composite opportunity score.')}
              </p>

              <div className="space-y-6">
                {FACTOR_KEYS.map((key) => (
                  <SliderRow key={key} factorKey={key} />
                ))}
              </div>

              {/* Total + Preview Score */}
              <div className="mt-6 pt-4 border-t space-y-3">
                <div className="flex justify-between items-center text-sm">
                  <span className="text-text-muted">{t('settings.scoringWeights.total', 'Total weight')}:</span>
                  <span className={`font-mono font-bold ${total === 100 ? 'text-success' : 'text-danger'}`}>
                    {total}%
                  </span>
                </div>
                {total !== 100 && (
                  <p className="text-xs text-danger">
                    {t('settings.scoringWeights.totalWarning', 'Weights should sum to 100% for accurate scoring.')}
                  </p>
                )}

                {/* Preview */}
                <div className="bg-gray-50 rounded-lg p-3">
                  <p className="text-xs text-text-muted mb-1">
                    {t('settings.scoringWeights.preview', 'Sample composite score preview')}
                  </p>
                  <div className="flex items-baseline gap-1">
                    <span className="text-2xl font-bold text-primary">
                      {previewScore.toFixed(1)}
                    </span>
                    <span className="text-sm text-text-muted">/ 100</span>
                  </div>
                  <p className="text-xs text-text-muted mt-1">
                    {t('settings.scoringWeights.previewDesc', 'Based on fixed sample factor values.')}
                  </p>
                </div>

                {/* Actions */}
                <div className="flex justify-between items-center pt-2">
                  <Button variant="ghost" size="sm" onClick={handleReset}>
                    <RotateCcw className="w-4 h-4 mr-1.5 inline" />
                    {t('settings.resetDefaults', 'Reset to defaults')}
                  </Button>
                  <Button onClick={handleSave} disabled={updateMutation.isPending}>
                    {updateMutation.isPending ? (
                      <>
                        <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin mr-1.5 inline-block" />
                        {t('common.loading', 'Saving...')}
                      </>
                    ) : (
                      <>
                        <Save className="w-4 h-4 mr-1.5 inline" />
                        {t('settings.save', 'Save Weights')}
                      </>
                    )}
                  </Button>
                </div>
              </div>
            </Card>
          )}

          {/* Active Configuration */}
          {config?.weights && (
            <Card className="p-6">
              <h2 className="text-lg font-semibold mb-4">
                {t('settings.activeConfig', 'Active Configuration')}
              </h2>
              <div className="space-y-2 text-sm">
                {config.weights.map((w) => (
                  <div key={w.factorKey} className="flex justify-between">
                    <span className="text-text-muted">
                      {t(`settings.scoringWeights.${w.factorKey}`, w.factorKey)}
                    </span>
                    <span className="font-mono font-medium">{w.weight}%</span>
                  </div>
                ))}
              </div>
            </Card>
          )}
        </div>
      </PageContainer>
    </ErrorBoundary>
  );
}
