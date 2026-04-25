import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import LogosManager from "@/pages/admin/LogosManager";

const mockUseLogos = vi.fn();
const mockCreateLogo = vi.fn();
const mockUpdateLogo = vi.fn();
const mockDeleteLogo = vi.fn();

vi.mock("@/hooks/useContentApi", () => ({
  useLogos: () => mockUseLogos(),
}));

vi.mock("@/services/adminApi", () => ({
  adminApi: {
    createLogo: (...args: unknown[]) => mockCreateLogo(...args),
    updateLogo: (...args: unknown[]) => mockUpdateLogo(...args),
    deleteLogo: (...args: unknown[]) => mockDeleteLogo(...args),
  },
}));

vi.mock("@/components/layout/AdminLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/components/PageState", () => ({
  PageLoading: () => <div>loading-state</div>,
  PageError: ({ message }: { message: string }) => <div>error-state:{message}</div>,
  PageEmpty: ({ message }: { message: string }) => <div>empty-state:{message}</div>,
}));

const ROUTER_FUTURE = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const makeLogosData = () => ({
  clients: [
    { id: 1, name: "Acme Corp", imageUrl: "/logo1.png", kind: "client", href: null },
    { id: 2, name: "Beta Ltd", imageUrl: "/logo2.png", kind: "client", href: "https://beta.com" },
  ],
  partners: [],
  suppliers: [],
});

const renderComponent = (kind: "clients" | "partners" | "suppliers" = "clients") =>
  render(
    <MemoryRouter future={ROUTER_FUTURE}>
      <I18nProvider>
        <LogosManager kind={kind} titleKey="admin.logos.clients" />
      </I18nProvider>
    </MemoryRouter>,
  );

describe("Admin LogosManager page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", vi.fn(() => true));
  });

  it("renders loading state", () => {
    mockUseLogos.mockReturnValue({ data: null, loading: true, error: null, refetch: vi.fn() });
    renderComponent();
    expect(screen.getByText("loading-state")).toBeInTheDocument();
  });

  it("renders error state", () => {
    mockUseLogos.mockReturnValue({ data: null, loading: false, error: "Failed", refetch: vi.fn() });
    renderComponent();
    expect(screen.getByText("error-state:Failed")).toBeInTheDocument();
  });

  it("renders logo names from API", () => {
    mockUseLogos.mockReturnValue({ data: makeLogosData(), loading: false, error: null, refetch: vi.fn() });
    renderComponent();
    expect(screen.getByText("Acme Corp")).toBeInTheDocument();
    expect(screen.getByText("Beta Ltd")).toBeInTheDocument();
  });

  it("shows empty state when no logos", () => {
    mockUseLogos.mockReturnValue({
      data: { clients: [], partners: [], suppliers: [] },
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderComponent();
    expect(screen.getByText(/empty-state/)).toBeInTheDocument();
  });

  it("renders logo images", () => {
    mockUseLogos.mockReturnValue({ data: makeLogosData(), loading: false, error: null, refetch: vi.fn() });
    renderComponent();
    const imgs = screen.getAllByRole("img");
    expect(imgs[0]).toHaveAttribute("src", "/logo1.png");
  });

  it("deletes a logo and refetches", async () => {
    const refetch = vi.fn();
    mockDeleteLogo.mockResolvedValue({});
    mockUseLogos.mockReturnValue({ data: makeLogosData(), loading: false, error: null, refetch });
    renderComponent();
    // Delete button renders t("common.delete") = "Delete"
    const deleteBtns = screen.getAllByText("Delete");
    expect(deleteBtns.length).toBeGreaterThan(0);
    fireEvent.click(deleteBtns[0]);
    await waitFor(() => {
      expect(mockDeleteLogo).toHaveBeenCalledWith(1);
      expect(refetch).toHaveBeenCalled();
    });
  });
});
