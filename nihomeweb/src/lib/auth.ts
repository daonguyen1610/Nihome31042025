// Bridge between Redux auth state and legacy components that call getCurrentUser / logout.
// Login and Register pages now use Redux directly.

import { store } from "@/store";
import { logoutThunk, type AuthUser } from "@/store/authSlice";

export type { AuthUser } from "@/store/authSlice";

/**
 * The single public-area role code. Any user whose canonical role code is NOT
 * this is treated as an admin-area user — this covers both system admin codes
 * (`ADMIN`, `SUPER_ADMIN`) and any custom business role created via the RBAC
 * admin page. Granular permission checks (per feature) remain handled by
 * `usePermissions` / `<RequirePermission>`; this helper is intentionally a
 * coarse routing gate used by Login / Header / ProtectedRoute.
 */
const PUBLIC_ROLE_CODE = "USER";

/**
 * Returns true when the user belongs to the admin area (any role except the
 * single public `USER` role). Accepts the canonical RBAC role code returned by
 * the backend (`AuthResponse.role` / `MeResponse.role`).
 */
export const isAdminRole = (role: string | null | undefined): boolean => {
  if (!role) return false;
  return role.toUpperCase() !== PUBLIC_ROLE_CODE;
};

/**
 * Read current user from the Redux store.
 * Returns a shape compatible with the old mock (name, email, role) for backward compat.
 */
export const getCurrentUser = (): { name: string; email: string; role: "admin" | "user" } | null => {
  const user = store.getState().auth.user;
  if (!user) return null;
  return {
    name: user.fullName,
    email: user.email ?? "",
    role: isAdminRole(user.role) ? "admin" : "user",
  };
};

export const logout = () => {
  store.dispatch(logoutThunk());
};
