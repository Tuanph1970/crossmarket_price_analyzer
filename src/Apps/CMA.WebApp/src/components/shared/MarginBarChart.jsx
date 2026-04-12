/**
 * P3-F04: Horizontal bar chart comparing profit margins across top 10 opportunities.
 * Uses recharts BarChart. Accessible — includes screen-reader summary table.
 */
import { memo, useMemo } from 'react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Cell, LabelList,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

const MARGIN_COLORS = [
  '#16a34a', // green-600  ≥30%
  '#65a30d', // lime-600   20–30%
  '#ca8a04', // yellow-600 10–20%
  '#dc2626', // red-600    <10%
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
    <div className="bg-bg-secondary border border-border rounded-lg px-3 py-2 text-sm shadow-md">
      <p className="font-medium text-text-primary">{label}</p>
      <p className="text-text-muted">
        {payload[0].value.toFixed(1)}% margin
      </p>
    </div>
  );
};

const MarginBarChartComponent = ({ scores = [], className = '' }) => {
  const { t } = useTranslation();

  const data = useMemo(() => {
    return [...scores]
      .sort((a, b) => (b.profitMarginPct ?? 0) - (a.profitMarginPct ?? 0))
      .slice(0, 10)
      .map((s, i) => ({
        name: `#${(s.matchId ?? '').slice(0, 6) || i + 1}`,
        margin: parseFloat((s.profitMarginPct ?? 0).toFixed(2)),
        raw: s,
      }));
  }, [scores]);

  if (data.length === 0) {
    return (
      <div className={cn('flex items-center justify-center h-48 text-text-muted text-sm', className)}>
        {t('dashboard.noData', 'No opportunity data to display')}
      </div>
    );
  }

  return (
    <div className={cn('relative', className)}>
      {/* Accessible summary for screen readers */}
      <table className="sr-only">
        <caption>{t('dashboard.marginChart.caption', 'Top 10 opportunities by profit margin')}</caption>
        <thead>
          <tr>
            <th>Match ID</th>
            <th>Profit Margin %</th>
          </tr>
        </thead>
        <tbody>
          {data.map(d => (
            <tr key={d.name}>
              <td>{d.name}</td>
              <td>{d.margin}%</td>
            </tr>
          ))}
        </tbody>
      </table>

      <ResponsiveContainer width="100%" height={280}>
        <BarChart
          data={data}
          layout="vertical"
          margin={{ top: 4, right: 24, left: 8, bottom: 4 }}
          aria-hidden="true"
        >
          <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" horizontal={false} />
          <XAxis
            type="number"
            domain={[0, 'dataMax + 5']}
            tickFormatter={v => `${v}%`}
            tick={{ fontSize: 12, fill: 'var(--color-text-muted)' }}
            axisLine={{ stroke: 'var(--color-border)' }}
            tickLine={false}
          />
          <YAxis
            type="category"
            dataKey="name"
            width={48}
            tick={{ fontSize: 12, fill: 'var(--color-text-muted)' }}
            axisLine={false}
            tickLine={false}
          />
          <Tooltip content={<CustomTooltip />} cursor={{ fill: 'rgba(0,0,0,0.05)' }} />
          <Bar dataKey="margin" radius={[0, 4, 4, 0]} maxBarSize={24}>
            {data.map((entry) => (
              <Cell key={entry.name} fill={getBarColor(entry.margin)} />
            ))}
            <LabelList
              dataKey="margin"
              position="right"
              formatter={v => `${v}%`}
              style={{ fontSize: 11, fill: 'var(--color-text-muted)' }}
            />
          </Bar>
        </BarChart>
      </ResponsiveContainer>

      {/* Legend */}
      <div className="flex flex-wrap gap-3 mt-2 justify-center" aria-hidden="true">
        {[
          { color: MARGIN_COLORS[0], label: t('dashboard.marginChart.high', '≥30% High') },
          { color: MARGIN_COLORS[1], label: t('dashboard.marginChart.good', '20–30% Good') },
          { color: MARGIN_COLORS[2], label: t('dashboard.marginChart.moderate', '10–20% Moderate') },
          { color: MARGIN_COLORS[3], label: t('dashboard.marginChart.low', '<10% Low') },
        ].map(l => (
          <span key={l.label} className="flex items-center gap-1.5 text-xs text-text-muted">
            <span className="inline-block w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: l.color }} />
            {l.label}
          </span>
        ))}
      </div>
    </div>
  );
};

export const MarginBarChart = memo(MarginBarChartComponent);
