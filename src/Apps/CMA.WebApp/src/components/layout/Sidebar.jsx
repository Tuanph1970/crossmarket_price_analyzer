import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, GitCompare, FolderOpen, LineChart, Search, Bell, Settings, ChevronLeft, Bookmark } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useUiStore } from '@/store/uiStore';

const BASE_NAV_ITEMS = [
  { to: '/',             icon: LayoutDashboard, labelKey: 'nav.dashboard' },
  { to: '/compare',      icon: GitCompare,      labelKey: 'nav.compare' },
  { to: '/categories',   icon: FolderOpen,      labelKey: 'nav.categories' },
  { to: '/history',      icon: LineChart,       labelKey: 'nav.history' },
  { to: '/quick-lookup', icon: Search,          labelKey: 'nav.quickLookup' },
  { to: '/watchlist',    icon: Bookmark,        labelKey: 'nav.watchlist' },
  { to: '/alerts',       icon: Bell,            labelKey: 'nav.alerts' },
  { to: '/settings',     icon: Settings,        labelKey: 'nav.settings' },
];

export default function Sidebar() {
  const { t } = useTranslation();
  const { sidebarOpen, setSidebarOpen, lastViewedProductId } = useUiStore();

  const navItems = BASE_NAV_ITEMS.map((item) =>
    item.to === '/history' && lastViewedProductId
      ? { ...item, to: `/history/${lastViewedProductId}` }
      : item
  );

  return (
    <aside className={cn(
      'sticky top-14 h-[calc(100vh-3.5rem)] border-r border-border bg-background',
      'transition-all duration-300 ease-in-out flex flex-col shrink-0',
      sidebarOpen ? 'w-52' : 'w-14'
    )}>
      {/* Brand */}
      <div className={cn(
        'flex items-center h-14 border-b border-border shrink-0',
        sidebarOpen ? 'px-4 gap-3' : 'justify-center'
      )}>
        <div className="w-7 h-7 rounded-lg bg-primary flex items-center justify-center shrink-0">
          <span className="font-display font-black text-xs text-background leading-none">CX</span>
        </div>
        {sidebarOpen && (
          <span className="font-display font-bold text-sm text-text-primary tracking-wide whitespace-nowrap">
            CrossMarket
          </span>
        )}
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto py-3 px-2 space-y-0.5">
        {navItems.map(({ to, icon: Icon, labelKey }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            title={!sidebarOpen ? t(labelKey) : undefined}
            className={({ isActive }) => cn(
              'relative flex items-center gap-3 py-2 rounded-lg text-sm font-medium transition-all duration-150',
              sidebarOpen ? 'px-3' : 'justify-center px-0',
              isActive
                ? 'bg-primary/10 text-primary'
                : 'text-text-muted hover:bg-surface-raised hover:text-text-primary'
            )}
          >
            {({ isActive }) => (
              <>
                {isActive && sidebarOpen && (
                  <span className="absolute left-0 inset-y-2 w-0.5 bg-primary rounded-r-full" />
                )}
                <Icon className="w-4 h-4 flex-shrink-0" />
                {sidebarOpen && <span className="truncate">{t(labelKey)}</span>}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      {/* Collapse toggle */}
      <div className="border-t border-border p-2 shrink-0">
        <button
          onClick={() => setSidebarOpen(!sidebarOpen)}
          className={cn(
            'w-full flex items-center gap-2 px-2 py-2 rounded-lg text-xs text-text-muted',
            'hover:bg-surface-raised hover:text-text-primary transition-all duration-150',
            !sidebarOpen && 'justify-center'
          )}
        >
          <ChevronLeft className={cn(
            'w-4 h-4 flex-shrink-0 transition-transform duration-300',
            !sidebarOpen && 'rotate-180'
          )} />
          {sidebarOpen && <span>Collapse</span>}
        </button>
      </div>
    </aside>
  );
}
