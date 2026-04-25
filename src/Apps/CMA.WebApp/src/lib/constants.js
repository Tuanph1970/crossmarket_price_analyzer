export const SOURCE_LABELS = {
  Amazon: 'Amazon', Walmart: 'Walmart', Shopee: 'Shopee',
  Lazada: 'Lazada', Tiki: 'Tiki', CigarPage: 'CigarPage', Manual: 'Manual',
};

export const STATUS_COLORS = {
  Pending:   'bg-warning/15 text-warning   border border-warning/20',
  Confirmed: 'bg-success/15 text-success   border border-success/20',
  Rejected:  'bg-danger/15  text-danger    border border-danger/20',
};

export const CONFIDENCE_COLORS = {
  High:   'bg-primary/15 text-primary border border-primary/20',
  Medium: 'bg-gold/15    text-gold    border border-gold/20',
  Low:    'bg-danger/15  text-danger  border border-danger/20',
};

export const SCORING_WEIGHTS = {
  profitMargin: 40, demand: 25, competition: 20, stability: 10, confidence: 5,
};
