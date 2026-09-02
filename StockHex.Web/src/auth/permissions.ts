import type { PermissionKey } from '../api/types';

/**
 * Claves de permiso que el frontend necesita nombrar para decidir qué ofrecer.
 *
 * No es el catálogo: el catálogo completo vive en el backend y se consume de
 * `GET /api/permissions` (regla 7 de CLAUDE.md). Esto es sólo el subconjunto que
 * aparece en el código de la interfaz, escrito como constantes para que un typo
 * rompa la compilación en lugar de esconder un botón para siempre.
 */
export const P = {
  dashboard: { view: 'dashboard.view' },
  products: {
    view: 'products.view',
    create: 'products.create',
    edit: 'products.edit',
    delete: 'products.delete',
  },
  movements: {
    view: 'movements.view',
    create: 'movements.create',
    reverse: 'movements.reverse',
  },
  categories: {
    view: 'categories.view',
    create: 'categories.create',
    edit: 'categories.edit',
    delete: 'categories.delete',
  },
  suppliers: {
    view: 'suppliers.view',
    create: 'suppliers.create',
    edit: 'suppliers.edit',
    delete: 'suppliers.delete',
  },
  clients: {
    view: 'clients.view',
    create: 'clients.create',
    edit: 'clients.edit',
    delete: 'clients.delete',
  },
  reports: { view: 'reports.view', export: 'reports.export' },
  users: {
    view: 'users.view',
    create: 'users.create',
    edit: 'users.edit',
    delete: 'users.delete',
    changePassword: 'users.change_password',
  },
  roles: {
    view: 'roles.view',
    create: 'roles.create',
    edit: 'roles.edit',
    delete: 'roles.delete',
  },
} as const satisfies Record<string, Record<string, PermissionKey>>;

/** Permisos de escritura de un módulo, para el patrón CRUD compartido. */
export interface CrudPermissions {
  view: PermissionKey;
  create: PermissionKey;
  edit: PermissionKey;
  delete: PermissionKey;
}
