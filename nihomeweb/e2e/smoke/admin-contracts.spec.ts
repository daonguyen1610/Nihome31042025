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

    await page.getByRole("tab", { name: /Phụ lục|Variation orders|变更附录|変更契約/i }).click();
    await expect(
        page.getByText(/VO trong hệ thống|VO in the system|系统中的 VO|システム上のVO/i),
    ).toBeVisible();
    await expect(
        page.getByText(/Người có quyền chỉnh sửa có thể xoá VO ở mọi trạng thái|Users with edit permission may delete a VO in any status|拥有编辑权限的用户可以删除任何状态的 VO|編集権限を持つユーザーは、どのステータスのVOでも削除できます/i),
    ).toBeVisible();

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

test("paid milestone date is suggested, customizable, and displayed", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    const contractId = 455100;
    let actualPaymentDate: string | null = null;
    let status = "Pending";
    const contract = () => ({
        id: contractId,
        contractNumber: "HD-2026-4551",
        customerId: 1,
        customerName: "NICON",
        operationalProjectId: null,
        opportunityId: null,
        opportunityTitle: null,
        quoteId: null,
        quoteCode: null,
        designProjectId: null,
        designProjectCode: null,
        designProjectName: null,
        designProjectCurrentStage: null,
        ownerUserId: 1,
        ownerName: "E2E Admin",
        status: "Signed",
        signedDate: "2026-08-01T00:00:00Z",
        startDate: null,
        endDate: null,
        value: 100_000_000,
        approvedVoTotal: 0,
        currentValue: 100_000_000,
        hasSignedScan: false,
        attachmentCount: 0,
        appendixCount: 0,
        scopeOfWork: null,
        note: null,
        createdAt: "2026-08-01T00:00:00Z",
        updatedAt: "2026-08-30T00:00:00Z",
        rowVersion: "AAAAAAAAB9M=",
        paymentMilestones: [{
            id: 1,
            order: 1,
            name: "Final payment",
            percentValue: 100,
            amount: 100_000_000,
            dueDate: "2026-08-20T00:00:00Z",
            actualPaymentDate,
            status,
            note: null,
            createdAt: "2026-08-01T00:00:00Z",
            updatedAt: "2026-08-30T00:00:00Z",
        }],
    });

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.route(new RegExp(`/api/(?:v1/)?contracts/${contractId}(?:/.*)?$`), async route => {
        const request = route.request();
        const path = new URL(request.url()).pathname;
        if (request.method() === "PATCH" && path.endsWith("/milestones/1/status")) {
            const payload = request.postDataJSON() as { status: string; actualPaymentDate: string | null };
            status = payload.status;
            actualPaymentDate = payload.actualPaymentDate;
            await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(contract()) });
            return;
        }
        if (request.method() === "GET" && path.endsWith("/appendices")) {
            await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
            return;
        }
        if (request.method() === "GET" && path.endsWith("/attachments")) {
            await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
            return;
        }
        if (request.method() === "GET" && path.endsWith("/timeline")) {
            await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
            return;
        }
        await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(contract()) });
    });

    await page.goto(`${baseURL}/admin/contracts/${contractId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Lịch thanh toán|Payment schedule|付款计划|支払スケジュール/i }).click();
    await page.getByRole("button", { name: /Đánh dấu Đã thanh toán|Mark as Paid|标记为已付款|支払済にする/i }).click();

    const dateInput = page.locator("#contract-actual-payment-date");
    const localToday = await page.evaluate(() => {
        const now = new Date();
        const month = String(now.getMonth() + 1).padStart(2, "0");
        const day = String(now.getDate()).padStart(2, "0");
        return `${now.getFullYear()}-${month}-${day}`;
    });
    await expect(dateInput).toHaveValue(localToday);
    await dateInput.fill("2026-08-30");
    await page.getByRole("button", { name: /Xác nhận đã thanh toán|Confirm paid|确认已付款|支払済を確認/i }).click();

    await expect.poll(() => actualPaymentDate).toBe("2026-08-30T00:00:00.000Z");
    await expect(page.getByText(/Ngày thanh toán thực tế: 30\/08\/2026|Actual payment date: 30\/08\/2026|实际付款日期: 30\/08\/2026|実際の支払日: 30\/08\/2026/i)).toBeVisible();

    await page.route(new RegExp("/api/(?:v1/)?users/me/permissions$"), route =>
        route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({ role: "CONTRACT_VIEWER", roleId: null, permissions: ["crm.contracts.view"] }),
        }));
    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Lịch thanh toán|Payment schedule|付款计划|支払スケジュール/i }).click();
    await expect(page.getByRole("button", { name: /Sửa ngày thanh toán|Edit payment date|编辑付款日期|支払日を編集/i })).toHaveCount(0);
    await expect(page.getByRole("button", { name: /Trả về Chưa yêu cầu|Revert to Pending|退回为待处理|未請求に戻す/i })).toHaveCount(0);
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
