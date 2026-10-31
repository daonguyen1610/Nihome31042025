import api from "@/lib/api";
import type { ConfirmDeletionRequest, DeletionImpactResponse } from "@/services/adminApi";

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
  roleGroupId?: number | null;
  roleGroupCode?: string | null;
  roleGroupLabelKey?: string | null;
}

export interface RoleGroupResponse {
  id: number;
  code: string;
  labelKey: string;
  sortOrder: number;
  roleCount: number;
  names: Record<"vi" | "en" | "zh" | "ja", string>;
  baselinePermissions: string[];
}

export interface RoleGroupNames {
  vi: string;
  en: string;
  zh: string;
  ja: string;
}

export interface CreateRoleGroupRequest {
  code: string;
  names: RoleGroupNames;
  sortOrder: number;
}

export interface UpdateRoleGroupRequest {
  names: RoleGroupNames;
  sortOrder: number;
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
  roleGroupId?: number | null;
  importGroupBaseline?: boolean;
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
  getMyPermissions: () => api.get<MePermissionsResponse>("/users/me/permissions"),

  listRoles: () => api.get<RoleResponse[]>("/admin/rbac/roles"),

  listRoleGroups: () => api.get<RoleGroupResponse[]>("/admin/rbac/role-groups"),
  createRoleGroup: (data: CreateRoleGroupRequest) =>
    api.post<RoleGroupResponse>("/admin/rbac/role-groups", data),
  updateRoleGroup: (id: number, data: UpdateRoleGroupRequest) =>
    api.put<RoleGroupResponse>(`/admin/rbac/role-groups/${id}`, data),
  getRoleGroupDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/admin/rbac/role-groups/${id}/deletion-impact`),
  deleteRoleGroup: (id: number, data: ConfirmDeletionRequest) =>
    api.delete<RoleGroupResponse>(`/admin/rbac/role-groups/${id}`, { data }),

  getRole: (id: number) => api.get<RoleResponse>(`/admin/rbac/roles/${id}`),

  getRolePermissions: (id: number) =>
    api.get<RolePermissionsResponse>(`/admin/rbac/roles/${id}/permissions`),

  listPermissions: () => api.get<PermissionResponse[]>("/admin/rbac/permissions"),

  createRole: (data: CreateRoleRequest) => api.post<RoleResponse>("/admin/rbac/roles", data),

  updateRole: (id: number, data: UpdateRoleRequest) =>
    api.put<RoleResponse>(`/admin/rbac/roles/${id}`, data),

  updateRolePermissions: (id: number, data: UpdateRolePermissionsRequest) =>
    api.put<RolePermissionsResponse>(`/admin/rbac/roles/${id}/permissions`, data),

  setRoleGroup: (id: number, roleGroupId: number | null) =>
    api.put<RoleResponse>(`/admin/rbac/roles/${id}/group`, { roleGroupId }),

  updateRoleGroupBaseline: (id: number, data: UpdateRolePermissionsRequest) =>
    api.put<RoleGroupResponse>(`/admin/rbac/role-groups/${id}/baseline`, data),

  deleteRole: (id: number) => api.delete<RoleResponse>(`/admin/rbac/roles/${id}`),
};
