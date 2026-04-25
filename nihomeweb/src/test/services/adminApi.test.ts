import { beforeEach, describe, expect, it, vi } from "vitest";

const { mockApi } = vi.hoisted(() => ({
  mockApi: {
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("axios", () => ({
  default: {
    create: vi.fn(() => mockApi),
  },
}));

import { adminApi, slugify } from "@/services/adminApi";

describe("adminApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

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

  it("deleteLogo sends correct route", async () => {
    await adminApi.deleteLogo(7);
    expect(mockApi.delete).toHaveBeenCalledWith("/logos/7");
  });
});

describe("slugify", () => {
  it("normalizes vietnamese characters and separators", () => {
    expect(slugify("Dự án Nhà Máy Mới!!!")).toBe("du-an-nha-may-moi");
  });

  it("falls back to item timestamp for empty input", () => {
    const result = slugify("   ");
    expect(result.startsWith("item-")).toBe(true);
  });
});
