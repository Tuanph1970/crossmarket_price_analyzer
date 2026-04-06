import { Suspense, lazy } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Toaster } from 'sonner';
import Layout from '@/components/layout/Layout';
import { Skeleton } from '@/components/ui/Skeleton';

const DashboardPage    = lazy(() => import('@/pages/DashboardPage'));
const ComparisonPage  = lazy(() => import('@/pages/ComparisonPage'));
const CategoryPage    = lazy(() => import('@/pages/CategoryPage'));
const PriceHistoryPage = lazy(() => import('@/pages/PriceHistoryPage'));
const QuickLookupPage = lazy(() => import('@/pages/QuickLookupPage'));
const AlertsPage      = lazy(() => import('@/pages/AlertsPage'));
const SettingsPage    = lazy(() => import('@/pages/SettingsPage'));

function PageFallback() {
  return (
    <div className="space-y-4 p-6">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-3/4" />
      <div className="grid gap-4 md:grid-cols-3 mt-4">
        <Skeleton className="h-32" />
        <Skeleton className="h-32" />
        <Skeleton className="h-32" />
      </div>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Layout>
        <Suspense fallback={<PageFallback />}>
          <Routes>
            <Route path="/"                        element={<DashboardPage />} />
            <Route path="/compare/:matchId"         element={<ComparisonPage />} />
            <Route path="/categories"               element={<CategoryPage />} />
            <Route path="/categories/:categoryId"  element={<CategoryPage />} />
            <Route path="/history/:productId"      element={<PriceHistoryPage />} />
            <Route path="/quick-lookup"            element={<QuickLookupPage />} />
            <Route path="/alerts"                  element={<AlertsPage />} />
            <Route path="/settings"                element={<SettingsPage />} />
          </Routes>
        </Suspense>
      </Layout>
      <Toaster position="top-right" richColors />
    </BrowserRouter>
  );
}
