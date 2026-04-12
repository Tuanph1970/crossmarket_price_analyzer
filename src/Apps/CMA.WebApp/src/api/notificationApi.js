import api from './axiosClient';

export const notificationApi = {
  /** GET /api/notifications/email/preview */
  previewEmail: (matchId) =>
    api.get('/notifications/email/preview', { params: { matchId } }).then((r) => r.data),

  /** POST /api/notifications/telegram/preview */
  previewTelegram: (matchId) =>
    api.post('/notifications/telegram/preview', { matchId }).then((r) => r.data),
};

/**
 * P4-F06: Triggers a PDF opportunity report download.
 * The backend returns a blob; we create an object URL and trigger a browser download.
 * @param {{ matchId?, period? }} options
 * @param {string} [filename] Optional download filename
 */
export function downloadOpportunityReport({ matchId, period = 'daily' } = {}, filename) {
  return api
    .post(
      '/notifications/reports/opportunity',
      { matchId, period },
      { responseType: 'blob' }
    )
    .then((response) => {
      const blob     = new Blob([response.data], { type: 'application/pdf' });
      const url      = URL.createObjectURL(blob);
      const link     = document.createElement('a');
      link.href      = url;
      link.download  = filename ?? `opportunity-report-${matchId ? matchId.slice(0, 8) : 'all'}-${Date.now()}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    });
}