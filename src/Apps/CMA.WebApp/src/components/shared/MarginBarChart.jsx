import { memo, useMemo } from 'react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Cell, LabelList,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

const MARGIN_COLORS = [
  '#06D6A0', // teal   ≥ 30%
  '#F5C518', // gold   20–30%
  '#F59E0B', // amber  10–20%
  '#EF4444', // red    < 10%
];

function getBarColor(margin) {
  if (margin >= 30) return MARGIN_COLORS[0];
  if (margin >= 20) return MARGIN_COLORS[1];
  if (margin >= 10) return MARGIN_COLORS[2];
  return MARGIN_COLORS[3];
}

const CustomTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-surface-raised border border-border rounded-lg px-3 py-2 text-sm shadow-xl">
      <p className="font-medium text-text-primary mb-0.5">{label}</p>
      <p className="text-text-muted font-mono text-xs">{payload[0].value.toFixed(1)}% margin</p>
    </div>
  );
};

const MarginBarChartComponent = ({ scores = [], className = '' }) => {
  const { t } = useTranslation();

  const data = useMemo(() =>
    [...scores]
      .sort((a, b) => (b.profitMarginPct ?? 0) - (a.profitMarginPct ?? 0))
      .slice(0, 10)
      .map((s, i) => ({
        name:   `#${(s.matchId ?? '').slice(0, 6) || i + 1}`,
        margin: parseFloat((s.profitMarginPct ?? 0).toFixed(2)),
        raw:    s,
      })),
  [scores]);

  if (data.length === 0) {
    return (
      <div className={cn('flex items-center justify-center h-48 text-text-muted text-sm', className)}>
        {t('dashboard.noData', 'No opportunity data to display')}
      </div>
    );
  }

  return (
    <div className={cn('relative', className)}>
      <table className="sr-only">
        <caption>Top 10 opportunities by profit margin</caption>
        <thead><tr><th>Match ID</th><th>Profit Margin %</th></tr></thead>
        <tbody>{data.map(d => <tr key={d.name}><td>{d.name}</td><td>{d.margin}%</td></tr>)}</tbody>
      </table>

      <ResponsiveContainer width="100%" height={260}>
        <BarChart
          data={data}
          layout="vertical"
          margin={{ top: 4, right: 32, left: 8, bottom: 4 }}
          aria-hidden="true"
        >
          <CartesianGrid strokeDasharray="2 4" stroke="#1A2233" horizontal={false} />
          <XAxis
            type="number"
            domain={[0, 'dataMax + 5']}
            tickFormatter={v => `${v}%`}
            tick={{ fontSize: 11, fill: '#8B9AB0', fontFamily: '"JetBrains Mono"' }}
            axisLine={{ stroke: '#1A2233' }}
            tickLine={false}
          />
          <YAxis
            type="category"
            dataKey="name"
            width={52}
            tick={{ fontSize: 11, fill: '#8B9AB0', fontFamily: '"JetBrains Mono"' }}
            axisLine={false}
            tickLine={false}
          />
          <Tooltip content={<CustomTooltip />} cursor={{ fill: 'rgba(255,255,255,0.03)' }} />
          <Bar dataKey="margin" radius={[0, 4, 4, 0]} maxBarSize={18}>
            {data.map((entry) => (
              <Cell key={entry.name} fill={getBarColor(entry.margin)} />
            ))}
            <LabelList
              dataKey="margin"
              position="right"
              formatter={v => `${v}%`}
              style={{ fontSize: 10, fill: '#8B9AB0', fontFamily: '"JetBrains Mono"' }}
            />
          </Bar>
        </BarChart>
      </ResponsiveContainer>

      <div className="flex flex-wrap gap-4 mt-2 justify-center" aria-hidden="true">
        {[
          { color: MARGIN_COLORS[0], label: '≥30% High' },
          { color: MARGIN_COLORS[1], label: '20–30% Good' },
          { color: MARGIN_COLORS[2], label: '10–20% Moderate' },
          { color: MARGIN_COLORS[3], label: '<10% Low' },
        ].map(l => (
          <span key={l.label} className="flex items-center gap-1.5 text-xs text-text-muted">
            <span className="inline-block w-2 h-2 rounded-sm" style={{ backgroundColor: l.color }} />
            {l.label}
          </span>
        ))}
      </div>
    </div>
  );
};

export const MarginBarChart = memo(MarginBarChartComponent);
