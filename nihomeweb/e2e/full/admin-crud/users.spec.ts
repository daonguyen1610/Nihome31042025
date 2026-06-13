import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("super-admin full CRUD round-trip for a user", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const phone = `0987${Math.floor(100000 + Math.random() * 899999)}`;
  const created = await api.post("/api/users", {
    ...auth,
    data: {
      phoneNumber: phone,
      fullName: "E2E User",
      email: "e2e@example.com",
      password: "P@ssword1",
      role: "ADMIN",
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/users/${item.id}`, {
    ...auth,
    data: { fullName: "E2E User v2", role: "ADMIN", isActive: false },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/users/${item.id}`, auth)).status()).toBe(204);
});
