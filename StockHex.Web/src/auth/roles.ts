import type { PermissionKey } from '../api/types';
import { P } from './permissions';

/**
 * Secciones del menú. Cada una declara el permiso que la habilita, no una lista
 * de roles: con roles configurables, un rol nuevo aparece en el menú correcto sin
 * tocar este archivo.
 */
export interface NavItem {
  label: string;
  path: string;
  icon: string;
  permission: PermissionKey;
}

/** El orden es el de uso diario, no el alfabético. */
export const NAV: readonly NavItem[] = [
  { label: 'Dashboard', path: '/', icon: 'grid', permission: P.dashboard.view },
  { label: 'Productos', path: '/productos', icon: 'box', permission: P.products.view },
  { label: 'Movimientos', path: '/movimientos', icon: 'swap', permission: P.movements.view },
  { label: 'Reportes', path: '/reportes', icon: 'chart', permission: P.reports.view },
  { label: 'Categorías', path: '/categorias', icon: 'tag', permission: P.categories.view },
  { label: 'Proveedores', path: '/proveedores', icon: 'truck', permission: P.suppliers.view },
  { label: 'Clientes', path: '/clientes', icon: 'users', permission: P.clients.view },
  { label: 'Usuarios', path: '/usuarios', icon: 'shield', permission: P.users.view },
  { label: 'Roles', path: '/roles', icon: 'lock', permission: P.roles.view },
];

/** Secciones visibles con el conjunto de permisos indicado. */
export function navFor(permissions: ReadonlySet<PermissionKey>): NavItem[] {
  return NAV.filter((item) => permissions.has(item.permission));
}

/** Primera ruta a la que se puede entrar. Sirve para redirigir tras un 403. */
export function firstAllowedPath(permissions: ReadonlySet<PermissionKey>): string {
  return navFor(permissions)[0]?.path ?? '/sin-acceso';
}
