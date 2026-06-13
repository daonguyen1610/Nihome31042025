import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for an employment type", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const code = `et-${Date.now()}`;

  const created = await api.post("/api/employment-types", {
    ...auth,
    data: { code, name: code, isActive: true, sortOrder: 0 },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/employment-types/${item.id}`, {
    ...auth,
    data: { code, name: code + "-v2", isActive: false, sortOrder: 1 },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/employment-types/${item.id}`, auth)).status()).toBe(204);
});
