import { apiSlice } from '@/shared/api/apiSlice';
import type { LoginInput, TokenPair, UserProfile } from './types';

export const authEndpoints = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    login: builder.mutation<TokenPair, LoginInput>({
      query: (body) => ({
        url: '/auth/login',
        method: 'POST',
        body,
      }),
    }),

    logout: builder.mutation<void, void>({
      query: () => ({ url: '/auth/logout', method: 'POST' }),
    }),

    refreshToken: builder.mutation<TokenPair, void>({
      query: () => ({ url: '/auth/refresh', method: 'POST' }),
    }),

    /**
     * Session validation on app boot.
     * A 200 means the httpOnly cookie is valid — returns the current user profile.
     * A 401 means no valid session — AuthGuard will redirect to login.
     */
    getMe: builder.query<UserProfile, void>({
      query: () => '/auth/me',
      providesTags: ['Auth'] as never,
    }),
  }),

  overrideExisting: false,
});

export const { useLoginMutation, useLogoutMutation, useRefreshTokenMutation, useGetMeQuery } =
  authEndpoints;
