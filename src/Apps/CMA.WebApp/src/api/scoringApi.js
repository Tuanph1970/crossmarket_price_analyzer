import api from './axiosClient';

export const scoringApi = {
  getScores: (params) => api.get('/scores', { params }),
  getScoreBreakdown: (matchId) => api.get(`/scores/${matchId}`),
  getScoringConfig: () => api.get('/scores/config'),
  updateWeights: (weights) => api.put('/scores/weights', weights),
  recalculate: () => api.post('/scores/recalculate'),

  /** P3-F06: POST /api/scores/export/excel — triggers a multi-sheet Excel workbook download */
  exportToExcel: (request = {}) => {
    return api.post('/scores/export/excel', request, {
      responseType: 'blob',
      headers: { Accept: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' },
    });
  },

  /** P3-B09: GET /api/scores/websocket/health — WebSocket server health + connection counts */
  getWebSocketHealth: () => api.get('/scores/websocket/health'),

  /** P3-B09: POST /api/scores/broadcast — manually trigger a top-20 snapshot broadcast */
  broadcastSnapshot: () => api.post('/scores/broadcast'),
};
