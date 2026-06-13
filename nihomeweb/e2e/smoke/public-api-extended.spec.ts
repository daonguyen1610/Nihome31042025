import { test, expect } from "../fixtures/auth";

/**
 * Smoke coverage for the remaining public GET endpoints that back the public
 * site and recruitment flow. Each test is a single GET; this whole file runs
 * in a few seconds.
 */
const publicGets = [
  "/api/activities",
  "/api/logos",
  "/api/processes",
  "/api/slideshow",
  "/api/about-sections",
  "/api/employment-types",
  "/api/job-positions",
];

for (const path of publicGets) {
  test(`public API: GET ${path} returns 200`, async ({ api }) => {
    const res = await api.get(path);
    expect(res.status(), `status for ${path}`).toBe(200);
  });
}

test("public API: GET /api/recruitment-dropdown-options requires type", async ({ api }) => {
  const noType = await api.get("/api/recruitment-dropdown-options");
  expect(noType.status()).toBe(400);

  const withType = await api.get("/api/recruitment-dropdown-options?type=experienceLevel");
  expect(withType.status()).toBe(200);
  expect(Array.isArray(await withType.json())).toBe(true);
});
