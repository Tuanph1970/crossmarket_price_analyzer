import api from './axiosClient';

export const scoringApi = {
  getScores: (params) => api.get('/scores', { params }),
  getScoreBreakdown: (matchId) => api.get(`/scores/${matchId}`),
  getScoringConfig: () => api.get('/scores/config'),
  updateWeights: (weights) => api.put('/scores/weights', weights),
  recalculate: () => api.post('/scores/recalculate'),
};
