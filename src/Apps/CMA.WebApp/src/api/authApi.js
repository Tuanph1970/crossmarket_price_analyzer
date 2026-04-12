import api from './axiosClient';

export const authApi = {
  /** POST /api/auth/register */
  register: (data) =>
    api.post('/auth/register', data).then((r) => r.data),

  /** POST /api/auth/login */
  login: (data) =>
    api.post('/auth/login', data).then((r) => r.data),

  /** POST /api/auth/refresh */
  refresh: (refreshToken) =>
    api.post('/auth/refresh', { refreshToken }).then((r) => r.data),
};

export const watchlistApi = {
  /** GET /api/watchlist */
  getWatchlist: (page = 1, pageSize = 20) =>
    api.get('/watchlist', { params: { page, pageSize } }).then((r) => r.data),

  /** POST /api/watchlist */
  addItem: (data) =>
    api.post('/watchlist', data).then((r) => r.data),

  /** DELETE /api/watchlist/{itemId} */
  removeItem: (itemId) =>
    api.delete(`/watchlist/${itemId}`),
};

export const alertThresholdApi = {
  /** GET /api/alerts/thresholds */
  getThresholds: () =>
    api.get('/alerts/thresholds').then((r) => r.data),

  /** POST /api/alerts/thresholds */
  create: (data) =>
    api.post('/alerts/thresholds', data).then((r) => r.data),

  /** PUT /api/alerts/thresholds/{thresholdId} */
  update: (thresholdId, data) =>
    api.put(`/alerts/thresholds/${thresholdId}`, data).then((r) => r.data),

  /** DELETE /api/alerts/thresholds/{thresholdId} */
  delete: (thresholdId) =>
    api.delete(`/alerts/thresholds/${thresholdId}`),
};