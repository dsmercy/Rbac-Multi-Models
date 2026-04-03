import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useLoginMutation, useLazyGetMeQuery } from '../authEndpoints';
import { useAppDispatch } from '@/app/hooks';
import { setAuth } from '../authSlice';
import type { UserProfile } from '../types';

const loginSchema = z.object({
  tenantId: z.string().uuid('Must be a valid tenant UUID'),
  email: z.string().email('Invalid email'),
  password: z.string().min(1, 'Password is required'),
});

type LoginInput = z.infer<typeof loginSchema>;

// Placeholder profile used to seed Redux with the access token so that
// prepareHeaders can attach Authorization on the /auth/me call immediately after.
const placeholderProfile = (tenantId: string, email: string): UserProfile => ({
  id: '',
  tenantId,
  email,
  displayName: '',
  isActive: true,
  isSuperAdmin: false,
  onboardingCompleted: false,
});

export default function LoginPage() {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const [login, { isLoading }] = useLoginMutation();
  const [serverError, setServerError] = useState<string | null>(null);

  // useLazyQuery — fires only when triggerGetMe() is called explicitly
  const [triggerGetMe] = useLazyGetMeQuery();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginInput>({ resolver: zodResolver(loginSchema) });

  const onSubmit = async (data: LoginInput) => {
    setServerError(null);
    try {
      // 1. Authenticate — backend returns token pair in response body
      const tokenPair = await login(data).unwrap();

      // 2. Store the access token in Redux immediately.
      //    dispatch() is synchronous, so prepareHeaders in buildBaseQuery will
      //    read the token on the very next RTK Query request.
      dispatch(setAuth({ user: placeholderProfile(data.tenantId, data.email), accessToken: tokenPair.accessToken }));

      // 3. Fetch the real user profile — Authorization header is now attached
      const profile = await triggerGetMe().unwrap();
      dispatch(setAuth({ user: profile, accessToken: tokenPair.accessToken }));

      navigate(`/${data.tenantId}/dashboard`);
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

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate autoComplete="on">
          <div className="space-y-1">
            <label htmlFor="tenantId" className="text-sm font-medium">
              Tenant ID
            </label>
            <input
              id="tenantId"
              {...register('tenantId')}
              className="w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              autoComplete="on"
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
              autoComplete="on"
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
