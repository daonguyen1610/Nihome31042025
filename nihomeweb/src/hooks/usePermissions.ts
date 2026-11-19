import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { rbacApi, type MePermissionsResponse } from "@/services/rbacApi";
import { useAppSelector } from "@/store";

export interface UsePermissionsResult {
  permissions: ReadonlySet<string>;
  role: string | null;
  roleId: number | null;
  isLoading: boolean;
  isError: boolean;
  has: (code: string) => boolean;
  hasAny: (codes: readonly string[]) => boolean;
  hasAll: (codes: readonly string[]) => boolean;
}

const EMPTY = new Set<string>();

/**
 * Reactively reads the current user's permission codes from
 * GET /api/users/me/permissions. Cached per access token by react-query,
 * so multiple consumers on the same page share one network round-trip.
 *
 * The server already expands wildcards (SUPER_ADMIN -> every catalog code),
 * so the FE only needs straight set membership.
 *
 * The accessToken is included in the queryKey so that when the user logs out
 * and logs back in (or when the token is refreshed), the cache is invalidated
 * and fresh permissions are fetched.
 */
export function usePermissions(): UsePermissionsResult {
  const accessToken = useAppSelector((s) => s.auth.accessToken);
  const userId = useAppSelector((s) => s.auth.user?.userId ?? null);

  const query = useQuery<MePermissionsResponse>({
    queryKey: ["me", "permissions", userId, accessToken],
    queryFn: async () => (await rbacApi.getMyPermissions()).data,
    enabled: Boolean(accessToken && userId),
    staleTime: 60_000,
  });

  const set = useMemo(() => (query.data ? new Set(query.data.permissions) : EMPTY), [query.data]);
  // Stable while the permissions are unchanged: effects that depend on `has`
  // would otherwise re-run after every render (the CM email preview looped).
  const checks = useMemo(
    () => ({
      has: (code: string) => set.has(code),
      hasAny: (codes: readonly string[]) => codes.some((c) => set.has(c)),
      hasAll: (codes: readonly string[]) => codes.every((c) => set.has(c)),
    }),
    [set],
  );

  return {
    permissions: set,
    role: query.data?.role ?? null,
    roleId: query.data?.roleId ?? null,
    isLoading: query.isLoading,
    isError: query.isError,
    ...checks,
  };
}
