import { useTranslation } from 'react-i18next';

const LOCALE_MAP = { USD: 'en-US', VND: 'vi-VN', EUR: 'de-DE', GBP: 'en-GB' };

export function PriceDisplay({ amount, currency = 'USD', className = '' }) {
  const { t } = useTranslation();
  const locale = LOCALE_MAP[currency] ?? 'en-US';
  const fmt = new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
    minimumFractionDigits: currency === 'VND' ? 0 : 2,
    maximumFractionDigits: currency === 'VND' ? 0 : 2,
  });

  return (
    <span className={className}>
      {fmt.format(amount ?? 0)}
    </span>
  );
}
