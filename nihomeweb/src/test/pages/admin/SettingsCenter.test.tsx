import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import SettingsCenter from "@/pages/admin/SettingsCenter";

const { mockGetOtpSettings, mockUpdateOtpSettings, mockToast } = vi.hoisted(() => ({
  mockGetOtpSettings: vi.fn(),
  mockUpdateOtpSettings: vi.fn(),
  mockToast: vi.fn(),
}));

vi.mock("@/services/adminApi", () => ({
  adminApi: {
    getOtpSettings: () => mockGetOtpSettings(),
    updateOtpSettings: (payload: unknown) => mockUpdateOtpSettings(payload),
  },
}));

vi.mock("@/hooks/use-toast", () => ({
  useToast: () => ({ toast: mockToast }),
}));

vi.mock("@/components/layout/AdminLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

const ROUTER_FUTURE = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const renderSettings = () =>
  render(
    <MemoryRouter initialEntries={["/admin/settings?tab=general"]} future={ROUTER_FUTURE}>
      <I18nProvider>
        <SettingsCenter />
      </I18nProvider>
    </MemoryRouter>,
  );

describe("SettingsCenter OTP settings", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads and renders backend OTP settings in the general tab", async () => {
    mockGetOtpSettings.mockResolvedValueOnce({
      data: {
        enableOtpForRegistration: true,
        enableOtpForForgotPassword: false,
      },
    });

    renderSettings();

    const registrationToggle = await screen.findByRole("button", {
      name: "Enable OTP for registration",
    });
    const forgotToggle = screen.getByRole("button", {
      name: "Enable OTP for forgot password",
    });

    expect(registrationToggle).toHaveAttribute("aria-pressed", "true");
    expect(forgotToggle).toHaveAttribute("aria-pressed", "false");
  });

  it("auto-saves a changed OTP setting", async () => {
    mockGetOtpSettings.mockResolvedValueOnce({
      data: {
        enableOtpForRegistration: true,
        enableOtpForForgotPassword: false,
      },
    });
    mockUpdateOtpSettings.mockResolvedValueOnce({
      data: {
        enableOtpForRegistration: true,
        enableOtpForForgotPassword: true,
      },
    });

    renderSettings();

    const forgotToggle = await screen.findByRole("button", {
      name: "Enable OTP for forgot password",
    });
    fireEvent.click(forgotToggle);

    await waitFor(() => {
      expect(mockUpdateOtpSettings).toHaveBeenCalledWith({
        enableOtpForRegistration: true,
        enableOtpForForgotPassword: true,
      });
    });

    await waitFor(() => {
      expect(forgotToggle).toHaveAttribute("aria-pressed", "true");
    });
    expect(mockToast).toHaveBeenCalledWith({ title: "Settings saved" });
  });

  it("rolls back the toggle when auto-save fails", async () => {
    mockGetOtpSettings.mockResolvedValueOnce({
      data: {
        enableOtpForRegistration: true,
        enableOtpForForgotPassword: true,
      },
    });
    mockUpdateOtpSettings.mockRejectedValueOnce(new Error("save failed"));

    renderSettings();

    const registrationToggle = await screen.findByRole("button", {
      name: "Enable OTP for registration",
    });
    fireEvent.click(registrationToggle);

    await waitFor(() => {
      expect(mockUpdateOtpSettings).toHaveBeenCalledWith({
        enableOtpForRegistration: false,
        enableOtpForForgotPassword: true,
      });
    });

    await waitFor(() => {
      expect(registrationToggle).toHaveAttribute("aria-pressed", "true");
    });
    expect(mockToast).toHaveBeenCalledWith({
      title: "An error occurred",
      description: "Could not save OTP settings.",
      variant: "destructive",
    });
  });
});
