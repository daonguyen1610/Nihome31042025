import api from "@/lib/api";
import { collectPeoplePages } from "@/lib/people";
import { withIdempotencyKey } from "@/lib/api";
import type {
  UserListResponse,
  UserDetailResponse,
  CreateUserRequest,
  UpdateUserRequest,
  RoleCatalogResponse,
  ListAuditLogParams,
  AuditLogPage,
  DeleteAuditRangeParams,
  AuditConfigDto,
} from "./types/usersAudit";

export const usersAuditApi = {
  getUsers: (params: {
    skip?: number;
    take?: number;
    search?: string;
    role?: string;
    departmentId?: number;
  }) => api.get<UserListResponse>("/users", { params }),
  getPeopleDirectory: async (params: { role?: string } = {}) => {
    const first = await api.get<UserListResponse>("/users", { params: { ...params, take: 100 } });
    const items = await collectPeoplePages(
      first.data,
      async (skip, take) =>
        (await api.get<UserListResponse>("/users", { params: { ...params, skip, take } })).data,
    );
    return {
      ...first,
      data: {
        items,
        total: first.data.total,
      },
    };
  },
  getUser: (id: number) => api.get<UserDetailResponse>(`/users/${id}`),
  createUser: (data: CreateUserRequest, idempotencyKey?: string) =>
    api.post<UserDetailResponse>("/users", data, withIdempotencyKey(idempotencyKey)),
  updateUser: (id: number, data: UpdateUserRequest, idempotencyKey?: string) =>
    api.put<UserDetailResponse>(`/users/${id}`, data, withIdempotencyKey(idempotencyKey)),
  toggleUserActive: (id: number) => api.patch<UserDetailResponse>(`/users/${id}/toggle-active`),
  deleteUser: (id: number) => api.delete(`/users/${id}`),
  hardDeleteUser: (id: number) => api.delete(`/users/${id}/hard`),
  getUserRoles: () => api.get<RoleCatalogResponse>("/users/roles"),
  listAuditLogs: (params: ListAuditLogParams = {}) => {
    const query = new URLSearchParams();
    if (params.page) query.append("page", String(params.page));
    if (params.pageSize) query.append("pageSize", String(params.pageSize));
    if (params.from) query.append("from", params.from);
    if (params.to) query.append("to", params.to);
    if (params.action) query.append("action", params.action);
    if (params.actorPhone) query.append("actorPhone", params.actorPhone);
    if (params.ip) query.append("ip", params.ip);
    if (params.status) query.append("status", params.status);
    if (params.resourceType) query.append("resourceType", params.resourceType);
    if (params.resourceId) query.append("resourceId", params.resourceId);
    if (params.correlationId) query.append("correlationId", params.correlationId);
    if (params.search) query.append("search", params.search);
    return api.get<AuditLogPage>(`/audit-logs?${query}`);
  },
  deleteAuditLog: (id: number) => api.delete(`/audit-logs/${id}`),
  deleteAuditLogRange: (params: DeleteAuditRangeParams) => {
    const query = new URLSearchParams();
    if (params.before) query.append("before", params.before);
    if (params.action) query.append("action", params.action);
    return api.delete<{ deleted: number }>(`/audit-logs?${query}`);
  },
  getAuditConfig: () => api.get<AuditConfigDto>("/audit-logs/config"),
  updateAuditConfig: (data: AuditConfigDto) => api.put<AuditConfigDto>("/audit-logs/config", data),
};
