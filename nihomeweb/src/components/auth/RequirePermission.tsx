import { Outlet } from "react-router-dom";
import { PageLoading } from "@/components/PageState";
import { usePermissions } from "@/hooks/usePermissions";
import Forbidden from "@/pages/Forbidden";

export interface RequirePermissionProps {
  /**
   * Required permission code(s). OR semantics for arrays (user needs at least
   * one). Server already expands wildcards so plain set membership is enough.
   */
  code: string | readonly string[];
}

/**
 * Route guard that defers to the server-issued permission set instead of the
 * legacy `Role` enum. Renders the inline `<Forbidden />` page (no redirect)
 * when the user lacks the required permission so the URL bar still reflects
 * the page they tried to reach. Pair with a parent `<ProtectedRoute />` for
 * authentication.
 */
export default function RequirePermission({ code }: RequirePermissionProps) {
  const { hasAny, isLoading, isError } = usePermissions();
  const codes = Array.isArray(code) ? code : [code as string];

  if (isLoading) return <PageLoading />;
  if (isError || !hasAny(codes)) return <Forbidden />;
  return <Outlet />;
}
