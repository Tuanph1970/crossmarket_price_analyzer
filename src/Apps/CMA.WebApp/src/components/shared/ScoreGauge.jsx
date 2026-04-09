import { cn } from '@/lib/utils';

export function ScoreGauge({ score, size = 'md' }) {
  const pct = Math.round(score ?? 0);
  const color = pct >= 80 ? 'text-green-600' : pct >= 60 ? 'text-yellow-600' : 'text-red-600';
  const bg = pct >= 80 ? 'bg-green-100' : pct >= 60 ? 'bg-yellow-100' : 'bg-red-100';

  const sizeMap = {
    sm: { text: 'text-lg', svg: 48, stroke: 4 },
    md: { text: 'text-2xl', svg: 64, stroke: 6 },
    lg: { text: 'text-4xl', svg: 96, stroke: 8 },
  };
  const { text, svg: size2, stroke } = sizeMap[size] ?? sizeMap.md;
  const r = (size2 - stroke) / 2;
  const circ = 2 * Math.PI * r;
  const offset = circ * (1 - pct / 100);

  return (
    <div className={cn('flex flex-col items-center', color)}>
      <svg width={size2} height={size2} className="-rotate-90">
        <circle cx={size2 / 2} cy={size2 / 2} r={r} fill="none" stroke="#e5e7eb" strokeWidth={stroke} />
        <circle
          cx={size2 / 2} cy={size2 / 2} r={r} fill="none"
          stroke="currentColor" strokeWidth={stroke}
          strokeDasharray={circ} strokeDashoffset={offset}
          strokeLinecap="round"
        />
      </svg>
      <span className={cn('font-bold -mt-12', text)}>{pct}</span>
    </div>
  );
}
