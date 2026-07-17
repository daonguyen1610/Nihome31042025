import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * SPA smoke for NIH-230 — master data admin page. Full API and RBAC behaviour
 * is covered by nihomebackend.integration.tests/MasterDataControllerTests;
 * this spec only verifies the deployed SPA renders the page for SUPER_ADMIN
 * without JS errors and that at least one seeded category renders options.
 */
test("SPA renders /admin/master-data without console errors for SUPER_ADMIN", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/master-data`, { waitUntil: "networkidle" });

    await expect(
        page.getByRole("heading", { name: /Dữ liệu chuẩn|Master data|主数据|マスタデータ/i }),
    ).toBeVisible();

    // A category picker + at least one row (or card) means the API round trip
    // succeeded and the seeded catalogue rendered.
    await expect(page.locator("#md-category")).toBeVisible();
    const rows = page.locator("table tbody tr, ul.grid > li");
    await expect(rows.first()).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});
