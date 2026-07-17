import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * SPA smoke for NIH-98 — capability documents admin page. Full API and RBAC
 * behaviour is covered by nihomebackend.integration.tests; this spec is
 * intentionally narrow: it only verifies the deployed SPA renders the page
 * for a role that has view access and does not throw JS errors. Cross-role
 * route access is already checked in admin-rbac-matrix.spec.ts.
 */
test("SPA renders /admin/capability-documents without console errors for SALES_MANAGER", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`${baseURL}/admin/capability-documents`, { waitUntil: "networkidle" });

    await expect(
        page.getByRole("heading", { name: /Hồ sơ năng lực|Capability documents|能力文件|能力書類/i }),
    ).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});
