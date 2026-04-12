import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Bell, BellOff, CheckCheck, Trash2 } from 'lucide-react';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';

export default function AlertsPage() {
  const { t } = useTranslation();
  const [alerts, setAlerts] = useState([]);
  const [filter, setFilter] = useState('all'); // 'all' | 'unread' | 'read'

  const filtered = alerts.filter((a) => {
    if (filter === 'unread') return !a.read;
    if (filter === 'read') return a.read;
    return true;
  });

  const handleMarkRead = (id) => {
    setAlerts((prev) => prev.map((a) => (a.id === id ? { ...a, read: true } : a)));
  };

  const handleMarkUnread = (id) => {
    setAlerts((prev) => prev.map((a) => (a.id === id ? { ...a, read: false } : a)));
  };

  const handleDelete = (id) => {
    setAlerts((prev) => prev.filter((a) => a.id !== id));
  };

  return (
    <ErrorBoundary>
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

        {filtered.length === 0 ? (
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
              <Card key={alert.id} className="p-4">
                <CardContent className="p-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-start gap-3 flex-1 min-w-0">
                      <Bell className="w-5 h-5 text-primary shrink-0 mt-0.5" />
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-semibold text-text-primary">{alert.title}</span>
                          {!alert.read && (
                            <Badge variant="primary" className="text-xs">New</Badge>
                          )}
                        </div>
                        <p className="text-sm text-text-muted mt-0.5">{alert.message}</p>
                        {alert.createdAt && (
                          <p className="text-xs text-gray-400 mt-1">
                            {new Date(alert.createdAt).toLocaleString()}
                          </p>
                        )}
                      </div>
                    </div>
                    <div className="flex gap-1 shrink-0">
                      {!alert.read ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleMarkRead(alert.id)}
                          title={t('alerts.markRead', 'Mark as read')}
                        >
                          <CheckCheck className="w-4 h-4" />
                        </Button>
                      ) : (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleMarkUnread(alert.id)}
                          title={t('alerts.markUnread', 'Mark as unread')}
                        >
                          <Bell className="w-4 h-4" />
                        </Button>
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDelete(alert.id)}
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
