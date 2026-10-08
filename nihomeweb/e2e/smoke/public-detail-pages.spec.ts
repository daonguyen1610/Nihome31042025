import { test, expect, TEST_USERS } from "../fixtures/auth";
import type { APIRequestContext } from "@playwright/test";

/**
 * Smoke coverage for public detail pages. Seeds one of each entity through the
 * admin API, renders the public detail route, then cleans up.
 */
const VITE_NOISE = /ws:\/\/|websocket|hmr|\[vite\]/i;

async function getFirstCategory(api: APIRequestContext, path: string) {
  const response = await api.get(path);
  const body = await response.text();
  expect(response.ok(), `load categories from ${path} (${response.status()}): ${body}`).toBeTruthy();
  const categories = JSON.parse(body) as Array<{ id: number; name: string }>;
  expect(categories.length, `seeded categories from ${path}`).toBeGreaterThan(0);
  return categories[0];
}

async function expectsCleanRender(page: import("@playwright/test").Page, path: string) {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (m) => {
    if (m.type() !== "error") return;
    const text = m.text();
    if (VITE_NOISE.test(text)) return;
    errors.push(text);
  });

  const res = await page.goto(path, { waitUntil: "networkidle" });
  expect(res?.status(), `HTTP status for ${path}`).toBeLessThan(400);
  await expect(page.locator("#root")).not.toBeEmpty();
  expect(errors, `no JS errors on ${path}`).toEqual([]);
}

test("public news detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const category = await getFirstCategory(api, "/api/news-categories");
  const slug = `smoke-news-${Date.now()}`;
  const created = await api.post("/api/news", {
    ...auth,
    data: {
      slug,
      date: "2026-06-13",
      imageUrl: "/images/news/x.jpg",
      category: category.name,
      newsCategoryId: category.id,
      title: "Smoke News",
      excerpt: "x",
      content: ["body"],
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  try {
    await expectsCleanRender(page, `/news/${slug}`);
  } finally {
    await api.delete(`/api/news/${id}`, auth);
  }
});

test("public project detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `smoke-proj-${Date.now()}`;
  const created = await api.post("/api/projects", {
    ...auth,
    data: {
      slug,
      imageUrl: "/images/projects/x.jpg",
      name: "Smoke Project",
      client: "Client",
      location: "HCM",
      scope: "Build",
      status: "ongoing",
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  try {
    await expectsCleanRender(page, `/projects/${slug}`);
  } finally {
    await api.delete(`/api/projects/${id}`, auth);
  }
});

test("public service detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `smoke-svc-${Date.now()}`;
  const created = await api.post("/api/services", {
    ...auth,
    data: {
      slug,
      title: "Smoke Service",
      shortTitle: "S",
      tagline: "T",
      intro: "I",
      sections: [{ heading: "Heading", body: ["line 1", "line 2"] }],
      highlights: ["h"],
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  try {
    await expectsCleanRender(page, `/services/${slug}`);
  } finally {
    await api.delete(`/api/services/${id}`, auth);
  }
});

test("public activity detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const category = await getFirstCategory(api, "/api/activity-categories");
  const slug = `smoke-act-${Date.now()}`;
  const created = await api.post("/api/activities", {
    ...auth,
    data: {
      slug,
      date: "2026-06-13",
      imageUrl: "/images/activities/x.jpg",
      category: category.name,
      categoryId: category.id,
      title: "Smoke Activity",
      excerpt: "x",
      content: ["p"],
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  try {
    await expectsCleanRender(page, `/activities/${slug}`);
  } finally {
    await api.delete(`/api/activities/${id}`, auth);
  }
});
