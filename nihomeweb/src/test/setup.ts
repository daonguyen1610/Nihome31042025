import "@testing-library/jest-dom";
import React from "react";

// ─── Mock i18n so component tests get real English strings without API calls ──
vi.mock("@/lib/i18n", () => {
  const t: Record<string, string> = {
    // auth - shared
    "auth.phone": "Phone number",
    "auth.email": "Email",
    "auth.password": "Password",
    "auth.processing": "Processing...",
    "auth.error": "Error",
    // auth - login
    "auth.login.eyebrow": "Sign in",
    "auth.login.titleA": "Welcome",
    "auth.login.titleB": "back",
    "auth.login.desc": "Access the NICON member area.",
    "auth.login.btn": "Sign in",
    "auth.login.forgot": "Forgot password?",
    "auth.login.noAcc": "No account yet?",
    "auth.login.signup": "Sign up",
    "auth.login.demo": "Demo admin account",
    "auth.login.anyPwd": "any password",
    // auth - register
    "auth.reg.eyebrow": "Sign up",
    "auth.reg.titleA": "Join the",
    "auth.reg.titleB": "community",
    "auth.reg.desc": "Create an account to follow projects and receive news.",
    "auth.reg.fullName": "Full name",
    "auth.reg.btn": "Create account",
    "auth.reg.hasAcc": "Already have an account?",
    // auth - otp
    "auth.otp.eyebrow": "Verification",
    "auth.otp.title": "Enter OTP code",
    "auth.otp.desc": "An OTP code has been sent to",
    "auth.otp.placeholder": "OTP Code",
    "auth.otp.verify": "Verify",
    "auth.otp.resend": "Resend code",
    "auth.otp.resent": "OTP code resent",
    // auth - forgot
    "auth.forgot.eyebrow": "Forgot password",
    "auth.forgot.title": "Reset password",
    "auth.forgot.desc": "Enter your registered phone number.",
    "auth.forgot.btn": "Continue",
    "auth.forgot.backLogin": "Back to login",
    "auth.forgot.newPwdTitle": "Set new password",
    "auth.forgot.newPwd": "New password",
    "auth.forgot.resetBtn": "Reset password",
    "auth.forgot.success": "Password reset successfully",
    "auth.forgot.doneTitle": "All done!",
    "auth.forgot.doneDesc": "Your password has been changed. You can now sign in.",
  };

  return {
    I18nProvider: ({ children }: { children: React.ReactNode }) =>
      React.createElement(React.Fragment, null, children),
    useI18n: () => ({
      lang: "en" as const,
      setLang: vi.fn(),
      t: (key: string) => t[key] ?? key,
    }),
    translateError: (_t: unknown, message: string) => message,
  };
});

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => {},
  }),
});
