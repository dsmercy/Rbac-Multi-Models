import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthState, UserProfile } from './types';

/**
 * SECURITY NOTE: Tokens are stored in httpOnly cookies set by the backend.
 * This slice never stores token values — only the parsed user profile and
 * the current tenantId derived from it.
 *
 * DO NOT add accessToken or refreshToken fields to this state.
 * Storing tokens in JS-accessible storage (localStorage, Redux, sessionStorage)
 * is an XSS risk. The browser sends httpOnly cookies automatically on every
 * same-origin request — no JS involvement needed.
 */
const initialState: AuthState = {
  user: null,
  tenantId: null,
  isAuthenticated: false,
  isLoading: true, // true on boot until /auth/me resolves
};

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setUser(state, action: PayloadAction<UserProfile>) {
      state.user = action.payload;
      state.tenantId = action.payload.tenantId;
      state.isAuthenticated = true;
      state.isLoading = false;
    },
    setTenantId(state, action: PayloadAction<string>) {
      // Called by TenantLayout when the URL tenantId param is validated.
      state.tenantId = action.payload;
    },
    setLoading(state, action: PayloadAction<boolean>) {
      state.isLoading = action.payload;
    },
    logout(state) {
      state.user = null;
      state.tenantId = null;
      state.isAuthenticated = false;
      state.isLoading = false;
    },
  },
});

export const { setUser, setTenantId, setLoading, logout } = authSlice.actions;
export default authSlice.reducer;
