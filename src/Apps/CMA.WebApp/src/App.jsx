import { Suspense, lazy } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'sonner';
import Layout from '@/components/layout/Layout';
import { Skeleton } from '@/components/ui/Skeleton';
import { ProtectedRoute } from '@/components/shared/ProtectedRoute';

const DashboardPage     = lazy(() => import('@/pages/DashboardPage'));
const ComparisonPage    = lazy(() => import('@/pages/ComparisonPage'));
const CategoryPage      = lazy(() => import('@/pages/CategoryPage'));
const PriceHistoryPage  = lazy(() => import('@/pages/PriceHistoryPage'));
const QuickLookupPage   = lazy(() => import('@/pages/QuickLookupPage'));
const AlertsPage        = lazy(() => import('@/pages/AlertsPage'));
const SettingsPage      = lazy(() => import('@/pages/SettingsPage'));
const LoginPage         = lazy(() => import('@/pages/LoginPage'));
const RegisterPage      = lazy(() => import('@/pages/RegisterPage'));
const WatchlistPage     = lazy(() => import('@/pages/WatchlistPage'));
const ProfilePage       = lazy(() => import('@/pages/ProfilePage'));

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
            {/* Public — no auth needed */}
            <Route path="/login"    element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />

            {/* Protected — auth required */}
            <Route path="/" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
            <Route path="/compare/:matchId"  element={<ProtectedRoute><ComparisonPage /></ProtectedRoute>} />
            <Route path="/categories"         element={<ProtectedRoute><CategoryPage /></ProtectedRoute>} />
            <Route path="/categories/:categoryId" element={<ProtectedRoute><CategoryPage /></ProtectedRoute>} />
            <Route path="/history/:productId" element={<ProtectedRoute><PriceHistoryPage /></ProtectedRoute>} />
            <Route path="/quick-lookup"       element={<ProtectedRoute><QuickLookupPage /></ProtectedRoute>} />
            <Route path="/alerts"            element={<ProtectedRoute><AlertsPage /></ProtectedRoute>} />
            <Route path="/settings"         element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
            <Route path="/watchlist"         element={<ProtectedRoute><WatchlistPage /></ProtectedRoute>} />
            <Route path="/profile"           element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />

            {/* Catch-all → dashboard */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
      </Layout>
      <Toaster position="top-right" richColors />
    </BrowserRouter>
  );
}
