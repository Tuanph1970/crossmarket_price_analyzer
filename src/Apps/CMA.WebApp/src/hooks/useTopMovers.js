/**
 * P3-F03: Tracks top-20 movers — the set of opportunities whose
 * composite score changes most significantly over a rolling 1-hour window.
 * Returns the top-5 movers by absolute score delta.
 */
import { useState, useEffect, useRef } from 'react';
import { useRealtimeOpportunities } from './useRealtimeOpportunities';

const ONE_HOUR_MS = 60 * 60 * 1000;

export function useTopMovers() {
  const [movers, setMovers] = useState([]);
  // Map: matchId → { matchId, previousScore, newScore, delta, timestamp }
  const historyRef = useRef(new Map());

  const handleSnapshot = (snapshot) => {
    if (!snapshot?.topOpportunities) return;

    const now = Date.now();
    snapshot.topOpportunities.forEach((opp) => {
      const existing = historyRef.current.get(opp.matchId);
      const prevScore = existing?.newScore ?? opp.compositeScore;
      const delta = opp.compositeScore - prevScore;

      historyRef.current.set(opp.matchId, {
        matchId: opp.matchId,
        previousScore: prevScore,
        newScore: opp.compositeScore,
        delta,
        timestamp: now,
      });
    });

    // Expire entries older than 1 hour
    for (const [id, entry] of historyRef.current) {
      if (now - entry.timestamp > ONE_HOUR_MS) {
        historyRef.current.delete(id);
      }
    }

    // Sort by absolute delta descending, take top 5
    const top5 = [...historyRef.current.values()]
      .filter(m => Math.abs(m.delta) > 0.5) // minimum meaningful delta
      .sort((a, b) => Math.abs(b.delta) - Math.abs(a.delta))
      .slice(0, 5);

    setMovers(top5);
  };

  const { isConnected } = useRealtimeOpportunities({ onSnapshot: handleSnapshot });

  return { movers, isConnected };
}