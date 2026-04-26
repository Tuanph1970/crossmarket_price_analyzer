import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export const useUiStore = create(
  persist(
    (set) => ({
      sidebarOpen: true,
      theme: 'light',
      isLoading: false,
      lastViewedProductId: null,
      setSidebarOpen: (open) => set({ sidebarOpen: open }),
      setTheme: (theme) => set({ theme }),
      setLoading: (loading) => set({ isLoading: loading }),
      setLastViewedProductId: (id) => set({ lastViewedProductId: id }),
    }),
    { name: 'cma-ui-store' }
  )
);
