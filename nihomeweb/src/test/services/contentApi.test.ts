import { beforeEach, describe, expect, it, vi } from "vitest";

const { mockApi } = vi.hoisted(() => ({
  mockApi: {
    get: vi.fn(),
  },
}));

vi.mock("@/lib/api", () => ({
  default: mockApi,
}));

import { contentApi } from "@/services/contentApi";

describe("contentApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApi.get.mockResolvedValue({ data: [] });
  });

  // ── Activities ────────────────────────────────────────────────

  it("getActivities calls correct route with default lang", async () => {
    await contentApi.getActivities();
    expect(mockApi.get).toHaveBeenCalledWith("/activities?lang=vi");
  });

  it("getActivities calls correct route with custom lang", async () => {
    await contentApi.getActivities("en");
    expect(mockApi.get).toHaveBeenCalledWith("/activities?lang=en");
  });

  it("getActivity calls correct route with slug and lang", async () => {
    mockApi.get.mockResolvedValueOnce({ data: { id: 1, slug: "a", imageUrl: "" } });
    await contentApi.getActivity("hero-opening", "ja");
    expect(mockApi.get).toHaveBeenCalledWith("/activities/hero-opening?lang=ja");
  });

  // ── News ──────────────────────────────────────────────────────

  it("getNews calls correct route with default lang", async () => {
    await contentApi.getNews();
    expect(mockApi.get).toHaveBeenCalledWith("/news?lang=vi");
  });

  it("getNewsItem calls correct route with slug and lang", async () => {
    mockApi.get.mockResolvedValueOnce({ data: { id: 1, slug: "n", imageUrl: "" } });
    await contentApi.getNewsItem("gmp-standards", "en");
    expect(mockApi.get).toHaveBeenCalledWith("/news/gmp-standards?lang=en");
  });

  // ── Projects ──────────────────────────────────────────────────

  it("getProjects calls correct route", async () => {
    await contentApi.getProjects();
    expect(mockApi.get).toHaveBeenCalledWith("/projects");
  });

  it("getProject calls correct route with slug", async () => {
    mockApi.get.mockResolvedValueOnce({ data: { id: 1, slug: "p", imageUrl: "", gallery: [] } });
    await contentApi.getProject("nha-may-bma");
    expect(mockApi.get).toHaveBeenCalledWith("/projects/nha-may-bma");
  });

  // ── Services ──────────────────────────────────────────────────

  it("getServices calls correct route", async () => {
    await contentApi.getServices();
    expect(mockApi.get).toHaveBeenCalledWith("/services");
  });

  it("getService calls correct route with slug", async () => {
    mockApi.get.mockResolvedValueOnce({ data: {} });
    await contentApi.getService("design-and-build");
    expect(mockApi.get).toHaveBeenCalledWith("/services/design-and-build");
  });

  // ── Logos ─────────────────────────────────────────────────────

  it("getLogos calls correct route", async () => {
    mockApi.get.mockResolvedValueOnce({
      data: { clients: [], partners: [], suppliers: [] },
    });
    await contentApi.getLogos();
    expect(mockApi.get).toHaveBeenCalledWith("/logos");
  });

  // ── Processes ─────────────────────────────────────────────────

  it("getProcesses calls correct route", async () => {
    mockApi.get.mockResolvedValueOnce({ data: {} });
    await contentApi.getProcesses();
    expect(mockApi.get).toHaveBeenCalledWith("/processes");
  });

  // ── Slideshow ─────────────────────────────────────────────────

  it("getSlideshow calls correct route with default lang", async () => {
    await contentApi.getSlideshow();
    expect(mockApi.get).toHaveBeenCalledWith("/slideshow?lang=vi");
  });

  it("getSlideshow calls correct route with custom lang", async () => {
    await contentApi.getSlideshow("en");
    expect(mockApi.get).toHaveBeenCalledWith("/slideshow?lang=en");
  });

  // ── Image URL resolution ──────────────────────────────────────

  it("resolves relative imageUrl on activities", async () => {
    mockApi.get.mockResolvedValueOnce({
      data: [{ id: 1, slug: "a", imageUrl: "/images/test.jpg", category: "", title: "", excerpt: "", content: [] }],
    });
    const result = await contentApi.getActivities();
    // In test environment window.location.origin is '' so resolveImageUrl keeps the path
    expect(result.data[0].imageUrl).toContain("/images/test.jpg");
  });

  it("keeps absolute imageUrl unchanged on projects", async () => {
    mockApi.get.mockResolvedValueOnce({
      data: [{ id: 1, slug: "p", imageUrl: "https://cdn.example.com/img.jpg", gallery: [] }],
    });
    const result = await contentApi.getProjects();
    expect(result.data[0].imageUrl).toBe("https://cdn.example.com/img.jpg");
  });

  it("resolves gallery URLs on projects", async () => {
    mockApi.get.mockResolvedValueOnce({
      data: [{ id: 1, slug: "p", imageUrl: "/img.jpg", gallery: ["/img/a.jpg", "/img/b.jpg"] }],
    });
    const result = await contentApi.getProjects();
    expect(result.data[0].gallery).toHaveLength(2);
    expect(result.data[0].gallery![0]).toContain("/img/a.jpg");
  });
});
