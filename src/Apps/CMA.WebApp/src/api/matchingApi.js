import api from './axiosClient';

export const matchingApi = {
  getMatches: (params) => api.get('/matches', { params }),
  getMatch: (id) => api.get(`/matches/${id}`),
  confirmMatch: (id) => api.post(`/matches/${id}/confirm`),
  rejectMatch: (id) => api.post(`/matches/${id}/reject`),
  batchReview: (ids, action) => api.post('/matches/batch-review', { ids, action }),
};
