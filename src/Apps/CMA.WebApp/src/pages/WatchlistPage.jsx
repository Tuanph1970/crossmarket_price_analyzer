/**
 * P4-F03: Watchlist page — displays the user's saved product matches (watchlist).
 * Supports add-to-watchlist from opportunity cards + remove items.
 */
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Trash2, ExternalLink, Bookmark, Star } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { watchlistApi } from '@/api/authApi';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { Skeleton } from '@/components/ui/Skeleton';
import { toast } from 'sonner';

const PAGE_SIZE = 20;

export default function WatchlistPage() {
  const [page, setPage] = useState(1);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['watchlist', page],
    queryFn: () => watchlistApi.getWatchlist(page, PAGE_SIZE),
  });

  const qc = useQueryClient();
  const removeMut = useMutation({
    mutationFn: (itemId) => watchlistApi.removeItem(itemId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['watchlist'] });
      toast.success('Item removed from watchlist');
    },
    onError: () => toast.error('Failed to remove item'),
  });

  const items = data?.items ?? [];
  const meta  = data?.meta;

  return (
    <ErrorBoundary>
      <PageContainer>
        <div className="flex flex-wrap justify-between items-center gap-4 mb-6">
          <h1 className="text-2xl font-bold text-text-primary">Watchlist</h1>
          {meta && (
            <span className="text-sm text-text-muted">
              {meta.totalCount} saved items
            </span>
          )}
        </div>

        {isLoading && (
          <div className="space-y-4">
            {[...Array(4)].map((_, i) => (
              <Skeleton key={i} className="h-20 w-full" />
            ))}
          </div>
        )}

        {isError && (
          <p className="text-danger text-sm">Failed to load watchlist. Please try again.</p>
        )}

        {!isLoading && !isError && items.length === 0 && (
          <EmptyState
            icon={Bookmark}
            title="Your watchlist is empty"
            description="Browse opportunities and click 'Add to watchlist' to start tracking products."
            action={() => window.location.href = '/'}
            actionLabel="Browse opportunities"
          />
        )}

        {!isLoading && !isError && items.length > 0 && (
          <>
            <div className="space-y-3">
              {items.map((item) => (
                <WatchlistCard
                  key={item.id}
                  item={item}
                  onRemove={() => removeMut.mutate(item.id)}
                  isRemoving={removeMut.isPending}
                />
              ))}
            </div>

            {meta && meta.totalPages > 1 && (
              <div className="flex justify-center gap-2 mt-6">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page === 1}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </Button>
                <span className="flex items-center px-3 text-sm text-text-muted">
                  {page} / {meta.totalPages}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= meta.totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            )}
          </>
        )}
      </PageContainer>
    </ErrorBoundary>
  );
}

function WatchlistCard({ item, onRemove, isRemoving }) {
  const scoreColor = (s) => {
    if (s >= 70) return 'text-success';
    if (s >= 40) return 'text-yellow-600';
    return 'text-danger';
  };

  return (
    <Card className="p-4">
      <CardContent className="p-0">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <Link
                to={`/compare/${item.matchId}`}
                className="font-semibold text-text-primary hover:text-primary transition-colors"
              >
                {item.usProductName ?? item.vnProductName ?? 'Unknown product'}
              </Link>
              {item.alertAboveScore != null && (
                <span className="text-xs bg-primary/10 text-primary px-2 py-0.5 rounded-full">
                  Alert ↑{item.alertAboveScore}
                </span>
              )}
              {item.alertBelowScore != null && (
                <span className="text-xs bg-danger/10 text-danger px-2 py-0.5 rounded-full">
                  Alert ↓{item.alertBelowScore}
                </span>
              )}
            </div>

            {item.vnProductName && item.usProductName && (
              <p className="text-sm text-text-muted mt-0.5">
                US: {item.usProductName} ↔ VN: {item.vnProductName}
              </p>
            )}

            <div className="flex items-center gap-3 mt-2 text-xs text-gray-400">
              {item.isMuted && (
                <span className="flex items-center gap-1">
                  <Star className="w-3 h-3" /> Muted
                </span>
              )}
              <span>Added {new Date(item.createdAt).toLocaleDateString()}</span>
            </div>
          </div>

          <div className="flex gap-1 shrink-0">
            <Button
              asChild
              variant="ghost"
              size="sm"
              title="View comparison"
            >
              <Link to={`/compare/${item.matchId}`}>
                <ExternalLink className="w-4 h-4" />
              </Link>
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={onRemove}
              disabled={isRemoving}
              title="Remove from watchlist"
            >
              <Trash2 className="w-4 h-4 text-danger" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
