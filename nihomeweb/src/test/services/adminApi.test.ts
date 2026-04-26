import { beforeEach, describe, expect, it, vi } from "vitest";

const { mockApi } = vi.hoisted(() => ({
  mockApi: {
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    get: vi.fn(),
    patch: vi.fn(),
  },
}));

vi.mock("@/lib/api", () => ({
  default: mockApi,
}));

import { adminApi, slugify } from "@/services/adminApi";

describe("adminApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("uploadImage sends multipart form data to system upload endpoint", async () => {
    const file = new File(["img"], "photo.png", { type: "image/png" });
    await adminApi.uploadImage(file, "/images/upload/old-photo.png");

    const [url, formData] = mockApi.post.mock.calls[0];
    expect(url).toBe("/system/upload-image");
    expect(formData).toBeInstanceOf(FormData);
    expect((formData as FormData).get("file")).toBe(file);
    expect((formData as FormData).get("previousImageUrl")).toBe("/images/upload/old-photo.png");
  });

  // ── Activities ────────────────────────────────────────────────

  it("createActivity sends correct route and payload", async () => {
    const payload = {
      slug: "activity-1",
      date: "25.04.2026",
      imageUrl: "/a.jpg",
      category: "Category",
      author: "Admin",
      title: "Title",
      excerpt: "Excerpt",
      content: ["Line 1"],
    };
    await adminApi.createActivity(payload);
    expect(mockApi.post).toHaveBeenCalledWith("/activities", payload);
  });

  it("updateActivity sends correct route and payload", async () => {
    const payload = {
      slug: "activity-updated",
      date: "26.04.2026",
      imageUrl: "/a2.jpg",
      category: "Event",
      title: "Updated",
      excerpt: "Excerpt",
      content: ["Content"],
    };
    await adminApi.updateActivity(5, payload);
    expect(mockApi.put).toHaveBeenCalledWith("/activities/5", payload);
  });

  it("deleteActivity sends correct route", async () => {
    await adminApi.deleteActivity(3);
    expect(mockApi.delete).toHaveBeenCalledWith("/activities/3");
  });

  // ── News ──────────────────────────────────────────────────────

  it("createNews sends correct route and payload", async () => {
    const payload = {
      slug: "news-1",
      date: "25.04.2026",
      imageUrl: "/n.jpg",
      category: "Industry",
      title: "News Title",
      excerpt: "Excerpt",
      content: ["Para 1"],
    };
    await adminApi.createNews(payload);
    expect(mockApi.post).toHaveBeenCalledWith("/news", payload);
  });

  it("updateNews sends correct route and payload", async () => {
    const payload = {
      slug: "news-updated",
      date: "26.04.2026",
      imageUrl: "/n2.jpg",
      category: "Industry",
      title: "Updated News",
      excerpt: "Excerpt",
      content: ["Para 1"],
    };
    await adminApi.updateNews(8, payload);
    expect(mockApi.put).toHaveBeenCalledWith("/news/8", payload);
  });

  it("deleteNews sends correct route", async () => {
    await adminApi.deleteNews(2);
    expect(mockApi.delete).toHaveBeenCalledWith("/news/2");
  });

  // ── Projects ──────────────────────────────────────────────────

  it("createProject sends correct route and payload", async () => {
    const payload = {
      slug: "project-new",
      imageUrl: "/p.jpg",
      name: "New Project",
      client: "Client",
      location: "HCM",
      scale: "5000m2",
      scope: "D&B",
      status: "ongoing",
    };
    await adminApi.createProject(payload);
    expect(mockApi.post).toHaveBeenCalledWith("/projects", payload);
  });

  it("updateProject sends correct route and payload", async () => {
    const payload = {
      slug: "project-1",
      imageUrl: "/p.jpg",
      name: "Project",
      client: "Client",
      location: "HCM",
      scale: "1000m2",
      scope: "Design",
      status: "ongoing",
    };
    await adminApi.updateProject(12, payload);
    expect(mockApi.put).toHaveBeenCalledWith("/projects/12", payload);
  });

  it("deleteProject sends correct route", async () => {
    await adminApi.deleteProject(9);
    expect(mockApi.delete).toHaveBeenCalledWith("/projects/9");
  });

  // ── Logos ─────────────────────────────────────────────────────

  it("createLogo sends correct route and payload", async () => {
    const payload = { name: "Logo", imageUrl: "/l.jpg", kind: "client" };
    await adminApi.createLogo(payload);
    expect(mockApi.post).toHaveBeenCalledWith("/logos", payload);
  });

  it("updateLogo sends correct route and payload", async () => {
    const payload = { name: "Logo Updated", imageUrl: "/l2.jpg", kind: "partner" };
    await adminApi.updateLogo(4, payload);
    expect(mockApi.put).toHaveBeenCalledWith("/logos/4", payload);
  });

  it("deleteLogo sends correct route", async () => {
    await adminApi.deleteLogo(7);
    expect(mockApi.delete).toHaveBeenCalledWith("/logos/7");
  });

  // ── Processes ─────────────────────────────────────────────────

  it("createProcess sends correct route and payload", async () => {
    const payload = { groupKey: "design", title: "Step 1" };
    await adminApi.createProcess(payload);
    expect(mockApi.post).toHaveBeenCalledWith("/processes", payload);
  });

  it("updateProcess sends correct route and payload", async () => {
    const payload = { groupKey: "build", title: "Step Updated" };
    await adminApi.updateProcess(6, payload);
    expect(mockApi.put).toHaveBeenCalledWith("/processes/6", payload);
  });

  it("deleteProcess sends correct route", async () => {
    await adminApi.deleteProcess(11);
    expect(mockApi.delete).toHaveBeenCalledWith("/processes/11");
  });

  it("createSlideshow sends correct route and payload", async () => {
    const payload = {
      slug: "hero-slide",
      imageUrl: "/img/hero.jpg",
      title: "Hero",
      subtitle: "Sub",
      linkUrl: "/projects",
      linkText: "View",
      isActive: true,
      sortOrder: 0,
    };
    await adminApi.createSlideshow(payload);
    expect(mockApi.post).toHaveBeenCalledWith("/slideshow", payload);
  });

  it("updateSlideshow sends correct route and payload", async () => {
    const payload = {
      slug: "hero-updated",
      imageUrl: "/img/updated.jpg",
      title: "Updated",
      isActive: false,
      sortOrder: 1,
    };
    await adminApi.updateSlideshow(3, payload);
    expect(mockApi.put).toHaveBeenCalledWith("/slideshow/3", payload);
  });

  it("deleteSlideshow sends correct route", async () => {
    await adminApi.deleteSlideshow(5);
    expect(mockApi.delete).toHaveBeenCalledWith("/slideshow/5");
  });
});

describe("slugify", () => {
  it("normalizes vietnamese characters and separators", () => {
    expect(slugify("Dự án Nhà Máy Mới!!!")).toBe("du-an-nha-may-moi");
  });

  it("converts đ to d", () => {
    expect(slugify("Đường Đại lộ")).toBe("duong-dai-lo");
  });

  it("preserves numbers", () => {
    expect(slugify("Nhà Máy B37")).toBe("nha-may-b37");
  });

  it("truncates to 80 characters", () => {
    const long = "a".repeat(100);
    expect(slugify(long)).toHaveLength(80);
  });

  it("strips leading and trailing hyphens", () => {
    expect(slugify("!!!Hello World!!!")).toBe("hello-world");
  });

  it("falls back to item timestamp for empty input", () => {
    const result = slugify("   ");
    expect(result.startsWith("item-")).toBe(true);
  });
});
