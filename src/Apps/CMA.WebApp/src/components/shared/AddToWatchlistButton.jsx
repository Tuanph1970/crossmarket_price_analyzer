/**
 * P4-F05: AddToWatchlistButton — toggles a product match in/out of the user's watchlist.
 * Renders on ComparisonPage and OpportunityCard.
 * Shows filled bookmark when already saved; outline when not.
 */
import { useState } from 'react';
import { Bookmark, BookmarkCheck } from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { watchlistApi } from '@/api/authApi';
import { useAuthStore } from '@/store/authStore';
import { toast } from 'sonner';

const STYLES = {
  sm:  'px-2 py-1 text-xs',
  md:  'px-3 py-1.5 text-sm',
  lg:  'px-4 py-2 text-base',
};

/**
 * @param {{ matchId, usProductName?, vnProductName?, alertAboveScore?, alertBelowScore?, size?: 'sm'|'md'|'lg' }} props
 */
export function AddToWatchlistButton({
  matchId,
  usProductName,
  vnProductName,
  alertAboveScore,
  alertBelowScore,
  size = 'md',
}) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [saved, setSaved] = useState(false);
  const qc = useQueryClient();

  const addMut = useMutation({
    mutationFn: () =>
      watchlistApi.addItem({
        matchId,
        usProductName,
        vnProductName,
        alertAboveScore,
        alertBelowScore,
      }),
    onSuccess: () => {
      setSaved(true);
      qc.invalidateQueries({ queryKey: ['watchlist'] });
      toast.success('Added to watchlist');
    },
    onError: (err) =>
      toast.error(
        err?.response?.status === 401
          ? 'Please log in to save to your watchlist.'
          : 'Failed to add to watchlist.'
      ),
  });

  const removeMut = useMutation({
    mutationFn: (itemId) => watchlistApi.removeItem(itemId),
    onSuccess: () => {
      setSaved(false);
      qc.invalidateQueries({ queryKey: ['watchlist'] });
      toast.success('Removed from watchlist');
    },
    // Don't show error for already-removed items
    onError: () => toast.error('Failed to remove from watchlist.'),
  });

  if (!isAuthenticated) {
    return null; // Don't render anything for anonymous users
  }

  if (saved) {
    return (
      <button
        onClick={() => removeMut.mutate(matchId)}
        disabled={removeMut.isPending}
        className={`inline-flex items-center gap-1.5 rounded-lg bg-primary text-white font-medium transition-colors hover:bg-primary-600 disabled:opacity-50 ${STYLES[size]}`}
        title="Remove from watchlist"
      >
        <BookmarkCheck className="w-4 h-4" />
        Saved
      </button>
    );
  }

  return (
    <button
      onClick={() => addMut.mutate()}
      disabled={addMut.isPending}
      className={`inline-flex items-center gap-1.5 rounded-lg border border-primary text-primary font-medium transition-colors hover:bg-primary/5 disabled:opacity-50 ${STYLES[size]}`}
      title="Add to watchlist"
    >
      <Bookmark className="w-4 h-4" />
      Watch
    </button>
  );
}
