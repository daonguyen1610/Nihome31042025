import { test, expect, TEST_USERS } from "../fixtures/auth";

test("admin can create, update and delete a project via API", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const slug = `e2e-smoke-${Date.now()}`;
  const payload = {
    slug,
    imageUrl: "/images/project-test.jpg",
    name: "E2E Smoke Project",
    client: "E2E Client",
    location: "Hanoi",
    scale: "Small",
    scope: "Residential",
    status: "ongoing",
    year: "2026",
    sortOrder: 0,
  };

  // Create
  const created = await api.post("/api/projects", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const { id } = await created.json();

  // List contains it
  const list = await api.get("/api/projects");
  expect(list.status()).toBe(200);
  const items = (await list.json()) as Array<{ slug: string }>;
  expect(items.some((x) => x.slug === slug)).toBe(true);

  // Update
  const updated = await api.put(`/api/projects/${id}`, {
    ...auth,
    data: { ...payload, name: "E2E Smoke Project (Updated)", status: "completed" },
  });
  expect(updated.status()).toBe(200);

  // Delete
  const deleted = await api.delete(`/api/projects/${id}`, auth);
  expect(deleted.status()).toBe(204);
});
