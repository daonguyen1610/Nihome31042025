import { test, expect, TEST_USERS } from "../../fixtures/auth";

test.describe("CORS", () => {
  test("preflight from configured origin is accepted", async ({ api }) => {
    const res = await api.fetch("/api/projects", {
      method: "OPTIONS",
      headers: {
        Origin: "http://localhost:3000",
        "Access-Control-Request-Method": "GET",
      },
    });
    expect([200, 204]).toContain(res.status());
  });
});

test.describe("Rate / brute-force protection", () => {
  test("repeated failed logins do not crash the server", async ({ api }) => {
    for (let i = 0; i < 5; i++) {
      const res = await api.post("/api/auth/login", {
        // Password must satisfy LoginRequest min-length so we exercise the auth
        // pipeline (401) rather than model validation (400).
        data: { phoneNumber: TEST_USERS.admin.phoneNumber, password: "definitely-wrong-pw" },
      });
      expect([401, 429], `attempt ${i}`).toContain(res.status());
    }
  });
});
