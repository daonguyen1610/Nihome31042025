import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for an activity", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-act-${Date.now()}`;
  const payload = {
    slug,
    date: "2026-06-13",
    imageUrl: "/images/activities/x.jpg",
    title: "E2E Activity",
    excerpt: "x",
    content: ["body"],
    sortOrder: 0,
  };

  const created = await api.post("/api/activities", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const item = await created.json();

  expect((await api.get(`/api/activities/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/activities/${item.id}`, { ...auth, data: { ...payload, title: "E2E Activity v2" } });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/activities/${item.id}`, auth)).status()).toBe(204);
});
