import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import News from "@/pages/News";

const mockUseNews = vi.fn();

vi.mock("@/hooks/useContentApi", () => ({
  useNews: () => mockUseNews(),
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

const makeNews = (overrides = {}) => ({
  id: 1,
  slug: "news-1",
  date: "25.04.2026",
  imageUrl: "/n.jpg",
  category: "Industry",
  author: "Admin",
  title: "First News",
  excerpt: "Excerpt",
  content: ["Content"],
  ...overrides,
});

const renderPage = () =>
  render(
    <MemoryRouter future={ROUTER_FUTURE}>
      <I18nProvider>
        <News />
      </I18nProvider>
    </MemoryRouter>,
  );

describe("News public page", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders loading state", () => {
    mockUseNews.mockReturnValue({ data: null, loading: true, error: null, refetch: vi.fn() });
    renderPage();
    expect(screen.getByText("loading-state")).toBeInTheDocument();
  });

  it("renders error state", () => {
    mockUseNews.mockReturnValue({ data: null, loading: false, error: "Server error", refetch: vi.fn() });
    renderPage();
    expect(screen.getByText("error-state:Server error")).toBeInTheDocument();
  });

  it("renders news titles", () => {
    mockUseNews.mockReturnValue({
      data: [
        makeNews({ id: 1, slug: "n1", title: "GMP Standards Update" }),
        makeNews({ id: 2, slug: "n2", title: "Fire Safety Regulations", category: "Safety" }),
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    expect(screen.getByText("GMP Standards Update")).toBeInTheDocument();
    expect(screen.getByText("Fire Safety Regulations")).toBeInTheDocument();
  });

  it("renders detail links with correct slug", () => {
    mockUseNews.mockReturnValue({
      data: [makeNews({ id: 1, slug: "gmp-standards", title: "GMP News" })],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    const links = screen.getAllByRole("link").filter((l) => l.getAttribute("href")?.includes("gmp-standards"));
    expect(links.length).toBeGreaterThan(0);
  });

  it("filters by category button", () => {
    mockUseNews.mockReturnValue({
      data: [
        makeNews({ id: 1, slug: "n1", title: "Industry Post", category: "Industry" }),
        makeNews({ id: 2, slug: "n2", title: "Safety Post", category: "Safety" }),
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    const safetyBtn = screen.getByRole("button", { name: "Safety" });
    fireEvent.click(safetyBtn);
    expect(screen.getByText("Safety Post")).toBeInTheDocument();
    expect(screen.queryByText("Industry Post")).not.toBeInTheDocument();
  });

  it("filters by search query", () => {
    mockUseNews.mockReturnValue({
      data: [
        makeNews({ id: 1, slug: "n1", title: "ISO 9001 Update" }),
        makeNews({ id: 2, slug: "n2", title: "Annual Compliance" }),
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });
    renderPage();
    const searchInput = screen.getByRole("textbox");
    fireEvent.change(searchInput, { target: { value: "ISO" } });
    expect(screen.getByText("ISO 9001 Update")).toBeInTheDocument();
    expect(screen.queryByText("Annual Compliance")).not.toBeInTheDocument();
  });

  it("shows empty state when no news", () => {
    mockUseNews.mockReturnValue({ data: [], loading: false, error: null, refetch: vi.fn() });
    renderPage();
    expect(screen.getByText(/empty-state/)).toBeInTheDocument();
  });
});
