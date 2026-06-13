import { test, expect } from "../../fixtures/auth";

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
      // Ignore Vite HMR / dev-mode WebSocket noise that is not a real app error.
      if (/ws:\/\/|websocket|hmr/i.test(text)) return;
      errors.push(text);
    });

    const res = await page.goto(path);
    expect(res?.status(), `HTTP status for ${path}`).toBeLessThan(400);
    await expect(page.locator("body")).toBeVisible();
    expect(errors, `no JS errors on ${path}`).toEqual([]);
  });
}
