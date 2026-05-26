import { useEffect, useState } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { PageLoading } from "@/components/PageState";
import { isAdminRole } from "@/lib/auth";
import { refreshThunk } from "@/store/authSlice";
import { useAppDispatch, useAppSelector } from "@/store";

type ProtectedRouteProps = {
  roles?: string[];
};

const normalizeRole = (role: string) => role.toUpperCase();

export default function ProtectedRoute({ roles }: ProtectedRouteProps) {
  const dispatch = useAppDispatch();
  const location = useLocation();
  const { user, accessToken, refreshToken } = useAppSelector((state) => state.auth);
  const [refreshAttempted, setRefreshAttempted] = useState(false);

  useEffect(() => {
    if (!user && accessToken && refreshToken && !refreshAttempted) {
      setRefreshAttempted(true);
      void dispatch(refreshThunk());
    }
  }, [accessToken, dispatch, refreshAttempted, refreshToken, user]);

  if (!accessToken || !refreshToken) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (!user) {
    return <PageLoading />;
  }

  const allowedRoles = roles?.map(normalizeRole);
  if (allowedRoles && !allowedRoles.includes(normalizeRole(user.role))) {
    return <Navigate to={isAdminRole(user.role) ? "/admin" : "/profile"} replace />;
  }

  return <Outlet />;
}
