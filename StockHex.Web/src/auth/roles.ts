import type { UserRole } from '../api/types';

/** Secciones del menú y qué roles las ven. Espeja la autorización de la API. */
export interface NavItem {
  label: string;
  path: string;
  icon: string;
  roles: readonly UserRole[];
}

const ALL: readonly UserRole[] = ['Admin', 'Manager', 'Operator'];
const MANAGE: readonly UserRole[] = ['Admin', 'Manager'];
const ADMIN: readonly UserRole[] = ['Admin'];

/** El orden es el de uso diario, no el alfabético. */
export const NAV: readonly NavItem[] = [
  { label: 'Dashboard', path: '/', icon: 'grid', roles: ALL },
  { label: 'Productos', path: '/productos', icon: 'box', roles: ALL },
  { label: 'Movimientos', path: '/movimientos', icon: 'swap', roles: ALL },
  { label: 'Reportes', path: '/reportes', icon: 'chart', roles: ALL },
  { label: 'Categorías', path: '/categorias', icon: 'tag', roles: MANAGE },
  { label: 'Proveedores', path: '/proveedores', icon: 'truck', roles: MANAGE },
  { label: 'Clientes', path: '/clientes', icon: 'users', roles: MANAGE },
  { label: 'Usuarios', path: '/usuarios', icon: 'shield', roles: ADMIN },
];

export function navFor(role: UserRole): NavItem[] {
  return NAV.filter((item) => item.roles.includes(role));
}

/**
 * Permisos de escritura. Es un espejo de lo que impone la API, no la
 * autorización real: sirve para no ofrecer botones que van a dar 403.
 */
export const can = {
  manageCatalog: (role: UserRole) => role === 'Admin' || role === 'Manager',
  manageUsers: (role: UserRole) => role === 'Admin',
  reverseMovements: (role: UserRole) => role === 'Admin' || role === 'Manager',
  createMovements: (_role: UserRole) => true,
};

export const ROLE_LABEL: Record<UserRole, string> = {
  Admin: 'Admin',
  Manager: 'Manager',
  Operator: 'Operator',
};
