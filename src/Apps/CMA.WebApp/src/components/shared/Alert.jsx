/**
 * P4-F01: Alert — dismissible inline alert / error / success / warning message.
 * Used in login / register forms and other pages.
 */
import { X, AlertCircle, CheckCircle, Info, AlertTriangle } from 'lucide-react';
import { cn } from '@/lib/utils';

const ICONS = {
  error:   AlertCircle,
  success: CheckCircle,
  info:    Info,
  warning: AlertTriangle,
};

const STYLES = {
  error:   'border-danger/30 bg-danger/5 text-danger',
  success: 'border-success/30 bg-success/5 text-success',
  info:    'border-primary/30  bg-primary/5  text-primary',
  warning: 'border-yellow-400/30 bg-yellow-50 text-yellow-700',
};

/**
 * @param {{ message: string, type?: 'error'|'success'|'info'|'warning', onDismiss?, className? }} props
 */
export function Alert({ message, type = 'error', onDismiss, className = '' }) {
  const Icon = ICONS[type] ?? Info;

  return (
    <div
      className={cn(
        'flex items-start gap-3 rounded-lg border px-4 py-3 text-sm',
        STYLES[type], className
      )}
      role="alert"
    >
      <Icon className="w-4 h-4 mt-0.5 shrink-0" />
      <span className="flex-1">{message}</span>
      {onDismiss && (
        <button
          onClick={onDismiss}
          className="shrink-0 opacity-70 hover:opacity-100"
          aria-label="Dismiss"
        >
          <X className="w-4 h-4" />
        </button>
      )}
    </div>
  );
}