import { cn } from '@/lib/utils';

const variants = {
  default: 'border-gray-200',
  primary: 'border-primary/20 bg-primary/5',
  success: 'border-success/20 bg-success/5',
  warning: 'border-warning/20 bg-warning/5',
  danger: 'border-danger/20 bg-danger/5',
};

export function MetricCard({ label, value, variant = 'default', className = '', ...props }) {
  return (
    <div className={cn('rounded-xl border bg-surface p-4 shadow-sm', variants[variant], className)} {...props}>
      <p className="text-sm text-text-muted">{label}</p>
      <p className="mt-1 text-2xl font-bold text-text-primary">{value ?? '—'}</p>
    </div>
  );
}
