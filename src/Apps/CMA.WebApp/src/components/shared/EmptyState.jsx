import { cn } from '@/lib/utils';

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  actionLabel,
  className = '',
}) {
  return (
    <div className={cn('flex flex-col items-center justify-center py-20 px-4 text-center', className)}>
      {Icon && (
        <div className="w-14 h-14 rounded-2xl bg-surface border border-border flex items-center justify-center mb-4">
          <Icon className="w-6 h-6 text-text-muted" />
        </div>
      )}
      <h3 className="text-sm font-semibold text-text-primary mb-1">{title}</h3>
      {description && (
        <p className="text-sm text-text-muted max-w-sm mb-5">{description}</p>
      )}
      {action && actionLabel && (
        <button
          onClick={action}
          className="px-4 py-2 text-sm font-medium bg-primary text-background rounded-lg hover:bg-primary-600 transition-colors"
        >
          {actionLabel}
        </button>
      )}
    </div>
  );
}
