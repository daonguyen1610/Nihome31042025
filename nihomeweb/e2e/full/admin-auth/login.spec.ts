import { test, expect, TEST_USERS } from "../../fixtures/auth";

test.describe("Admin login flow", () => {
  // TODO: fill in the real selectors from src/pages/Login.tsx (input names/labels
  // and submit button label) and remove `.skip`. Skeleton kept as a template.
  test.skip("login page submits and persists session", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel(/phone|sđt|số điện thoại/i).fill(TEST_USERS.admin.phoneNumber);
    await page.getByLabel(/password|mật khẩu/i).fill(TEST_USERS.admin.password);
    await page.getByRole("button", { name: /login|đăng nhập/i }).click();

    await page.waitForURL((url) => !url.pathname.endsWith("/login"), { timeout: 10_000 });
  });

  test.skip("logout clears the session", async ({ page, loginInBrowserAs }) => {
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin");
    const logout = page.getByRole("button", { name: /logout|đăng xuất/i }).first();
    if (await logout.isVisible().catch(() => false)) {
      await logout.click();
      await page.waitForURL(/\/login/);
    }
  });
});
