import { createContext } from 'react';
import type { UserResponse } from '../api/types';

export interface AuthState {
  user: UserResponse | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: (allSessions?: boolean) => Promise<void>;
}

export const AuthContext = createContext<AuthState | null>(null);
