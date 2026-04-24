import api from './axiosClient';

export const productApi = {
  getProducts: (params) => api.get('/products', { params }),
  getProduct: (id) => api.get(`/products/${id}`),
  getPriceHistory: (id, params) => api.get(`/products/${id}/price-history`, { params }),
  upsertFromScrape: (data) => api.post('/products/upsert-from-scrape', data),
  quickLookup: (data) => api.post('/products/quick-lookup', data),
  scrapeListing: (data) => api.post('/products/scrape-listing', data),
};
