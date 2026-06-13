import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin full CRUD round-trip for a job position", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  // Seed an EmploymentType first so the job position has a valid foreign reference.
  const etCode = `ft-${Date.now()}`;
  const et = await api.post("/api/employment-types", {
    ...auth,
    data: { code: etCode, name: etCode, isActive: true, sortOrder: 0 },
  });
  expect(et.status()).toBe(201);
  const etItem = await et.json();

  const created = await api.post("/api/job-positions", {
    ...auth,
    data: {
      title: `E2E Dev ${Date.now()}`,
      department: "Tech",
      location: "HCM",
      employmentType: etCode,
      experienceLevel: "mid",
      description: "x",
      requirements: ["csharp"],
      isActive: true,
      sortOrder: 0,
    },
  });
  expect(created.status()).toBe(201);
  const item = await created.json();

  const updated = await api.put(`/api/job-positions/${item.id}`, {
    ...auth,
    data: {
      title: "E2E Dev v2",
      department: "Tech",
      location: "HCM",
      employmentType: etCode,
      experienceLevel: "senior",
      description: "x2",
      requirements: ["csharp", "dotnet"],
      isActive: false,
      sortOrder: 1,
    },
  });
  expect(updated.status()).toBe(200);

  expect((await api.delete(`/api/job-positions/${item.id}`, auth)).status()).toBe(204);
  expect((await api.delete(`/api/employment-types/${etItem.id}`, auth)).status()).toBe(204);
});
