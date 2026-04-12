/**
 * P4-F02: Auth store — JWT token management + user identity.
 * Persisted in localStorage so sessions survive page refresh.
 * Tokens are written after every login / refresh so the axios
 * interceptor picks them up automatically.
 */
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { authApi } from '@/api/authApi';

export const useAuthStore = create(
  persist(
    (set, get) => ({
      // ── State ────────────────────────────────────────────────────────
      accessToken: null,
      refreshToken: null,
      user: null,          // { id, email, fullName }
      isAuthenticated: false,

      // ── Actions ───────────────────────────────────────────────────────

      /** Register a new account. Throws on 409 (email taken). */
      register: async ({ email, password, fullName }) => {
        const data = await authApi.register({ email, password, fullName });
        setTokens(data);
        return data;
      },

      /** Log in with email + password. Throws on 401. */
      login: async ({ email, password }) => {
        const data = await authApi.login({ email, password });
        setTokens(data);
        return data;
      },

      /** Exchange current refresh token for a new access token. */
      refreshTokens: async () => {
        const { refreshToken: rt } = get();
        if (!rt) throw new Error('No refresh token');
        const data = await authApi.refresh(rt);
        setTokens(data);
        return data;
      },

      /** Wipe all auth state (log out). */
      logout: () =>
        set({ accessToken: null, refreshToken: null, user: null, isAuthenticated: false }),

      // ── Internals ────────────────────────────────────────────────────

      setUser: (user) => set({ user }),

      _updateTokens: (accessToken, refreshToken, user) =>
        set({ accessToken, refreshToken, user, isAuthenticated: true }),
    }),
    {
      name: 'cma-auth-store',
      // Only persist the tokens + basic user info — NOT derived state
      partialize: (state) => ({
        accessToken:  state.accessToken,
        refreshToken: state.refreshToken,
        user:         state.user,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
);

// ── Helpers ──────────────────────────────────────────────────────────────────

function setTokens(data) {
  const { accessToken, refreshToken, user } = data;
  useAuthStore.getState()._updateTokens(accessToken, refreshToken, user);
}
