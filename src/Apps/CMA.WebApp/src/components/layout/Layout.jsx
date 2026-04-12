import Header from './Header';
import Sidebar from './Sidebar';
import { SkipToContent } from '@/components/shared/AccessibleNav';
import { useUiStore } from '@/store/uiStore';

export default function Layout({ children }) {
  const { sidebarOpen } = useUiStore();

  return (
    <div className="min-h-screen bg-background flex flex-col">
      <SkipToContent />
      <Header />
      <div className="flex flex-1">
        <Sidebar />
        <main
          id="main-content"
          className={`flex-1 transition-all duration-200 ${sidebarOpen ? 'ml-64' : 'ml-16'}`}
          tabIndex={-1}
        >
          {children}
        </main>
      </div>
    </div>
  );
}