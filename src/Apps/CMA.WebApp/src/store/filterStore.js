import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export const useFilterStore = create(
  persist(
    (set) => ({
      category: null, minMargin: 0,
      demandLevel: null, competitionLevel: null,
      source: null, dateRange: { from: null, to: null },
      // P3-F03: additional filters
      minScore: 0, minStability: 0,

      setCategory: (category) => set({ category }),
      setMinMargin: (minMargin) => set({ minMargin }),
      setDemandLevel: (demandLevel) => set({ demandLevel }),
      setCompetitionLevel: (competitionLevel) => set({ competitionLevel }),
      setSource: (source) => set({ source }),
      setDateRange: (dateRange) => set({ dateRange }),
      setMinScore: (minScore) => set({ minScore }),
      setMinStability: (minStability) => set({ minStability }),
      resetFilters: () =>
        set({
          category: null, minMargin: 0, demandLevel: null,
          competitionLevel: null, source: null,
          dateRange: { from: null, to: null },
          minScore: 0, minStability: 0,
        }),
    }),
    { name: 'cma-filter-store' }
  )
);
