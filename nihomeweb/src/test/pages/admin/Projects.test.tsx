import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import AdminProjects from "@/pages/admin/Projects";

const mockUseProjects = vi.fn();
const mockToast = vi.fn();
const mockDeleteProject = vi.fn();

vi.mock("@/hooks/useContentApi", () => ({
  useProjects: () => mockUseProjects(),
}));

vi.mock("@/hooks/use-toast", () => ({
  useToast: () => ({ toast: mockToast }),
}));

vi.mock("@/services/adminApi", () => ({
  adminApi: {
    deleteProject: (...args: unknown[]) => mockDeleteProject(...args),
  },
}));

vi.mock("@/components/layout/AdminLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/components/PageState", () => ({
  PageLoading: () => <div>loading-state</div>,
  PageError: ({ message }: { message: string }) => <div>error-state:{message}</div>,
}));

const ROUTER_FUTURE = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const makeProject = (overrides = {}) => ({
  id: 1,
  slug: "project-slug-1",
  imageUrl: "/p.jpg",
  name: "Factory A",
  client: "Client Co",
  location: "HCM",
  scale: "5000m2",
  scope: "D&B",
  status: "ongoing",
  ...overrides,
});

describe("Admin Projects page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", vi.fn(() => true));
  });

  it("renders loading state", () => {
    mockUseProjects.mockReturnValue({ data: null, loading: true, error: null, refetch: vi.fn() });
    render(
      <MemoryRouter future={ROUTER_FUTURE}>
        <I18nProvider>
          <AdminProjects />
        </I18nProvider>
      </MemoryRouter>,
    );
    expect(screen.getByText("loading-state")).toBeInTheDocument();
  });

  it("renders error state", () => {
    mockUseProjects.mockReturnValue({ data: null, loading: false, error: "Failed to load", refetch: vi.fn() });
    render(
      <MemoryRouter future={ROUTER_FUTURE}>
        <I18nProvider>
          <AdminProjects />
        </I18nProvider>
      </MemoryRouter>,
    );
    expect(screen.getByText("error-state:Failed to load")).toBeInTheDocument();
  });

  it("renders project names from API", () => {
    mockUseProjects.mockReturnValue({
      data: [makeProject({ id: 1, slug: "proj-1", name: "Factory A" }), makeProject({ id: 2, slug: "proj-2", name: "Office B", status: "completed" })],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    render(
      <MemoryRouter future={ROUTER_FUTURE}>
        <I18nProvider>
          <AdminProjects />
        </I18nProvider>
      </MemoryRouter>,
    );
    expect(screen.getByText("Factory A")).toBeInTheDocument();
    expect(screen.getByText("Office B")).toBeInTheDocument();
  });

  it("filters projects by status tab", () => {
    mockUseProjects.mockReturnValue({
      data: [makeProject({ id: 1, slug: "p1", name: "Ongoing Project", status: "ongoing" }), makeProject({ id: 2, slug: "p2", name: "Completed Project", status: "completed" })],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    render(
      <MemoryRouter future={ROUTER_FUTURE}>
        <I18nProvider>
          <AdminProjects />
        </I18nProvider>
      </MemoryRouter>,
    );
    // Default tab shows all
    expect(screen.getByText("Ongoing Project")).toBeInTheDocument();
    expect(screen.getByText("Completed Project")).toBeInTheDocument();

    // Click "Ongoing" tab button — use getAllByRole to avoid matching the status badge <span>
    const ongoingTabBtn = screen.getAllByRole("button", { name: "Ongoing" })[0];
    fireEvent.click(ongoingTabBtn);
    expect(screen.getByText("Ongoing Project")).toBeInTheDocument();
    expect(screen.queryByText("Completed Project")).not.toBeInTheDocument();
  });

  it("renders slug-based detail links", () => {
    mockUseProjects.mockReturnValue({
      data: [makeProject({ id: 1, slug: "factory-abc", name: "Factory ABC" })],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    render(
      <MemoryRouter future={ROUTER_FUTURE}>
        <I18nProvider>
          <AdminProjects />
        </I18nProvider>
      </MemoryRouter>,
    );
    const link = screen.getByRole("link", { name: /factory abc/i });
    expect(link).toHaveAttribute("href", "/admin/projects/factory-abc");
  });

  it("deletes a project and refetches", async () => {
    const refetch = vi.fn();
    mockDeleteProject.mockResolvedValue({});
    mockUseProjects.mockReturnValue({
      data: [makeProject({ id: 5, slug: "del-me", name: "Delete Me" })],
      loading: false,
      error: null,
      refetch,
    });
    const { container } = render(
      <MemoryRouter future={ROUTER_FUTURE}>
        <I18nProvider>
          <AdminProjects />
        </I18nProvider>
      </MemoryRouter>,
    );

    // Delete button renders t("common.delete") = "Delete"
    const deleteBtn = screen.getAllByText("Delete")[0];
    expect(deleteBtn).toBeTruthy();
    fireEvent.click(deleteBtn);

    await waitFor(() => {
      expect(mockDeleteProject).toHaveBeenCalledWith(5);
      expect(refetch).toHaveBeenCalled();
    });
  });
});
