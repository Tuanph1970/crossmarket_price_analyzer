import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export const useFilterStore = create(
  persist(
    (set) => ({
      category: null, minMargin: 0, demandLevel: null,
      competitionLevel: null, source: null, dateRange: { from: null, to: null },
      setCategory: (category) => set({ category }),
      setMinMargin: (minMargin) => set({ minMargin }),
      setDemandLevel: (demandLevel) => set({ demandLevel }),
      setCompetitionLevel: (competitionLevel) => set({ competitionLevel }),
      setSource: (source) => set({ source }),
      setDateRange: (dateRange) => set({ dateRange }),
      resetFilters: () =>
        set({ category: null, minMargin: 0, demandLevel: null,
          competitionLevel: null, source: null, dateRange: { from: null, to: null } }),
    }),
    { name: 'cma-filter-store' }
  )
);
