import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a process document", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const payload = {
    groupKey: "general",
    code: `P-${Date.now()}`,
    title: `E2E Process ${Date.now()}`,
    sortOrder: 0,
    images: [],
    files: [],
  };

  const created = await api.post("/api/processes", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/processes/${item.id}`, { ...auth, data: { ...payload, title: payload.title + "-v2" } });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/processes/${item.id}`, auth)).status()).toBe(204);
});
