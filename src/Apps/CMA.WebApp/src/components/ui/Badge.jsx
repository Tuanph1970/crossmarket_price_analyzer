import { cn } from '@/lib/utils';

const badgeVariants = {
  default:  'bg-surface-raised text-text-muted border border-border',
  primary:  'bg-primary/15 text-primary border border-primary/20',
  success:  'bg-success/15 text-success border border-success/20',
  warning:  'bg-warning/15 text-warning border border-warning/20',
  danger:   'bg-danger/15 text-danger border border-danger/20',
  gold:     'bg-gold/15 text-gold border border-gold/20',
};

export function Badge({ children, variant = 'default', className = '', ...props }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
        badgeVariants[variant],
        className
      )}
      {...props}
    >
      {children}
    </span>
  );
}
