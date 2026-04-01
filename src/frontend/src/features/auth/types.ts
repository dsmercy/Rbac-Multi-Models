export interface LoginInput {
  tenantId: string;
  email: string;
  password: string;
}

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  tokenVersion: number;
}

export interface UserProfile {
  id: string;
  tenantId: string;
  email: string;
  displayName: string;
  isActive: boolean;
  isSuperAdmin: boolean;
  onboardingCompleted: boolean;
}

export interface AuthState {
  user: UserProfile | null;
  tenantId: string | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}
