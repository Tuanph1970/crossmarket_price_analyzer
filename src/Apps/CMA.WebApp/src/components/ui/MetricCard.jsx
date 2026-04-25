import { cn } from '@/lib/utils';

const variants = {
  default: { border: 'border-border',         value: 'text-text-primary',  glow: '' },
  primary: { border: 'border-primary/20',     value: 'text-primary',       glow: 'shadow-[0_0_24px_rgba(6,214,160,0.06)]' },
  success: { border: 'border-success/20',     value: 'text-success',       glow: '' },
  warning: { border: 'border-gold/25',        value: 'text-gold',          glow: '' },
  danger:  { border: 'border-danger/20',      value: 'text-danger',        glow: '' },
};

export function MetricCard({ label, value, variant = 'default', className = '', ...props }) {
  const v = variants[variant] ?? variants.default;
  return (
    <div
      className={cn('rounded-xl border bg-surface p-5', v.border, v.glow, className)}
      {...props}
    >
      <p className="text-xs font-medium text-text-muted uppercase tracking-widest mb-2">{label}</p>
      <p className={cn('font-display text-3xl font-bold leading-none', v.value)}>
        {value ?? '—'}
      </p>
    </div>
  );
}
