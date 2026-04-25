import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import AdminPosts from "@/pages/admin/Posts";

const mockUseActivities = vi.fn();
const mockToast = vi.fn();
const mockDeleteActivity = vi.fn();

vi.mock("@/hooks/useContentApi", () => ({
  useActivities: () => mockUseActivities(),
}));

vi.mock("@/hooks/use-toast", () => ({
  useToast: () => ({ toast: mockToast }),
}));

vi.mock("@/services/adminApi", () => ({
  adminApi: {
    deleteActivity: (...args: unknown[]) => mockDeleteActivity(...args),
  },
}));

vi.mock("@/components/layout/AdminLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/components/PageState", () => ({
  PageLoading: () => <div>loading-state</div>,
  PageError: ({ message }: { message: string }) => <div>error-state:{message}</div>,
}));

describe("Admin Posts page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("nicon_lang", "en");
    vi.stubGlobal("confirm", vi.fn(() => true));
  });

  it("renders loading state", () => {
    mockUseActivities.mockReturnValue({
      data: undefined,
      loading: true,
      error: null,
      refetch: vi.fn(),
    });

    render(
      <MemoryRouter>
        <I18nProvider>
          <AdminPosts />
        </I18nProvider>
      </MemoryRouter>,
    );
    expect(screen.getByText("loading-state")).toBeInTheDocument();
  });

  it("renders posts from API and uses slug-based links", () => {
    mockUseActivities.mockReturnValue({
      data: [
        {
          id: 1,
          slug: "post-slug-1",
          date: "25.04.2026",
          imageUrl: "/img.jpg",
          category: "Updates",
          author: "Admin",
          title: "First Post",
          excerpt: "Excerpt",
          content: ["line"],
        },
      ],
      loading: false,
      error: null,
      refetch: vi.fn(),
    });

    render(
      <MemoryRouter>
        <I18nProvider>
          <AdminPosts />
        </I18nProvider>
      </MemoryRouter>,
    );

    expect(screen.getByText("First Post")).toBeInTheDocument();
    const link = screen.getByRole("link", { name: /first post/i });
    expect(link).toHaveAttribute("href", "/admin/posts/post-slug-1");
  });

  it("deletes a post and refetches", async () => {
    const refetch = vi.fn();
    mockDeleteActivity.mockResolvedValue({});
    mockUseActivities.mockReturnValue({
      data: [
        {
          id: 5,
          slug: "delete-me",
          date: "25.04.2026",
          imageUrl: "/img.jpg",
          category: "Updates",
          author: "Admin",
          title: "Delete Me",
          excerpt: "Excerpt",
          content: ["line"],
        },
      ],
      loading: false,
      error: null,
      refetch,
    });

    const { container } = render(
      <MemoryRouter>
        <I18nProvider>
          <AdminPosts />
        </I18nProvider>
      </MemoryRouter>,
    );
    const deleteButton = container.querySelector("tbody tr button");
    expect(deleteButton).toBeTruthy();

    fireEvent.click(deleteButton as HTMLButtonElement);

    await waitFor(() => {
      expect(mockDeleteActivity).toHaveBeenCalledWith(5);
      expect(refetch).toHaveBeenCalled();
      expect(mockToast).toHaveBeenCalled();
    });
  });
});
