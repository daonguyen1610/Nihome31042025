import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent } from "@testing-library/react";
import Login from "@/pages/Login";
import { renderWithProviders } from "@/test/helpers/renderWithProviders";

// Mock Layout to avoid rendering Header/Footer dependencies
vi.mock("@/components/layout/Layout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div data-testid="layout">{children}</div>,
}));

describe("Login page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("nicon_lang", "en");
  });

  it("renders phone and password inputs", () => {
    renderWithProviders(<Login />);
    expect(screen.getByPlaceholderText("Phone number")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Password")).toBeInTheDocument();
  });

  it("renders sign in button", () => {
    renderWithProviders(<Login />);
    expect(screen.getByRole("button", { name: /sign in/i })).toBeInTheDocument();
  });

  it("renders forgot password link", () => {
    renderWithProviders(<Login />);
    expect(screen.getByText("Forgot password?")).toBeInTheDocument();
  });

  it("renders sign up link", () => {
    renderWithProviders(<Login />);
    expect(screen.getByText("Sign up")).toBeInTheDocument();
  });

  it("allows typing in phone and password fields", () => {
    renderWithProviders(<Login />);
    const phoneInput = screen.getByPlaceholderText("Phone number") as HTMLInputElement;
    const passInput = screen.getByPlaceholderText("Password") as HTMLInputElement;
    fireEvent.change(phoneInput, { target: { value: "0901234567" } });
    fireEvent.change(passInput, { target: { value: "password123" } });
    expect(phoneInput.value).toBe("0901234567");
    expect(passInput.value).toBe("password123");
  });

  it("disables button when loading", () => {
    renderWithProviders(<Login />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: true,
          error: null,
          otpRequired: false,
          otpPhone: null,
          otpFlow: null,
          otpPassword: null,
        },
      },
    });
    expect(screen.getByRole("button")).toBeDisabled();
    expect(screen.getByText("Processing...")).toBeInTheDocument();
  });

  it("phone input has type=tel", () => {
    renderWithProviders(<Login />);
    const phoneInput = screen.getByPlaceholderText("Phone number");
    expect(phoneInput).toHaveAttribute("type", "tel");
  });

  it("password input has type=password", () => {
    renderWithProviders(<Login />);
    const passInput = screen.getByPlaceholderText("Password");
    expect(passInput).toHaveAttribute("type", "password");
  });
});
