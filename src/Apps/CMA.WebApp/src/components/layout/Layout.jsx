import Header from './Header';
import Sidebar from './Sidebar';
import { useUiStore } from '@/store/uiStore';

export default function Layout({ children }) {
  const { sidebarOpen } = useUiStore();

  return (
    <div className="min-h-screen bg-background flex flex-col">
      <Header />
      <div className="flex flex-1">
        <Sidebar />
        <main className={`flex-1 transition-all duration-200 ${sidebarOpen ? 'ml-64' : 'ml-16'}`}>
          {children}
        </main>
      </div>
    </div>
  );
}
