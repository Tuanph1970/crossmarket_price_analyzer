export const SOURCE_LABELS = {
  Amazon: 'Amazon', Walmart: 'Walmart', Shopee: 'Shopee',
  Lazada: 'Lazada', Tiki: 'Tiki', CigarPage: 'CigarPage', Manual: 'Manual',
};

export const STATUS_COLORS = {
  Pending:   'bg-yellow-100 text-yellow-800',
  Confirmed: 'bg-green-100 text-green-800',
  Rejected:  'bg-red-100 text-red-800',
};

export const CONFIDENCE_COLORS = {
  High:   'bg-green-100 text-green-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Low:    'bg-red-100 text-red-800',
};

export const SCORING_WEIGHTS = {
  profitMargin: 40, demand: 25, competition: 20, stability: 10, confidence: 5,
};
