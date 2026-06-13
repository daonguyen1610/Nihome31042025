import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a news article", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-news-${Date.now()}`;
  const payload = {
    slug,
    date: "2026-06-13",
    imageUrl: "/images/news/x.jpg",
    category: "general",
    title: "E2E News",
    excerpt: "x",
    content: ["body"],
    sortOrder: 0,
  };

  const created = await api.post("/api/news", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const item = await created.json();

  expect((await api.get(`/api/news/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/news/${item.id}`, { ...auth, data: { ...payload, title: "E2E News v2" } });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/news/${item.id}`, auth)).status()).toBe(204);
});
