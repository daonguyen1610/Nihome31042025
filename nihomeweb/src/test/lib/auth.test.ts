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

import { getCurrentUser, logout } from "@/lib/auth";
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
            role: "admin",
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

    it("maps non-admin role to 'user'", () => {
      vi.mocked(store.getState).mockReturnValue({
        auth: {
          user: {
            userId: 2,
            phoneNumber: "0901234567",
            fullName: "Regular",
            email: "r@b.com",
            role: "member",
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
            role: "user",
            isActive: true,
          },
        },
      } as never);
      const user = getCurrentUser();
      expect(user?.email).toBe("");
    });
  });

  describe("logout", () => {
    it("dispatches logoutThunk", () => {
      logout();
      expect(store.dispatch).toHaveBeenCalledWith(logoutThunk());
    });
  });
});
