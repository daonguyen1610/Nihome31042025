import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * SPA smoke for NIH-85 / NIH-95 / NIH-96 — tender admin page. Full API
 * behaviour is covered by nihomebackend.integration.tests; this spec is
 * intentionally narrow: verify the deployed SPA renders the page for a
 * role that has view access and does not throw JS errors. Cross-role
 * route gating lives in admin-rbac-matrix.spec.ts.
 */
test("SPA renders /admin/tenders without console errors for SALES_MANAGER", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`${baseURL}/admin/tenders`, { waitUntil: "networkidle" });

    await expect(
        page.getByRole("heading", { name: /Qu\u1ea3n l\u00fd G\u00f3i th\u1ea7u|Tender management|投标管理|入札管理/i }),
    ).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});
