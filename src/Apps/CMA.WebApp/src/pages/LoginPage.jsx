/**
 * P4-F01: Login page — email + password form with JWT authentication.
 * On success stores tokens in authStore and redirects to intended destination.
 * Shows a generic "Invalid credentials" message on 401 without leaking info.
 */
import { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Eye, EyeOff, LogIn } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useAuthStore } from '@/store/authStore';
import PageContainer from '@/components/layout/PageContainer';
import { Card, CardContent, CardHeader } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Alert } from '@/components/shared/Alert';

const schema = z.object({
  email:    z.string().email('Valid email required'),
  password: z.string().min(6, 'At least 6 characters'),
});

export default function LoginPage() {
  const navigate    = useNavigate();
  const location    = useLocation();
  const login       = useAuthStore((s) => s.login);
  const [showPw, setShowPw] = useState(false);
  const [error, setError]   = useState('');

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async ({ email, password }) => {
    setError('');
    try {
      await login({ email, password });
      const from = location.state?.from ?? '/';
      navigate(from, { replace: true });
    } catch (err) {
      // 401 → generic message; 409/500 → show server message
      setError(
        err?.response?.status === 401
          ? 'Invalid email or password.'
          : err?.response?.data?.error ?? 'Login failed. Please try again.'
      );
    }
  };

  return (
    <PageContainer className="min-h-screen flex items-center justify-center bg-gray-50/50">
      <Card className="w-full max-w-md">
        <CardHeader>
          <h1 className="text-2xl font-bold text-center text-text-primary">Sign in</h1>
          <p className="text-center text-sm text-text-muted mt-1">
            New to CrossMarket?{' '}
            <Link to="/register" className="text-primary font-medium hover:underline">
              Create an account
            </Link>
          </p>
        </CardHeader>

        <CardContent>
          {error && <Alert message={error} type="error" className="mb-4" />}

          <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-text-primary mb-1">Email</label>
              <Input
                type="email"
                placeholder="you@example.com"
                autoComplete="email"
                {...register('email')}
                error={errors.email?.message}
              />
              {errors.email && (
                <p className="mt-1 text-sm text-danger">{errors.email.message}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-text-primary mb-1">Password</label>
              <div className="relative">
                <Input
                  type={showPw ? 'text' : 'password'}
                  placeholder="••••••••"
                  autoComplete="current-password"
                  {...register('password')}
                  error={errors.password?.message}
                />
                <button
                  type="button"
                  onClick={() => setShowPw(!showPw)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showPw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
              {errors.password && (
                <p className="mt-1 text-sm text-danger">{errors.password.message}</p>
              )}
            </div>

            <Button type="submit" className="w-full" loading={isSubmitting}>
              <LogIn className="w-4 h-4 mr-2" />
              Sign in
            </Button>
          </form>
        </CardContent>
      </Card>
    </PageContainer>
  );
}
