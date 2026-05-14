import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, fireEvent, act } from "@testing-library/react";
import Register from "@/pages/Register";
import { renderWithProviders } from "@/test/helpers/renderWithProviders";

const { mockRegisterResendOtp } = vi.hoisted(() => ({
  mockRegisterResendOtp: vi.fn(),
}));

vi.mock("@/services/authApi", () => ({
  authApi: {
    registerResendOtp: mockRegisterResendOtp,
  },
}));

vi.mock("@/components/layout/Layout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div data-testid="layout">{children}</div>,
}));

describe("Register page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("nicon_lang", "en");
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders registration form fields", () => {
    renderWithProviders(<Register />);
    expect(screen.getByPlaceholderText("Full name")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Phone number")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Email")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Password")).toBeInTheDocument();
  });

  it("renders create account button", () => {
    renderWithProviders(<Register />);
    expect(screen.getByRole("button", { name: /create account/i })).toBeInTheDocument();
  });

  it("renders sign in link", () => {
    renderWithProviders(<Register />);
    expect(screen.getByText("Sign in")).toBeInTheDocument();
  });

  it("allows typing in all fields", () => {
    renderWithProviders(<Register />);
    const name = screen.getByPlaceholderText("Full name") as HTMLInputElement;
    const phone = screen.getByPlaceholderText("Phone number") as HTMLInputElement;
    const email = screen.getByPlaceholderText("Email") as HTMLInputElement;
    const pass = screen.getByPlaceholderText("Password") as HTMLInputElement;

    fireEvent.change(name, { target: { value: "Test User" } });
    fireEvent.change(phone, { target: { value: "0901234567" } });
    fireEvent.change(email, { target: { value: "test@test.com" } });
    fireEvent.change(pass, { target: { value: "pass123" } });

    expect(name.value).toBe("Test User");
    expect(phone.value).toBe("0901234567");
    expect(email.value).toBe("test@test.com");
    expect(pass.value).toBe("pass123");
  });

  it("disables button when loading", () => {
    renderWithProviders(<Register />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: true,
          error: null,
          otpRequired: false,
          otpPhone: null,
          otpEmail: null,
          otpFlow: null,
          otpPassword: null,
        },
      },
    });
    expect(screen.getByRole("button")).toBeDisabled();
  });

  it("shows OTP form with resend countdown when otpRequired and otpFlow is register", () => {
    vi.useFakeTimers();
    renderWithProviders(<Register />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: false,
          error: null,
          otpRequired: true,
          otpPhone: "0901234567",
          otpEmail: "test@example.com",
          otpFlow: "register" as const,
          otpPassword: "pass123",
        },
      },
    });
    expect(screen.getByPlaceholderText("OTP Code")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /verify/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /resend in 05:00/i })).toBeDisabled();
    // Registration form fields should not be visible
    expect(screen.queryByPlaceholderText("Full name")).not.toBeInTheDocument();
  });

  it("enables resend when the countdown reaches zero", () => {
    vi.useFakeTimers();
    renderWithProviders(<Register />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: false,
          error: null,
          otpRequired: true,
          otpPhone: "0901234567",
          otpEmail: "test@example.com",
          otpFlow: "register" as const,
          otpPassword: "pass123",
        },
      },
    });

    act(() => {
      vi.advanceTimersByTime(300_000);
    });

    expect(screen.getByRole("button", { name: /resend code/i })).not.toBeDisabled();
  });

  it("restarts countdown after a successful resend", async () => {
    vi.useFakeTimers();
    mockRegisterResendOtp.mockResolvedValueOnce({ data: { message: "OTP resent" } });
    renderWithProviders(<Register />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: false,
          error: null,
          otpRequired: true,
          otpPhone: "0901234567",
          otpEmail: "test@example.com",
          otpFlow: "register" as const,
          otpPassword: "pass123",
        },
      },
    });

    act(() => {
      vi.advanceTimersByTime(300_000);
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /resend code/i }));
    });

    expect(mockRegisterResendOtp).toHaveBeenCalledWith("0901234567");
    expect(screen.getByRole("button", { name: /resend in 05:00/i })).toBeDisabled();
  });

  it("OTP form shows the email address", () => {
    renderWithProviders(<Register />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: false,
          error: null,
          otpRequired: true,
          otpPhone: "0901234567",
          otpEmail: "test@example.com",
          otpFlow: "register" as const,
          otpPassword: "pass123",
        },
      },
    });
    expect(screen.getByText("test@example.com")).toBeInTheDocument();
  });

  it("allows typing OTP code", () => {
    renderWithProviders(<Register />, {
      preloadedState: {
        auth: {
          user: null,
          accessToken: null,
          refreshToken: null,
          loading: false,
          error: null,
          otpRequired: true,
          otpPhone: "0901234567",
          otpEmail: "test@example.com",
          otpFlow: "register" as const,
          otpPassword: "pass123",
        },
      },
    });
    const otpInput = screen.getByPlaceholderText("OTP Code") as HTMLInputElement;
    fireEvent.change(otpInput, { target: { value: "123456" } });
    expect(otpInput.value).toBe("123456");
  });

  it("phone input has type=tel", () => {
    renderWithProviders(<Register />);
    const phone = screen.getByPlaceholderText("Phone number");
    expect(phone).toHaveAttribute("type", "tel");
  });

  it("email input has type=email", () => {
    renderWithProviders(<Register />);
    const email = screen.getByPlaceholderText("Email");
    expect(email).toHaveAttribute("type", "email");
  });
});
