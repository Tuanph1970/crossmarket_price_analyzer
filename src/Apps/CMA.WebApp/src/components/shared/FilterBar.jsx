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

  // P3-F03: filter by minimum stability score
  const handleStabilityChange = useCallback((e) => {
    filters.setMinStability && filters.setMinStability(Number(e.target.value) || null);
  }, [filters]);

  // P3-F03: filter by minimum composite score
  const handleMinScoreChange = useCallback((e) => {
    filters.setMinScore && filters.setMinScore(Number(e.target.value) || null);
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
        <option value="50">Margin ≥ 50%</option>
      </select>

      <label htmlFor="filter-score" className="sr-only">Minimum composite score</label>
      <select
        id="filter-score"
        className="border border-border rounded-lg px-3 py-1.5 text-sm bg-bg-primary"
        value={filters.minScore ?? ''}
        onChange={handleMinScoreChange}
        aria-label="Minimum composite score"
      >
        <option value="">Any Score</option>
        <option value="50">Score ≥ 50</option>
        <option value="60">Score ≥ 60</option>
        <option value="75">Score ≥ 75</option>
        <option value="90">Score ≥ 90</option>
      </select>

      <label htmlFor="filter-stability" className="sr-only">Minimum price stability</label>
      <select
        id="filter-stability"
        className="border border-border rounded-lg px-3 py-1.5 text-sm bg-bg-primary"
        value={filters.minStability ?? ''}
        onChange={handleStabilityChange}
        aria-label="Minimum price stability score"
      >
        <option value="">Any Stability</option>
        <option value="50">Stability ≥ 50</option>
        <option value="70">Stability ≥ 70</option>
        <option value="85">Stability ≥ 85</option>
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
