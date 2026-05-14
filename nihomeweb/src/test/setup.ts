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
    "auth.otp.desc": "An OTP code has been sent to email",
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
    // common
    "common.all": "All",
    "common.search": "Search",
    "common.save": "Save",
    "common.cancel": "Cancel",
    "common.edit": "Edit",
    "common.delete": "Delete",
    "common.view": "View",
    "common.new": "New",
    "common.actions": "Actions",
    "common.retry": "Retry",
    "common.showing": "showing",
    "common.readMore": "Read more",
    "common.viewAll": "View all",
    "common.viewDetail": "View detail",
    "common.back": "Back",
    "common.notFound": "Not found",
    "common.noResults": "No results found",
    "common.error": "An error occurred",
    // form
    "form.confirmDelete": "Are you sure?",
    "form.deleted": "Deleted",
    "form.created": "Created",
    "form.updated": "Updated",
    "form.create": "Create",
    "form.update": "Update",
    "form.back": "Back",
    "form.required": "Required",
    // not found
    "nf.oops": "Oops! Page not found",
    "nf.home": "Return to home",
    // activities page
    "actPage.eyebrow": "Activities",
    "actPage.title": "Latest Activities",
    "actPage.desc": "Stay updated with our latest activities.",
    "actPage.empty": "No activities found.",
    // news page
    "newsPage.eyebrow": "News",
    "newsPage.title": "Latest News",
    "newsPage.desc": "Stay updated with industry news.",
    "newsPage.empty": "No news found.",
    // projects page
    "proj.title": "Projects",
    "proj.add": "Add project",
    "proj.ongoing": "Ongoing",
    "proj.completed": "Completed",
    "proj.empty": "No projects found.",
    // processes
    "proc.add": "Add process",
    "proc.searchPh": "Search by title or code...",
    "proc.empty": "No processes yet.",
    "proc.title": "Process title",
    "proc.code": "Process code",
    "proc.view": "View details",
    "proc.edit": "Edit process",
    "proc.download": "Download",
    "proc.uploadImage": "Upload image",
    "proc.uploadFile": "Upload file",
    "proc.images": "Images",
    "proc.files": "Files",
    "proc.noImages": "No process images yet.",
    "proc.noFiles": "No downloadable files yet.",
    "proc.save": "Save",
    "proc.cancel": "Cancel",
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
