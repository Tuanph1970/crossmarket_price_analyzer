import { useQuery } from '@tanstack/react-query';
import { productApi } from '@/api/productApi';

export const CATEGORY_KEYS = {
  all: ['categories'],
};

export function useCategories(options = {}) {
  return useQuery({
    queryKey: CATEGORY_KEYS.all,
    queryFn: () => productApi.getCategories().then(r => r.data),
    staleTime: 5 * 60 * 1000,
    ...options,
  });
}
