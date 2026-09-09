import { test, expect, TEST_USERS } from "../fixtures/auth";

test("Finance primary-contract list is upstream-only and server-paginated", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await loginInBrowserAs(page, TEST_USERS.accountant);
    const listResponse = page.waitForResponse((response) => {
        if (response.request().method() !== "GET") return false;
        const url = new URL(response.url());
        return url.pathname.endsWith("/api/contracts")
            && url.searchParams.get("direction") === "Upstream"
            && url.searchParams.get("page") === "1"
            && url.searchParams.get("pageSize") === "20";
    });

    await page.goto(`${baseURL}/admin/finance/contracts`, { waitUntil: "networkidle" });

    expect((await listResponse).ok()).toBe(true);
    await expect(page.getByRole("heading", { name: /Hợp đồng chính|Primary contracts|主合同|主要契約/i })).toBeVisible();
    await expect(page.locator("#c-owner")).toBeVisible();
    await expect(page.locator("#c-end-from")).toHaveAttribute("aria-label", /Ngày kết thúc từ|End date from|结束日期起|終了日/);
    await expect(page.locator("#c-end-to")).toHaveAttribute("aria-label", /Ngày kết thúc đến|End date to|结束日期止|終了日/);
    await expect(page.locator("#c-direction")).toHaveCount(0);
    await expect(page.locator("#c-vendor")).toHaveCount(0);
    await page.getByRole("button", { name: /Bộ lọc nâng cao|Advanced filters|高级筛选|詳細フィルター/i }).click();
    await expect(page.locator("#c-sort-by")).toBeVisible();
    await expect(page.locator("#c-sort-direction")).toBeVisible();
});

test("Finance create and detail navigation stay in the primary-contract context", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/finance/contracts`, { waitUntil: "networkidle" });

    await page.locator("header").getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|新規契約/i }).click();
    await expect(page.locator("#c-direction-form")).toHaveCount(0);
    await expect(page.getByText(/Đầu ra - Khách hàng|Upstream - Customer|上游 - 客户|上流 - 顧客/i).last()).toBeVisible();
    await page.getByRole("button", { name: /Huỷ|Hủy|Cancel|取消|キャンセル/i }).click();

    const filteredResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname.endsWith("/api/contracts") && url.searchParams.get("search") === "HD";
    });
    await page.locator("#c-search").fill("HD");
    expect((await filteredResponse).ok()).toBe(true);
    const endDateResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname.endsWith("/api/contracts") && url.searchParams.get("endFrom")?.startsWith("2026-01-01");
    });
    await page.locator("#c-end-from").fill("2026-01-01");
    expect((await endDateResponse).ok()).toBe(true);
    await expect(page).toHaveURL(/search=HD/);

    const row = page.locator('[data-testid^="contract-row-"]').first();
    await expect(row).toBeVisible();
    await row.locator("td").nth(1).click();
    const returnLink = page.locator('main a[href^="/admin/finance/contracts?"]').first();
    await expect(returnLink).toBeVisible();
    await returnLink.click();
    await expect(page).toHaveURL(/\/admin\/finance\/contracts\?search=HD.*endFrom=2026-01-01/);
    await expect(page.locator("#c-search")).toHaveValue("HD");
});

test("Finance scope denies Sales and recovers from list API errors", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    await loginInBrowserAs(page, TEST_USERS.sale);
    await page.goto(`${baseURL}/admin/finance/contracts`, { waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: "403" })).toBeVisible();

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    let attempts = 0;
    await page.route(/\/api\/(?:v1\/)?contracts\?.*$/, async route => {
        attempts += 1;
        if (attempts === 1) {
            await route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ detail: "List unavailable" }) });
            return;
        }
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({ total: 0, page: 1, pageSize: 20, items: [], totalCurrentValue: 0, overdueContractCount: 0, dueSoonContractCount: 0 }),
        });
    });
    await page.goto(`${baseURL}/admin/finance/contracts`, { waitUntil: "networkidle" });
    await expect(page.getByText("List unavailable")).toBeVisible();
    await page.getByRole("button", { name: /Thử lại|Retry|重试|再試行/i }).click();
    await expect(page.getByText(/Chưa có hợp đồng nào|No contracts yet|暂无合同|契約がありません/i)).toBeVisible();
    await page.locator("#c-search").fill("NO-MATCH");
    await expect(page.getByText(/Không có hợp đồng nào khớp|No contracts match|没有符合|一致する契約はありません/i)).toBeVisible();
});

test("Contract pagination and CSV export use every filtered API page", async ({
    page,
    loginInBrowserAs,
    baseURL,
}) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    const exportRequests: string[] = [];
    const exportSearches: string[] = [];
    await page.route(/\/api\/(?:v1\/)?contracts(?:\/export-data)?\?.*$/, async route => {
        const url = new URL(route.request().url());
        const requestedPage = Number(url.searchParams.get("page") ?? "1");
        const pageSize = Number(url.searchParams.get("pageSize") ?? "20");
        const isExport = url.pathname.endsWith("/export-data");
        const total = isExport ? 101 : 21;
        if (isExport) {
            exportRequests.push(url.pathname);
            exportSearches.push(url.searchParams.get("search") ?? "");
        }
        const from = isExport ? 0 : (requestedPage - 1) * pageSize;
        const count = isExport ? total : Math.max(0, Math.min(pageSize, total - from));
        const items = Array.from({ length: count }, (_, index) => {
            const id = 700_000 + from + index + 1;
            return {
                id,
                contractNumber: `HD-PAGE-${String(id).padStart(6, "0")}`,
                customerId: 1,
                customerName: "Pagination customer",
                direction: "Upstream",
                type: "DesignAndBuild",
                operationalProjectId: 1,
                operationalProjectCode: "PJ-PAGE",
                operationalProjectName: "Pagination project",
                ownerUserId: 1,
                ownerName: "Pagination owner",
                status: index === 0 ? "InProgress" : "Draft",
                signedDate: null,
                startDate: null,
                endDate: index === 0 ? "2020-01-01T00:00:00Z" : null,
                value: id,
                approvedVoTotal: index === 0 ? 45 : 0,
                currentValue: index === 0 ? id + 45 : id,
                nextPaymentDueDate: null,
                nextPaymentAmount: null,
                outstandingScheduledAmount: 0,
                overduePaymentMilestoneCount: 0,
                hasSignedScan: false,
                attachmentCount: 0,
                appendixCount: 0,
                createdAt: "2026-09-09T00:00:00Z",
                updatedAt: "2026-09-09T00:00:00Z",
                rowVersion: "AAAAAAAAB9M=",
                paymentMilestones: [],
            };
        });
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({ total, page: requestedPage, pageSize: isExport ? total : pageSize, items }),
        });
    });

    await page.goto(`${baseURL}/admin/contracts`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("contract-row-700001")).toBeVisible();
    await expect(page.getByTestId("contract-row-700001")).toContainText("700.046");
    await expect(page.getByTestId("contract-row-700001")).toContainText(/Đã quá hạn|Overdue|已逾期|期限超過/i);
    await expect(page.getByTestId("contract-row-700001")).not.toContainText(/Sắp hết hạn|Ending soon|即将到期|終了間近/i);
    const filteredListResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname.endsWith("/api/contracts")
            && url.searchParams.get("search") === "HD-PAGE"
            && url.searchParams.get("pageSize") === "20";
    });
    await page.locator("#c-search").fill("HD-PAGE");
    await filteredListResponse;
    await page.getByRole("button", { name: /Sau|Next|下一个|次へ/i }).last().click();
    await expect(page.getByTestId("contract-row-700021")).toBeVisible();

    const download = page.waitForEvent("download");
    await page.getByRole("button", { name: /Xuất CSV|Export CSV|导出 CSV|CSV エクスポート/i }).click();
    expect((await download).suggestedFilename()).toMatch(/^contracts-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(exportRequests).toHaveLength(1);
    expect(exportRequests[0]).toMatch(/\/contracts\/export-data$/);
    expect(exportSearches).toEqual(["HD-PAGE"]);
});

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
    api,
    loginAs,
}) => {
    const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const customerResponse = await api.post("/api/customers", { headers, data: {
        name: `Contract editor customer ${suffix}`, type: "Individual", sourceCode: "marketing",
        primaryContact: { fullName: "Factory investor", phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`, isPrimary: true },
    } });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    const customerId = (await customerResponse.json()).id;
    const projectResponse = await api.post("/api/operational-projects", { headers, data: { name: `Contract editor project ${suffix}`, customerId } });
    expect(projectResponse.status(), await projectResponse.text()).toBe(201);
    const contractResponse = await api.post("/api/contracts", { headers, data: {
        customerId, operationalProjectId: (await projectResponse.json()).id,
        direction: "Upstream", type: "DesignAndBuild", value: 1_000_000,
        scopeOfWork: "Editable draft for contract form regression",
    } });
    expect(contractResponse.status(), await contractResponse.text()).toBe(201);
    const editableContract = await contractResponse.json();
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
    await expect(page.locator("#c-project")).toBeVisible();
    await page.getByRole("button", { name: /Bộ lọc nâng cao|Advanced filters|高级筛选|詳細フィルター/i }).click();
    await expect(page.locator("#c-direction")).toBeVisible();
    await expect(page.locator("#c-type")).toBeVisible();
    await expect(page.locator("#c-vendor")).toBeVisible();

    await page.locator("#c-project").click();
    const projectOption = page.getByRole("option").filter({ hasText: /PJ-/ }).first();
    const filteredResponse = page.waitForResponse((response) =>
        response.request().method() === "GET"
        && response.url().includes("/api/contracts")
        && /[?&]operationalProjectId=\d+/.test(response.url()),
    );
    await projectOption.click();
    expect((await filteredResponse).ok()).toBe(true);
    await page.locator("#c-project").click();
    await page.getByRole("option", { name: /Tất cả dự án vận hành|All operational projects|全部运营项目|すべての運用プロジェクト/i }).click();

    await page.locator("header").getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|新規契約/i }).click();
    await page.locator("#c-direction-form").click();
    await page.getByRole("option", { name: /Đầu vào - Đối tác|Downstream - Partner|下游 - 合作方|下流 - パートナー/i }).click();
    await expect(page.locator("#c-type-form")).toContainText(/Cung ứng|Supply|供应|供給/i);
    await expect(page.locator("#c-vendor-form")).toBeVisible();
    await page.locator("#c-direction-form").click();
    await page.getByRole("option", { name: /Đầu ra - Khách hàng|Upstream - Customer|上游 - 客户|上流 - 顧客/i }).click();
    await expect(page.locator("#c-vendor-form")).toBeHidden();
    await page.getByRole("button", { name: /Huỷ|Hủy|Cancel|取消|キャンセル/i }).click();

    // Own the editable fixture: the newest shared row may be an immutable
    // RFQ-awarded contract created by another business pipeline.
    await page.locator("#c-search").fill(editableContract.contractNumber);
    const row = page.getByTestId(`contract-row-${editableContract.id}`);
    await expect(row).toBeVisible();
    const contractId = await row.getAttribute("data-testid");
    await row.locator("td").nth(2).hover();
    await expect(row).toHaveAttribute("data-navigation-active", "true");
    await row.locator("[data-contract-actions]").getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).hover();
    await expect(row).toHaveAttribute("data-navigation-active", "false");
    const detailResponse = page.waitForResponse((response) =>
        response.request().method() === "GET" && new RegExp(`/api/(?:v1/)?contracts/${contractId?.replace("contract-row-", "")}$`).test(response.url()),
    );
    await row.locator("td").nth(1).click();
    expect((await detailResponse).ok()).toBe(true);

    await expect(page).toHaveURL(new RegExp(`/admin/contracts/${contractId?.replace("contract-row-", "")}\\?`));

    const editButton = page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).first();
    await expect(editButton).toBeVisible({ timeout: 15_000 });
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
    await expect(editForm.locator("#contract-detail-project")).not.toHaveText(/Chọn dự án vận hành|Select operational project|选择运营项目|運用プロジェクトを選択/i);
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
    const detailResponse = page.waitForResponse((response) =>
        response.request().method() === "GET" && new RegExp(`/api/(?:v1/)?contracts/${contractId?.replace("contract-card-", "")}$`).test(response.url()),
    );
    await cardLink.click();
    expect((await detailResponse).ok()).toBe(true);

    await expect(page).toHaveURL(new RegExp(`/admin/contracts/${contractId?.replace("contract-card-", "")}\\?`));
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
        direction: "Upstream",
        type: "DesignAndBuild",
        vendorId: null,
        vendorCode: null,
        vendorName: null,
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
            responsibleAccountantUserId: 77,
            responsibleAccountantName: "E2E Accountant",
            requestedAt: null,
            note: null,
            createdAt: "2026-08-01T00:00:00Z",
            updatedAt: "2026-08-30T00:00:00Z",
        }],
    });

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([{ userId: 77, userName: "E2E Accountant", positionCode: "PROJECT_ACCOUNTING" }]),
    }));
    await page.route(new RegExp(`/api/(?:v1/)?contracts/${contractId}(?:/.*)?$`), async route => {
        const request = route.request();
        const path = new URL(request.url()).pathname;
        if (request.method() === "PATCH" && path.endsWith("/milestones/1/status")) {
            const payload = request.postDataJSON() as { status: string; actualPaymentDate: string | null; responsibleAccountantUserId: number };
            status = payload.status;
            actualPaymentDate = payload.actualPaymentDate;
            expect(payload.responsibleAccountantUserId).toBe(77);
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
