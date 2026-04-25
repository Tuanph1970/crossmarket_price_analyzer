import React, { memo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/utils';
import { Wifi } from 'lucide-react';
import { AddToWatchlistButton } from './AddToWatchlistButton';

const SCORE_CONFIG = {
  high:   { border: 'border-l-primary',  badge: 'bg-primary/15 text-primary border border-primary/20' },
  medium: { border: 'border-l-gold',     badge: 'bg-gold/15    text-gold    border border-gold/20' },
  low:    { border: 'border-l-danger',   badge: 'bg-danger/15  text-danger  border border-danger/20' },
};

const OpportunityCardComponent = ({ score, className = '', isLive = false, ...props }) => {
  const { t } = useTranslation();
  const pct = score.compositeScore ?? 0;
  const tier = pct >= 80 ? 'high' : pct >= 60 ? 'medium' : 'low';
  const { border, badge } = SCORE_CONFIG[tier];

  return (
    <Link
      to={`/compare/${score.matchId}`}
      data-testid="opportunity-card"
      className={cn(
        'group block bg-surface border border-border border-l-2 rounded-lg p-4',
        'hover:bg-surface-raised hover:border-border-primary transition-all duration-150',
        isLive && 'ring-1 ring-primary/30',
        border,
        className
      )}
      {...props}
    >
      <div className="flex justify-between items-start gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-mono text-xs text-text-muted">
              {t('opportunity.matchId', 'Match')} #{score.matchId?.slice(0, 8)}
            </span>
            {isLive && (
              <span className="flex items-center gap-1 text-xs text-success font-medium" aria-label="Live update">
                <Wifi className="w-3 h-3" aria-hidden="true" />
                Live
              </span>
            )}
          </div>
          <div className="text-xs text-text-muted mt-1 font-mono">
            <span className="text-text-subtle">Landed </span>
            {score.landedCostVnd?.toLocaleString('vi-VN')} ₫ ·{' '}
            <span className="text-text-subtle">Retail </span>
            {score.vietnamRetailVnd?.toLocaleString('vi-VN')} ₫
          </div>
        </div>
        <div data-testid="composite-score-badge"
          className={cn('shrink-0 px-3 py-1 rounded-full text-sm font-bold font-display', badge)}>
          {score.compositeScore?.toFixed(0) ?? 0}
        </div>
      </div>

      <div className="mt-3 flex justify-between items-center">
        <div className="grid grid-cols-5 gap-x-4 gap-y-1 text-xs flex-1">
          {[
            { label: t('opportunity.margin',      'Margin'),      value: `${score.profitMarginPct?.toFixed(1)}%` },
            { label: t('opportunity.demand',      'Demand'),      value: score.demandScore },
            { label: t('opportunity.competition', 'Competition'), value: score.competitionScore },
            { label: t('opportunity.stability',   'Stability'),   value: score.priceStabilityScore ?? '—' },
            { label: t('opportunity.confidence',  'Conf.'),       value: score.matchConfidenceScore },
          ].map(({ label, value }) => (
            <div key={label}>
              <span className="block text-text-subtle uppercase tracking-wider" style={{ fontSize: '0.625rem' }}>{label}</span>
              <span className="font-mono font-medium text-text-primary">{value}</span>
            </div>
          ))}
        </div>
        <div onClick={(e) => e.preventDefault()} className="ml-3 shrink-0">
          <AddToWatchlistButton
            matchId={score.matchId}
            usProductName={score.usProductName}
            vnProductName={score.vietnamProductName}
            size="sm"
          />
        </div>
      </div>
    </Link>
  );
};

export const OpportunityCard = memo(OpportunityCardComponent);
