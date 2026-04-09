import { CONFIDENCE_COLORS } from '@/lib/constants';

export function ConfidenceBadge({ level, className = '' }) {
  const colors = CONFIDENCE_COLORS[level] ?? 'bg-gray-100 text-gray-700';
  const label = level ?? 'Unknown';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${colors} ${className}`}>
      {label}
    </span>
  );
}
