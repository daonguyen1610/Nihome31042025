import { test, expect, TEST_USERS } from "../../fixtures/auth";

/**
 * Reference CRUD spec. Copy this file per entity (news, services, activities,
 * job positions, logos, slideshow, etc.) — change only the endpoint + payload.
 */
test("admin full CRUD round-trip for a project", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const slug = `e2e-full-${Date.now()}`;
  const payload = {
    slug,
    imageUrl: "/images/project-test.jpg",
    name: "E2E Full CRUD Project",
    client: "E2E Client",
    location: "Hanoi",
    scale: "Medium",
    scope: "Commercial",
    status: "ongoing",
    year: "2026",
    sortOrder: 0,
  };

  const created = await api.post("/api/projects", { ...auth, data: payload });
  expect(created.status()).toBe(201);
  const project = await created.json();

  const fetched = await api.get(`/api/projects/${slug}`);
  expect(fetched.status()).toBe(200);

  const updated = await api.put(`/api/projects/${project.id}`, {
    ...auth,
    data: { ...payload, status: "completed" },
  });
  expect(updated.status()).toBe(200);

  const deleted = await api.delete(`/api/projects/${project.id}`, auth);
  expect(deleted.status()).toBe(204);
});
