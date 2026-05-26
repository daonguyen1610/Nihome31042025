import { describe, expect, it } from "vitest";
import { Route, Routes } from "react-router-dom";
import { screen } from "@testing-library/react";
import ProtectedRoute from "@/components/auth/ProtectedRoute";
import { renderWithProviders } from "@/test/helpers/renderWithProviders";

const authState = {
  accessToken: null,
  refreshToken: null,
  user: null,
  loading: false,
  error: null,
  otpRequired: false,
  otpEmail: null,
  otpPhone: null,
  otpFlow: null,
  otpPassword: null,
};

describe("ProtectedRoute", () => {
  it("redirects to login when no token exists", () => {
    renderWithProviders(
      <Routes>
        <Route element={<ProtectedRoute roles={["ADMIN"]} />}>
          <Route path="/admin" element={<div>Admin</div>} />
        </Route>
        <Route path="/login" element={<div>Login page</div>} />
      </Routes>,
      { route: "/admin" },
    );

    expect(screen.getByText("Login page")).toBeInTheDocument();
  });

  it("renders child route when role is allowed", () => {
    renderWithProviders(
      <Routes>
        <Route element={<ProtectedRoute roles={["SUPER_ADMIN"]} />}>
          <Route path="/admin/users" element={<div>User admin</div>} />
        </Route>
      </Routes>,
      {
        route: "/admin/users",
        preloadedState: {
          auth: {
            ...authState,
            accessToken: "access",
            refreshToken: "refresh",
            user: {
              userId: 1,
              phoneNumber: "0901234567",
              fullName: "Super Admin",
              role: "SUPER_ADMIN",
              isActive: true,
            },
          },
        },
      },
    );

    expect(screen.getByText("User admin")).toBeInTheDocument();
  });

  it("redirects admins away from super-admin-only routes", () => {
    renderWithProviders(
      <Routes>
        <Route path="/admin" element={<div>Dashboard</div>} />
        <Route element={<ProtectedRoute roles={["SUPER_ADMIN"]} />}>
          <Route path="/admin/users" element={<div>User admin</div>} />
        </Route>
      </Routes>,
      {
        route: "/admin/users",
        preloadedState: {
          auth: {
            ...authState,
            accessToken: "access",
            refreshToken: "refresh",
            user: {
              userId: 2,
              phoneNumber: "0901234568",
              fullName: "Admin",
              role: "ADMIN",
              isActive: true,
            },
          },
        },
      },
    );

    expect(screen.getByText("Dashboard")).toBeInTheDocument();
  });
});
