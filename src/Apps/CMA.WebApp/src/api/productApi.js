import api from './axiosClient';

const ANALYSIS_URLS_KEY = 'cma-analysis-urls';
const getStoredUrls = () => {
  try { return JSON.parse(localStorage.getItem(ANALYSIS_URLS_KEY) ?? '[]'); }
  catch { return []; }
};

export const productApi = {
  getProducts: (params) => api.get('/products', { params }),
  getProduct: (id) => api.get(`/products/${id}`),
  getPriceHistory: (id, params) => api.get(`/products/${id}/price-history`, { params }),
  upsertFromScrape: (data) => api.post('/products/upsert-from-scrape', data),
  quickLookup: (data) => api.post('/products/quick-lookup', data, { timeout: 180_000 }),
  scrapeListing: (data) => api.post('/products/scrape-listing', data, { timeout: 180_000 }),

  getAnalysisUrls: () => Promise.resolve({ data: getStoredUrls() }),
  saveAnalysisUrl: (url, type) => {
    const entry = { id: `${Date.now()}-${Math.random().toString(36).slice(2)}`, url, urlType: type };
    localStorage.setItem(ANALYSIS_URLS_KEY, JSON.stringify([...getStoredUrls(), entry]));
    return Promise.resolve({ data: entry });
  },
  deleteAnalysisUrl: (id) => {
    localStorage.setItem(ANALYSIS_URLS_KEY, JSON.stringify(getStoredUrls().filter((u) => u.id !== id)));
    return Promise.resolve({ data: null });
  },
  clearAnalysisUrls: () => {
    localStorage.removeItem(ANALYSIS_URLS_KEY);
    return Promise.resolve({ data: null });
  },
};
