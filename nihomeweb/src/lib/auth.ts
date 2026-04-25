// Bridge between Redux auth state and legacy components that call getCurrentUser / logout.
// Login and Register pages now use Redux directly.

import { store } from "@/store";
import { logoutThunk, type AuthUser } from "@/store/authSlice";

export type { AuthUser } from "@/store/authSlice";

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
    role: user.role === "admin" ? "admin" : "user",
  };
};

export const logout = () => {
  store.dispatch(logoutThunk());
};
