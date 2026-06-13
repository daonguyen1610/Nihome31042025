import { test, expect } from "../../fixtures/auth";

/**
 * Public auth flow surface — registration / forgot-password / login.
 *
 * These pages have multi-step OTP flows that are infeasible to fully exercise
 * in E2E without test SMS hooks. We cover:
 *   - the page renders with no JS errors,
 *   - the API rejects malformed payloads,
 *   - login with wrong credentials returns 401.
 */

const noiseRe = /ws:\/\/|websocket|hmr/i;

test("public auth pages render cleanly", async ({ page }) => {
  for (const path of ["/login", "/register", "/forgot-password"]) {
    const errors: string[] = [];
    page.on("pageerror", (e) => errors.push(e.message));
    page.on("console", (m) => {
      if (m.type() !== "error") return;
      const text = m.text();
      if (noiseRe.test(text)) return;
      errors.push(text);
    });

    const res = await page.goto(path);
    expect(res?.status()).toBeLessThan(400);
    await expect(page.locator("body")).toBeVisible();
    expect(errors, `errors on ${path}`).toEqual([]);
  }
});

test("login with bad credentials returns 401", async ({ api }) => {
  const res = await api.post("/api/auth/login", {
    data: { phoneNumber: "0900000000", password: "WrongPassword!" },
  });
  expect(res.status()).toBe(401);
});

test("register/start with empty phone returns 400", async ({ api }) => {
  const res = await api.post("/api/auth/register/start", {
    data: { phoneNumber: "", password: "x" },
  });
  expect(res.status()).toBe(400);
});

test("forgot/start with unknown phone returns 200 (soft-fail) or 400", async ({ api }) => {
  // The endpoint deliberately does not leak whether a phone exists; just confirm it doesn't 500.
  const res = await api.post("/api/auth/forgot/start", {
    data: { phoneNumber: "0999000000" },
  });
  expect([200, 400, 404]).toContain(res.status());
});
