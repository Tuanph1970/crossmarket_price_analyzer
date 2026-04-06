/** Format a number as USD currency */
export function formatUSD(amount) {
  if (amount == null) return '—';
  return new Intl.NumberFormat('en-US', {
    style: 'currency', currency: 'USD', minimumFractionDigits: 2,
  }).format(amount);
}

/** Format a number as VND currency */
export function formatVND(amount) {
  if (amount == null) return '—';
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency', currency: 'VND', minimumFractionDigits: 0, maximumFractionDigits: 0,
  }).format(amount);
}

/** Convert USD to VND */
export function convertPrice(usd, rate) {
  return usd * rate;
}

/** Get score color based on value */
export function scoreColor(score) {
  if (score >= 81) return 'text-success';
  if (score >= 61) return 'text-primary';
  if (score >= 31) return 'text-warning';
  return 'text-danger';
}
