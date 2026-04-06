import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { SCORING_WEIGHTS } from '@/lib/constants';

export const useScoringStore = create(
  persist(
    (set) => ({
      weights: { ...SCORING_WEIGHTS },
      setWeight: (key, value) =>
        set((state) => ({ weights: { ...state.weights, [key]: value } })),
      resetWeights: () => set({ weights: { ...SCORING_WEIGHTS } }),
    }),
    { name: 'cma-scoring-store' }
  )
);
