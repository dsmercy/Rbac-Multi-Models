import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthState, UserProfile } from './types';

/**
 * SECURITY NOTE: The access token is stored in Redux (JS memory) only — it is
 * never written to localStorage or sessionStorage. It is lost on page refresh,
 * which triggers a re-login. This is an acceptable trade-off for this admin panel.
 *
 * The alternative (httpOnly cookies) would require the backend to set/clear them,
 * which is a future improvement. For now, Bearer token in memory is the approach.
 */
const initialState: AuthState = {
  user: null,
  tenantId: null,
  accessToken: null,
  isAuthenticated: false,
  isLoading: true, // true on boot until /auth/me resolves
};

export const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setAuth(state, action: PayloadAction<{ user: UserProfile; accessToken: string }>) {
      state.user = action.payload.user;
      state.tenantId = action.payload.user.tenantId;
      state.accessToken = action.payload.accessToken;
      state.isAuthenticated = true;
      state.isLoading = false;
    },
    setUser(state, action: PayloadAction<UserProfile>) {
      state.user = action.payload;
      state.tenantId = action.payload.tenantId;
      state.isAuthenticated = true;
      state.isLoading = false;
    },
    setTenantId(state, action: PayloadAction<string>) {
      state.tenantId = action.payload;
    },
    setLoading(state, action: PayloadAction<boolean>) {
      state.isLoading = action.payload;
    },
    logout(state) {
      state.user = null;
      state.tenantId = null;
      state.accessToken = null;
      state.isAuthenticated = false;
      state.isLoading = false;
    },
  },
});

export const { setAuth, setUser, setTenantId, setLoading, logout } = authSlice.actions;
export default authSlice.reducer;
