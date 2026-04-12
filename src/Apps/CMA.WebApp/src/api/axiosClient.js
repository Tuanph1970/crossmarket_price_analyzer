/**
 * P4-F02 / P4-F06: Axios client — attaches JWT Bearer token to every request,
 * attempts silent refresh on 401, and handles blob responses for PDF downloads.
 */
import axios from 'axios';

const STORE_KEY = 'cma-auth-store';

function getTokens() {
  try {
    const raw = localStorage.getItem(STORE_KEY);
    if (!raw) return { accessToken: null, refreshToken: null };
    const { state } = JSON.parse(raw);
    return {
      accessToken:  state?.accessToken  ?? null,
      refreshToken: state?.refreshToken ?? null,
    };
  } catch {
    return { accessToken: null, refreshToken: null };
  }
}

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const { accessToken } = getTokens();
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

let isRefreshing = false;
let refreshQueue = [];

/**
 * Process queued requests after token refresh completes.
 */
function processQueue(error, token = null) {
  refreshQueue.forEach((prom) => {
    if (error) prom.reject(error);
    else       prom.resolve(token);
  });
  refreshQueue = [];
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    // Attempt silent refresh on 401 — only once per "wave"
    if (
      error.response?.status === 401 &&
      !originalRequest._retry &&
      !originalRequest.url.includes('/auth/refresh')
    ) {
      if (isRefreshing) {
        // Queue this request until refresh completes
        return new Promise((resolve, reject) => {
          refreshQueue.push({ resolve, reject });
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return api(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const { refreshToken } = getTokens();
      if (!refreshToken) {
        isRefreshing = false;
        processQueue(new Error('No refresh token'));
        // Redirect to login only if not already on login page
        if (!window.location.pathname.includes('/login')) {
          window.location.href = '/login';
        }
        return Promise.reject(error);
      }

      try {
        const { data } = await axios.post(
          `${import.meta.env.VITE_API_BASE_URL || '/api'}/auth/refresh`,
          { refreshToken }
        );
        const newAccessToken = data.accessToken;

        // Persist new tokens — sync with authStore
        try {
          const raw    = localStorage.getItem(STORE_KEY);
          const parsed = JSON.parse(raw ?? '{}');
          localStorage.setItem(
            STORE_KEY,
            JSON.stringify({
              ...parsed,
              state: {
                ...parsed.state,
                accessToken:  newAccessToken,
                refreshToken: data.refreshToken ?? parsed.state?.refreshToken,
                isAuthenticated: true,
              },
            })
          );
        } catch { /* persist failed — ignore */ }

        processQueue(null, newAccessToken);
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        return api(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError);
        localStorage.removeItem(STORE_KEY);
        if (!window.location.pathname.includes('/login')) {
          window.location.href = '/login';
        }
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

export default api;
