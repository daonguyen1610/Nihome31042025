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
  partners: [
    { id: 11, name: "Partner One", imageUrl: "/partner-1.png", kind: "partner", href: null, sortOrder: 1 },
  ],
  suppliers: [
    { id: 21, name: "Supplier One", imageUrl: "/supplier-1.png", kind: "supplier", href: null, sortOrder: 1 },
  ],
  awards: [
    { id: 31, name: "Award One", imageUrl: "/award-1.png", kind: "award", href: null, sortOrder: 1 },
  ],
});

const renderComponent = (kind: "clients" | "partners" | "suppliers" | "awards" = "clients") =>
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
      data: { clients: [], partners: [], suppliers: [], awards: [] },
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

  it("creates a partner logo with Partner kind", async () => {
    const refetch = vi.fn();
    mockCreateLogo.mockResolvedValue({});
    mockUseLogos.mockReturnValue({ data: makeLogosData(), loading: false, error: null, refetch });

    renderComponent("partners");

    fireEvent.click(screen.getByText("logoAdmin.add"));
    fireEvent.change(screen.getByPlaceholderText("logoAdmin.placeholderName"), { target: { value: "NCC Partner" } });
    fireEvent.change(screen.getByPlaceholderText("/images/upload/..."), { target: { value: "/images/upload/partner-new.png" } });

    fireEvent.click(screen.getByRole("button", { name: /create/i }));

    await waitFor(() => {
      expect(mockCreateLogo).toHaveBeenCalledWith({
        name: "NCC Partner",
        imageUrl: "/images/upload/partner-new.png",
        href: undefined,
        kind: "Partner",
        sortOrder: 2,
      });
      expect(refetch).toHaveBeenCalled();
    });
  });

  it("updates a partner logo with Partner kind", async () => {
    const refetch = vi.fn();
    mockUpdateLogo.mockResolvedValue({});
    mockUseLogos.mockReturnValue({ data: makeLogosData(), loading: false, error: null, refetch });

    renderComponent("partners");

    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    fireEvent.change(screen.getByPlaceholderText("logoAdmin.placeholderName"), { target: { value: "Partner Updated" } });
    fireEvent.click(screen.getByRole("button", { name: /update/i }));

    await waitFor(() => {
      expect(mockUpdateLogo).toHaveBeenCalledWith(
        11,
        expect.objectContaining({
          name: "Partner Updated",
          kind: "Partner",
        }),
      );
      expect(refetch).toHaveBeenCalled();
    });
  });

  it("creates an award logo with Award kind", async () => {
    const refetch = vi.fn();
    mockCreateLogo.mockResolvedValue({});
    mockUseLogos.mockReturnValue({ data: makeLogosData(), loading: false, error: null, refetch });

    renderComponent("awards");

    fireEvent.click(screen.getByText("logoAdmin.add"));
    fireEvent.change(screen.getByPlaceholderText("logoAdmin.placeholderName"), { target: { value: "Top 10 Brands" } });
    fireEvent.change(screen.getByPlaceholderText("/images/upload/..."), { target: { value: "/images/upload/award-new.png" } });
    fireEvent.click(screen.getByRole("button", { name: /create/i }));

    await waitFor(() => {
      expect(mockCreateLogo).toHaveBeenCalledWith({
        name: "Top 10 Brands",
        imageUrl: "/images/upload/award-new.png",
        href: undefined,
        kind: "Award",
        sortOrder: 2,
      });
      expect(refetch).toHaveBeenCalled();
    });
  });
});
