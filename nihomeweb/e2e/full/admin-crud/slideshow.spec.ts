import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a slideshow slide", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-slide-${Date.now()}`;
  const payload = {
    slug,
    imageUrl: "/images/slide.jpg",
    title: "E2E Slide",
    subtitle: "Sub",
    linkUrl: "/about",
    linkText: "More",
    isActive: true,
    sortOrder: 0,
  };

  const created = await api.post("/api/slideshow", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const item = await created.json();

  expect((await api.get(`/api/slideshow/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/slideshow/${item.id}`, { ...auth, data: { ...payload, title: "E2E Slide v2" } });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/slideshow/${item.id}`, auth)).status()).toBe(204);
});
