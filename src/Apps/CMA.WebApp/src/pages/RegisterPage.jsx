/**
 * P4-F01: Register page — creates a new account with full-name + email + password.
 * On success auto-logs-in and redirects to dashboard.
 */
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Eye, EyeOff, UserPlus } from 'lucide-react';
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
  fullName: z.string().min(2, 'At least 2 characters'),
  email:    z.string().email('Valid email required'),
  password: z.string().min(6, 'At least 6 characters'),
});

export default function RegisterPage() {
  const navigate  = useNavigate();
  const register = useAuthStore((s) => s.register);
  const [showPw, setShowPw] = useState(false);
  const [error, setError]   = useState('');

  const { register: reg, handleSubmit, formState: { errors, isSubmitting } } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', email: '', password: '' },
  });

  const onSubmit = async ({ fullName, email, password }) => {
    setError('');
    try {
      await register({ email, password, fullName });
      navigate('/', { replace: true });
    } catch (err) {
      setError(
        err?.response?.status === 409
          ? err.response.data?.error ?? 'Email already in use.'
          : 'Registration failed. Please try again.'
      );
    }
  };

  return (
    <PageContainer className="min-h-screen flex items-center justify-center bg-gray-50/50">
      <Card className="w-full max-w-md">
        <CardHeader>
          <h1 className="text-2xl font-bold text-center text-text-primary">Create account</h1>
          <p className="text-center text-sm text-text-muted mt-1">
            Already have an account?{' '}
            <Link to="/login" className="text-primary font-medium hover:underline">
              Sign in
            </Link>
          </p>
        </CardHeader>

        <CardContent>
          {error && <Alert message={error} type="error" className="mb-4" />}

          <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-text-primary mb-1">Full name</label>
              <Input
                type="text"
                placeholder="Nguyen Van A"
                autoComplete="name"
                {...reg('fullName')}
              />
              {errors.fullName && (
                <p className="mt-1 text-sm text-danger">{errors.fullName.message}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-text-primary mb-1">Email</label>
              <Input
                type="email"
                placeholder="you@example.com"
                autoComplete="email"
                {...reg('email')}
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
                  placeholder="Min. 6 characters"
                  autoComplete="new-password"
                  {...reg('password')}
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
              <UserPlus className="w-4 h-4 mr-2" />
              Create account
            </Button>
          </form>
        </CardContent>
      </Card>
    </PageContainer>
  );
}