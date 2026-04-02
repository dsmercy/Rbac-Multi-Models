import { apiSlice } from '@/shared/api/apiSlice';

export const onboardingEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    completeOnboarding: builder.mutation<void, { tenantId: string; userId: string }>({
      query: ({ tenantId, userId }) => ({
        url: `/tenants/${tenantId}/users/${userId}/onboarding`,
        method: 'PATCH',
      }),
    }),
  }),
  overrideExisting: false,
});

export const { useCompleteOnboardingMutation } = onboardingEndpoints;
