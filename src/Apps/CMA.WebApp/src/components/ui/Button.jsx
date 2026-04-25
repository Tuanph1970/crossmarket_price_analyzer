import { cn } from '@/lib/utils';

const variants = {
  primary:         'bg-primary text-background font-semibold hover:bg-primary-600 shadow-[0_0_16px_rgba(6,214,160,0.2)] hover:shadow-[0_0_24px_rgba(6,214,160,0.3)]',
  secondary:       'bg-surface-raised text-text-primary hover:bg-border',
  danger:          'bg-danger text-white font-semibold hover:bg-danger-600',
  outline:         'border border-border text-text-primary hover:bg-surface-raised hover:border-border-primary',
  'outline-primary': 'border border-primary text-primary hover:bg-primary/10',
  ghost:           'text-text-muted hover:bg-surface-raised hover:text-text-primary',
};

const sizes = {
  sm: 'px-3 py-1.5 text-xs',
  md: 'px-4 py-2 text-sm',
  lg: 'px-6 py-2.5 text-sm',
};

export function Button({
  children,
  variant = 'primary',
  size = 'md',
  className = '',
  disabled = false,
  ...props
}) {
  return (
    <button
      className={cn(
        'inline-flex items-center justify-center font-medium rounded-lg transition-all duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:ring-offset-1 focus-visible:ring-offset-background',
        'disabled:opacity-40 disabled:cursor-not-allowed disabled:shadow-none',
        variants[variant],
        sizes[size],
        className
      )}
      disabled={disabled}
      {...props}
    >
      {children}
    </button>
  );
}
