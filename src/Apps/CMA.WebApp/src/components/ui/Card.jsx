import { cn } from '@/lib/utils';

export function Card({ children, className = '', ...props }) {
  return (
    <div
      className={cn('rounded-xl border border-border bg-surface', className)}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardHeader({ children, className = '', ...props }) {
  return (
    <div className={cn('px-6 py-4 border-b border-border', className)} {...props}>
      {children}
    </div>
  );
}

export function CardContent({ children, className = '', ...props }) {
  return (
    <div className={cn('px-6 py-4', className)} {...props}>
      {children}
    </div>
  );
}
