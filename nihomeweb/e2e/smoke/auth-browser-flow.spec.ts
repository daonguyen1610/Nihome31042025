import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Browser-based authentication flow tests.
 * Tests the actual user interaction: typing credentials, clicking buttons,
 * and verifying redirects - not just API-level auth.
 */
test.describe("Browser authentication flow", () => {
  test("user can login via the login form and is redirected to admin", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    page.on("console", (msg) => {
      if (msg.type() !== "error") return;
      const text = msg.text();
      if (/ws:\/\/|websocket|hmr|\[vite\]/i.test(text)) return;
      errors.push(text);
    });

    await page.goto("/login");
    await expect(page.locator("input[type='text'], input[placeholder*='phone' i], input[placeholder*='điện thoại' i]").first()).toBeVisible();

    // Fill login form
    const phoneInput = page.locator("input[type='text'], input[placeholder*='phone' i], input[placeholder*='điện thoại' i]").first();
    const passwordInput = page.locator("input[type='password']").first();
    const loginButton = page.getByRole("button", { name: /đăng nhập|login|sign in/i });

    await phoneInput.fill(TEST_USERS.superAdmin.phoneNumber);
    await passwordInput.fill(TEST_USERS.superAdmin.password);
    await loginButton.click();

    // Should redirect to admin dashboard
    await page.waitForURL(/\/admin/, { timeout: 10000 });
    await expect(page.getByRole("heading", { name: /bảng điều khiển|dashboard|tổng quan/i })).toBeVisible({ timeout: 10000 });

    expect(errors, "no JS errors during login flow").toEqual([]);
  });

  test("logged in user can logout and is redirected to login page", async ({ page, loginInBrowserAs }) => {
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    page.on("console", (msg) => {
      if (msg.type() !== "error") return;
      const text = msg.text();
      if (/ws:\/\/|websocket|hmr|\[vite\]/i.test(text)) return;
      errors.push(text);
    });

    // Login via cookie injection (standard fixture approach)
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin");
    await expect(page.getByRole("heading", { name: /bảng điều khiển|dashboard|tổng quan/i })).toBeVisible();

    // Click logout button
    const logoutButton = page.getByRole("button", { name: /đăng xuất|logout|sign out/i });
    await logoutButton.click();

    // Should redirect to login page
    await page.waitForURL(/\/login/, { timeout: 10000 });
    await expect(page.locator("input[type='password']").first()).toBeVisible();

    expect(errors, "no JS errors during logout flow").toEqual([]);
  });

  test("login page shows validation error for invalid credentials", async ({ page }) => {
    await page.goto("/login");

    const phoneInput = page.locator("input[type='text'], input[placeholder*='phone' i], input[placeholder*='điện thoại' i]").first();
    const passwordInput = page.locator("input[type='password']").first();
    const loginButton = page.getByRole("button", { name: /đăng nhập|login|sign in/i });

    await phoneInput.fill("0000000000");
    await passwordInput.fill("wrongpassword");
    await loginButton.click();

    // Should show error message (toast or inline)
    await expect(
      page.getByText(/invalid|sai|không đúng|lỗi|error|failed/i).first()
    ).toBeVisible({ timeout: 5000 });
  });
});
