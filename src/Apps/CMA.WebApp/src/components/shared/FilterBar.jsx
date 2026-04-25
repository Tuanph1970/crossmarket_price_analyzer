import { useCallback } from 'react';
import { useFilterStore } from '@/store/filterStore';
import { Button } from '@/components/ui/Button';
import { SOURCE_LABELS } from '@/lib/constants';

const selectCls = [
  'border border-border bg-surface rounded-lg px-3 py-1.5 text-sm text-text-primary',
  'focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary',
  'transition-colors duration-150 h-8',
].join(' ');

export function FilterBar({ className = '' }) {
  const filters = useFilterStore();

  const handleMarginChange    = useCallback((e) => filters.setMinMargin(Number(e.target.value) || null), [filters]);
  const handleSourceChange    = useCallback((e) => filters.setSource(e.target.value || null), [filters]);
  const handleStabilityChange = useCallback((e) => filters.setMinStability?.(Number(e.target.value) || null), [filters]);
  const handleMinScoreChange  = useCallback((e) => filters.setMinScore?.(Number(e.target.value) || null), [filters]);
  const handleReset           = useCallback(() => filters.resetFilters(), [filters]);

  return (
    <div
      role="search"
      aria-label="Filters"
      className={`flex flex-wrap items-center gap-2 ${className}`}
    >
      <span className="text-xs font-medium text-text-subtle uppercase tracking-widest mr-1">Filters</span>

      <label htmlFor="filter-margin" className="sr-only">Minimum margin</label>
      <select id="filter-margin" className={selectCls}
        value={filters.minMargin ?? ''} onChange={handleMarginChange}
        aria-label="Minimum margin percentage">
        <option value="">Any Margin</option>
        <option value="10">≥ 10%</option>
        <option value="20">≥ 20%</option>
        <option value="30">≥ 30%</option>
        <option value="50">≥ 50%</option>
      </select>

      <label htmlFor="filter-score" className="sr-only">Minimum composite score</label>
      <select id="filter-score" className={selectCls}
        value={filters.minScore ?? ''} onChange={handleMinScoreChange}
        aria-label="Minimum composite score">
        <option value="">Any Score</option>
        <option value="50">Score ≥ 50</option>
        <option value="60">Score ≥ 60</option>
        <option value="75">Score ≥ 75</option>
        <option value="90">Score ≥ 90</option>
      </select>

      <label htmlFor="filter-stability" className="sr-only">Minimum price stability</label>
      <select id="filter-stability" className={selectCls}
        value={filters.minStability ?? ''} onChange={handleStabilityChange}
        aria-label="Minimum price stability score">
        <option value="">Any Stability</option>
        <option value="50">Stability ≥ 50</option>
        <option value="70">Stability ≥ 70</option>
        <option value="85">Stability ≥ 85</option>
      </select>

      <label htmlFor="filter-source" className="sr-only">Product source</label>
      <select id="filter-source" className={selectCls}
        value={filters.source ?? ''} onChange={handleSourceChange}
        aria-label="Product source">
        <option value="">Any Source</option>
        {Object.entries(SOURCE_LABELS).map(([k, v]) => (
          <option key={k} value={k}>{v}</option>
        ))}
      </select>

      <Button variant="ghost" size="sm" onClick={handleReset} aria-label="Reset all filters"
        className="text-text-muted hover:text-danger text-xs">
        Reset
      </Button>
    </div>
  );
}
