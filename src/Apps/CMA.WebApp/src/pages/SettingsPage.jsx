import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import PageContainer from '@/components/layout/PageContainer';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { useScoringConfig, useUpdateWeights } from '@/hooks/useScores';
import { useScoringStore } from '@/store/scoringStore';
import { SCORING_WEIGHTS } from '@/lib/constants';

export default function SettingsPage() {
  const { t } = useTranslation();
  const { data: config } = useScoringConfig();
  const updateMutation = useUpdateWeights();
  const store = useScoringStore();

  const handleSave = (e) => {
    e.preventDefault();
    updateMutation.mutate(store.weights, {
      onSuccess: () => alert('Weights saved!'),
    });
  };

  const factors = [
    { key: 'profitMargin', label: 'Profit Margin', icon: '💰' },
    { key: 'demand', label: 'Demand Score', icon: '📈' },
    { key: 'competition', label: 'Competition Level', icon: '⚔️' },
    { key: 'stability', label: 'Price Stability', icon: '📊' },
    { key: 'confidence', label: 'Match Confidence', icon: '🎯' },
  ];

  return (
    <PageContainer>
      <h1 className="text-2xl font-bold text-text-primary mb-6">{t('settings.title', 'Settings')}</h1>

      <div className="max-w-2xl space-y-6">
        {/* Scoring Weights */}
        <Card className="p-6">
          <h2 className="text-lg font-semibold mb-4">Scoring Weights</h2>
          <p className="text-sm text-text-muted mb-6">
            Adjust how each factor contributes to the composite opportunity score. Weights must sum to 100%.
          </p>
          <div className="space-y-5">
            {factors.map(({ key, label, icon }) => {
              const val = store.weights?.[key] ?? SCORING_WEIGHTS[key] ?? 0;
              const pct = Math.round(val);
              return (
                <div key={key} className="space-y-1">
                  <div className="flex justify-between items-center">
                    <label className="text-sm font-medium">
                      {icon} {label}
                    </label>
                    <span className="text-sm font-mono font-bold">{pct}%</span>
                  </div>
                  <input
                    type="range"
                    min={0}
                    max={100}
                    value={val}
                    onChange={(e) => store.setWeight(key, Number(e.target.value))}
                    className="w-full accent-primary"
                  />
                  <div className="w-full bg-gray-200 rounded-full h-1.5">
                    <div
                      className="bg-primary h-1.5 rounded-full transition-all"
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>

          <div className="mt-6 pt-4 border-t flex justify-between items-center">
            <span className="text-sm text-text-muted">
              Total: {Object.values(store.weights ?? SCORING_WEIGHTS).reduce((a, b) => a + b, 0)}%
            </span>
            <div className="flex gap-3">
              <Button variant="outline" onClick={() => store.resetWeights()}>Reset</Button>
              <Button onClick={handleSave} disabled={updateMutation.isPending}>
                {updateMutation.isPending ? 'Saving...' : 'Save Weights'}
              </Button>
            </div>
          </div>
        </Card>

        {/* Current Config */}
        {config?.weights && (
          <Card className="p-6">
            <h2 className="text-lg font-semibold mb-4">Active Configuration</h2>
            <div className="space-y-2 text-sm">
              {config.weights.map((w) => (
                <div key={w.factorKey} className="flex justify-between">
                  <span className="text-text-muted">{w.factorKey}</span>
                  <span className="font-mono">{w.weight}%</span>
                </div>
              ))}
            </div>
          </Card>
        )}
      </div>
    </PageContainer>
  );
}