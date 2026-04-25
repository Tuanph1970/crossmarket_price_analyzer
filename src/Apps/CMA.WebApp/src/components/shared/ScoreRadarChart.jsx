import { memo, useMemo } from 'react';
import {
  RadarChart, Radar, PolarGrid, PolarAngleAxis,
  ResponsiveContainer, Legend, Tooltip,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

const FACTORS = [
  { key: 'profitMarginPct',     label: 'Margin',     max: 50,  weight: 30 },
  { key: 'demandScore',         label: 'Demand',     max: 100, weight: 25 },
  { key: 'competitionScore',    label: 'Competition',max: 100, weight: 15 },
  { key: 'priceStabilityScore', label: 'Stability',  max: 100, weight: 15 },
  { key: 'matchConfidenceScore',label: 'Confidence', max: 100, weight: 15 },
];

const AVERAGE_SCORES = {
  profitMarginPct: 18, demandScore: 55, competitionScore: 45,
  priceStabilityScore: 62, matchConfidenceScore: 58,
};

function buildData(opp) {
  return FACTORS.map(f => ({
    factor:   f.label,
    key:      f.key,
    fullMark: f.max,
    [f.key]:  Math.min(Math.max(opp[f.key] ?? 0, 0), f.max),
    avg:      AVERAGE_SCORES[f.key],
  }));
}

const CustomTooltip = ({ active, payload }) => {
  if (!active || !payload?.length) return null;
  const data = payload[0];
  return (
    <div className="bg-surface-raised border border-border rounded-lg px-3 py-2 text-sm shadow-xl">
      <p className="font-medium text-text-primary mb-1">{data.payload.factor}</p>
      <p className="text-text-muted font-mono text-xs">
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
      <table className="sr-only">
        <caption>Score breakdown for opportunity #{score.matchId?.slice(0, 8)}</caption>
        <thead><tr><th>Factor</th><th>Score</th><th>Platform Average</th></tr></thead>
        <tbody>
          {data.map(d => (
            <tr key={d.factor}>
              <td>{d.factor}</td><td>{d[d.key].toFixed(1)}</td><td>{d.avg.toFixed(1)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <ResponsiveContainer width="100%" height={280}>
        <RadarChart data={data} margin={{ top: 8, right: 24, left: 24, bottom: 8 }} aria-hidden="true">
          <PolarGrid stroke="#1A2233" />
          <PolarAngleAxis
            dataKey="factor"
            tick={{ fontSize: 11, fill: '#8B9AB0', fontFamily: '"DM Sans"' }}
          />
          <Radar
            name={t('dashboard.radarChart.opportunity', 'Opportunity')}
            dataKey={Object.keys(score ?? {}).find(k => FACTORS.some(f => f.key === k)) ?? 'demandScore'}
            stroke="#06D6A0"
            fill="#06D6A0"
            fillOpacity={0.15}
            strokeWidth={2}
          />
          <Radar
            name={t('dashboard.radarChart.average', 'Platform Avg')}
            dataKey="avg"
            stroke="#3D4A5C"
            fill="#3D4A5C"
            fillOpacity={0.1}
            strokeWidth={1.5}
            strokeDasharray="4 4"
          />
          <Tooltip content={<CustomTooltip />} />
          <Legend
            wrapperStyle={{ fontSize: 11, fontFamily: '"DM Sans"' }}
            formatter={(value) => <span style={{ color: '#8B9AB0' }}>{value}</span>}
          />
        </RadarChart>
      </ResponsiveContainer>
    </div>
  );
};

export const ScoreRadarChart = memo(ScoreRadarChartComponent);
