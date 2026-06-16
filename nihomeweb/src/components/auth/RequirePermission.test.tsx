import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import RequirePermission from "@/components/auth/RequirePermission";

const mockUsePermissions = vi.fn();
vi.mock("@/hooks/usePermissions", () => ({
  usePermissions: () => mockUsePermissions(),
}));

// i18n hook is invoked by the inline Forbidden page; stub so tests don't need
// a real I18nProvider + translation map.
vi.mock("@/lib/i18n", () => ({
  useI18n: () => ({ lang: "en", setLang: () => {}, t: (k: string) => k }),
}));

const setPerms = (
  codes: string[],
  extra: Partial<{ isLoading: boolean; isError: boolean }> = {},
) =>
  mockUsePermissions.mockReturnValue({
    permissions: new Set(codes),
    role: null,
    roleId: null,
    isLoading: extra.isLoading ?? false,
    isError: extra.isError ?? false,
    has: (c: string) => codes.includes(c),
    hasAny: (cs: readonly string[]) => cs.some((c) => codes.includes(c)),
    hasAll: (cs: readonly string[]) => cs.every((c) => codes.includes(c)),
  });

const renderAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<RequirePermission code="users.view" />}>
          <Route path="/admin/users" element={<div>admin users page</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );

describe("<RequirePermission>", () => {
  it("renders the protected route when the user has the required code", () => {
    setPerms(["users.view"]);
    renderAt("/admin/users");
    expect(screen.getByText("admin users page")).toBeInTheDocument();
    expect(screen.queryByText("forbidden.title")).not.toBeInTheDocument();
  });

  it("renders the Forbidden page when the user lacks the required code", () => {
    setPerms(["profile.me.view"]);
    renderAt("/admin/users");
    expect(screen.queryByText("admin users page")).not.toBeInTheDocument();
    expect(screen.getByText("forbidden.title")).toBeInTheDocument();
  });

  it("renders Forbidden when the permission lookup errors out", () => {
    setPerms([], { isError: true });
    renderAt("/admin/users");
    expect(screen.getByText("forbidden.title")).toBeInTheDocument();
  });

  it("renders a loading placeholder (not Forbidden) while the lookup is in flight", () => {
    setPerms([], { isLoading: true });
    renderAt("/admin/users");
    expect(screen.queryByText("admin users page")).not.toBeInTheDocument();
    expect(screen.queryByText("forbidden.title")).not.toBeInTheDocument();
  });
});
