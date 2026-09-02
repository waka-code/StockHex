import { api } from './client';
import type {
  AuthResponse, CategoryResponse, ChangePasswordRequest, ClientResponse,
  CreateRoleRequest, CurrentUserResponse, PermissionCatalogResponse,
  ResetPasswordRequest, RoleResponse, UpdateRoleRequest,
  CreateCategoryRequest, CreateClientRequest, CreateMovementRequest,
  CreateProductRequest, CreateSupplierRequest, CreateUserRequest,
  InventorySummaryResponse, LoginRequest, LowStockItemResponse, MovementQuery,
  MovementResponse, MovementSummaryResponse, PageQuery, PagedResponse,
  ProductQuery, ProductResponse, ReverseMovementRequest, SupplierResponse,
  UpdateCategoryRequest, UpdateClientRequest, UpdateProductRequest,
  UpdateSupplierRequest, UpdateUserRequest, UserQuery, UserResponse,
} from './types';

export const auth = {
  login: (body: LoginRequest) => api.anon<AuthResponse>('/api/auth/login', body),
  me: () => api.get<CurrentUserResponse>('/api/auth/me'),
  logout: (refreshToken: string, allSessions = false) =>
    api.post<void>('/api/auth/logout', { refreshToken, allSessions }),
};

export const categories = {
  list: (query?: PageQuery) => api.get<PagedResponse<CategoryResponse>>('/api/categories', query),
  get: (id: string) => api.get<CategoryResponse>(`/api/categories/${id}`),
  create: (body: CreateCategoryRequest) => api.post<CategoryResponse>('/api/categories', body),
  update: (id: string, body: UpdateCategoryRequest) =>
    api.put<CategoryResponse>(`/api/categories/${id}`, body),
  remove: (id: string) => api.del<void>(`/api/categories/${id}`),
};

export const suppliers = {
  list: (query?: PageQuery) => api.get<PagedResponse<SupplierResponse>>('/api/suppliers', query),
  get: (id: string) => api.get<SupplierResponse>(`/api/suppliers/${id}`),
  create: (body: CreateSupplierRequest) => api.post<SupplierResponse>('/api/suppliers', body),
  update: (id: string, body: UpdateSupplierRequest) =>
    api.put<SupplierResponse>(`/api/suppliers/${id}`, body),
  remove: (id: string) => api.del<void>(`/api/suppliers/${id}`),
};

export const clients = {
  list: (query?: PageQuery) => api.get<PagedResponse<ClientResponse>>('/api/clients', query),
  get: (id: string) => api.get<ClientResponse>(`/api/clients/${id}`),
  create: (body: CreateClientRequest) => api.post<ClientResponse>('/api/clients', body),
  update: (id: string, body: UpdateClientRequest) =>
    api.put<ClientResponse>(`/api/clients/${id}`, body),
  remove: (id: string) => api.del<void>(`/api/clients/${id}`),
};

export const products = {
  list: (query?: ProductQuery) => api.get<PagedResponse<ProductResponse>>('/api/products', query),
  get: (id: string) => api.get<ProductResponse>(`/api/products/${id}`),
  create: (body: CreateProductRequest) => api.post<ProductResponse>('/api/products', body),
  update: (id: string, body: UpdateProductRequest) =>
    api.put<ProductResponse>(`/api/products/${id}`, body),
  remove: (id: string) => api.del<void>(`/api/products/${id}`),
};

export const movements = {
  list: (query?: MovementQuery) =>
    api.get<PagedResponse<MovementResponse>>('/api/inventory-movements', query),
  get: (id: string) => api.get<MovementResponse>(`/api/inventory-movements/${id}`),
  create: (body: CreateMovementRequest) =>
    api.post<MovementResponse>('/api/inventory-movements', body),
  reverse: (id: string, body: ReverseMovementRequest) =>
    api.post<MovementResponse>(`/api/inventory-movements/${id}/reverse`, body),
};

export const users = {
  list: (query?: UserQuery) => api.get<PagedResponse<UserResponse>>('/api/users', query),
  get: (id: string) => api.get<UserResponse>(`/api/users/${id}`),
  create: (body: CreateUserRequest) => api.post<UserResponse>('/api/users', body),
  update: (id: string, body: UpdateUserRequest) => api.put<UserResponse>(`/api/users/${id}`, body),
  remove: (id: string) => api.del<void>(`/api/users/${id}`),
  changePassword: (body: ChangePasswordRequest) =>
    api.post<void>('/api/users/me/change-password', body),
  /** Restablece la de otro usuario. Exige el permiso users.change_password. */
  resetPassword: (id: string, body: ResetPasswordRequest) =>
    api.post<void>(`/api/users/${id}/reset-password`, body),
};

export const roles = {
  list: (query?: PageQuery) => api.get<PagedResponse<RoleResponse>>('/api/roles', query),
  get: (id: string) => api.get<RoleResponse>(`/api/roles/${id}`),
  create: (body: CreateRoleRequest) => api.post<RoleResponse>('/api/roles', body),
  update: (id: string, body: UpdateRoleRequest) => api.put<RoleResponse>(`/api/roles/${id}`, body),
  remove: (id: string) => api.del<void>(`/api/roles/${id}`),
};

/**
 * El catálogo vive en el backend y es la única fuente (regla 7). El frontend lo
 * pide una vez y lo cachea con TanStack Query; nunca lo redeclara.
 */
export const permissions = {
  catalog: () => api.get<PermissionCatalogResponse>('/api/permissions'),
};

export const reports = {
  inventorySummary: () => api.get<InventorySummaryResponse>('/api/reports/inventory-summary'),
  lowStock: (query?: PageQuery) =>
    api.get<PagedResponse<LowStockItemResponse>>('/api/reports/low-stock', query),
  movementSummary: (from?: string, to?: string) =>
    api.get<MovementSummaryResponse>('/api/reports/movement-summary', { from, to }),
};
