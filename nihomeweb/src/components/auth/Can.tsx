import type { ReactNode } from "react";
import { usePermissions } from "@/hooks/usePermissions";

interface CanProps {
  /** Single permission code (e.g. "rbac.roles.manage"). */
  permission?: string;
  /** Render children if the user has ANY of these codes. */
  anyOf?: readonly string[];
  /** Render children only if the user has ALL of these codes. */
  allOf?: readonly string[];
  /** Optional element to render while the permissions request is in flight. */
  loadingFallback?: ReactNode;
  /** Element to render when the user lacks the required permission(s). */
  fallback?: ReactNode;
  children: ReactNode;
}

/**
 * Declarative permission gate for UI elements. Hides children when the
 * current user does not satisfy the requested permission expression.
 *
 * The actual security boundary is the API (which re-checks every call);
 * this component only prevents users from seeing actions they cannot
 * perform.
 */
export function Can({
  permission,
  anyOf,
  allOf,
  loadingFallback = null,
  fallback = null,
  children,
}: CanProps) {
  const { has, hasAny, hasAll, isLoading } = usePermissions();

  if (isLoading) return <>{loadingFallback}</>;

  const allowed =
    (permission ? has(permission) : true) &&
    (anyOf ? hasAny(anyOf) : true) &&
    (allOf ? hasAll(allOf) : true);

  return <>{allowed ? children : fallback}</>;
}
