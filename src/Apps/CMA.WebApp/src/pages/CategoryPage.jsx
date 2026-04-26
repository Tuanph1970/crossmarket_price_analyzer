import { useState } from 'react';
import { Layers, Package, ChevronRight, X } from 'lucide-react';
import PageContainer from '@/components/layout/PageContainer';
import { Badge } from '@/components/ui/Badge';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/shared/EmptyState';
import { useCategories } from '@/hooks/useCategories';
import { useProducts } from '@/hooks/useProducts';
import { cn } from '@/lib/utils';
import { SOURCE_LABELS } from '@/lib/constants';

const HS_ICONS = {
  '8517': '📱',
  '2402': '🚬',
  '3304': '💊',
  '6109': '👕',
  '2106': '💊',
  '9506': '⚽',
  '9403': '🏠',
  '9503': '🎮',
  '2009': '🍜',
  '3307': '💄',
};

function CategoryCard({ category, isSelected, onClick }) {
  const icon = HS_ICONS[category.hsCode] ?? '📦';
  return (
    <button
      onClick={onClick}
      className={cn(
        'group relative text-left w-full rounded-lg border p-4 transition-all duration-150',
        'bg-surface hover:bg-surface-raised',
        isSelected
          ? 'border-primary ring-1 ring-primary/40'
          : 'border-border hover:border-border-primary'
      )}
      aria-pressed={isSelected}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="text-2xl leading-none mt-0.5" aria-hidden="true">{icon}</span>
        {isSelected && (
          <span className="shrink-0 w-4 h-4 rounded-full bg-primary/20 text-primary flex items-center justify-center">
            <ChevronRight className="w-2.5 h-2.5" />
          </span>
        )}
      </div>

      <p className="mt-2 text-sm font-semibold text-text-primary leading-snug">
        {category.name}
      </p>

      <div className="mt-1 flex items-center gap-2 flex-wrap">
        <span className="font-mono text-xs text-text-muted">HS {category.hsCode}</span>
        {category.parentCategoryName && (
          <span className="text-xs text-text-subtle">· {category.parentCategoryName}</span>
        )}
      </div>

      <div className="mt-2">
        <Badge variant={category.productCount > 0 ? 'primary' : 'default'}>
          {category.productCount} {category.productCount === 1 ? 'product' : 'products'}
        </Badge>
      </div>
    </button>
  );
}

function ProductRow({ product }) {
  const snapshot = product.latestSnapshot;
  return (
    <div className="flex items-center justify-between gap-3 py-3 border-b border-border last:border-0">
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-text-primary truncate">{product.name}</p>
        <div className="flex items-center gap-2 mt-0.5 flex-wrap">
          {product.brandName && (
            <span className="text-xs text-text-muted">{product.brandName}</span>
          )}
          <span className={cn(
            'text-xs font-mono px-1.5 py-0.5 rounded border',
            'bg-surface-raised text-text-subtle border-border'
          )}>
            {SOURCE_LABELS[product.source] ?? product.source}
          </span>
          {product.sku && (
            <span className="font-mono text-xs text-text-subtle">SKU: {product.sku}</span>
          )}
        </div>
      </div>
      <div className="text-right shrink-0">
        {snapshot ? (
          <>
            <p className="text-sm font-mono font-semibold text-text-primary">
              {snapshot.currency === 'VND'
                ? `${snapshot.price?.toLocaleString('vi-VN')} ₫`
                : `$${snapshot.price?.toFixed(2)}`}
            </p>
            <p className="text-xs text-text-muted">
              {new Date(snapshot.scrapedAt).toLocaleDateString()}
            </p>
          </>
        ) : (
          <span className="text-xs text-text-subtle italic">no price</span>
        )}
      </div>
    </div>
  );
}

function CategoryProductPanel({ category, onClose }) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useProducts(
    { categoryId: category.id, pageSize: 20, page },
    { enabled: !!category.id }
  );
  const items = data?.items ?? [];
  const total = data?.totalCount ?? 0;

  return (
    <div className="bg-surface border border-border rounded-lg">
      <div className="flex items-center justify-between px-4 py-3 border-b border-border">
        <div>
          <h2 className="text-sm font-semibold text-text-primary">{category.name}</h2>
          <p className="text-xs text-text-muted font-mono">HS {category.hsCode} · {total} product{total !== 1 ? 's' : ''}</p>
        </div>
        <button
          onClick={onClose}
          className="p-1.5 rounded-md hover:bg-surface-raised text-text-muted hover:text-text-primary transition-colors"
          aria-label="Close category panel"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      <div className="px-4">
        {isLoading ? (
          <div className="py-4 space-y-3">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : items.length === 0 ? (
          <EmptyState
            icon={Package}
            title="No products yet"
            description="Products will appear here once scraped and assigned to this category."
            className="py-10"
          />
        ) : (
          <div>
            {items.map(p => <ProductRow key={p.id} product={p} />)}
          </div>
        )}
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between px-4 py-3 border-t border-border">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            className="text-xs text-text-muted hover:text-text-primary disabled:opacity-40 disabled:cursor-not-allowed"
          >
            ← Previous
          </button>
          <span className="text-xs text-text-muted">Page {page} of {data.totalPages}</span>
          <button
            onClick={() => setPage(p => Math.min(data.totalPages, p + 1))}
            disabled={page >= data.totalPages}
            className="text-xs text-text-muted hover:text-text-primary disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Next →
          </button>
        </div>
      )}
    </div>
  );
}

export default function CategoryPage() {
  const [selectedCategory, setSelectedCategory] = useState(null);
  const { data: categories, isLoading, isError } = useCategories();

  const totalProducts = (categories ?? []).reduce((s, c) => s + c.productCount, 0);
  const populated = (categories ?? []).filter(c => c.productCount > 0).length;

  const handleSelect = (cat) => {
    setSelectedCategory(prev => prev?.id === cat.id ? null : cat);
  };

  return (
    <PageContainer>
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-text-primary flex items-center gap-2">
            <Layers className="w-6 h-6 text-primary" aria-hidden="true" />
            Category Explorer
          </h1>
          <p className="text-text-muted text-sm mt-1">
            Browse products by HS code category · {totalProducts} tracked products across {populated} categories
          </p>
        </div>
      </div>

      {isError && (
        <div className="rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger mb-6">
          Failed to load categories. Make sure ProductService is running.
        </div>
      )}

      {isLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3">
          {Array.from({ length: 10 }).map((_, i) => (
            <Skeleton key={i} className="h-32 w-full rounded-lg" />
          ))}
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3 mb-6">
            {(categories ?? []).map(cat => (
              <CategoryCard
                key={cat.id}
                category={cat}
                isSelected={selectedCategory?.id === cat.id}
                onClick={() => handleSelect(cat)}
              />
            ))}
            {(categories ?? []).length === 0 && !isLoading && (
              <div className="col-span-full">
                <EmptyState
                  icon={Layers}
                  title="No categories found"
                  description="Categories are seeded automatically on ProductService startup."
                />
              </div>
            )}
          </div>

          {selectedCategory && (
            <CategoryProductPanel
              key={selectedCategory.id}
              category={selectedCategory}
              onClose={() => setSelectedCategory(null)}
            />
          )}

          {!selectedCategory && (categories ?? []).length > 0 && (
            <p className="text-center text-sm text-text-subtle mt-2">
              Select a category above to browse its products.
            </p>
          )}
        </>
      )}
    </PageContainer>
  );
}
