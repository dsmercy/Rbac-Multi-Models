import { createApi } from '@reduxjs/toolkit/query/react';
import { buildBaseQuery } from './buildBaseQuery';

/**
 * Exhaustive list of cacheable entity types.
 * Add one entry here when introducing a new entity — nowhere else.
 * Tag types drive all cache invalidation across every feature.
 */
export const TAG_TYPES = [
  'Auth',        // session / user profile
  'Role',
  'Permission',
  'User',
  'Policy',
  'Delegation',
  'AuditLog',
  'Tenant',
  'Scope',
  'Notification',
] as const;

export type TagType = (typeof TAG_TYPES)[number];

/**
 * Single RTK Query API instance for the entire app.
 * Features inject endpoints via apiSlice.injectEndpoints() — never createApi() again.
 * One slice = one cache = cross-feature tag invalidation with no coupling.
 */
export const apiSlice = createApi({
  reducerPath: 'api',
  baseQuery: buildBaseQuery,
  tagTypes: TAG_TYPES as unknown as string[],
  // Per-entity cache TTL (seconds). Volatile data expires faster.
  keepUnusedDataFor: 300,
  endpoints: () => ({}),
});
