import { useEffect, useRef } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { PageError, PageLoading } from "@/components/PageState";
import { isAdminRole } from "@/lib/auth";
import { refreshThunk } from "@/store/authSlice";
import { useAppDispatch, useAppSelector } from "@/store";
import { useI18n } from "@/lib/i18n";

// Restoring the session is retried this many times before an error is shown.
const MAX_SESSION_CHECKS = 3;

type ProtectedRouteProps = {
  roles?: string[];
};

const normalizeRole = (role: string) => role.toUpperCase();

export default function ProtectedRoute({ roles }: ProtectedRouteProps) {
  const dispatch = useAppDispatch();
  const location = useLocation();
  const { user, accessToken, refreshToken, error, loading } = useAppSelector((state) => state.auth);
  const { t } = useI18n();
  const sessionChecks = useRef(0);

  // A check is skipped while another auth request is in flight, so try again
  // once it settles instead of waiting forever on a check that never ran.
  useEffect(() => {
    if (user || !accessToken || !refreshToken || loading || error) return;
    if (sessionChecks.current >= MAX_SESSION_CHECKS) return;
    sessionChecks.current += 1;
    void dispatch(refreshThunk());
  }, [accessToken, dispatch, error, loading, refreshToken, user]);

  if (!accessToken || !refreshToken) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (!user) {
    const retry = () => {
      sessionChecks.current = 0;
      void dispatch(refreshThunk());
    };
    if (error && !loading) return <PageError message={error} onRetry={retry} />;
    if (!loading && sessionChecks.current >= MAX_SESSION_CHECKS)
      return <PageError message={t("auth.err.unexpected")} onRetry={retry} />;
    return <PageLoading />;
  }

  const allowedRoles = roles?.map(normalizeRole);
  if (allowedRoles && !allowedRoles.includes(normalizeRole(user.role))) {
    return <Navigate to={isAdminRole(user.role) ? "/admin" : "/profile"} replace />;
  }

  return <Outlet />;
}
