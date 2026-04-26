import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Bell, BellOff, CheckCheck, Trash2 } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { alertApi } from '@/api/alertApi';
import { useAuthStore } from '@/store/authStore';

function AlertSkeleton() {
  return (
    <div className="space-y-3">
      {[1, 2, 3].map((i) => (
        <div key={i} className="p-4 border border-border rounded-xl">
          <Skeleton className="h-4 w-1/3 mb-2" />
          <Skeleton className="h-3 w-2/3" />
        </div>
      ))}
    </div>
  );
}

export default function AlertsPage() {
  const { t } = useTranslation();
  const [filter, setFilter] = useState('all');
  const user = useAuthStore((s) => s.user);
  const qc = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['alerts', user?.id],
    queryFn: () => alertApi.getAlerts({ userId: user?.id, page: 1, pageSize: 50 }).then(r => r.data),
    enabled: !!user?.id,
  });

  const alerts = data?.items ?? [];

  const markReadMut = useMutation({
    mutationFn: (id) => alertApi.markAsRead(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['alerts'] }),
  });

  const deleteMut = useMutation({
    mutationFn: (id) => alertApi.deleteAlert(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['alerts'] }),
  });

  const filtered = alerts.filter((a) => {
    if (filter === 'unread') return !a.isRead;
    if (filter === 'read') return a.isRead;
    return true;
  });

  return (
    <ErrorBoundary onReset={refetch}>
      <PageContainer>
        <div className="flex flex-wrap justify-between items-center gap-4 mb-6">
          <h1 className="text-2xl font-bold text-text-primary">{t('alerts.title', 'Alerts')}</h1>
          <div className="flex gap-2">
            {['all', 'unread', 'read'].map((f) => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                  filter === f
                    ? 'bg-primary text-white'
                    : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                }`}
              >
                {f === 'all' ? 'All' : f === 'unread' ? 'Unread' : 'Read'}
              </button>
            ))}
          </div>
        </div>

        {isLoading ? (
          <AlertSkeleton />
        ) : isError ? (
          <EmptyState
            icon={BellOff}
            title={t('common.error', 'An error occurred')}
            description="Could not load alerts. Please try again."
            action={refetch}
            actionLabel={t('common.retry', 'Retry')}
          />
        ) : filtered.length === 0 ? (
          <EmptyState
            icon={BellOff}
            title={t('alerts.noAlerts', 'No alerts at the moment.')}
            description={t(
              'alerts.noAlertsDesc',
              "You're all caught up. Alerts will appear here when opportunities meet your threshold."
            )}
          />
        ) : (
          <div className="space-y-3">
            {filtered.map((alert) => (
              <Card key={alert.id} className={`p-4 transition-opacity ${alert.isRead ? 'opacity-70' : ''}`}>
                <CardContent className="p-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-start gap-3 flex-1 min-w-0">
                      <Bell className={`w-5 h-5 shrink-0 mt-0.5 ${alert.isRead ? 'text-text-muted' : 'text-primary'}`} />
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-semibold text-text-primary text-sm">{alert.message}</span>
                          {!alert.isRead && (
                            <Badge variant="primary" className="text-xs">New</Badge>
                          )}
                        </div>
                        <div className="flex items-center gap-3 mt-1">
                          {alert.sentAt && (
                            <p className="text-xs text-gray-400">
                              {new Date(alert.sentAt).toLocaleString()}
                            </p>
                          )}
                          {alert.channel && (
                            <Badge variant="secondary" className="text-xs capitalize">{alert.channel}</Badge>
                          )}
                        </div>
                      </div>
                    </div>
                    <div className="flex gap-1 shrink-0">
                      {!alert.isRead && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => markReadMut.mutate(alert.id)}
                          disabled={markReadMut.isPending}
                          title={t('alerts.markRead', 'Mark as read')}
                        >
                          <CheckCheck className="w-4 h-4" />
                        </Button>
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => deleteMut.mutate(alert.id)}
                        disabled={deleteMut.isPending}
                        title={t('alerts.delete', 'Delete')}
                      >
                        <Trash2 className="w-4 h-4 text-danger" />
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </PageContainer>
    </ErrorBoundary>
  );
}
