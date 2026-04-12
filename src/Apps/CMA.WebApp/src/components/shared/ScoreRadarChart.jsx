/**
 * P3-F05: Radar/Spider chart showing the 5-factor score breakdown for a
 * single opportunity vs. the platform average.
 * Uses recharts RadarChart. Accessible — includes data table.
 */
import { memo, useMemo } from 'react';
import {
  RadarChart, Radar, PolarGrid, PolarAngleAxis,
  ResponsiveContainer, Legend, Tooltip,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

const FACTORS = [
  { key: 'profitMarginPct',    label: 'Margin',     max: 50,  weight: 30 },
  { key: 'demandScore',         label: 'Demand',      max: 100, weight: 25 },
  { key: 'competitionScore',    label: 'Competition', max: 100, weight: 15 },
  { key: 'priceStabilityScore', label: 'Stability',  max: 100, weight: 15 },
  { key: 'matchConfidenceScore',label: 'Confidence',  max: 100, weight: 15 },
];

const AVERAGE_SCORES = {
  profitMarginPct:    18,
  demandScore:         55,
  competitionScore:    45,
  priceStabilityScore: 62,
  matchConfidenceScore: 58,
};

function buildData(opp) {
  return FACTORS.map(f => ({
    factor:  f.label,
    fullMark: f.max,
    [f.key]:  Math.min(Math.max(opp[f.key] ?? 0, 0), f.max),
    avg:      AVERAGE_SCORES[f.key],
  }));
}

const CustomTooltip = ({ active, payload }) => {
  if (!active || !payload?.length) return null;
  const data = payload[0];
  return (
    <div className="bg-bg-secondary border border-border rounded-lg px-3 py-2 text-sm shadow-md">
      <p className="font-medium text-text-primary">{data.payload.factor}</p>
      <p className="text-text-muted">
        {data.name}: {data.value.toFixed(1)}
      </p>
    </div>
  );
};

const ScoreRadarChartComponent = ({ score, className = '' }) => {
  const { t } = useTranslation();

  const data = useMemo(() => buildData(score ?? {}), [score]);

  if (!score?.matchId) {
    return (
      <div className={cn('flex items-center justify-center h-64 text-text-muted text-sm', className)}>
        {t('dashboard.noData', 'Select an opportunity to view its score breakdown')}
      </div>
    );
  }

  return (
    <div className={cn('relative', className)}>
      {/* Accessible table */}
      <table className="sr-only">
        <caption>
          {t('dashboard.radarChart.caption', 'Score breakdown for opportunity')} #{score.matchId?.slice(0, 8)}
        </caption>
        <thead>
          <tr><th>Factor</th><th>Score</th><th>Platform Average</th></tr>
        </thead>
        <tbody>
          {data.map(d => (
            <tr key={d.factor}>
              <td>{d.factor}</td>
              <td>{d[d.key].toFixed(1)}</td>
              <td>{d.avg.toFixed(1)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <ResponsiveContainer width="100%" height={300}>
        <RadarChart data={data} margin={{ top: 8, right: 24, left: 24, bottom: 8 }} aria-hidden="true">
          <PolarGrid stroke="var(--color-border)" />
          <PolarAngleAxis
            dataKey="factor"
            tick={{ fontSize: 12, fill: 'var(--color-text-muted)' }}
          />
          <Radar
            name={t('dashboard.radarChart.opportunity', 'Opportunity')}
            dataKey={Object.keys(score ?? {}).find(k => FACTORS.some(f => f.key === k)) ?? 'demandScore'}
            stroke="#3b82f6"
            fill="#3b82f6"
            fillOpacity={0.25}
            strokeWidth={2}
          />
          <Radar
            name={t('dashboard.radarChart.average', 'Platform Avg')}
            dataKey="avg"
            stroke="#94a3b8"
            fill="#94a3b8"
            fillOpacity={0.1}
            strokeWidth={1.5}
            strokeDasharray="4 4"
          />
          <Tooltip content={<CustomTooltip />} />
          <Legend
            wrapperStyle={{ fontSize: 12 }}
            formatter={(value) => (
              <span style={{ color: 'var(--color-text-muted)' }}>{value}</span>
            )}
          />
        </RadarChart>
      </ResponsiveContainer>
    </div>
  );
};

export const ScoreRadarChart = memo(ScoreRadarChartComponent);
