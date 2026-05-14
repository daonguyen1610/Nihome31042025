import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import AboutContent from "@/pages/admin/AboutContent";

const mockGetAboutSections = vi.fn();
const mockToast = vi.fn();

vi.mock("@/lib/i18n", () => {
  const t = (key: string) => key;

  return {
    I18nProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
    useI18n: () => ({
      lang: "en",
      setLang: vi.fn(),
      t,
    }),
    translateError: (_t: unknown, message: string) => message,
  };
});

vi.mock("@/services/adminApi", () => ({
  adminApi: {
    getAboutSections: (...args: unknown[]) => mockGetAboutSections(...args),
  },
}));

vi.mock("@/hooks/use-toast", () => ({
  useToast: () => ({ toast: mockToast }),
}));

vi.mock("@/components/layout/AdminLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

const makeAboutSection = (overrides = {}) => ({
  id: 1,
  slug: "about-main",
  itemsJson: null,
  eyebrow: "",
  titleA: "",
  titleB: "",
  paragraph1: "",
  paragraph2: "",
  imageUrl: "",
  isActive: true,
  sortOrder: 0,
  ...overrides,
});

describe("Admin About content page", () => {
  beforeAll(() => {
    Object.defineProperty(HTMLElement.prototype, "scrollIntoView", {
      configurable: true,
      value: vi.fn(),
    });
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders organization tab content from object-shaped itemsJson", async () => {
    mockGetAboutSections.mockResolvedValue({
      data: [
        makeAboutSection({
          id: 5,
          slug: "organization-main",
          itemsJson: JSON.stringify({
            board: [{ sortOrder: 0, role: "Board Chair", name: "Nguyen Van A" }],
            directors: [{ sortOrder: 0, role: "General Director", name: "Tran Van B" }],
          }),
          eyebrow: "Organization",
          titleA: "Leadership",
          titleB: "Team",
          sortOrder: 4,
        }),
      ],
    });

    render(<AboutContent />);

    const organizationTab = await screen.findByRole("button", { name: "aboutAdmin.tab.organization" });
    fireEvent.click(organizationTab);

    expect(await screen.findByDisplayValue("Board Chair")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Nguyen Van A")).toBeInTheDocument();
    expect(screen.getByDisplayValue("General Director")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Tran Van B")).toBeInTheDocument();
  });

  it("keeps list editors stable when itemsJson is not an array", async () => {
    mockGetAboutSections.mockResolvedValue({
      data: [
        makeAboutSection({
          id: 2,
          slug: "stats-main",
          itemsJson: JSON.stringify({ board: [{ role: "Unexpected", name: "Shape" }] }),
          sortOrder: 1,
        }),
      ],
    });

    render(<AboutContent />);

    const statsTab = await screen.findByRole("button", { name: "aboutAdmin.tab.stats" });
    fireEvent.click(statsTab);

    await waitFor(() => {
      expect(screen.getByText("aboutAdmin.noStats")).toBeInTheDocument();
    });
  });
});
