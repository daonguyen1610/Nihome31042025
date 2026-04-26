import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent } from "@testing-library/react";
import ForgotPassword from "@/pages/ForgotPassword";
import { renderWithProviders } from "@/test/helpers/renderWithProviders";

vi.mock("@/components/layout/Layout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div data-testid="layout">{children}</div>,
}));

const defaultAuthState = {
  user: null,
  accessToken: null,
  refreshToken: null,
  loading: false,
  error: null,
  otpRequired: false,
  otpPhone: null,
  otpEmail: null,
  otpFlow: null as "register" | "forgot" | null,
  otpPassword: null,
};

describe("ForgotPassword page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("nicon_lang", "en");
  });

  describe("phone step", () => {
    it("renders phone input", () => {
      renderWithProviders(<ForgotPassword />);
      expect(screen.getByPlaceholderText("Phone number")).toBeInTheDocument();
    });

    it("renders continue button", () => {
      renderWithProviders(<ForgotPassword />);
      expect(screen.getByRole("button", { name: /continue/i })).toBeInTheDocument();
    });

    it("renders back to login link", () => {
      renderWithProviders(<ForgotPassword />);
      expect(screen.getByText("Back to login")).toBeInTheDocument();
    });

    it("allows typing phone number", () => {
      renderWithProviders(<ForgotPassword />);
      const input = screen.getByPlaceholderText("Phone number") as HTMLInputElement;
      fireEvent.change(input, { target: { value: "0901234567" } });
      expect(input.value).toBe("0901234567");
    });

    it("phone input has type=tel", () => {
      renderWithProviders(<ForgotPassword />);
      const input = screen.getByPlaceholderText("Phone number");
      expect(input).toHaveAttribute("type", "tel");
    });

    it("disables button when loading", () => {
      renderWithProviders(<ForgotPassword />, {
        preloadedState: {
          auth: { ...defaultAuthState, loading: true },
        },
      });
      expect(screen.getByRole("button")).toBeDisabled();
    });
  });

  describe("OTP step", () => {
    const otpState = {
      auth: {
        ...defaultAuthState,
        otpRequired: true,
        otpPhone: "0901234567",
        otpEmail: "test@example.com",
        otpFlow: "forgot" as const,
      },
    };

    it("shows OTP form when OTP required for forgot flow", () => {
      renderWithProviders(<ForgotPassword />, { preloadedState: otpState });
      expect(screen.getByPlaceholderText("OTP Code")).toBeInTheDocument();
    });

    it("shows the email in OTP step", () => {
      renderWithProviders(<ForgotPassword />, { preloadedState: otpState });
      expect(screen.getByText("test@example.com")).toBeInTheDocument();
    });

    it("shows verify button", () => {
      renderWithProviders(<ForgotPassword />, { preloadedState: otpState });
      expect(screen.getByRole("button", { name: /verify/i })).toBeInTheDocument();
    });

    it("shows resend button", () => {
      renderWithProviders(<ForgotPassword />, { preloadedState: otpState });
      expect(screen.getByText(/resend code/i)).toBeInTheDocument();
    });

    it("allows typing OTP code", () => {
      renderWithProviders(<ForgotPassword />, { preloadedState: otpState });
      const input = screen.getByPlaceholderText("OTP Code") as HTMLInputElement;
      fireEvent.change(input, { target: { value: "654321" } });
      expect(input.value).toBe("654321");
    });
  });

  describe("rendering", () => {
    it("renders inside Layout", () => {
      renderWithProviders(<ForgotPassword />);
      expect(screen.getByTestId("layout")).toBeInTheDocument();
    });

    it("renders title text", () => {
      renderWithProviders(<ForgotPassword />);
      expect(screen.getByText("Reset password")).toBeInTheDocument();
    });

    it("renders description text", () => {
      renderWithProviders(<ForgotPassword />);
      expect(screen.getByText("Enter your registered phone number.")).toBeInTheDocument();
    });
  });
});
