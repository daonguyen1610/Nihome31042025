export type UserRole = (string & {}) | "SUPER_ADMIN" | "ADMIN" | "USER";

export interface UserListItemResponse {
  id: number;
  phoneNumber: string;
  fullName?: string;
  email?: string;
  /** Canonical RBAC role code. Falls back to the legacy enum for legacy users. */
  role: UserRole;
  /** RBAC role id. Null only for legacy users not yet backfilled. */
  roleId?: number | null;
  /** Human-readable role name from the `roles` table. Null for legacy users. */
  roleName?: string | null;
  roleLabelKey?: string | null;
  departmentId?: number | null;
  departmentLabelKey?: string | null;
  isActive: boolean;
  avatarUrl?: string;
}

export interface UserDetailResponse extends UserListItemResponse {
  refreshTokenCount: number;
}

export interface UserListResponse {
  items: UserListItemResponse[];
  total: number;
}

export interface CreateUserRequest {
  phoneNumber: string;
  fullName: string;
  email: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  fullName?: string;
  email?: string;
  role?: UserRole;
  isActive?: boolean;
}

export interface RoleMetadataResponse {
  role: UserRole;
  labelKey: string;
  descriptionKey: string;
  userCount: number;
  isSystemRole: boolean;
}

export interface PermissionMatrixRowResponse {
  moduleKey: string;
  accessByRole: Record<UserRole, boolean>;
}

export interface RoleCatalogResponse {
  roles: RoleMetadataResponse[];
  permissionMatrix: PermissionMatrixRowResponse[];
}

export interface AuditLogItem {
  id: number;
  auditId: string;
  createdAt: string;
  action: string;
  resourceType: string;
  resourceId: string | null;
  message: string;
  actorUserId: number | null;
  actorPhone: string | null;
  actorRole: string | null;
  actorType: string;
  sourceSystem: string;
  targetSystem: string | null;
  channel: string;
  ipAddress: string | null;
  userAgent: string | null;
  status: string;
  failureReason: string | null;
  correlationId: string | null;
  requestId: string | null;
  oldValueJson: string | null;
  newValueJson: string | null;
  metadataJson: string | null;
}

export interface AuditLogPage {
  page: number;
  pageSize: number;
  total: number;
  actions: string[];
  items: AuditLogItem[];
}

export interface AuditConfigDto {
  retentionMinutes: number;
}

export interface ListAuditLogParams {
  page?: number;
  pageSize?: number;
  from?: string;
  to?: string;
  action?: string;
  actorPhone?: string;
  ip?: string;
  status?: string;
  resourceType?: string;
  resourceId?: string;
  correlationId?: string;
  search?: string;
}

export interface DeleteAuditRangeParams {
  before?: string;
  action?: string;
}
