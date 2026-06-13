import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a service", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const slug = `e2e-svc-${Date.now()}`;
  const payload = {
    slug,
    title: "E2E Service",
    shortTitle: "ESvc",
    tagline: "Tag",
    intro: "Intro",
    sections: [{ title: "Section", items: [] }],
    highlights: ["h1"],
    sortOrder: 0,
  };

  const created = await api.post("/api/services", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const item = await created.json();

  expect((await api.get(`/api/services/${slug}`)).status()).toBe(200);

  const updated = await api.put(`/api/services/${item.id}`, { ...auth, data: { ...payload, title: "E2E Service v2" } });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/services/${item.id}`, auth)).status()).toBe(204);
});
