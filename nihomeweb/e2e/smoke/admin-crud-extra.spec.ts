import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Smoke CRUD round-trips for the other primary content types. Projects already
 * covered in admin-projects-crud.spec.ts — keep this file focused on news,
 * services and activities so the smoke suite exercises every main editor flow.
 */

test("admin CRUD round-trip for a news article", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `smoke-news-crud-${Date.now()}`;
  const payload = {
    slug,
    date: "2026-06-13",
    imageUrl: "/images/news/x.jpg",
    category: "general",
    title: "Smoke News CRUD",
    excerpt: "x",
    content: ["body"],
    sortOrder: 0,
  };

  const created = await api.post("/api/news", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const { id } = await created.json();

  expect((await api.get(`/api/news/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/news/${id}`, {
    ...auth,
    data: { ...payload, title: "Smoke News v2" },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/news/${id}`, auth)).status()).toBe(204);
});

test("admin CRUD round-trip for a service", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `smoke-svc-crud-${Date.now()}`;
  const payload = {
    slug,
    title: "Smoke Service CRUD",
    shortTitle: "S",
    tagline: "T",
    intro: "I",
    sections: [{ title: "Section", items: [] }],
    highlights: ["h"],
    sortOrder: 0,
  };

  const created = await api.post("/api/services", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const { id } = await created.json();

  expect((await api.get(`/api/services/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/services/${id}`, {
    ...auth,
    data: { ...payload, title: "Smoke Service v2" },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/services/${id}`, auth)).status()).toBe(204);
});

test("admin CRUD round-trip for an activity", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `smoke-act-crud-${Date.now()}`;
  const payload = {
    slug,
    date: "2026-06-13",
    imageUrl: "/images/activities/x.jpg",
    title: "Smoke Activity CRUD",
    excerpt: "x",
    content: ["body"],
    sortOrder: 0,
  };

  const created = await api.post("/api/activities", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const { id } = await created.json();

  expect((await api.get(`/api/activities/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/activities/${id}`, {
    ...auth,
    data: { ...payload, title: "Smoke Activity v2" },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/activities/${id}`, auth)).status()).toBe(204);
});
