import { useEffect, useRef, useCallback } from 'react';
import { createPortal } from 'react-dom';

/**
 * Skip-to-content link — visually hidden until focused.
 * Place once at the top of Layout.
 */
export function SkipToContent({ targetId = 'main-content', children = 'Skip to main content' }) {
  return (
    <a
      href={`#${targetId}`}
      className="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-[100] focus:px-4 focus:py-2 focus:bg-primary focus:text-white focus:rounded focus:shadow-lg focus:outline-none focus:ring-2 focus:ring-primary"
    >
      {children}
    </a>
  );
}

/**
 * ARIA live region — announce dynamic content updates to screen readers.
 *
 * Usage:
 *   <LiveRegion message={countMessage} politeness="polite" />
 */
export function LiveRegion({ message, politeness = 'polite', className = 'sr-only' }) {
  return (
    <div
      role="status"
      aria-live={politeness}
      aria-atomic="true"
      className={className}
    >
      {message}
    </div>
  );
}

/**
 * Focus trap for modals and drawers.
 *
 * Usage:
 *   <FocusTrap active={isOpen} onEscape={handleClose}>
 *     <div role="dialog" aria-modal="true">...</div>
 *   </FocusTrap>
 */
export function FocusTrap({ children, active, onEscape }) {
  const containerRef = useRef(null);

  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape' && active) {
      onEscape?.();
      return;
    }

    if (!active || e.key !== 'Tab') return;

    const container = containerRef.current;
    if (!container) return;

    const focusableSelectors = [
      'a[href]',
      'button:not([disabled])',
      'input:not([disabled])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])',
    ].join(', ');

    const focusable = Array.from(container.querySelectorAll(focusableSelectors));
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (e.shiftKey) {
      if (document.activeElement === first) {
        e.preventDefault();
        last.focus();
      }
    } else {
      if (document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
  }, [active, onEscape]);

  useEffect(() => {
    if (!active) return;
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [active, handleKeyDown]);

  // When activated, move focus to first focusable element
  useEffect(() => {
    if (!active || !containerRef.current) return;
    const focusableSelectors = [
      'a[href]', 'button:not([disabled])', 'input:not([disabled])',
      'select:not([disabled])', 'textarea:not([disabled])', '[tabindex]:not([tabindex="-1"])',
    ].join(', ');
    const first = containerRef.current.querySelector(focusableSelectors);
    if (first) first.focus();
  }, [active]);

  return (
    <div ref={containerRef} aria-hidden={!active}>
      {children}
    </div>
  );
}

/**
 * Dismiss on escape or outside-click for dropdowns/popovers.
 */
export function useDismiss(onDismiss) {
  const handleKeyDown = useCallback((e) => {
    if (e.key === 'Escape') onDismiss?.();
  }, [onDismiss]);

  const handlePointerDown = useCallback((e) => {
    // Don't dismiss if click was inside the triggering element
    if (e.target.closest('[data-dismiss-ignore]')) return;
    onDismiss?.();
  }, [onDismiss]);

  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown);
    document.addEventListener('pointerdown', handlePointerDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.removeEventListener('pointerdown', handlePointerDown);
    };
  }, [handleKeyDown, handlePointerDown]);
}

/**
 * Utility: add descriptive alt text to product images.
 * Ensures no empty or placeholder alt attributes.
 */
export function getImageAlt({ name, brand, source, fallback = 'Product image' }) {
  if (name) {
    const parts = [name];
    if (brand) parts.unshift(brand);
    return parts.join(' by ') + (source ? ` from ${source}` : '');
  }
  return fallback;
}
