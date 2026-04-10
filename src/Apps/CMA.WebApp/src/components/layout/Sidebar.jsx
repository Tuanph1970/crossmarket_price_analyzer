import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LayoutDashboard, GitCompare, FolderOpen, LineChart, Search, Bell, Settings, ChevronLeft } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useUiStore } from '@/store/uiStore';

const navItems = [
  { to: '/',          icon: LayoutDashboard, labelKey: 'nav.dashboard' },
  { to: '/compare',   icon: GitCompare,     labelKey: 'nav.compare' }, // matchId supplied via URL param
  { to: '/categories', icon: FolderOpen,     labelKey: 'nav.categories' },
  { to: '/history',   icon: LineChart,     labelKey: 'nav.history' },
  { to: '/quick-lookup', icon: Search,      labelKey: 'nav.quickLookup' },
  { to: '/alerts',    icon: Bell,          labelKey: 'nav.alerts' },
  { to: '/settings',   icon: Settings,       labelKey: 'nav.settings' },
];

export default function Sidebar() {
  const { t } = useTranslation();
  const { sidebarOpen, setSidebarOpen } = useUiStore();

  return (
    <aside className={cn(
      'sticky top-16 h-[calc(100vh-4rem)] border-r border-gray-200 bg-surface transition-all duration-200',
      sidebarOpen ? 'w-64' : 'w-16'
    )}>
      <nav className="flex flex-col h-full p-3 gap-1">
        {navItems.map(({ to, icon: Icon, labelKey }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) => cn(
              'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
              isActive ? 'bg-primary text-white' : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900',
              !sidebarOpen && 'justify-center px-2'
            )}
            title={!sidebarOpen ? t(labelKey) : undefined}
          >
            <Icon className="w-5 h-5 flex-shrink-0" />
            {sidebarOpen && <span>{t(labelKey)}</span>}
          </NavLink>
        ))}

        <div className="mt-auto">
          <button
            onClick={() => setSidebarOpen(!sidebarOpen)}
            className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm text-gray-400 hover:bg-gray-100 transition-colors"
          >
            <ChevronLeft className={cn('w-5 h-5 flex-shrink-0 transition-transform', !sidebarOpen && 'rotate-180')} />
            {sidebarOpen && <span>Collapse</span>}
          </button>
        </div>
      </nav>
    </aside>
  );
}
