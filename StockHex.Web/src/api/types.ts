/** Espejo de los DTOs de la API. Los enums viajan como texto, no como número. */

export type MovementType = 'In' | 'Out' | 'Adjustment';

/**
 * Clave de un permiso, por ejemplo 'products.create'. No se enumeran los valores
 * a propósito: el catálogo es del backend y se consume de GET /api/permissions.
 * Declararlo aquí sería una segunda fuente de verdad (regla 7 de CLAUDE.md).
 */
export type PermissionKey = string;

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/** Rol resumido, tal como viene incrustado en un usuario. */
export interface RoleSummary {
  id: string;
  name: string;
  isSystem: boolean;
}

export interface UserResponse {
  id: string;
  name: string;
  email: string;
  role: RoleSummary;
  isActive: boolean;
  emailConfirmed: boolean;
  createdAt: string;
  updatedAt: string | null;
  lastLoginAt: string | null;
}

/**
 * Perfil del usuario autenticado, con sus permisos efectivos. El frontend los usa
 * para no ofrecer acciones que van a fallar; la autorización la impone la API.
 */
export interface CurrentUserResponse {
  id: string;
  name: string;
  email: string;
  role: RoleSummary;
  permissions: PermissionKey[];
  isActive: boolean;
  lastLoginAt: string | null;
}

export interface RoleResponse {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  permissions: PermissionKey[];
  permissionCount: number;
  userCount: number;
  createdAt: string;
  updatedAt: string | null;
}

// ───────────────────────────────── catálogo de permisos

/** Una entrada del catálogo. Lo define el backend; aquí sólo se describe su forma. */
export interface PermissionResponse {
  key: PermissionKey;
  module: string;
  moduleLabel: string;
  action: string;
  actionLabel: string;
  /** True cuando la acción no es una de las cuatro estándar de la rejilla. */
  isSpecial: boolean;
}

export interface PermissionModuleResponse {
  module: string;
  label: string;
  permissions: PermissionKey[];
}

export interface PermissionActionResponse {
  action: string;
  label: string;
}

export interface PermissionCatalogResponse {
  permissions: PermissionResponse[];
  modules: PermissionModuleResponse[];
  standardActions: PermissionActionResponse[];
  totalCount: number;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: CurrentUserResponse;
}

export interface CategoryResponse {
  id: string;
  name: string;
  description: string | null;
  productCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface SupplierResponse {
  id: string;
  name: string;
  description: string | null;
  phoneNumber: string | null;
  email: string | null;
  productCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface ClientResponse {
  id: string;
  name: string;
  address: string | null;
  phoneNumber: string | null;
  email: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductResponse {
  id: string;
  name: string;
  description: string | null;
  sku: string;
  price: number;
  stockQuantity: number;
  minimumStock: number;
  isLowStock: boolean;
  isActive: boolean;
  categoryId: string;
  categoryName: string | null;
  supplierId: string | null;
  supplierName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface MovementResponse {
  id: string;
  movementType: MovementType;
  productId: string;
  productName: string | null;
  productSku: string | null;
  quantity: number;
  unitPrice: number | null;
  stockBefore: number;
  stockAfter: number;
  movementDate: string;
  userId: string;
  userName: string | null;
  clientId: string | null;
  clientName: string | null;
  supplierId: string | null;
  supplierName: string | null;
  reversalOfMovementId: string | null;
  comment: string | null;
}

export interface InventorySummaryResponse {
  totalProducts: number;
  activeProducts: number;
  lowStockProducts: number;
  totalStockValue: number;
  generatedAt: string;
}

export interface LowStockItemResponse {
  productId: string;
  name: string;
  sku: string;
  stockQuantity: number;
  minimumStock: number;
  deficit: number;
  categoryName: string | null;
}

export interface MovementSummaryLine {
  movementType: MovementType;
  movements: number;
  units: number;
}

export interface MovementSummaryResponse {
  from: string;
  to: string;
  lines: MovementSummaryLine[];
  generatedAt: string;
}

// ─────────────────────────────────────────────────────── peticiones

export interface LoginRequest { email: string; password: string; }
export interface RefreshTokenRequest { refreshToken: string; }
export interface LogoutRequest { refreshToken: string; allSessions?: boolean; }

export interface CreateCategoryRequest { name: string; description: string | null; }
export type UpdateCategoryRequest = CreateCategoryRequest;

export interface CreateSupplierRequest {
  name: string;
  description: string | null;
  phoneNumber: string | null;
  email: string | null;
}
export type UpdateSupplierRequest = CreateSupplierRequest;

export interface CreateClientRequest {
  name: string;
  address: string | null;
  phoneNumber: string | null;
  email: string | null;
}
export type UpdateClientRequest = CreateClientRequest;

export interface CreateProductRequest {
  name: string;
  description: string | null;
  sku: string;
  price: number;
  minimumStock: number;
  categoryId: string;
  supplierId: string | null;
}
export interface UpdateProductRequest extends CreateProductRequest {
  isActive: boolean;
}

export interface CreateMovementRequest {
  productId: string;
  movementType: MovementType;
  quantity: number;
  unitPrice: number | null;
  clientId: string | null;
  supplierId: string | null;
  comment: string | null;
}
export interface ReverseMovementRequest { comment: string | null; }

export interface CreateUserRequest {
  name: string;
  email: string;
  password: string;
  confirmPassword: string;
  roleId: string;
}
export interface UpdateUserRequest {
  name: string;
  email: string;
  roleId: string;
  isActive: boolean;
}

export interface CreateRoleRequest {
  name: string;
  description: string | null;
  permissions: PermissionKey[];
}
export type UpdateRoleRequest = CreateRoleRequest;
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

/** Restablecimiento hecho por otra persona: no pide la contraseña actual. */
export interface ResetPasswordRequest {
  newPassword: string;
  confirmPassword: string;
  /** Revoca los tokens de refresco del afectado, forzándolo a entrar de nuevo. */
  revokeSessions: boolean;
}

// ─────────────────────────────────────────────────────── filtros

/**
 * Espejo de `PageRequest.AllowedPageSizes` y `PageRequest.DefaultPageSize`
 * (`Domain/Common/PageRequest.cs`). El backend es el dueño de estos valores; si
 * cambian allí, se cambian aquí, igual que cualquier otro dato del contrato.
 *
 * Ojo con el defecto: cuando la URL no trae `pageSize` el frontend no lo envía y
 * responde el defecto del backend, así que los dos números tienen que coincidir
 * o la primera página mostraría un total de filas que el selector no refleja.
 */
export const PAGE_SIZES = [10, 15, 25] as const;
export type PageSize = (typeof PAGE_SIZES)[number];
export const DEFAULT_PAGE_SIZE: PageSize = 15;

export interface PageQuery {
  page?: number;
  pageSize?: number;
  search?: string;
}

export interface UserQuery extends PageQuery {
  roleId?: string;
  isActive?: boolean;
}

export interface ProductQuery extends PageQuery {
  categoryId?: string;
  supplierId?: string;
  isActive?: boolean;
  lowStockOnly?: boolean;
}

export interface MovementQuery extends PageQuery {
  productId?: string;
  clientId?: string;
  supplierId?: string;
  userId?: string;
  movementType?: MovementType;
  from?: string;
  to?: string;
}
