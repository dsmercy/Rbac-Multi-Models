import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { tenantRoutes } from '@/routes';
import AuthGuard from '@/shared/components/AuthGuard';
import RouteGuard from '@/shared/components/RouteGuard';
import TenantLayout from '@/shared/components/TenantLayout';
import NotFoundPage from '@/shared/components/NotFoundPage';
import { Skeleton } from '@/shared/components/Skeleton';

const LoginPage = lazy(() => import('@/features/auth/components/LoginPage'));
const OnboardingWizard = lazy(() => import('@/features/onboarding/components/OnboardingWizard'));
const DashboardPage = lazy(() => import('@/shared/components/DashboardPage'));

export const router = createBrowserRouter([
  {
    path: '/login',
    element: (
      <Suspense fallback={<Skeleton />}>
        <LoginPage />
      </Suspense>
    ),
  },
  {
    path: '/:tenantId',
    element: (
      <AuthGuard>
        <TenantLayout />
      </AuthGuard>
    ),
    children: [
      {
        index: true,
        element: <Navigate to="dashboard" replace />,
      },
      {
        path: 'dashboard',
        element: (
          <Suspense fallback={<Skeleton />}>
            <DashboardPage />
          </Suspense>
        ),
      },
      {
        path: 'onboarding',
        element: (
          <Suspense fallback={<Skeleton />}>
            <OnboardingWizard />
          </Suspense>
        ),
      },
      ...tenantRoutes.map((route) => ({
        path: route.path,
        element: (
          <RouteGuard guard={route.guard}>
            <Suspense fallback={<Skeleton />}>
              <route.component />
            </Suspense>
          </RouteGuard>
        ),
      })),
    ],
  },
  {
    path: '/',
    element: <Navigate to="/login" replace />,
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);
