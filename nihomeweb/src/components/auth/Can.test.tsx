import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Can } from "@/components/auth/Can";

const mockUsePermissions = vi.fn();
vi.mock("@/hooks/usePermissions", () => ({
  usePermissions: () => mockUsePermissions(),
}));

const setPerms = (codes: string[], extra: Partial<{ isLoading: boolean }> = {}) =>
  mockUsePermissions.mockReturnValue({
    permissions: new Set(codes),
    role: null,
    roleId: null,
    isLoading: extra.isLoading ?? false,
    isError: false,
    has: (c: string) => codes.includes(c),
    hasAny: (cs: readonly string[]) => cs.some((c) => codes.includes(c)),
    hasAll: (cs: readonly string[]) => cs.every((c) => codes.includes(c)),
  });

describe("<Can>", () => {
  it("renders children when the user has the required permission", () => {
    setPerms(["users.view"]);
    render(
      <Can permission="users.view">
        <span>visible</span>
      </Can>,
    );
    expect(screen.getByText("visible")).toBeInTheDocument();
  });

  it("renders fallback when permission is missing", () => {
    setPerms(["profile.me.view"]);
    render(
      <Can permission="users.manage" fallback={<span>denied</span>}>
        <span>visible</span>
      </Can>,
    );
    expect(screen.queryByText("visible")).not.toBeInTheDocument();
    expect(screen.getByText("denied")).toBeInTheDocument();
  });

  it("OR semantics: renders when the user has ANY listed permission", () => {
    setPerms(["content.news.view"]);
    render(
      <Can anyOf={["content.news.manage", "content.news.view"]}>
        <span>visible</span>
      </Can>,
    );
    expect(screen.getByText("visible")).toBeInTheDocument();
  });

  it("renders the loading fallback while permissions are in flight", () => {
    setPerms([], { isLoading: true });
    render(
      <Can permission="users.view" loadingFallback={<span>loading</span>}>
        <span>visible</span>
      </Can>,
    );
    expect(screen.getByText("loading")).toBeInTheDocument();
    expect(screen.queryByText("visible")).not.toBeInTheDocument();
  });
});
