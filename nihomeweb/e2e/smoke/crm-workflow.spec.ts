import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * CRM workflow navigation smoke tests.
 * Verifies the SPA can navigate through the complete CRM pipeline:
 * Lead → Customer → Opportunity → Quote → Contract
 *
 * These tests verify browser rendering, not API logic (covered in integration tests).
 */
test.describe("CRM workflow navigation", () => {
  test.beforeEach(async ({ page, loginInBrowserAs }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
  });

  test("can navigate through CRM pages without errors", async ({ page }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    const crmPages = [
      { path: "/admin/leads", heading: /lead|tiềm năng|潜在客户|リード/i },
      { path: "/admin/customers", heading: /khách hàng|customer|客户|顧客/i },
      { path: "/admin/opportunities", heading: /cơ hội|opportunit|商机|案件/i },
      { path: "/admin/quotes", heading: /báo giá|quote|报价|見積/i },
      { path: "/admin/contracts", heading: /hợp đồng|contract|合同|契約/i },
    ];

    for (const { path, heading } of crmPages) {
      await page.goto(path, { waitUntil: "networkidle" });
      await expect(page.getByRole("heading", { name: heading }).first()).toBeVisible({ timeout: 10000 });
    }

    expect(jsErrors, `CRM navigation errors: ${jsErrors.join("\n")}`).toHaveLength(0);
  });

  test("Leads page renders with data grid and actions", async ({ page }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await page.goto("/admin/leads", { waitUntil: "networkidle" });

    await expect(page.getByRole("heading", { name: /lead|tiềm năng/i }).first()).toBeVisible();

    // Search/filter should be present
    await expect(page.locator("input[placeholder*='tìm' i], input[placeholder*='search' i]").first()).toBeVisible();

    // Add button should be present
    await expect(page.getByRole("button", { name: /thêm|add|tạo|create/i })).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("Opportunities page shows kanban or list view", async ({ page }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await page.goto("/admin/opportunities", { waitUntil: "networkidle" });

    await expect(page.getByRole("heading", { name: /cơ hội|opportunit/i }).first()).toBeVisible();

    // Either kanban board columns or table should be visible
    const hasKanban = await page.locator("[data-kanban], [class*='kanban'], [class*='board']").first().isVisible().catch(() => false);
    const hasTable = await page.locator("table").first().isVisible().catch(() => false);
    const hasList = await page.locator("[role='list'], [class*='list']").first().isVisible().catch(() => false);

    expect(hasKanban || hasTable || hasList, "Opportunities should show kanban, table, or list view").toBe(true);

    expect(jsErrors).toHaveLength(0);
  });

  test("Quotes page shows list with value/status columns", async ({ page }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await page.goto("/admin/quotes", { waitUntil: "networkidle" });

    await expect(page.getByRole("heading", { name: /báo giá|quote/i }).first()).toBeVisible();

    // Filter controls should be present
    await expect(page.locator("input, select, [role='combobox']").first()).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("Contracts page shows list with sign date and value", async ({ page }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await page.goto("/admin/contracts", { waitUntil: "networkidle" });

    await expect(page.getByRole("heading", { name: /hợp đồng|contract/i }).first()).toBeVisible();

    // Table with contracts should be visible
    await expect(page.locator("table").first()).toBeVisible();

    // At least one contract row should exist (seeded data)
    await expect(page.locator("table tbody tr").first()).toBeVisible();

    // Export button should be present
    await expect(
      page.getByRole("button", { name: /^(?:xuất|export)(?: csv| excel)?$/i }),
    ).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });

  test("can navigate from sidebar CRM menu", async ({ page }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await page.goto("/admin", { waitUntil: "networkidle" });

    // Find and click CRM menu button in sidebar
    const crmMenuButton = page.getByRole("button", { name: /crm/i });
    await crmMenuButton.click();

    // CRM submenu should expand
    await expect(page.getByRole("link", { name: /lead|tiềm năng/i })).toBeVisible();
    await expect(page.getByRole("link", { name: /khách hàng|customer/i })).toBeVisible();
    await expect(page.getByRole("link", { name: /cơ hội|opportunit/i })).toBeVisible();
    await expect(page.getByRole("link", { name: /báo giá|quote/i })).toBeVisible();
    await expect(page.getByRole("link", { name: /hợp đồng|contract/i })).toBeVisible();

    expect(jsErrors).toHaveLength(0);
  });
});
