import Header from './Header';
import Sidebar from './Sidebar';
import { SkipToContent } from '@/components/shared/AccessibleNav';

export default function Layout({ children }) {
  return (
    <div className="min-h-screen bg-background flex flex-col">
      <SkipToContent />
      <Header />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar />
        <main
          id="main-content"
          className="flex-1 min-w-0 overflow-y-auto"
          tabIndex={-1}
        >
          {children}
        </main>
      </div>
    </div>
  );
}
