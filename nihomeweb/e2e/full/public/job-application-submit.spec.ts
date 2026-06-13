import { test, expect, TEST_USERS } from "../../fixtures/auth";

/**
 * Public job-application API — anonymous submit + admin lifecycle.
 *
 * The form lives at /recruitment but the controller accepts anonymous POST,
 * so we exercise the API directly to keep the test deterministic.
 */

test("anonymous user can submit a job application", async ({ api, loginAs }) => {
  const adminToken = await loginAs(TEST_USERS.admin);
  const adminAuth = { headers: { Authorization: `Bearer ${adminToken}` } };

  // Seed an EmploymentType + JobPosition for the application to reference.
  const etCode = `ft-${Date.now().toString(36)}`;
  const et = await api.post("/api/employment-types", {
    ...adminAuth,
    data: { code: etCode, name: etCode, isActive: true, sortOrder: 0 },
  });
  expect(et.status()).toBe(201);

  const positionRes = await api.post("/api/job-positions", {
    ...adminAuth,
    data: {
      title: `E2E Position ${Date.now()}`,
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
  expect(positionRes.status()).toBe(201);
  const position = await positionRes.json();

  // Anonymous (no auth header) submit:
  const submitted = await api.post("/api/job-applications", {
    data: {
      jobPositionId: position.id,
      candidateName: "Jane Candidate",
      email: "jane@example.test",
      phone: "0900111222",
      experienceYears: 3,
      coverLetter: "Hello, I would like to apply.",
    },
  });
  expect(submitted.status()).toBe(201);
  const application = await submitted.json();

  // Admin can list and delete it:
  const list = await api.get("/api/job-applications", adminAuth);
  expect(list.status()).toBe(200);

  const patched = await api.patch(`/api/job-applications/${application.id}/status`, {
    ...adminAuth,
    data: { status: "interview" },
  });
  expect(patched.status()).toBe(200);

  expect((await api.delete(`/api/job-applications/${application.id}`, adminAuth)).status()).toBe(204);
  expect((await api.delete(`/api/job-positions/${position.id}`, adminAuth)).status()).toBe(204);
});

test("anonymous submit with missing required fields returns 400", async ({ api }) => {
  const res = await api.post("/api/job-applications", {
    data: { jobPositionId: 0, candidateName: "", email: "not-an-email" },
  });
  expect(res.status()).toBe(400);
});

test("anonymous user cannot list job applications", async ({ api }) => {
  const res = await api.get("/api/job-applications");
  expect(res.status()).toBe(401);
});
