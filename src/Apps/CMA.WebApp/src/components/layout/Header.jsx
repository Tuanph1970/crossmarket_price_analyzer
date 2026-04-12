import { useTranslation } from 'react-i18next';
import { Menu, Bell, Search, Globe } from 'lucide-react';
import { useUiStore } from '@/store/uiStore';
import { Input } from '@/components/ui/Input';
import i18n from '@/i18n';

const LOCALES = [
  { code: 'en', label: 'EN', flag: '🇺🇸' },
  { code: 'vi', label: 'VI', flag: '🇻🇳' },
];

export default function Header() {
  const { t } = useTranslation();
  const { sidebarOpen, setSidebarOpen } = useUiStore();
  const currentLang = LOCALES.find((l) => l.code === i18n.language) ?? LOCALES[0];

  const handleLangChange = (code) => {
    i18n.changeLanguage(code);
  };

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-4 border-b border-gray-200 bg-surface px-6">
      <button
        onClick={() => setSidebarOpen(!sidebarOpen)}
        className="p-2 rounded-lg hover:bg-gray-100 transition-colors"
        aria-label="Toggle sidebar"
      >
        <Menu className="w-5 h-5 text-gray-600" />
      </button>

      <div className="flex-1 flex items-center gap-4">
        <div className="relative max-w-md w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <Input placeholder="Search products..." className="pl-9" />
        </div>
      </div>

      <div className="flex items-center gap-3">
        {/* Language Switcher */}
        <div className="relative flex items-center gap-1 border border-gray-200 rounded-lg px-2 py-1">
          <Globe className="w-4 h-4 text-gray-500" />
          {LOCALES.map((locale) => (
            <button
              key={locale.code}
              onClick={() => handleLangChange(locale.code)}
              title={locale.label}
              className={`px-2 py-0.5 text-xs font-medium rounded transition-colors ${
                locale.code === currentLang.code
                  ? 'bg-primary text-white'
                  : 'text-gray-500 hover:bg-gray-100'
              }`}
            >
              {locale.flag} {locale.label}
            </button>
          ))}
        </div>

        <button className="relative p-2 rounded-lg hover:bg-gray-100 transition-colors" aria-label="Notifications">
          <Bell className="w-5 h-5 text-gray-600" />
          <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-danger rounded-full" />
        </button>
        <button className="flex items-center gap-2 p-2 rounded-lg hover:bg-gray-100 transition-colors">
          <div className="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-sm font-medium">U</div>
        </button>
      </div>
    </header>
  );
}
