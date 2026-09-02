import { useContext } from 'react';
import { AuthContext, type AuthState } from './context';
import type { CurrentUserResponse } from '../api/types';

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth debe usarse dentro de <AuthProvider>.');
  return context;
}

/** El usuario autenticado. Sólo para árboles ya protegidos por <RequireAuth>. */
export function useCurrentUser(): CurrentUserResponse {
  const { user } = useAuth();
  if (!user) throw new Error('No hay usuario autenticado en este árbol.');
  return user;
}

/**
 * Atajo para comprobar permisos en un componente.
 * `const can = usePermissions(); can(P.products.create)`
 */
export function usePermissions(): AuthState['can'] {
  return useAuth().can;
}
