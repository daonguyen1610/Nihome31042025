import { describe, it, expect, vi, beforeEach } from "vitest";
import { configureStore } from "@reduxjs/toolkit";
import authReducer, {
  loginThunk,
  registerStartThunk,
  registerVerifyOtpThunk,
  registerCompleteThunk,
  resendRegisterOtpThunk,
  forgotStartThunk,
  forgotVerifyOtpThunk,
  forgotCompleteThunk,
  forgotResetDirectThunk,
  resendForgotOtpThunk,
  logoutThunk,
  refreshThunk,
  clearError,
  clearOtpFlow,
  setUser,
} from "@/store/authSlice";
import type { AuthResponse } from "@/services/authApi";

// Mock the authApi module
vi.mock("@/services/authApi", () => ({
  authApi: {
    login: vi.fn(),
    registerStart: vi.fn(),
    registerVerifyOtp: vi.fn(),
    registerComplete: vi.fn(),
    registerResendOtp: vi.fn(),
    forgotStart: vi.fn(),
    forgotVerifyOtp: vi.fn(),
    forgotComplete: vi.fn(),
    forgotResetDirect: vi.fn(),
    forgotResendOtp: vi.fn(),
    refresh: vi.fn(),
    logout: vi.fn(),
  },
}));

import { authApi } from "@/services/authApi";

const mockAuthResponse: AuthResponse = {
  accessToken: "access-123",
  refreshToken: "refresh-456",
  expiresAt: "2026-12-31T00:00:00Z",
  userId: 1,
  phoneNumber: "0901234567",
  fullName: "Test User",
  role: "user",
  isActive: true,
  email: "test@example.com",
};

function createTestStore() {
  return configureStore({ reducer: { auth: authReducer } });
}

function clearCookies() {
  document.cookie.split(";").forEach((c) => {
    const name = c.split("=")[0].trim();
    if (name) document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
  });
}

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

describe("authSlice", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearCookies();
  });

  // ===== Reducers =====

  describe("reducers", () => {
    it("clearError resets error to null", () => {
      const store = createTestStore();
      // Force an error state via a rejected login
      vi.mocked(authApi.login).mockRejectedValueOnce(new Error("fail"));
      return store.dispatch(loginThunk({ phone: "123", password: "pwd" })).then(() => {
        store.dispatch(clearError());
        expect(store.getState().auth.error).toBeNull();
      });
    });

    it("clearOtpFlow resets OTP-related state", () => {
      const store = createTestStore();
      // Simulate OTP state
      vi.mocked(authApi.registerStart).mockResolvedValueOnce({
        data: { message: "OTP sent", otpRequired: true },
      } as never);
      return store
        .dispatch(registerStartThunk({ phone: "0901234567", fullName: "User", email: "a@b.com", password: "pass123" }))
        .then(() => {
          expect(store.getState().auth.otpRequired).toBe(true);
          store.dispatch(clearOtpFlow());
          const s = store.getState().auth;
          expect(s.otpRequired).toBe(false);
          expect(s.otpEmail).toBeNull();
          expect(s.otpFlow).toBeNull();
          expect(s.otpPassword).toBeNull();
        });
    });

    it("setUser sets the user", () => {
      const store = createTestStore();
      const user = { userId: 1, phoneNumber: "123", fullName: "Test", role: "admin", isActive: true };
      store.dispatch(setUser(user));
      expect(store.getState().auth.user).toEqual(user);
    });

    it("setUser(null) clears the user", () => {
      const store = createTestStore();
      store.dispatch(setUser({ userId: 1, phoneNumber: "123", fullName: "Test", role: "admin", isActive: true }));
      store.dispatch(setUser(null));
      expect(store.getState().auth.user).toBeNull();
    });
  });

  // ===== Login =====

  describe("loginThunk", () => {
    it("sets loading=true on pending", () => {
      // Never resolve to keep it pending
      vi.mocked(authApi.login).mockReturnValueOnce(new Promise(() => {}) as never);
      const store = createTestStore();
      store.dispatch(loginThunk({ phone: "0901234567", password: "pass123" }));
      expect(store.getState().auth.loading).toBe(true);
      expect(store.getState().auth.error).toBeNull();
    });

    it("sets user and tokens on success", async () => {
      vi.mocked(authApi.login).mockResolvedValueOnce({ data: mockAuthResponse } as never);
      const store = createTestStore();
      await store.dispatch(loginThunk({ phone: "0901234567", password: "pass123" }));
      const s = store.getState().auth;
      expect(s.loading).toBe(false);
      expect(s.user?.fullName).toBe("Test User");
      expect(s.user?.phoneNumber).toBe("0901234567");
      expect(s.accessToken).toBe("access-123");
      expect(s.refreshToken).toBe("refresh-456");
    });

    it("persists tokens to cookies on success", async () => {
      vi.mocked(authApi.login).mockResolvedValueOnce({ data: mockAuthResponse } as never);
      const store = createTestStore();
      await store.dispatch(loginThunk({ phone: "0901234567", password: "pass123" }));
      expect(getCookie("nicon_access_token")).toBe("access-123");
      expect(getCookie("nicon_refresh_token")).toBe("refresh-456");
    });

    it("sets error on failure", async () => {
      vi.mocked(authApi.login).mockRejectedValueOnce(new Error("Invalid credentials"));
      const store = createTestStore();
      await store.dispatch(loginThunk({ phone: "0901234567", password: "wrong" }));
      const s = store.getState().auth;
      expect(s.loading).toBe(false);
      expect(s.error).toBe("An unexpected error occurred");
      expect(s.user).toBeNull();
    });

    it("extracts error message from Axios error response", async () => {
      const axiosError = {
        isAxiosError: true,
        response: { data: { message: "Invalid phone or password" } },
        message: "Request failed",
      };
      // Mark it as an AxiosError
      Object.defineProperty(axiosError, "isAxiosError", { value: true });
      vi.mocked(authApi.login).mockRejectedValueOnce(axiosError);
      const store = createTestStore();
      await store.dispatch(loginThunk({ phone: "0901234567", password: "wrong" }));
      expect(store.getState().auth.error).toBe("Invalid phone or password");
    });
  });

  // ===== Register Start =====

  describe("registerStartThunk", () => {
    it("sets user directly when OTP is disabled (response contains accessToken)", async () => {
      vi.mocked(authApi.registerStart).mockResolvedValueOnce({ data: mockAuthResponse } as never);
      const store = createTestStore();
      await store.dispatch(registerStartThunk({ phone: "0901234567", fullName: "User", email: "a@b.com", password: "pass123" }));
      const s = store.getState().auth;
      expect(s.user?.fullName).toBe("Test User");
      expect(s.accessToken).toBe("access-123");
      expect(s.otpRequired).toBe(false);
    });

    it("sets OTP flow state when OTP is required", async () => {
      vi.mocked(authApi.registerStart).mockResolvedValueOnce({
        data: { message: "OTP sent", otpRequired: true },
      } as never);
      const store = createTestStore();
      await store.dispatch(registerStartThunk({ phone: "0901234567", fullName: "User", email: "a@b.com", password: "pass123" }));
      const s = store.getState().auth;
      expect(s.otpRequired).toBe(true);
      expect(s.otpEmail).toBe("a@b.com");
      expect(s.otpPhone).toBe("0901234567");
      expect(s.otpFlow).toBe("register");
      expect(s.otpPassword).toBe("pass123");
      expect(s.user).toBeNull();
    });

    it("sets error on failure", async () => {
      vi.mocked(authApi.registerStart).mockRejectedValueOnce(new Error("Phone already registered"));
      const store = createTestStore();
      await store.dispatch(registerStartThunk({ phone: "0901234567", fullName: "User", email: "a@b.com", password: "pass123" }));
      expect(store.getState().auth.error).toBeTruthy();
      expect(store.getState().auth.loading).toBe(false);
    });
  });

  // ===== Register Verify OTP =====

  describe("registerVerifyOtpThunk", () => {
    it("sets loading false on success", async () => {
      vi.mocked(authApi.registerVerifyOtp).mockResolvedValueOnce({ data: { message: "OK" } } as never);
      const store = createTestStore();
      await store.dispatch(registerVerifyOtpThunk({ phone: "0901234567", otpCode: "123456" }));
      expect(store.getState().auth.loading).toBe(false);
      expect(store.getState().auth.error).toBeNull();
    });

    it("sets error on invalid OTP", async () => {
      vi.mocked(authApi.registerVerifyOtp).mockRejectedValueOnce(new Error("Invalid OTP"));
      const store = createTestStore();
      await store.dispatch(registerVerifyOtpThunk({ phone: "0901234567", otpCode: "000000" }));
      expect(store.getState().auth.error).toBeTruthy();
    });
  });

  // ===== Register Complete =====

  describe("registerCompleteThunk", () => {
    it("sets user and clears OTP state on success", async () => {
      vi.mocked(authApi.registerComplete).mockResolvedValueOnce({ data: mockAuthResponse } as never);
      const store = createTestStore();
      await store.dispatch(registerCompleteThunk({ phone: "0901234567", password: "pass123" }));
      const s = store.getState().auth;
      expect(s.user?.fullName).toBe("Test User");
      expect(s.accessToken).toBe("access-123");
      expect(s.otpRequired).toBe(false);
      expect(s.otpEmail).toBeNull();
      expect(s.otpFlow).toBeNull();
      expect(s.otpPassword).toBeNull();
    });
  });

  // ===== Resend Register OTP =====

  describe("resendRegisterOtpThunk", () => {
    it("does not change state on success", async () => {
      vi.mocked(authApi.registerResendOtp).mockResolvedValueOnce({ data: { message: "OTP resent" } } as never);
      const store = createTestStore();
      const before = store.getState().auth;
      await store.dispatch(resendRegisterOtpThunk("0901234567"));
      expect(store.getState().auth.error).toBe(before.error);
    });

    it("sets error on failure", async () => {
      vi.mocked(authApi.registerResendOtp).mockRejectedValueOnce(new Error("Rate limited"));
      const store = createTestStore();
      await store.dispatch(resendRegisterOtpThunk("0901234567"));
      expect(store.getState().auth.error).toBeTruthy();
    });
  });

  // ===== Forgot Password Start =====

  describe("forgotStartThunk", () => {
    it("sets OTP flow when OTP is required", async () => {
      vi.mocked(authApi.forgotStart).mockResolvedValueOnce({
        data: { message: "OTP sent", otpRequired: true },
      } as never);
      const store = createTestStore();
      await store.dispatch(forgotStartThunk("0901234567"));
      const s = store.getState().auth;
      expect(s.otpRequired).toBe(true);
      expect(s.otpEmail).toBe("0901234567");
      expect(s.otpFlow).toBe("forgot");
    });

    it("does not set OTP flow when OTP is disabled", async () => {
      vi.mocked(authApi.forgotStart).mockResolvedValueOnce({
        data: { message: "Reset directly", otpRequired: false },
      } as never);
      const store = createTestStore();
      await store.dispatch(forgotStartThunk("0901234567"));
      const s = store.getState().auth;
      expect(s.otpRequired).toBe(false);
      expect(s.otpFlow).toBeNull();
    });

    it("sets error on failure", async () => {
      vi.mocked(authApi.forgotStart).mockRejectedValueOnce(new Error("Phone not found"));
      const store = createTestStore();
      await store.dispatch(forgotStartThunk("0000000000"));
      expect(store.getState().auth.error).toBeTruthy();
    });
  });

  // ===== Forgot Verify OTP =====

  describe("forgotVerifyOtpThunk", () => {
    it("sets loading false on success", async () => {
      vi.mocked(authApi.forgotVerifyOtp).mockResolvedValueOnce({ data: { message: "OK" } } as never);
      const store = createTestStore();
      await store.dispatch(forgotVerifyOtpThunk({ phone: "0901234567", otpCode: "123456" }));
      expect(store.getState().auth.loading).toBe(false);
    });

    it("sets error on invalid OTP", async () => {
      vi.mocked(authApi.forgotVerifyOtp).mockRejectedValueOnce(new Error("Invalid OTP"));
      const store = createTestStore();
      await store.dispatch(forgotVerifyOtpThunk({ phone: "0901234567", otpCode: "000000" }));
      expect(store.getState().auth.error).toBeTruthy();
    });
  });

  // ===== Forgot Complete =====

  describe("forgotCompleteThunk", () => {
    it("clears OTP state on success", async () => {
      vi.mocked(authApi.forgotComplete).mockResolvedValueOnce({ data: { message: "Password reset" } } as never);
      const store = createTestStore();
      await store.dispatch(forgotCompleteThunk({ phone: "0901234567", newPassword: "newpass" }));
      const s = store.getState().auth;
      expect(s.otpRequired).toBe(false);
      expect(s.otpEmail).toBeNull();
      expect(s.otpFlow).toBeNull();
      expect(s.loading).toBe(false);
    });

    it("sets error on failure", async () => {
      vi.mocked(authApi.forgotComplete).mockRejectedValueOnce(new Error("fail"));
      const store = createTestStore();
      await store.dispatch(forgotCompleteThunk({ phone: "0901234567", newPassword: "short" }));
      expect(store.getState().auth.error).toBeTruthy();
    });
  });

  // ===== Forgot Reset Direct =====

  describe("forgotResetDirectThunk", () => {
    it("sets loading false on success", async () => {
      vi.mocked(authApi.forgotResetDirect).mockResolvedValueOnce({ data: { message: "Reset done" } } as never);
      const store = createTestStore();
      await store.dispatch(forgotResetDirectThunk({ phone: "0901234567", newPassword: "newpass" }));
      expect(store.getState().auth.loading).toBe(false);
      expect(store.getState().auth.error).toBeNull();
    });
  });

  // ===== Resend Forgot OTP =====

  describe("resendForgotOtpThunk", () => {
    it("sets error on failure", async () => {
      vi.mocked(authApi.forgotResendOtp).mockRejectedValueOnce(new Error("Rate limited"));
      const store = createTestStore();
      await store.dispatch(resendForgotOtpThunk("0901234567"));
      expect(store.getState().auth.error).toBeTruthy();
    });
  });

  // ===== Refresh =====

  describe("refreshThunk", () => {
    it("updates tokens and user on success", async () => {
      const newAuth = { ...mockAuthResponse, accessToken: "new-access", refreshToken: "new-refresh" };
      vi.mocked(authApi.refresh).mockResolvedValueOnce({ data: newAuth } as never);
      // Pre-set a refresh token
      const store = configureStore({
        reducer: { auth: authReducer },
        preloadedState: { auth: { user: null, accessToken: "old", refreshToken: "old-refresh", loading: false, error: null, otpRequired: false, otpPhone: null, otpEmail: null, otpFlow: null, otpPassword: null } },
      });
      await store.dispatch(refreshThunk());
      const s = store.getState().auth;
      expect(s.accessToken).toBe("new-access");
      expect(s.refreshToken).toBe("new-refresh");
      expect(s.user?.fullName).toBe("Test User");
    });

    it("rejects when no refresh token exists", async () => {
      const store = createTestStore();
      const result = await store.dispatch(refreshThunk());
      expect(result.meta.requestStatus).toBe("rejected");
    });

    it("clears tokens on failure", async () => {
      vi.mocked(authApi.refresh).mockRejectedValueOnce(new Error("Expired"));
      const store = configureStore({
        reducer: { auth: authReducer },
        preloadedState: { auth: { user: null, accessToken: "a", refreshToken: "r", loading: false, error: null, otpRequired: false, otpPhone: null, otpEmail: null, otpFlow: null, otpPassword: null } },
      });
      await store.dispatch(refreshThunk());
      expect(store.getState().auth.user).toBeNull();
      expect(store.getState().auth.accessToken).toBeNull();
      expect(getCookie("nicon_access_token")).toBeNull();
    });
  });

  // ===== Logout =====

  describe("logoutThunk", () => {
    it("clears all state and cookies", async () => {
      vi.mocked(authApi.logout).mockResolvedValueOnce({ data: { message: "OK" } } as never);
      const store = configureStore({
        reducer: { auth: authReducer },
        preloadedState: {
          auth: {
            user: { userId: 1, phoneNumber: "123", fullName: "Test", role: "user", isActive: true },
            accessToken: "a",
            refreshToken: "r",
            loading: false,
            error: null,
            otpRequired: true,
            otpPhone: "123",
            otpEmail: "test@example.com",
            otpFlow: "register" as const,
            otpPassword: "pwd",
          },
        },
      });
      await store.dispatch(logoutThunk());
      const s = store.getState().auth;
      expect(s.user).toBeNull();
      expect(s.accessToken).toBeNull();
      expect(s.refreshToken).toBeNull();
      expect(s.otpRequired).toBe(false);
      expect(s.otpEmail).toBeNull();
      expect(getCookie("nicon_access_token")).toBeNull();
    });

    it("does not throw when logout API fails", async () => {
      vi.mocked(authApi.logout).mockRejectedValueOnce(new Error("Network error"));
      const store = configureStore({
        reducer: { auth: authReducer },
        preloadedState: { auth: { user: null, accessToken: "a", refreshToken: "r", loading: false, error: null, otpRequired: false, otpPhone: null, otpEmail: null, otpFlow: null, otpPassword: null } },
      });
      await store.dispatch(logoutThunk());
      // Should still clear state
      expect(store.getState().auth.refreshToken).toBeNull();
    });

    it("skips API call when no refresh token", async () => {
      const store = createTestStore();
      await store.dispatch(logoutThunk());
      expect(authApi.logout).not.toHaveBeenCalled();
    });
  });
});
