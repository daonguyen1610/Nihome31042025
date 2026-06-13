import { test, expect } from "../fixtures/auth";

test.describe("Public homepage", () => {
  test("renders without console errors and has nav", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    page.on("console", (msg) => {
      if (msg.type() === "error") errors.push(msg.text());
    });

    await page.goto("/");
    await expect(page).toHaveTitle(/.+/);
    await expect(page.locator("nav, header").first()).toBeVisible();

    expect(errors, "no console errors on homepage").toEqual([]);
  });

  test("main navigation links resolve to a 200 page", async ({ page }) => {
    const paths = ["/services", "/projects", "/news", "/activities", "/recruitment", "/contact"];
    for (const path of paths) {
      const res = await page.goto(path);
      expect(res?.status(), `${path} responds 2xx`).toBeLessThan(400);
    }
  });
});
