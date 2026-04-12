/**
 * P4-F01: ProtectedRoute — redirects unauthenticated users to /login.
 * P4-F02: AuthGuard — wraps protected page content when authenticated.
 */
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/store/authStore';
import { Skeleton } from '@/components/ui/Skeleton';

/**
 * Route wrapper that redirects to /login if the user is not authenticated.
 * Use inside <Routes> just like a normal <Route element={...}>.
 */
export function ProtectedRoute({ children }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const location = useLocation();

  if (isAuthenticated === null) {
    // Store not yet rehydrated from localStorage — show brief loading
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Skeleton className="h-8 w-48" />
      </div>
    );
  }

  if (!isAuthenticated) {
    // Remember where the user was trying to go
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return children;
}

/**
 * AuthGuard — wraps page content that should only render for authenticated users.
 * Unlike ProtectedRoute this does NOT redirect; it shows the loading or nothing.
 * Use inside page components when you need conditional rendering.
 */
export function AuthGuard({ children }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  if (isAuthenticated === null) {
    return (
      <div className="p-6 space-y-4">
        <Skeleton className="h-6 w-32" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-3/4" />
      </div>
    );
  }

  if (!isAuthenticated) return null;
  return children;
}
