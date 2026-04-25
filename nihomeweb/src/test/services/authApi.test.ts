import { describe, it, expect, vi, beforeEach } from "vitest";
import api from "@/lib/api";
import { authApi } from "@/services/authApi";

vi.mock("@/lib/api", () => ({
  default: { post: vi.fn() },
}));

describe("authApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("login sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.login("0901234567", "pass123");
    expect(api.post).toHaveBeenCalledWith("/auth/login", { phoneNumber: "0901234567", password: "pass123" });
  });

  it("registerStart sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.registerStart("0901234567", "Test User", "test@test.com", "pass123");
    expect(api.post).toHaveBeenCalledWith("/auth/register/start", {
      phoneNumber: "0901234567",
      fullName: "Test User",
      email: "test@test.com",
      password: "pass123",
    });
  });

  it("registerVerifyOtp sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.registerVerifyOtp("0901234567", "123456");
    expect(api.post).toHaveBeenCalledWith("/auth/register/verify-otp", { phoneNumber: "0901234567", otpCode: "123456" });
  });

  it("registerComplete sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.registerComplete("0901234567", "pass123");
    expect(api.post).toHaveBeenCalledWith("/auth/register/complete", { phoneNumber: "0901234567", password: "pass123" });
  });

  it("registerResendOtp sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.registerResendOtp("0901234567");
    expect(api.post).toHaveBeenCalledWith("/auth/register/resend-otp", { phoneNumber: "0901234567" });
  });

  it("forgotStart sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.forgotStart("0901234567");
    expect(api.post).toHaveBeenCalledWith("/auth/forgot/start", { phoneNumber: "0901234567" });
  });

  it("forgotVerifyOtp sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.forgotVerifyOtp("0901234567", "654321");
    expect(api.post).toHaveBeenCalledWith("/auth/forgot/verify-otp", { phoneNumber: "0901234567", otpCode: "654321" });
  });

  it("forgotComplete sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.forgotComplete("0901234567", "newpass");
    expect(api.post).toHaveBeenCalledWith("/auth/forgot/complete", { phoneNumber: "0901234567", newPassword: "newpass" });
  });

  it("forgotResetDirect sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.forgotResetDirect("0901234567", "newpass");
    expect(api.post).toHaveBeenCalledWith("/auth/forgot/reset-direct", { phoneNumber: "0901234567", newPassword: "newpass" });
  });

  it("forgotResendOtp sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.forgotResendOtp("0901234567");
    expect(api.post).toHaveBeenCalledWith("/auth/forgot/resend-otp", { phoneNumber: "0901234567" });
  });

  it("refresh sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.refresh("refresh-token-123");
    expect(api.post).toHaveBeenCalledWith("/auth/refresh", { refreshToken: "refresh-token-123" });
  });

  it("logout sends correct payload", async () => {
    vi.mocked(api.post).mockResolvedValueOnce({ data: {} });
    await authApi.logout("refresh-token-123");
    expect(api.post).toHaveBeenCalledWith("/auth/logout", { refreshToken: "refresh-token-123" });
  });
});
