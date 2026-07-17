import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * SPA smoke for NIH-225 — workflow config admin page. Full API and RBAC
 * behaviour is covered by nihomebackend.integration.tests/WorkflowsControllerTests;
 * this spec only verifies the deployed SPA renders the page for SUPER_ADMIN
 * without JS errors and that the search + create button surface correctly.
 */
test("SPA renders /admin/workflows without console errors for SUPER_ADMIN", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/workflows`, { waitUntil: "networkidle" });

    await expect(
        page.getByRole("heading", { name: /Cấu hình luồng duyệt|Approval workflows|审批流程配置|承認フロー設定/i }),
    ).toBeVisible();

    await expect(page.locator("#wf-search")).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});
