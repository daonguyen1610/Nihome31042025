import api from "@/lib/api";

// Mirrors the RoleResponse / PermissionResponse DTOs in
// nihomebackend/Models/DTOs/Responses/RbacResponses.cs.

export interface PermissionResponse {
  id: number;
  module: string;
  action: string;
  code: string;
  descriptionKey?: string | null;
}

export interface RoleResponse {
  id: number;
  code: string;
  name: string;
  labelKey?: string | null;
  descriptionKey?: string | null;
  isSystem: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
}

export interface RolePermissionsResponse {
  role: RoleResponse;
  permissions: string[];
}

export interface MePermissionsResponse {
  role: string;
  roleId: number | null;
  permissions: string[];
}

export interface CreateRoleRequest {
  code: string;
  name: string;
  labelKey?: string;
  descriptionKey?: string;
  permissions?: string[];
}

export interface UpdateRoleRequest {
  name?: string;
  labelKey?: string;
  descriptionKey?: string;
  isActive?: boolean;
}

export interface UpdateRolePermissionsRequest {
  permissions: string[];
}

export const rbacApi = {
  getMyPermissions: () =>
    api.get<MePermissionsResponse>("/users/me/permissions"),

  listRoles: () =>
    api.get<RoleResponse[]>("/admin/rbac/roles"),

  getRole: (id: number) =>
    api.get<RoleResponse>(`/admin/rbac/roles/${id}`),

  getRolePermissions: (id: number) =>
    api.get<RolePermissionsResponse>(`/admin/rbac/roles/${id}/permissions`),

  listPermissions: () =>
    api.get<PermissionResponse[]>("/admin/rbac/permissions"),

  createRole: (data: CreateRoleRequest) =>
    api.post<RoleResponse>("/admin/rbac/roles", data),

  updateRole: (id: number, data: UpdateRoleRequest) =>
    api.put<RoleResponse>(`/admin/rbac/roles/${id}`, data),

  updateRolePermissions: (id: number, data: UpdateRolePermissionsRequest) =>
    api.put<RolePermissionsResponse>(`/admin/rbac/roles/${id}/permissions`, data),

  deleteRole: (id: number) =>
    api.delete<RoleResponse>(`/admin/rbac/roles/${id}`),
};
