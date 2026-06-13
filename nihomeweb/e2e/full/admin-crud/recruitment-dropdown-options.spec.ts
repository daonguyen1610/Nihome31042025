import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a recruitment dropdown option", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const code = `e2e-${Date.now().toString(36)}`;

  // GET seeds defaults for known type, anonymous OK
  const seedRes = await api.get("/api/recruitment-dropdown-options?type=experience-level");
  expect(seedRes.status()).toBe(200);

  const created = await api.post("/api/recruitment-dropdown-options", {
    ...auth,
    data: {
      type: "experience-level",
      code,
      name: "E2E Custom Level",
      isActive: true,
      sortOrder: 99,
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/recruitment-dropdown-options/${item.id}`, {
    ...auth,
    data: {
      type: "experience-level",
      code,
      name: "E2E Renamed",
      isActive: false,
      sortOrder: 100,
    },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/recruitment-dropdown-options/${item.id}`, auth)).status()).toBe(204);
});

test("anonymous cannot create dropdown options", async ({ api }) => {
  const res = await api.post("/api/recruitment-dropdown-options", {
    data: { type: "benefit", code: "x", name: "x", isActive: true, sortOrder: 0 },
  });
  expect(res.status()).toBe(401);
});

test("GET without type returns 400", async ({ api }) => {
  const res = await api.get("/api/recruitment-dropdown-options");
  expect(res.status()).toBe(400);
});
