import api from './axiosClient';

export const matchingApi = {
  getMatches: (params) => api.get('/matches', { params }),
  getMatch: (id) => api.get(`/matches/${id}`),
  createMatch: (data) => api.post('/matches', data),
  confirmMatch: (id, data) => api.post(`/matches/${id}/confirm`, data),
  rejectMatch: (id, data) => api.post(`/matches/${id}/reject`, data),
  batchReview: (data, userId) => api.post('/matches/batch-review', data, { params: { userId } }),
};
