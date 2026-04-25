import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import Activities from "@/pages/Activities";

const mockUseActivities = vi.fn();

vi.mock("@/hooks/useContentApi", () => ({
  useActivities: () => mockUseActivities(),
}));

vi.mock("@/components/layout/Layout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div data-testid="layout">{children}</div>,
}));

vi.mock("@/components/PageHeader", () => ({
  default: () => <div data-testid="page-header" />,
}));

vi.mock("@/components/PageState", () => ({
  PageLoading: () => <div>loading-state</div>,
  PageError: ({ message }: { message: string }) => <div>error-state:{message}</div>,
  PageEmpty: ({ message }: { message: string }) => <div>empty-state:{message}</div>,
}));

const ROUTER_FUTURE = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const makeActivity = (overrides = {}) => ({
  id: 1,
  slug: "activity-1",
  date: "25.04.2026",
  imageUrl: "/a.jpg",
  category: "Industry",
  author: "Admin",
  title: "First Activity",
  excerpt: "Excerpt text",
  content: ["Content line"],
  ...overrides,
});

const renderPage = () =>
  render(
    <MemoryRouter future={ROUTER_FUTURE}>
      <I18nProvider>
        <Activities />
      </I18nProvider>
    </MemoryRouter>,
  );

describe("Activities public page", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders loading state", () => {
    mockUseActivities.mockReturnValue({ data: null, loading: true, error: null, refetch: vi.fn() });
    renderPage();
    expect(screen.getByText("loading-state")).toBeInTheDocument();
  });

  it("renders error state", () => {
    mockUseActivities.mockReturnValue({ data: null, loading: false, error: "Server error", refetch: vi.fn() });
    renderPage();
    expect(screen.getByText("error-state:Server error")).toBeInTheDocument();
  });

  it("renders activity titles", () => {
    mockUseActivities.mockReturnValue({
      data: [
        makeActivity({ id: 1, slug: "a1", title: "First Activity" }),
        makeActivity({ id: 2, slug: "a2", title: "Second Activity", category: "Event" }),
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    expect(screen.getByText("First Activity")).toBeInTheDocument();
    expect(screen.getByText("Second Activity")).toBeInTheDocument();
  });

  it("renders detail links with correct slug", () => {
    mockUseActivities.mockReturnValue({
      data: [makeActivity({ id: 1, slug: "my-activity", title: "My Activity" })],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    const links = screen.getAllByRole("link").filter((l) => l.getAttribute("href")?.includes("my-activity"));
    expect(links.length).toBeGreaterThan(0);
  });

  it("filters by category button", () => {
    mockUseActivities.mockReturnValue({
      data: [
        makeActivity({ id: 1, slug: "a1", title: "Industry Post", category: "Industry" }),
        makeActivity({ id: 2, slug: "a2", title: "Event Post", category: "Event" }),
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    // "Industry" category button appears
    const industryBtn = screen.getByRole("button", { name: "Industry" });
    fireEvent.click(industryBtn);
    expect(screen.getByText("Industry Post")).toBeInTheDocument();
    expect(screen.queryByText("Event Post")).not.toBeInTheDocument();
  });

  it("filters by search query", () => {
    mockUseActivities.mockReturnValue({
      data: [
        makeActivity({ id: 1, slug: "a1", title: "Factory Opening" }),
        makeActivity({ id: 2, slug: "a2", title: "Annual Report" }),
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    const searchInput = screen.getByRole("textbox");
    fireEvent.change(searchInput, { target: { value: "Factory" } });
    expect(screen.getByText("Factory Opening")).toBeInTheDocument();
    expect(screen.queryByText("Annual Report")).not.toBeInTheDocument();
  });

  it("shows empty state when no activities", () => {
    mockUseActivities.mockReturnValue({ data: [], loading: false, error: null, refetch: vi.fn() });
    renderPage();
    expect(screen.getByText(/empty-state/)).toBeInTheDocument();
  });
});
