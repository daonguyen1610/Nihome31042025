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
    const row = page.locator('[data-testid^="contract-row-"]').first();
    await expect(row).toBeVisible();
    const contractId = await row.getAttribute("data-testid");
    await row.locator("td").nth(2).hover();
    await expect(row).toHaveAttribute("data-navigation-active", "true");
    await row.locator("[data-contract-actions]").getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).hover();
    await expect(row).toHaveAttribute("data-navigation-active", "false");
    await row.locator("td").nth(2).click();

    await expect(page).toHaveURL(new RegExp(`/admin/contracts/${contractId?.replace("contract-row-", "")}$`));

    const editButton = page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).first();
    await expect(editButton).toBeVisible();
    const contractNumber = await page.getByRole("heading", { level: 1 }).textContent();
    await editButton.click();
    const editForm = page.getByTestId("contract-inline-edit-form");
    await expect(editForm).toBeVisible();
    const numberInput = editForm.locator("#contract-detail-number");
    await numberInput.fill(`${contractNumber?.trim()}-cancelled`);
    await page.getByRole("button", { name: /Huỷ|Hủy|Cancel|取消|キャンセル/i }).click();
    await expect(editForm).toBeHidden();
    await expect(page.getByRole("heading", { level: 1 })).toContainText(contractNumber?.trim() ?? "");

    await editButton.click();
    const updateResponse = page.waitForResponse((response) =>
        response.request().method() === "PUT" && /\/api\/contracts\/\d+$/.test(response.url()),
    );
    await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
    expect((await updateResponse).ok()).toBe(true);
    await expect(editForm).toBeHidden();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});

test("mobile contract card opens the complete contract detail", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/contracts`, { waitUntil: "networkidle" });

    const card = page.locator('[data-testid^="contract-card-"]').first();
    await expect(card).toBeVisible();
    const contractId = await card.getAttribute("data-testid");
    const cardLink = card.locator(":scope > a");
    const cardBox = await card.boundingBox();
    const linkBox = await cardLink.boundingBox();
    expect(Math.abs((linkBox?.width ?? 0) - (cardBox?.width ?? 0))).toBeLessThanOrEqual(2);
    expect(Math.abs((linkBox?.height ?? 0) - (cardBox?.height ?? 0))).toBeLessThanOrEqual(2);
    await card.getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).hover();
    expect(await cardLink.evaluate((element) => element.matches(":hover"))).toBe(false);
    await cardLink.click();

    await expect(page).toHaveURL(new RegExp(`/admin/contracts/${contractId?.replace("contract-card-", "")}$`));
    await expect(page.getByRole("link", { name: /Hợp đồng|Contracts|销售合同|販売契約/i })).toBeVisible();
});
