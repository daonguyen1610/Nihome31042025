import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock the store module before importing auth
vi.mock("@/store", () => {
  const mockDispatch = vi.fn(() => Promise.resolve());
  return {
    store: {
      getState: vi.fn(() => ({
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
        },
      })),
      dispatch: mockDispatch,
    },
  };
});

vi.mock("@/store/authSlice", () => ({
  logoutThunk: vi.fn(() => ({ type: "auth/logout" })),
}));

import { getCurrentUser, logout, isAdminRole } from "@/lib/auth";
import { store } from "@/store";
import { logoutThunk } from "@/store/authSlice";

describe("auth bridge", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("getCurrentUser", () => {
    it("returns null when no user in store", () => {
      vi.mocked(store.getState).mockReturnValue({
        auth: { user: null, accessToken: null, refreshToken: null },
      } as never);
      expect(getCurrentUser()).toBeNull();
    });

    it("returns mapped user with backward-compat shape", () => {
      vi.mocked(store.getState).mockReturnValue({
        auth: {
          user: {
            userId: 1,
            phoneNumber: "0901234567",
            fullName: "Test User",
            email: "test@example.com",
            role: "ADMIN",
            isActive: true,
          },
        },
      } as never);
      const user = getCurrentUser();
      expect(user).toEqual({
        name: "Test User",
        email: "test@example.com",
        role: "admin",
      });
    });

    it("maps SUPER_ADMIN to admin", () => {
      vi.mocked(store.getState).mockReturnValue({
        auth: {
          user: {
            userId: 1,
            phoneNumber: "0901234567",
            fullName: "Super Admin",
            email: "sa@test.com",
            role: "SUPER_ADMIN",
            isActive: true,
          },
        },
      } as never);
      const user = getCurrentUser();
      expect(user?.role).toBe("admin");
    });

    it("maps non-admin role to 'user'", () => {
      vi.mocked(store.getState).mockReturnValue({
        auth: {
          user: {
            userId: 2,
            phoneNumber: "0901234567",
            fullName: "Regular",
            email: "r@b.com",
            role: "USER",
            isActive: true,
          },
        },
      } as never);
      const user = getCurrentUser();
      expect(user?.role).toBe("user");
    });

    it("returns empty string for email when undefined", () => {
      vi.mocked(store.getState).mockReturnValue({
        auth: {
          user: {
            userId: 3,
            phoneNumber: "0901234567",
            fullName: "No Email",
            role: "USER",
            isActive: true,
          },
        },
      } as never);
      const user = getCurrentUser();
      expect(user?.email).toBe("");
    });
  });

  describe("isAdminRole", () => {
    it("returns true for ADMIN", () => {
      expect(isAdminRole("ADMIN")).toBe(true);
    });

    it("returns true for SUPER_ADMIN", () => {
      expect(isAdminRole("SUPER_ADMIN")).toBe(true);
    });

    it("returns true case-insensitive", () => {
      expect(isAdminRole("admin")).toBe(true);
      expect(isAdminRole("super_admin")).toBe(true);
    });

    it("returns false for USER", () => {
      expect(isAdminRole("USER")).toBe(false);
    });

    it("returns false for arbitrary role", () => {
      expect(isAdminRole("member")).toBe(false);
    });
  });

  describe("logout", () => {
    it("dispatches logoutThunk", () => {
      logout();
      expect(store.dispatch).toHaveBeenCalledWith(logoutThunk());
    });
  });
});
