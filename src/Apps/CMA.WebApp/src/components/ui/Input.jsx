import { forwardRef } from 'react';
import { cn } from '@/lib/utils';

export const Input = forwardRef(function Input({ className = '', ...props }, ref) {
  return (
    <input
      ref={ref}
      className={cn(
        'flex h-10 w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-text-primary',
        'placeholder:text-text-muted',
        'focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary',
        'transition-colors duration-150',
        'disabled:cursor-not-allowed disabled:opacity-40',
        className
      )}
      {...props}
    />
  );
});
