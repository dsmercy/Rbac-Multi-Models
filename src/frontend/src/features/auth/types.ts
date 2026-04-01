export interface LoginInput {
  tenantId: string;
  email: string;
  password: string;
}

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  tenantId: string;
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
  isAuthenticated: boolean;
  isLoading: boolean;
}
