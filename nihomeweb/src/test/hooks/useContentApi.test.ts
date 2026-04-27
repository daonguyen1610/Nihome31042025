import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useActivities, useNews, useProjects, useServices, useLogos, useProcesses, useSlideshow } from "@/hooks/useContentApi";

const mockGet = vi.fn();

vi.mock("@/services/contentApi", () => ({
  contentApi: {
    getActivities: () => mockGet("activities"),
    getActivity: (slug: string) => mockGet("activity", slug),
    getNews: () => mockGet("news"),
    getNewsItem: (slug: string) => mockGet("newsItem", slug),
    getProjects: () => mockGet("projects"),
    getProject: (slug: string) => mockGet("project", slug),
    getServices: () => mockGet("services"),
    getService: (slug: string) => mockGet("service", slug),
    getLogos: () => mockGet("logos"),
    getProcesses: () => mockGet("processes"),
    getSlideshow: () => mockGet("slideshow"),
  },
}));

describe("useActivities", () => {
  beforeEach(() => vi.clearAllMocks());

  it("starts in loading state", () => {
    mockGet.mockReturnValue(new Promise(() => {}));
    const { result } = renderHook(() => useActivities());
    expect(result.current.loading).toBe(true);
    expect(result.current.data).toBeNull();
  });

  it("resolves data on success", async () => {
    const items = [{ id: 1, slug: "a", title: "Activity" }];
    mockGet.mockResolvedValue({ data: items });
    const { result } = renderHook(() => useActivities());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(items);
    expect(result.current.error).toBeNull();
  });

  it("sets error on failure", async () => {
    mockGet.mockRejectedValue({ message: "Network error" });
    const { result } = renderHook(() => useActivities());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe("Network error");
    expect(result.current.data).toBeNull();
  });
});

describe("useNews", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resolves news data", async () => {
    const items = [{ id: 1, slug: "n", title: "News item" }];
    mockGet.mockResolvedValue({ data: items });
    const { result } = renderHook(() => useNews());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(items);
  });
});

describe("useProjects", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resolves project data", async () => {
    const items = [{ id: 1, slug: "p", name: "Project" }];
    mockGet.mockResolvedValue({ data: items });
    const { result } = renderHook(() => useProjects());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(items);
  });
});

describe("useServices", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resolves service data", async () => {
    const items = [{ id: 1, slug: "s", title: "Service" }];
    mockGet.mockResolvedValue({ data: items });
    const { result } = renderHook(() => useServices());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(items);
  });
});

describe("useLogos", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resolves logos data", async () => {
    const logos = { clients: [], partners: [], suppliers: [], awards: [] };
    mockGet.mockResolvedValue({ data: logos });
    const { result } = renderHook(() => useLogos());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(logos);
  });
});

describe("useProcesses", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resolves processes data", async () => {
    const data = { design: [{ id: 1, groupKey: "design", title: "Step 1" }] };
    mockGet.mockResolvedValue({ data });
    const { result } = renderHook(() => useProcesses());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(data);
  });
});

describe("useSlideshow", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resolves slideshow data", async () => {
    const items = [{ id: 1, slug: "hero", title: "Hero", imageUrl: "/h.jpg", isActive: true, sortOrder: 0 }];
    mockGet.mockResolvedValue({ data: items });
    const { result } = renderHook(() => useSlideshow());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toEqual(items);
  });
});

describe("refetch", () => {
  it("re-triggers fetch and updates data", async () => {
    mockGet
      .mockResolvedValueOnce({ data: [{ id: 1, slug: "a" }] })
      .mockResolvedValueOnce({ data: [{ id: 1, slug: "a" }, { id: 2, slug: "b" }] });

    const { result } = renderHook(() => useActivities());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toHaveLength(1);

    result.current.refetch();
    await waitFor(() => expect(result.current.data).toHaveLength(2));
  });
});
