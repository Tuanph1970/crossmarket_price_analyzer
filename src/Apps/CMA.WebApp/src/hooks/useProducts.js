import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { productApi } from '@/api/productApi';

export const PRODUCT_KEYS = {
  all: ['products'],
  list: (params) => ['products', 'list', params],
  detail: (id) => ['products', 'detail', id],
  priceHistory: (id) => ['products', 'prices', id],
  quickLookup: (url) => ['products', 'quickLookup', url],
};

export function useProducts(params = {}, options = {}) {
  return useQuery({
    queryKey: PRODUCT_KEYS.list(params),
    queryFn: () => productApi.getProducts(params).then(r => r.data),
    ...options,
  });
}

export function useProduct(id, options = {}) {
  return useQuery({
    queryKey: PRODUCT_KEYS.detail(id),
    queryFn: () => productApi.getProduct(id).then(r => r.data),
    enabled: !!id,
    ...options,
  });
}

export function usePriceHistory(id, params = {}, options = {}) {
  return useQuery({
    queryKey: PRODUCT_KEYS.priceHistory(id),
    queryFn: () => productApi.getPriceHistory(id, params).then(r => r.data),
    enabled: !!id,
    ...options,
  });
}

export function useUpsertFromScrape(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data) => productApi.upsertFromScrape(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PRODUCT_KEYS.all });
    },
    ...options,
  });
}

export function useQuickLookup(options = {}) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data) => productApi.quickLookup(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: PRODUCT_KEYS.all });
    },
    ...options,
  });
}