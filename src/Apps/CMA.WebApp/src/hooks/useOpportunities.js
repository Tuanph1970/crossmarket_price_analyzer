import { useQuery } from '@tanstack/react-query';
import { keepPreviousData } from '@tanstack/react-query';
import { scoringApi } from '@/api/scoringApi';
import { matchingApi } from '@/api/matchingApi';
import api from '@/api/axiosClient';

export const OPPORTUNITY_KEYS = {
  all: ['opportunities'],
  list: (params) => ['opportunities', 'list', params],
};

export function useOpportunities(params = {}, options = {}) {
  return useQuery({
    queryKey: OPPORTUNITY_KEYS.list(params),
    queryFn: async () => {
      const [scoresRes, matchesRes] = await Promise.all([
        scoringApi.getScores(params).catch(() => ({ data: { items: [], totalCount: 0 } })),
        matchingApi.getMatches({ status: 'Confirmed', ...params }).catch(() => ({ data: { items: [] } })),
      ]);
      return { scores: scoresRes.data, matches: matchesRes.data };
    },
    placeholderData: keepPreviousData,
    ...options,
  });
}

export function useDashboardMetrics(params = {}, options = {}) {
  return useQuery({
    queryKey: ['dashboard', 'metrics', params],
    queryFn: async () => {
      const data = await scoringApi.getScores({ ...params, pageSize: 100 }).then(r => r.data);
      const items = data?.items ?? [];
      const total = items.length;
      const avgMargin = total > 0
        ? items.reduce((s, i) => s + (i.profitMarginPct ?? 0), 0) / total
        : 0;
      const avgDemand = total > 0
        ? items.reduce((s, i) => s + (i.demandScore ?? 0), 0) / total
        : 0;
      const highOpps = items.filter(i => (i.compositeScore ?? 0) >= 80).length;
      return { total, avgMargin: Math.round(avgMargin * 10) / 10, avgDemand: Math.round(avgDemand * 10) / 10, highOpps };
    },
    staleTime: 30_000,
    ...options,
  });
}
