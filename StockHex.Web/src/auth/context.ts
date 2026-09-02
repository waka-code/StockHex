import { createContext } from 'react';
import type { CurrentUserResponse, PermissionKey } from '../api/types';

export interface AuthState {
  user: CurrentUserResponse | null;
  isAuthenticated: boolean;
  /** Permisos efectivos del usuario. Vacío si no hay sesión. */
  permissions: ReadonlySet<PermissionKey>;
  /**
   * True si el usuario tiene el permiso. Sirve para no ofrecer acciones que van a
   * fallar; NO es control de acceso: eso lo impone la API en cada endpoint.
   */
  can: (permission: PermissionKey) => boolean;
  /** True si tiene al menos uno de los permisos indicados. */
  canAny: (...permissions: PermissionKey[]) => boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: (allSessions?: boolean) => Promise<void>;
}

export const AuthContext = createContext<AuthState | null>(null);
