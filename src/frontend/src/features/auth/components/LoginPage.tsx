import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useLoginMutation } from '../authEndpoints';

const loginSchema = z.object({
  tenantId: z.string().uuid('Must be a valid tenant UUID'),
  email: z.string().email('Invalid email'),
  password: z.string().min(1, 'Password is required'),
});

type LoginInput = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const navigate = useNavigate();
  const [login, { isLoading, error }] = useLoginMutation();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginInput>({ resolver: zodResolver(loginSchema) });

  const onSubmit = async (data: LoginInput) => {
    setServerError(null);
    try {
      const result = await login(data).unwrap();
      navigate(`/${result.tenantId}/dashboard`);
    } catch {
      setServerError('Invalid credentials. Please try again.');
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="w-full max-w-md space-y-6 p-8 border rounded-lg shadow-sm bg-card">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold">RBAC Admin</h1>
          <p className="text-sm text-muted-foreground">Sign in to your tenant account</p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="space-y-1">
            <label htmlFor="tenantId" className="text-sm font-medium">
              Tenant ID
            </label>
            <input
              id="tenantId"
              {...register('tenantId')}
              className="w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              autoComplete="off"
            />
            {errors.tenantId && (
              <p className="text-xs text-destructive">{errors.tenantId.message}</p>
            )}
          </div>

          <div className="space-y-1">
            <label htmlFor="email" className="text-sm font-medium">
              Email
            </label>
            <input
              id="email"
              type="email"
              {...register('email')}
              className="w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              autoComplete="email"
            />
            {errors.email && (
              <p className="text-xs text-destructive">{errors.email.message}</p>
            )}
          </div>

          <div className="space-y-1">
            <label htmlFor="password" className="text-sm font-medium">
              Password
            </label>
            <input
              id="password"
              type="password"
              {...register('password')}
              className="w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              autoComplete="current-password"
            />
            {errors.password && (
              <p className="text-xs text-destructive">{errors.password.message}</p>
            )}
          </div>

          {serverError && (
            <p className="text-sm text-destructive bg-destructive/10 px-3 py-2 rounded-md">
              {serverError}
            </p>
          )}
          {error && !serverError && (
            <p className="text-sm text-destructive bg-destructive/10 px-3 py-2 rounded-md">
              Login failed. Please check your credentials.
            </p>
          )}

          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-2 px-4 bg-primary text-primary-foreground rounded-md text-sm font-medium disabled:opacity-50"
          >
            {isLoading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
}
