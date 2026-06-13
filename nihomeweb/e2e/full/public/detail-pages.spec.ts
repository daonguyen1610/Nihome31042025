import { test, expect, TEST_USERS } from "../../fixtures/auth";

/**
 * Public detail-page rendering.
 *
 * Strategy: seed each entity through the admin API as ADMIN, hit the public
 * detail route, assert: 200 status, body visible, no JS errors. Cleanup via
 * DELETE in finally to keep the seeded DB clean.
 */

const VITE_NOISE = /ws:\/\/|websocket|hmr/i;

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
  // The SPA shell is always present; assert the React root rendered something.
  await expect(page.locator("#root")).not.toBeEmpty();
  expect(errors, `no JS errors on ${path}`).toEqual([]);
}

test("public news detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-news-detail-${Date.now()}`;
  const created = await api.post("/api/news", {
    ...auth,
    data: {
      slug,
      date: "2026-06-13",
      imageUrl: "/images/news/x.jpg",
      category: "general",
      title: "Detail E2E News",
      excerpt: "x",
      content: ["body line"],
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  try {
    await expectsCleanRender(page, `/news/${slug}`);
  } finally {
    await api.delete(`/api/news/${item.id}`, auth);
  }
});

test("public activity detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-act-detail-${Date.now()}`;
  const created = await api.post("/api/activities", {
    ...auth,
    data: {
      slug,
      date: "2026-06-13",
      imageUrl: "/images/activities/x.jpg",
      category: "Events",
      title: "Detail E2E Activity",
      excerpt: "x",
      content: ["p"],
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  try {
    await expectsCleanRender(page, `/activities/${slug}`);
  } finally {
    await api.delete(`/api/activities/${item.id}`, auth);
  }
});

test("public project detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-proj-detail-${Date.now()}`;
  const created = await api.post("/api/projects", {
    ...auth,
    data: {
      slug,
      imageUrl: "/images/projects/x.jpg",
      name: "Detail E2E Project",
      client: "Client",
      location: "HCM",
      scope: "Build",
      status: "ongoing",
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  try {
    await expectsCleanRender(page, `/projects/${slug}`);
  } finally {
    await api.delete(`/api/projects/${item.id}`, auth);
  }
});

test("public service detail page renders", async ({ api, loginAs, page }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-svc-detail-${Date.now()}`;
  const created = await api.post("/api/services", {
    ...auth,
    data: {
      slug,
      title: "Detail E2E Service",
      shortTitle: "S",
      tagline: "T",
      intro: "I",
      sections: [{ heading: "Heading", body: ["line 1", "line 2"] }],
      highlights: ["h"],
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  try {
    await expectsCleanRender(page, `/services/${slug}`);
  } finally {
    await api.delete(`/api/services/${item.id}`, auth);
  }
});

test("public unknown news slug renders without crashing", async ({ page }) => {
  // SPA returns 200 with a NotFound view; we only assert the page renders cleanly.
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (m) => {
    if (m.type() !== "error") return;
    const text = m.text();
    if (VITE_NOISE.test(text)) return;
    // Backend will 404 for the missing slug; the SPA logs the API error but the page itself renders.
    if (/404|not\s*found/i.test(text)) return;
    errors.push(text);
  });
  await page.goto("/news/this-slug-does-not-exist", { waitUntil: "networkidle" });
  await expect(page.locator("#root")).not.toBeEmpty();
  expect(errors).toEqual([]);
});
