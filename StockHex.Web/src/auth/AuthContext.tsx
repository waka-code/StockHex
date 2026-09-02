import {
  useCallback, useEffect, useMemo, useState, type ReactNode,
} from 'react';
import { getSession, onSessionChange, setSession } from '../api/client';
import { auth as authApi } from '../api/endpoints';
import type { PermissionKey } from '../api/types';
import type { StoredSession } from './storage';
import { AuthContext, type AuthState } from './context';

const EMPTY: ReadonlySet<PermissionKey> = new Set<PermissionKey>();

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setLocal] = useState<StoredSession | null>(() => getSession());

  // El cliente HTTP es el dueño de la sesión: puede renovarla o invalidarla por
  // su cuenta, y este efecto mantiene React al día con lo que decida.
  useEffect(() => onSessionChange(setLocal), []);

  // Al renovar el token la API devuelve los permisos otra vez, así que un cambio
  // de rol se refleja sin recargar la página.
  const permissions = useMemo<ReadonlySet<PermissionKey>>(
    () => (session ? new Set(session.user.permissions) : EMPTY),
    [session],
  );

  const login = useCallback(async (email: string, password: string) => {
    const response = await authApi.login({ email, password });
    setSession(response);
  }, []);

  const logout = useCallback(async (allSessions = false) => {
    const current = getSession();
    if (current) {
      try {
        await authApi.logout(current.refreshToken, allSessions);
      } catch {
        // Si la revocación falla igual se cierra localmente: dejar al usuario
        // dentro porque el servidor no respondió sería peor.
      }
    }
    setSession(null);
  }, []);

  const value = useMemo<AuthState>(() => ({
    user: session?.user ?? null,
    isAuthenticated: Boolean(session),
    permissions,
    can: (permission) => permissions.has(permission),
    canAny: (...wanted) => wanted.some((permission) => permissions.has(permission)),
    login,
    logout,
  }), [session, permissions, login, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
