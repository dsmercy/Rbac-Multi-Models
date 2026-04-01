import { createApi } from '@reduxjs/toolkit/query/react';
import { buildBaseQuery } from './buildBaseQuery';

/**
 * All RTK Query tag types used across the app.
 * Each feature injects its own endpoints via apiSlice.injectEndpoints().
 * Tag invalidation here keeps cache coherence between features.
 */
export const TAG_TYPES = [
  'Auth',
  'Role',
  'Permission',
  'User',
  'Policy',
  'Delegation',
  'AuditLog',
  'Scope',
] as const;

export type TagType = (typeof TAG_TYPES)[number];

/**
 * Single RTK Query API instance. All features inject endpoints into this —
 * never create a second createApi(). This ensures one shared cache and
 * one reducer/middleware pair in the Redux store.
 */
export const apiSlice = createApi({
  reducerPath: 'api',
  baseQuery: buildBaseQuery,
  tagTypes: TAG_TYPES as unknown as string[],
  endpoints: () => ({}),
});
