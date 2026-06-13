import { test, expect } from "../fixtures/auth";

/**
 * Smoke check that every top-level public route renders without crashing.
 * Faster than the full per-route suite: one shared helper, no per-page assertions
 * beyond "React mounted and the console is clean".
 */
const publicRoutes = [
  "/",
  "/services",
  "/projects",
  "/news",
  "/activities",
  "/clients",
  "/recruitment",
  "/contact",
  "/login",
  "/register",
  "/forgot-password",
];

for (const path of publicRoutes) {
  test(`public route ${path} renders cleanly`, async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (e) => errors.push(e.message));
    page.on("console", (m) => {
      if (m.type() !== "error") return;
      const text = m.text();
      if (/ws:\/\/|websocket|hmr|\[vite\]/i.test(text)) return;
      errors.push(text);
    });

    const res = await page.goto(path);
    expect(res?.status(), `HTTP status for ${path}`).toBeLessThan(400);
    await expect(page.locator("#root")).not.toBeEmpty();
    expect(errors, `no JS errors on ${path}`).toEqual([]);
  });
}
