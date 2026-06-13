import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for an activity category", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const name = `E2E ACat ${Date.now()}`;

  const created = await api.post("/api/activity-categories", { ...auth, data: { name, isActive: true, sortOrder: 0 } });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/activity-categories/${item.id}`, {
    ...auth,
    data: { name: name + "-v2", isActive: false, sortOrder: 1 },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/activity-categories/${item.id}`, auth)).status()).toBe(204);
});
