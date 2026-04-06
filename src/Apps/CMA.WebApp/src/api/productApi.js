import api from './axiosClient';

export const productApi = {
  getProducts: (params) => api.get('/products', { params }),
  getProduct: (id) => api.get(`/products/${id}`),
  getPriceHistory: (id) => api.get(`/products/${id}/prices`),
  quickLookup: (url) => api.post('/products/quick-lookup', { productUrl: url }),
  getCategories: () => api.get('/categories'),
  getOpportunities: (params) => api.get('/opportunities', { params }),
  getExchangeRates: () => api.get('/exchange-rates/current'),
};
