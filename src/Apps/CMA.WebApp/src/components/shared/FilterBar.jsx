import { useCallback } from 'react';
import { useFilterStore } from '@/store/filterStore';
import { Button } from '@/components/ui/Button';
import { SOURCE_LABELS } from '@/lib/constants';

export function FilterBar({ className = '' }) {
  const filters = useFilterStore();

  const handleMarginChange = useCallback((e) => {
    filters.setMinMargin(Number(e.target.value) || null);
  }, [filters]);

  const handleSourceChange = useCallback((e) => {
    filters.setSource(e.target.value || null);
  }, [filters]);

  const handleReset = useCallback(() => {
    filters.resetFilters();
  }, [filters]);

  return (
    <div
      role="search"
      aria-label="Filters"
      className={`flex flex-wrap gap-3 p-4 bg-bg-secondary border border-border rounded-lg ${className}`}
    >
      <span className="text-sm font-medium text-text-muted self-center" aria-hidden="true">Filters:</span>

      <label htmlFor="filter-margin" className="sr-only">Minimum margin</label>
      <select
        id="filter-margin"
        className="border border-border rounded-lg px-3 py-1.5 text-sm bg-bg-primary"
        value={filters.minMargin ?? ''}
        onChange={handleMarginChange}
        aria-label="Minimum margin percentage"
      >
        <option value="">Any Margin</option>
        <option value="10">Margin ≥ 10%</option>
        <option value="20">Margin ≥ 20%</option>
        <option value="30">Margin ≥ 30%</option>
      </select>

      <label htmlFor="filter-source" className="sr-only">Product source</label>
      <select
        id="filter-source"
        className="border border-border rounded-lg px-3 py-1.5 text-sm bg-bg-primary"
        value={filters.source ?? ''}
        onChange={handleSourceChange}
        aria-label="Product source"
      >
        <option value="">Any Source</option>
        {Object.entries(SOURCE_LABELS).map(([k, v]) => (
          <option key={k} value={k}>{v}</option>
        ))}
      </select>

      <Button variant="outline" size="sm" onClick={handleReset} aria-label="Reset all filters">
        Reset
      </Button>
    </div>
  );
}
