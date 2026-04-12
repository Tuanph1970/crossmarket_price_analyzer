/**
 * P4-F04 / P4-F07: User profile page — view profile info + manage alert thresholds.
 * Also includes scheduled report subscriptions.
 */
import { useState } from 'react';
import { User, Mail, Plus, Trash2, Edit2, Bell, FileText } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '@/store/authStore';
import { alertThresholdApi } from '@/api/authApi';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardContent, CardHeader } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { EmptyState } from '@/components/shared/EmptyState';
import { ErrorBoundary } from '@/components/shared/ErrorBoundary';
import { Alert } from '@/components/shared/Alert';
import { Skeleton } from '@/components/ui/Skeleton';
import { toast } from 'sonner';

const CHANNEL_OPTIONS = [
  { value: 'email',    label: 'Email' },
  { value: 'telegram', label: 'Telegram' },
  { value: 'inapp',    label: 'In-App' },
];

const SCHEDULE_OPTIONS = [
  { value: 'daily',   label: 'Daily' },
  { value: 'weekly',  label: 'Weekly' },
  { value: 'monthly', label: 'Monthly' },
];

export default function ProfilePage() {
  const user = useAuthStore((s) => s.user);
  const [activeTab, setActiveTab] = useState('profile');

  return (
    <ErrorBoundary>
      <PageContainer>
        <div className="flex flex-wrap gap-2 mb-6">
          {['profile', 'thresholds', 'reports'].map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                activeTab === tab
                  ? 'bg-primary text-white'
                  : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
              }`}
            >
              {tab === 'profile'   ? 'Profile'     :
               tab === 'thresholds' ? 'Alert Thresholds' :
               'Scheduled Reports'}
            </button>
          ))}
        </div>

        {activeTab === 'profile'   && <ProfileTab user={user} />}
        {activeTab === 'thresholds' && <ThresholdsTab />}
        {activeTab === 'reports'    && <ReportsTab />}
      </PageContainer>
    </ErrorBoundary>
  );
}

// ── Profile tab ──────────────────────────────────────────────────────────────

function ProfileTab({ user }) {
  return (
    <div className="max-w-lg">
      <Card>
        <CardHeader className="flex items-center gap-3">
          <div className="w-12 h-12 rounded-full bg-primary text-white flex items-center justify-center text-lg font-bold">
            {user?.fullName?.split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase() ?? 'U'}
          </div>
          <div>
            <h2 className="text-lg font-bold text-text-primary">{user?.fullName ?? '—'}</h2>
            <p className="text-sm text-text-muted">{user?.email ?? '—'}</p>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-3 text-sm">
            <Mail className="w-4 h-4 text-text-muted" />
            <span className="text-text-secondary">{user?.email ?? '—'}</span>
          </div>
          <div className="flex items-center gap-3 text-sm">
            <User className="w-4 h-4 text-text-muted" />
            <span className="text-text-secondary">Member since {new Date().toLocaleDateString()}</span>
          </div>
          <p className="text-xs text-gray-400">
            Profile editing will be available in a future update.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

// ── Alert thresholds tab ────────────────────────────────────────────────────

function ThresholdsTab() {
  const { data: thresholds, isLoading, isError } = useQuery({
    queryKey: ['alert-thresholds'],
    queryFn: () => alertThresholdApi.getThresholds(),
  });

  const qc = useQueryClient();
  const deleteMut = useMutation({
    mutationFn: (id) => alertThresholdApi.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['alert-thresholds'] }); toast.success('Threshold deleted'); },
    onError:   () => toast.error('Failed to delete threshold'),
  });

  const channelLabel = (ch) => CHANNEL_OPTIONS.find((o) => o.value === ch)?.label ?? ch;
  const channelColor = (ch) =>
    ch === 'email' ? 'bg-blue-100 text-blue-700' :
    ch === 'telegram' ? 'bg-sky-100 text-sky-700' : 'bg-purple-100 text-purple-700';

  return (
    <div className="space-y-6">
      <ThresholdForm />

      {isLoading && <Skeleton className="h-20 w-full" />}
      {isError   && <Alert message="Failed to load thresholds." />}

      {!isLoading && (thresholds ?? []).length === 0 && (
        <EmptyState
          icon={Bell}
          title="No alert thresholds yet"
          description="Create one above to get notified when opportunities match your criteria."
        />
      )}

      {(thresholds ?? []).length > 0 && (
        <div className="space-y-3">
          {thresholds.map((t) => (
            <Card key={t.id} className="p-4">
              <CardContent className="p-0">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <ThresholdBadge label="Score ≥" value={t.minScore} />
                      {t.maxScore != null && (
                        <ThresholdBadge label="≤" value={t.maxScore} />
                      )}
                      {t.minMarginPct != null && (
                        <ThresholdBadge label="Margin ≥" value={`${t.minMarginPct}%`} />
                      )}
                      <span className={`text-xs px-2 py-0.5 rounded-full ${channelColor(t.channel)}`}>
                        {channelLabel(t.channel)}
                      </span>
                    </div>
                    <p className="text-xs text-gray-400 mt-1">
                      Target: {t.deliveryTarget}
                    </p>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => deleteMut.mutate(t.id)}
                    disabled={deleteMut.isPending}
                  >
                    <Trash2 className="w-4 h-4 text-danger" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

function ThresholdBadge({ label, value }) {
  return (
    <span className="text-sm font-semibold text-text-primary bg-gray-100 px-2 py-0.5 rounded">
      {label} {value}
    </span>
  );
}

function ThresholdForm() {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    name: '', minScore: '50', maxScore: '', minMarginPct: '', matchId: '', channel: 'email', deliveryTarget: '',
  });
  const [error, setError] = useState('');

  const createMut = useMutation({
    mutationFn: (payload) => alertThresholdApi.create(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['alert-thresholds'] });
      toast.success('Threshold created');
      setOpen(false);
      setForm({ name: '', minScore: '50', maxScore: '', minMarginPct: '', matchId: '', channel: 'email', deliveryTarget: '' });
    },
    onError: (err) => setError(err?.response?.data?.error ?? 'Failed to create threshold'),
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    setError('');
    createMut.mutate({
      name: form.name || undefined,
      minScore: Number(form.minScore),
      maxScore: form.maxScore ? Number(form.maxScore) : undefined,
      minMarginPct: form.minMarginPct ? Number(form.minMarginPct) : undefined,
      matchId: form.matchId ? (form.matchId.includes('-') ? form.matchId : undefined) : undefined,
      channel: form.channel,
      deliveryTarget: form.deliveryTarget,
    });
  };

  return (
    <div className="border rounded-xl p-4 bg-surface">
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-2 text-sm font-semibold text-primary hover:text-primary-600 transition-colors"
      >
        <Plus className="w-4 h-4" />
        {open ? 'Cancel' : 'New alert threshold'}
      </button>

      {open && (
        <form onSubmit={handleSubmit} className="mt-4 grid grid-cols-1 sm:grid-cols-2 gap-4">
          {error && <div className="sm:col-span-2"><Alert message={error} /></div>}

          <div>
            <label className="block text-xs font-medium text-text-muted mb-1">Min Score *</label>
            <Input
              type="number" min={0} max={100}
              value={form.minScore}
              onChange={(e) => setForm((f) => ({ ...f, minScore: e.target.value }))}
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-text-muted mb-1">Max Score</label>
            <Input
              type="number" min={0} max={100}
              placeholder="Optional"
              value={form.maxScore}
              onChange={(e) => setForm((f) => ({ ...f, maxScore: e.target.value }))}
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-text-muted mb-1">Min Margin %</label>
            <Input
              type="number" min={0} step="0.1"
              placeholder="e.g. 10"
              value={form.minMarginPct}
              onChange={(e) => setForm((f) => ({ ...f, minMarginPct: e.target.value }))}
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-text-muted mb-1">Channel *</label>
            <Select
              value={form.channel}
              onChange={(e) => setForm((f) => ({ ...f, channel: e.target.value }))}
              options={CHANNEL_OPTIONS}
            />
          </div>
          <div className="sm:col-span-2">
            <label className="block text-xs font-medium text-text-muted mb-1">
              {form.channel === 'email' ? 'Email address *' : 'Telegram chat ID *'}
            </label>
            <Input
              type={form.channel === 'email' ? 'email' : 'text'}
              placeholder={form.channel === 'email' ? 'you@example.com' : '123456789'}
              value={form.deliveryTarget}
              onChange={(e) => setForm((f) => ({ ...f, deliveryTarget: e.target.value }))}
            />
          </div>
          <div className="sm:col-span-2">
            <Button type="submit" loading={createMut.isPending} size="sm">
              Create threshold
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}

// ── Scheduled reports tab ───────────────────────────────────────────────────

function ReportsTab() {
  return (
    <div className="max-w-lg">
      <Card>
        <CardHeader className="flex items-center gap-2">
          <FileText className="w-5 h-5 text-primary" />
          <h2 className="font-semibold text-text-primary">Scheduled Reports</h2>
        </CardHeader>
        <CardContent>
          <EmptyState
            icon={FileText}
            title="No scheduled reports"
            description="Report scheduling will be available in a future update. Subscribe to daily, weekly, or monthly opportunity digests."
          />
        </CardContent>
      </Card>
    </div>
  );
}
