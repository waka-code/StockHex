import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import type { UserRole } from '../api/types';
import { useAuth } from './useAuth';

interface Props {
  children: ReactNode;
  /** Si se indica, además de estar autenticado hay que tener uno de estos roles. */
  roles?: readonly UserRole[];
}

export function RequireAuth({ children, roles }: Props) {
  const { user, isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated || !user) {
    // Se recuerda a dónde iba para volver ahí después del login.
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (roles && !roles.includes(user.role)) {
    return <Navigate to="/sin-acceso" replace />;
  }

  return <>{children}</>;
}
