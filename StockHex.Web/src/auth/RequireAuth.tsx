import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import type { PermissionKey } from '../api/types';
import { useAuth } from './useAuth';

interface Props {
  children: ReactNode;
  /**
   * Permiso necesario además de estar autenticado. Es un espejo del que exige el
   * endpoint: evita que la pantalla se monte para luego llenarse de 403.
   */
  permission?: PermissionKey;
}

export function RequireAuth({ children, permission }: Props) {
  const { user, isAuthenticated, can } = useAuth();
  const location = useLocation();

  if (!isAuthenticated || !user) {
    // Se recuerda a dónde iba para volver ahí después del login.
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (permission && !can(permission)) {
    return <Navigate to="/sin-acceso" replace state={{ permission }} />;
  }

  return <>{children}</>;
}
