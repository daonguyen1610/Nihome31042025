import { useAppSelector } from "@/store";
import { usePermissions } from "@/hooks/usePermissions";

export type ConstructionScope = "execute" | "review";

export interface ScopeProps {
  projectId: number;
  scope?: ConstructionScope;
}

export interface DesignProjectScopeProps {
  designProjectId: number | null;
  designProjects?: { id: number; projectCode: string; name: string }[];
  operationalProjectId?: number | null;
  scope?: ConstructionScope;
}

interface ConstructionActionPermissions {
  /** BCH execution permission (create/manage). */
  manage?: string | string[];
  /** CM supervision permission (cm-review). Only applies in review scope. */
  supervisor?: string;
  /** Approval permission. Only applies in review scope. */
  approve?: string;
}

function hasAny(has: (permission: string) => boolean, permission?: string | string[]) {
  if (!permission) return false;
  return Array.isArray(permission) ? permission.some((item) => has(item)) : has(permission);
}

/**
 * Derives what the current user may do in a construction screen.
 *
 * `scope` is the screen context: `execute` is the BCH portal, `review` is a CM
 * area. Management is permission-based in both scopes so Phòng CM can create
 * and correct records on BCH's behalf. Deletion remains author-only unless a
 * workflow defines a separate delete permission. Approval stays review-only so a BCH
 * user never sees approve/reject actions even if a broad permission matches.
 */
export function useConstructionActions(
  scope: ConstructionScope | undefined,
  permissions: ConstructionActionPermissions,
) {
  const { has } = usePermissions();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);
  const resolvedScope = scope ?? "execute";
  const canSupervise = resolvedScope === "review" && hasAny(has, permissions.supervisor);
  const canManage = hasAny(has, permissions.manage) || canSupervise;
  const canApprove = resolvedScope === "review" && hasAny(has, permissions.approve);
  const canEditRow = (createdByUserId?: number | null) =>
    canManage && (canSupervise || (createdByUserId != null && createdByUserId === currentUserId));
  const canDeleteRow = (createdByUserId?: number | null) =>
    canManage && createdByUserId != null && createdByUserId === currentUserId;

  return { canManage, canApprove, canSupervise, canEditRow, canDeleteRow, currentUserId };
}
