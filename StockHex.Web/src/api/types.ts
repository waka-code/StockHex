/** Espejo de los DTOs de la API. Los enums viajan como texto, no como número. */

export type UserRole = 'Admin' | 'Manager' | 'Operator';
export type MovementType = 'In' | 'Out' | 'Adjustment';

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface UserResponse {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  emailConfirmed: boolean;
  createdAt: string;
  updatedAt: string | null;
  lastLoginAt: string | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserResponse;
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
  role: UserRole;
}
export interface UpdateUserRequest {
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
}
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

// ─────────────────────────────────────────────────────── filtros

export interface PageQuery {
  page?: number;
  pageSize?: number;
  search?: string;
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
