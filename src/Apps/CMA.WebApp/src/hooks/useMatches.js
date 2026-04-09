import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { matchingApi } from '@/api/matchingApi';

export const MATCH_KEYS = {
  all: ['matches'],
  list: (params) => ['matches', 'list', params],
  detail: (id) => ['matches', 'detail', id],
};

export function useMatches(params = {}, options = {}) {
  return useQuery({
    queryKey: MATCH_KEYS.list(params),
    queryFn: () => matchingApi.getMatches(params).then(r => r.data),
    ...options,
  });
}

export function useMatch(id, options = {}) {
  return useQuery({
    queryKey: MATCH_KEYS.detail(id),
    queryFn: () => matchingApi.getMatch(id).then(r => r.data),
    enabled: !!id,
    ...options,
  });
}

export function useConfirmMatch(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, userId, notes }) => matchingApi.confirmMatch(id, { UserId: userId, Notes: notes }),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: MATCH_KEYS.detail(vars.id) });
      qc.invalidateQueries({ queryKey: MATCH_KEYS.all });
    },
    ...options,
  });
}

export function useRejectMatch(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, userId, notes }) => matchingApi.rejectMatch(id, { UserId: userId, Notes: notes }),
    onSuccess: (_, vars) => {
      qc.invalidateQueries({ queryKey: MATCH_KEYS.detail(vars.id) });
      qc.invalidateQueries({ queryKey: MATCH_KEYS.all });
    },
    ...options,
  });
}

export function useBatchReview(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ items, userId }) => matchingApi.batchReview({ Items: items }, userId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: MATCH_KEYS.all });
    },
    ...options,
  });
}