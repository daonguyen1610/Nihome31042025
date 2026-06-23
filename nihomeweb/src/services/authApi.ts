import api, { withIdempotencyKey } from "@/lib/api";

// --- Types matching backend DTOs ---

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userId: number;
  phoneNumber: string;
  fullName: string;
  /** Canonical RBAC role code; falls back to the legacy enum for legacy users. */
  role: string;
  /** RBAC role id. Null only for legacy users not yet backfilled. */
  roleId?: number | null;
  isActive: boolean;
  otpRequired?: boolean;
  email?: string;
  avatarUrl?: string;
}

export interface RegisterStartResponse {
  message: string;
  phone?: string;
  email?: string;
  otpRequired: boolean;
}

export interface OtpMessageResponse {
  message: string;
}

export interface ForgotStartResponse {
  message: string;
  phone?: string;
  email?: string;
  otpRequired: boolean;
}

// --- API calls ---

export const authApi = {
  login: (phoneNumber: string, password: string) =>
    api.post<AuthResponse>("/auth/login", { phoneNumber, password }),

  registerStart: (
    phoneNumber: string,
    fullName: string,
    email: string,
    password: string,
    idempotencyKey?: string,
  ) =>
    api.post<AuthResponse | RegisterStartResponse>(
      "/auth/register/start",
      { phoneNumber, fullName, email, password },
      withIdempotencyKey(idempotencyKey),
    ),

  registerVerifyOtp: (phoneNumber: string, otpCode: string) =>
    api.post<OtpMessageResponse>("/auth/register/verify-otp", { phoneNumber, otpCode }),

  registerComplete: (phoneNumber: string, password: string, idempotencyKey?: string) =>
    api.post<AuthResponse>(
      "/auth/register/complete",
      { phoneNumber, password },
      withIdempotencyKey(idempotencyKey),
    ),

  registerResendOtp: (phoneNumber: string) =>
    api.post<OtpMessageResponse>("/auth/register/resend-otp", { phoneNumber }),

  forgotStart: (phoneNumber: string) =>
    api.post<ForgotStartResponse>("/auth/forgot/start", { phoneNumber }),

  forgotVerifyOtp: (phoneNumber: string, otpCode: string) =>
    api.post<OtpMessageResponse>("/auth/forgot/verify-otp", { phoneNumber, otpCode }),

  forgotComplete: (phoneNumber: string, newPassword: string) =>
    api.post<OtpMessageResponse>("/auth/forgot/complete", { phoneNumber, newPassword }),

  forgotResetDirect: (phoneNumber: string, newPassword: string) =>
    api.post<OtpMessageResponse>("/auth/forgot/reset-direct", { phoneNumber, newPassword }),

  forgotResendOtp: (phoneNumber: string) =>
    api.post<OtpMessageResponse>("/auth/forgot/resend-otp", { phoneNumber }),

  refresh: (refreshToken: string) =>
    api.post<AuthResponse>("/auth/refresh", { refreshToken }),

  logout: (refreshToken: string) =>
    api.post<OtpMessageResponse>("/auth/logout", { refreshToken }),
};
