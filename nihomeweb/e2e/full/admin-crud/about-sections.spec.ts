import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for an about section", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-about-${Date.now()}`;
  const payload = {
    slug,
    eyebrow: "About",
    titleA: "Title A",
    titleB: "Title B",
    paragraph1: "P1",
    paragraph2: "P2",
    imageUrl: "/images/x.jpg",
    isActive: true,
    sortOrder: 0,
  };

  const created = await api.post("/api/about-sections", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const item = await created.json();

  expect((await api.get(`/api/about-sections/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/about-sections/${item.id}`, { ...auth, data: { ...payload, eyebrow: "About v2" } });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/about-sections/${item.id}`, auth)).status()).toBe(204);
});
