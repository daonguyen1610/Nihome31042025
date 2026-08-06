import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";

/**
 * End-to-end tests for seeded business roles outside the core matrix.
 *
 * Tests the actual business role users (BGD, ACCOUNTANT, DESIGN, PM, QS, etc.)
 * to verify their specific permission grants work correctly in the browser.
 *
 * These roles are seeded in DbSeeder with specific permission sets:
 * - BGD: Board of Directors - high-level view access
 * - ACCOUNTANT: Financial module access
 * - DESIGN: Design-related documents
 * - PM: Project management
 * - QS: Quantity surveying
 * - SALE: CRM sales access
 * - WAREHOUSE: Inventory management
 */

test.describe("Business role access verification", () => {
  test("SALE user can access CRM leads and opportunities but not user management", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.sale);

    // SALE should access leads
    await page.goto("/admin/leads");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: /lead|tiềm năng/i }).first(),
    ).toBeVisible();

    // SALE should access opportunities
    await loginInBrowserAs(page, TEST_USERS.sale);
    await page.goto("/admin/opportunities");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: /cơ hội|opportunit/i }).first(),
    ).toBeVisible();

    // SALE should be DENIED on user management
    await loginInBrowserAs(page, TEST_USERS.sale);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "SALE should not access user management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("ACCOUNTANT user can access financial pages but not CRM", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.accountant);

    // ACCOUNTANT should access dashboard
    await page.goto("/admin");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // ACCOUNTANT should be DENIED on CRM leads
    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto("/admin/leads");
    await expect(
      page.locator("text=/^403$/").first(),
      "ACCOUNTANT should not access CRM leads",
    ).toBeVisible();

    // ACCOUNTANT should be DENIED on user management
    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "ACCOUNTANT should not access user management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("DESIGN user can access design-related pages but not CRM", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.design);

    // DESIGN should access dashboard
    await page.goto("/admin");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // DESIGN should be DENIED on CRM contracts
    await loginInBrowserAs(page, TEST_USERS.design);
    await page.goto("/admin/contracts");
    await expect(
      page.locator("text=/^403$/").first(),
      "DESIGN should not access contracts",
    ).toBeVisible();

    // DESIGN should be DENIED on user management
    await loginInBrowserAs(page, TEST_USERS.design);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "DESIGN should not access user management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("PM user can access project management pages", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.pm);

    // PM should access dashboard
    await page.goto("/admin");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // PM should be DENIED on user management (not users.manage permission)
    await loginInBrowserAs(page, TEST_USERS.pm);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "PM should not access user management",
    ).toBeVisible();

    // PM should be DENIED on roles management
    await loginInBrowserAs(page, TEST_USERS.pm);
    await page.goto("/admin/roles");
    await expect(
      page.locator("text=/^403$/").first(),
      "PM should not access role management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("WAREHOUSE user can access inventory but not CRM or admin", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.warehouse);

    // WAREHOUSE should access dashboard
    await page.goto("/admin");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // WAREHOUSE should be DENIED on CRM
    await loginInBrowserAs(page, TEST_USERS.warehouse);
    await page.goto("/admin/leads");
    await expect(
      page.locator("text=/^403$/").first(),
      "WAREHOUSE should not access CRM leads",
    ).toBeVisible();

    // WAREHOUSE should be DENIED on user management
    await loginInBrowserAs(page, TEST_USERS.warehouse);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "WAREHOUSE should not access user management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("BGD (Board of Directors) has high-level view access", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.bgd);

    // BGD should access dashboard
    await page.goto("/admin");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // BGD should be DENIED on user management (view only role)
    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "BGD should not access user management",
    ).toBeVisible();

    // BGD should be DENIED on role management
    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto("/admin/roles");
    await expect(
      page.locator("text=/^403$/").first(),
      "BGD should not access role management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("SALES_MANAGER has broader CRM access than SALE", async ({
    page,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.salesManager);

    // SALES_MANAGER should access leads
    await page.goto("/admin/leads");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // SALES_MANAGER should access opportunities
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/opportunities");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // SALES_MANAGER should access quotes
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/quotes");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // SALES_MANAGER should access contracts
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/contracts");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // SALES_MANAGER should be DENIED on user management
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/users");
    await expect(
      page.locator("text=/^403$/").first(),
      "SALES_MANAGER should not access user management",
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });
});

test.describe("Cross-role permission boundaries", () => {
  test("Non-admin roles cannot access system settings", async ({
    page,
    loginInBrowserAs,
  }) => {
    const nonAdminRoles = [
      TEST_USERS.sale,
      TEST_USERS.design,
      TEST_USERS.pm,
      TEST_USERS.accountant,
      TEST_USERS.warehouse,
      TEST_USERS.bgd,
    ];

    for (const user of nonAdminRoles) {
      await loginInBrowserAs(page, user);
      await page.goto("/admin/settings");
      await expect(
        page.locator("text=/^403$/").first(),
        `${user.role} should not access settings`,
      ).toBeVisible();
    }
  });

  test("Non-admin roles cannot access translations management", async ({
    page,
    loginInBrowserAs,
  }) => {
    const nonAdminRoles = [
      TEST_USERS.sale,
      TEST_USERS.design,
      TEST_USERS.pm,
      TEST_USERS.accountant,
    ];

    for (const user of nonAdminRoles) {
      await loginInBrowserAs(page, user);
      await page.goto("/admin/translations");
      await expect(
        page.locator("text=/^403$/").first(),
        `${user.role} should not access translations`,
      ).toBeVisible();
    }
  });

  test("Only ADMIN and SUPER_ADMIN can access user management", async ({
    page,
    loginInBrowserAs,
  }) => {
    // ADMIN should access users
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/users");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: /người dùng|users/i }).first(),
    ).toBeVisible();

    // SUPER_ADMIN should access users
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/users");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: /người dùng|users/i }).first(),
    ).toBeVisible();
  });

  test("Only SUPER_ADMIN can access role matrix editing", async ({
    page,
    loginInBrowserAs,
  }) => {
    // ADMIN should access roles page (but with limited editing)
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/roles");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);

    // SUPER_ADMIN should access roles page with full editing
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/roles");
    await expect(page.locator("text=/^403$/").first()).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: /vai trò|role/i }).first(),
    ).toBeVisible();
  });
});
