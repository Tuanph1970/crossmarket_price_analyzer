import { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Eye, EyeOff, ArrowRight } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useAuthStore } from '@/store/authStore';
import { Alert } from '@/components/shared/Alert';

const schema = z.object({
  email:    z.string().email('Valid email required'),
  password: z.string().min(6, 'At least 6 characters'),
});

const fieldCls = 'h-10 w-full rounded-lg border border-border bg-surface-raised px-3 text-sm text-text-primary placeholder:text-text-subtle focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-colors';

export default function LoginPage() {
  const navigate  = useNavigate();
  const location  = useLocation();
  const login     = useAuthStore((s) => s.login);
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
      setError(
        err?.response?.status === 401
          ? 'Invalid email or password.'
          : err?.response?.data?.error ?? 'Login failed. Please try again.'
      );
    }
  };

  return (
    /* Modal overlay — fills the main content area */
    <div className="fixed inset-0 z-40 flex items-center justify-center">
      {/* Backdrop — blurs the app behind */}
      <div className="absolute inset-0 bg-background/80 backdrop-blur-sm" />

      {/* Subtle atmosphere orbs */}
      <div className="absolute inset-0 pointer-events-none overflow-hidden">
        <div className="absolute top-1/4 left-1/3 w-96 h-96 bg-primary/6 rounded-full blur-[100px]" />
        <div className="absolute bottom-1/4 right-1/3 w-80 h-80 bg-gold/4 rounded-full blur-[80px]" />
      </div>

      {/* Dialog */}
      <div className="relative z-10 w-full max-w-[380px] mx-4 animate-fade-in">
        {/* Brand mark */}
        <div className="text-center mb-7">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-primary/10 border border-primary/20 mb-4 shadow-[0_0_40px_rgba(6,214,160,0.15)]">
            <span className="font-display font-black text-xl text-primary leading-none">CX</span>
          </div>
          <h1 className="font-display text-2xl font-bold text-text-primary">CrossMarket</h1>
          <p className="text-text-muted text-xs mt-1 tracking-wide">US → Vietnam Intelligence Platform</p>
        </div>

        {/* Card */}
        <div className="bg-surface rounded-2xl border border-border shadow-[0_24px_80px_rgba(0,0,0,0.6)] overflow-hidden">
          <div className="h-px bg-gradient-to-r from-transparent via-primary/50 to-transparent" />

          <div className="p-7">
            <h2 className="font-display text-lg font-semibold text-text-primary mb-0.5">Welcome back</h2>
            <p className="text-sm text-text-muted mb-6">
              No account?{' '}
              <Link to="/register" className="text-primary hover:text-primary-400 font-medium transition-colors">
                Create one
              </Link>
            </p>

            {error && <Alert message={error} type="error" className="mb-4" />}

            <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-text-muted uppercase tracking-widest mb-1.5">
                  Email
                </label>
                <input type="email" placeholder="you@example.com" autoComplete="email"
                  {...register('email')} className={fieldCls} />
                {errors.email && <p className="mt-1 text-xs text-danger">{errors.email.message}</p>}
              </div>

              <div>
                <label className="block text-xs font-medium text-text-muted uppercase tracking-widest mb-1.5">
                  Password
                </label>
                <div className="relative">
                  <input
                    type={showPw ? 'text' : 'password'}
                    placeholder="••••••••"
                    autoComplete="current-password"
                    {...register('password')}
                    className={`${fieldCls} pr-10`}
                  />
                  <button type="button" onClick={() => setShowPw(!showPw)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-text-primary transition-colors">
                    {showPw ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                {errors.password && <p className="mt-1 text-xs text-danger">{errors.password.message}</p>}
              </div>

              <button
                type="submit"
                disabled={isSubmitting}
                className="mt-1 w-full h-10 rounded-lg bg-primary text-background text-sm font-semibold font-display flex items-center justify-center gap-2 hover:bg-primary-600 transition-all shadow-[0_0_20px_rgba(6,214,160,0.2)] hover:shadow-[0_0_32px_rgba(6,214,160,0.35)] disabled:opacity-40 disabled:shadow-none disabled:cursor-not-allowed"
              >
                {isSubmitting ? (
                  <span className="flex items-center gap-2">
                    <svg className="animate-spin w-4 h-4" viewBox="0 0 24 24" fill="none">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                    Signing in…
                  </span>
                ) : (
                  <>Sign in <ArrowRight className="w-4 h-4" /></>
                )}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}
