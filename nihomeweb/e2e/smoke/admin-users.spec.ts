import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Admin Users page smoke tests.
 * Verifies the SPA renders the user management page correctly for authorized roles.
 */
test.describe("Admin Users page", () => {
  test("SUPER_ADMIN can view users list with table and filters", async ({ page, loginInBrowserAs }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/users", { waitUntil: "networkidle" });

    // Page heading should be visible
    await expect(
      page.getByRole("heading", { name: /người dùng|users|用户|ユーザー/i }).first()
    ).toBeVisible();

    // Search input should be present
    await expect(
      page.locator("input[placeholder*='tìm' i], input[placeholder*='search' i]").first()
    ).toBeVisible();

    // Role filter should be present
    await expect(
      page.locator("[role='combobox'], select").first()
    ).toBeVisible();

    // Users table should have at least one row (seeded data)
    await expect(page.locator("table tbody tr").first()).toBeVisible();

    // Add user button should be visible
    await expect(
      page.getByRole("button", { name: /thêm|add|create|tạo/i })
    ).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
  });

  test("ADMIN can view users list", async ({ page, loginInBrowserAs }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/users", { waitUntil: "networkidle" });

    // Page should load without 403
    await expect(
      page.getByRole("heading", { name: /người dùng|users|用户|ユーザー/i }).first()
    ).toBeVisible();

    // Table should be visible
    await expect(page.locator("table").first()).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
  });

  test("User detail edit dialog renders when clicking edit button", async ({ page, loginInBrowserAs }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/users", { waitUntil: "networkidle" });

    // Wait for table to load
    await expect(page.locator("table tbody tr").first()).toBeVisible();

    // Click edit button on first user row
    const editButton = page.locator("table tbody tr").first().getByRole("button", { name: /sửa|edit/i });
    await editButton.click();

    // Edit dialog/form should appear
    await expect(
      page.getByRole("dialog").or(page.locator("form")).first()
    ).toBeVisible({ timeout: 5000 });

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
  });
});
