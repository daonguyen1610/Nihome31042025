import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a logo", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const created = await api.post("/api/logos", {
    ...auth,
    data: { name: `E2E Logo ${Date.now()}`, imageUrl: "/images/logos/x.png", href: "https://x.com", kind: "Client", sortOrder: 0 },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/logos/${item.id}`, {
    ...auth,
    data: { name: `${item.name}-v2`, imageUrl: "/images/logos/x.png", href: "https://x.com", kind: "Partner", sortOrder: 1 },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/logos/${item.id}`, auth)).status()).toBe(204);
});
