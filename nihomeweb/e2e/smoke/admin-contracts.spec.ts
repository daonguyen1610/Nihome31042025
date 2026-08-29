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
    const response = await updateResponse;
    expect(
        response.ok(),
        `Contract update failed with ${response.status()}: ${await response.text()}`,
    ).toBe(true);
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

test("contract documents accept and upload multiple local files", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/contracts`, { waitUntil: "networkidle" });

    const row = page.locator('[data-testid^="contract-row-"]').first();
    await expect(row).toBeVisible();
    const rowTestId = await row.getAttribute("data-testid");
    const contractId = Number(rowTestId?.replace("contract-row-", ""));
    expect(contractId).toBeGreaterThan(0);

    const uploadedFiles: Array<Record<string, unknown>> = [];
    await page.route(`**/api/contracts/${contractId}/attachments`, async (route) => {
        const request = route.request();
        if (request.method() === "GET") {
            await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(uploadedFiles) });
            return;
        }
        if (request.method() !== "POST") {
            await route.continue();
            return;
        }

        const multipartBody = request.postDataBuffer()?.toString("utf8") ?? "";
        const fileName = multipartBody.match(/filename="([^"]+)"/)?.[1] ?? "unknown.pdf";
        const attachment = {
            id: uploadedFiles.length + 1,
            contractId,
            kind: "Supporting",
            filePath: `/files/contracts/${fileName}`,
            originalFileName: fileName,
            fileSize: 32,
            contentType: "application/pdf",
            label: null,
            createdAt: new Date().toISOString(),
            uploadedByUserId: 1,
            uploadedByName: "E2E Admin",
        };
        uploadedFiles.push(attachment);
        await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(attachment) });
    });

    await page.goto(`${baseURL}/admin/contracts/${contractId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu|Documents|文档|資料/i }).click();

    const fileInput = page.locator("#contract-attachment-files");
    await expect(fileInput).toHaveAttribute("multiple", "");
    await fileInput.setInputFiles([
        {
            name: "contract-batch-one.pdf",
            mimeType: "application/pdf",
            buffer: Buffer.from("%PDF-1.4\ncontract one\n%%EOF"),
        },
        {
            name: "contract-batch-two.pdf",
            mimeType: "application/pdf",
            buffer: Buffer.from("%PDF-1.4\ncontract two\n%%EOF"),
        },
    ]);

    await expect.poll(() => uploadedFiles.length).toBe(2);
    await expect(page.getByText("contract-batch-one.pdf", { exact: true })).toBeVisible();
    await expect(page.getByText("contract-batch-two.pdf", { exact: true })).toBeVisible();
});
