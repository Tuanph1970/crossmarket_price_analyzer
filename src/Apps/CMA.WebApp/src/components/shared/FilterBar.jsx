import { useFilterStore } from '@/store/filterStore';
import { Button } from '@/components/ui/Button';
import { SOURCE_LABELS } from '@/lib/constants';

export function FilterBar({ className = '' }) {
  const filters = useFilterStore();

  return (
    <div className={`flex flex-wrap gap-3 p-4 bg-bg-secondary border border-border rounded-lg ${className}`}>
      <span className="text-sm font-medium text-text-muted self-center">Filters:</span>

      <select
        className="border border-border rounded-lg px-3 py-1.5 text-sm bg-bg-primary"
        value={filters.minMargin ?? ''}
        onChange={(e) => filters.setMinMargin(Number(e.target.value) || null)}
      >
        <option value="">Any Margin</option>
        <option value="10">Margin ≥ 10%</option>
        <option value="20">Margin ≥ 20%</option>
        <option value="30">Margin ≥ 30%</option>
      </select>

      <select
        className="border border-border rounded-lg px-3 py-1.5 text-sm bg-bg-primary"
        value={filters.source ?? ''}
        onChange={(e) => filters.setSource(e.target.value || null)}
      >
        <option value="">Any Source</option>
        {Object.entries(SOURCE_LABELS).map(([k, v]) => (
          <option key={k} value={k}>{v}</option>
        ))}
      </select>

      <Button variant="outline" size="sm" onClick={() => filters.resetFilters()}>
        Reset
      </Button>
    </div>
  );
}
