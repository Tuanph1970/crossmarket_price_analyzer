import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { keepPreviousData } from '@tanstack/react-query';
import { scoringApi } from '@/api/scoringApi';

export const SCORING_KEYS = {
  all: ['scores'],
  list: (params) => ['scores', 'list', params],
  detail: (id) => ['scores', 'detail', id],
  config: () => ['scores', 'config'],
};

export function useScores(params = {}, options = {}) {
  return useQuery({
    queryKey: SCORING_KEYS.list(params),
    queryFn: () => scoringApi.getScores(params).then(r => r.data),
    staleTime: 30_000,  // P3: 30s stale time — WebSocket pushes keep cache fresh
    placeholderData: keepPreviousData,
    ...options,
  });
}

export function useScoreBreakdown(matchId, options = {}) {
  return useQuery({
    queryKey: SCORING_KEYS.detail(matchId),
    queryFn: () => scoringApi.getScoreBreakdown(matchId).then(r => r.data),
    enabled: !!matchId,
    ...options,
  });
}

export function useScoringConfig(options = {}) {
  return useQuery({
    queryKey: SCORING_KEYS.config(),
    queryFn: () => scoringApi.getScoringConfig().then(r => r.data),
    staleTime: 5 * 60 * 1000,
    ...options,
  });
}

export function useUpdateWeights(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (weights) => scoringApi.updateWeights(weights),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: SCORING_KEYS.config() });
    },
    ...options,
  });
}

export function useRecalculateScores(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => scoringApi.recalculate(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: SCORING_KEYS.all });
    },
    ...options,
  });
}