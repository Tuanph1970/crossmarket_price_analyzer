/**
 * P3-F02: Hook that merges real-time WebSocket updates into the
 * React Query scores cache without triggering unnecessary re-renders.
 * Calls useWebSocket internally; no-op when WebSocket is unavailable.
 */
import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { useWebSocket, BROADCAST_TYPES } from './useWebSocket';

/**
 * @param {number} pageSize - matches the query's pageSize so we can merge correctly
 */
export function useRealtimeOpportunities(pageSize = 20) {
  const queryClient = useQueryClient();

  // Merge a full snapshot into the cache
  const handleSnapshot = useCallback((snapshot) => {
    if (!snapshot?.topOpportunities) return;

    // Replace page 1 data with the live snapshot
    queryClient.setQueryData(['scores', { page: 1, pageSize }], (old) => {
      if (!old) {
        return {
          items: snapshot.topOpportunities,
          totalCount: snapshot.topOpportunities.length,
          page: 1,
          pageSize,
          totalPages: 1,
        };
      }
      return {
        ...old,
        items: snapshot.topOpportunities,
        totalCount: Math.max(old.totalCount, snapshot.topOpportunities.length),
      };
    });
  }, [queryClient, pageSize]);

  // Apply a delta update (score changed for one match)
  const handleDelta = useCallback((delta) => {
    // Update in-place in all cached score pages
    queryClient.setQueryData(['scores'], (old) => {
      if (!old?.items) return old;
      const idx = old.items.findIndex(s => s.matchId === delta.matchId);
      if (idx === -1) return old;
      const updated = [...old.items];
      updated[idx] = {
        ...updated[idx],
        compositeScore: delta.compositeScore,
        profitMarginPct: delta.profitMarginPct,
      };
      // Re-sort top pages if score changed significantly
      updated.sort((a, b) => (b.compositeScore ?? 0) - (a.compositeScore ?? 0));
      return { ...old, items: updated };
    });
  }, [queryClient]);

  // Handle export completion notification
  const handleExportDone = useCallback(({ recordCount, at }) => {
    // Optionally invalidate the cache to fetch the latest data
    queryClient.invalidateQueries({ queryKey: ['scores'] });
    console.info(`[WebSocket] Export completed: ${recordCount} records at ${at}`);
  }, [queryClient]);

  const { isConnected, lastMessage } = useWebSocket({
    onSnapshot: handleSnapshot,
    onDelta: handleDelta,
    onExportDone: handleExportDone,
  });

  return { isConnected, lastMessage };
}