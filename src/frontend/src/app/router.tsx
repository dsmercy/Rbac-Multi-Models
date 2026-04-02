import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { tenantRoutes } from '@/routes';
import AuthGuard from '@/shared/components/AuthGuard';
import RouteGuard from '@/shared/components/RouteGuard';
import TenantLayout from '@/shared/components/TenantLayout';
import NotFoundPage from '@/shared/components/NotFoundPage';
import { Skeleton } from '@/shared/components/Skeleton';
import { ErrorBoundary } from '@/shared/components/ErrorBoundary';

const LoginPage = lazy(() => import('@/features/auth/components/LoginPage'));
const DashboardPage = lazy(() => import('@/shared/components/DashboardPage'));

function PageShell({ children }: { children: React.ReactNode }) {
  return (
    <ErrorBoundary>
      <Suspense fallback={<Skeleton />}>
        {children}
      </Suspense>
    </ErrorBoundary>
  );
}

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
          <PageShell>
            <DashboardPage />
          </PageShell>
        ),
      },
      ...tenantRoutes.map((route) => ({
        path: route.path,
        element: (
          <RouteGuard guard={route.guard}>
            <PageShell>
              <route.component />
            </PageShell>
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
