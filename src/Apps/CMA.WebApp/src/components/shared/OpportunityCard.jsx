import React, { memo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { Wifi, WifiOff } from 'lucide-react';
import { AddToWatchlistButton } from './AddToWatchlistButton';

const SCORE_COLORS = {
  high:   'bg-green-100 text-green-800',
  medium: 'bg-yellow-100 text-yellow-800',
  low:    'bg-red-100 text-red-800',
};

const OpportunityCardComponent = ({ score, className = '', isLive = false, ...props }) => {
  const { t } = useTranslation();
  const pct = score.compositeScore ?? 0;
  const scoreColor = pct >= 80 ? 'high' : pct >= 60 ? 'medium' : 'low';

  return (
    <Link
      to={`/compare/${score.matchId}`}
      data-testid="opportunity-card"
      className={cn(
        'bg-bg-secondary border border-border rounded-lg p-4 hover:shadow-md transition-shadow cursor-pointer',
        isLive && 'ring-2 ring-green-400 ring-offset-1',
        className
      )}
      {...props}
    >
      <div className="flex justify-between items-start">
        <div>
          <div className="flex items-center gap-2">
            <div className="font-semibold text-text-primary">
              {t('opportunity.matchId', 'Match')} #{score.matchId?.slice(0, 8)}
            </div>
            {/* P3-F01: Live badge when score is from a WebSocket push */}
            {isLive && (
              <span className="flex items-center gap-1 text-xs text-green-600 font-medium" aria-label="Live update">
                <Wifi className="w-3 h-3" aria-hidden="true" />
                Live
              </span>
            )}
          </div>
          <div className="text-sm text-text-muted">
            {t('opportunity.landed', 'Landed')}: {score.landedCostVnd?.toLocaleString('vi-VN')} VND ·{' '}
            {t('opportunity.retail', 'Retail')}: {score.vietnamRetailVnd?.toLocaleString('vi-VN')} VND
          </div>
        </div>
        <div
          data-testid="composite-score-badge"
          className={cn('px-3 py-1 rounded-full text-sm font-bold', SCORE_COLORS[scoreColor])}
        >
          {score.compositeScore?.toFixed(0) ?? 0}
        </div>
      </div>
      {/* P4-F05: Add to watchlist button */}
      <div className="mt-2 flex justify-end" onClick={(e) => e.preventDefault()}>
        <AddToWatchlistButton
          matchId={score.matchId}
          usProductName={score.usProductName}
          vnProductName={score.vietnamProductName}
          size="sm"
        />
      </div>

      <div className="mt-2 grid grid-cols-5 gap-2 text-xs">
        <div>
          <span className="text-text-muted">{t('opportunity.margin', 'Margin')}:</span>{' '}
          {score.profitMarginPct?.toFixed(1)}%
        </div>
        <div>
          <span className="text-text-muted">{t('opportunity.demand', 'Demand')}:</span>{' '}
          {score.demandScore}
        </div>
        <div>
          <span className="text-text-muted">{t('opportunity.competition', 'Competition')}:</span>{' '}
          {score.competitionScore}
        </div>
        <div>
          <span className="text-text-muted">{t('opportunity.stability', 'Stability')}:</span>{' '}
          {score.priceStabilityScore ?? '—'}
        </div>
        <div>
          <span className="text-text-muted">{t('opportunity.confidence', 'Confidence')}:</span>{' '}
          {score.matchConfidenceScore}
        </div>
      </div>
    </Link>
  );
};

export const OpportunityCard = memo(OpportunityCardComponent);
