import { useTranslation } from 'react-i18next';
import { Menu, Bell, Search, LogOut, User } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useUiStore } from '@/store/uiStore';
import { useAuthStore } from '@/store/authStore';
import i18n from '@/i18n';

const LOCALES = [
  { code: 'en', label: 'EN', flag: '🇺🇸' },
  { code: 'vi', label: 'VI', flag: '🇻🇳' },
];

export default function Header() {
  const { t } = useTranslation();
  const { sidebarOpen, setSidebarOpen } = useUiStore();
  const { user, isAuthenticated, logout } = useAuthStore();
  const navigate = useNavigate();
  const currentLang = LOCALES.find((l) => l.code === i18n.language) ?? LOCALES[0];

  const handleLangChange = (code) => i18n.changeLanguage(code);

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const initials = user?.fullName
    ? user.fullName.split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase()
    : 'U';

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-border bg-background/95 backdrop-blur-sm px-4">
      <button
        onClick={() => setSidebarOpen(!sidebarOpen)}
        className="p-2 rounded-lg hover:bg-surface-raised transition-colors text-text-muted hover:text-text-primary"
        aria-label="Toggle sidebar"
      >
        <Menu className="w-4 h-4" />
      </button>

      <div className="flex-1 flex items-center">
        <div className="relative max-w-sm w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-subtle" />
          <input
            placeholder={t('header.search', 'Search products...')}
            className="h-8 w-full rounded-lg border border-border bg-surface pl-9 pr-3 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors"
          />
        </div>
      </div>

      <div className="flex items-center gap-2">
        {/* Language Switcher */}
        <div className="flex items-center gap-0.5 rounded-lg border border-border bg-surface p-1">
          {LOCALES.map((locale) => (
            <button
              key={locale.code}
              onClick={() => handleLangChange(locale.code)}
              className={`px-2 py-0.5 text-xs font-medium rounded-md transition-all ${
                locale.code === currentLang.code
                  ? 'bg-primary text-background font-semibold'
                  : 'text-text-muted hover:text-text-primary'
              }`}
            >
              {locale.flag} {locale.label}
            </button>
          ))}
        </div>

        <button
          onClick={() => navigate('/alerts')}
          className="relative p-2 rounded-lg hover:bg-surface-raised transition-colors text-text-muted hover:text-text-primary"
          aria-label="Notifications"
        >
          <Bell className="w-4 h-4" />
          <span className="absolute top-1.5 right-1.5 w-1.5 h-1.5 bg-danger rounded-full ring-1 ring-background" />
        </button>

        {isAuthenticated ? (
          <>
            <button
              onClick={() => navigate('/profile')}
              className="flex items-center gap-2 rounded-lg hover:bg-surface-raised px-2 py-1.5 transition-colors"
              title={t('header.profile', 'Profile')}
            >
              <div className="w-7 h-7 rounded-full bg-primary/15 border border-primary/25 flex items-center justify-center text-xs font-bold text-primary font-mono shrink-0">
                {initials}
              </div>
              {user?.fullName && (
                <span className="text-sm font-medium text-text-primary hidden md:block">
                  {user.fullName}
                </span>
              )}
            </button>
            <button
              onClick={handleLogout}
              className="p-2 rounded-lg hover:bg-surface-raised transition-colors text-text-muted hover:text-danger"
              title={t('header.logout', 'Log out')}
            >
              <LogOut className="w-4 h-4" />
            </button>
          </>
        ) : (
          <button
            onClick={() => navigate('/login')}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-border hover:bg-surface-raised transition-colors text-text-muted hover:text-text-primary text-sm"
          >
            <User className="w-3.5 h-3.5" />
            Sign in
          </button>
        )}
      </div>
    </header>
  );
}
