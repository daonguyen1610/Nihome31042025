import { test, expect, TEST_USERS } from "../fixtures/auth";

test.describe("Auth API", () => {
  test("login with valid super-admin returns access token", async ({ loginAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    expect(token).toMatch(/^eyJ/); // JWT shape
  });

  test("login with wrong password is rejected", async ({ api }) => {
    const res = await api.post("/api/auth/login", {
      // password must satisfy LoginRequest min-length validation so we hit the auth
      // path (401) rather than model-binding (400).
      data: { phoneNumber: TEST_USERS.superAdmin.phoneNumber, password: "wrong-password" },
    });
    expect(res.status()).toBe(401);
  });

  test("protected endpoint without token returns 401", async ({ api }) => {
    const res = await api.get("/api/job-applications");
    expect(res.status()).toBe(401);
  });

  test("protected endpoint with admin token returns 200", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.admin);
    const res = await api.get("/api/job-applications", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(200);
  });
});
