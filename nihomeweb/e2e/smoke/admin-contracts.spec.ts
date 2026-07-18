import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * SPA smoke for NIH-102 — /admin/contracts list page. Full API + RBAC
 * behaviour is covered by nihomebackend.integration.tests/ContractsControllerTests.
 * This spec verifies the SPA renders for SUPER_ADMIN with seeded sample rows
 * and the filter row is present.
 */
test("SPA renders /admin/contracts without console errors for SUPER_ADMIN", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/contracts`, { waitUntil: "networkidle" });

    await expect(
        page.getByRole("heading", { name: /Hợp đồng|Contracts|销售合同|販売契約/i }),
    ).toBeVisible();

    // Filters row (status select + search input) always renders.
    await expect(page.locator("#c-search")).toBeVisible();
    await expect(page.locator("#c-status")).toBeVisible();

    // Sample seeder inserts at least one row for freshly booted stacks.
    await expect(page.locator("table tbody tr").first()).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});
