import { lazy } from 'react';
import type { AppRoute } from '@/routes/types';
export const onboardingRoutes: AppRoute[] = [
  { path: 'onboarding', component: lazy(() => import('./components/OnboardingWizard')) },
];
