import { test, expect, TEST_USERS } from "../fixtures/auth";
import type { Page } from "@playwright/test";
import { rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const specDirectory = path.dirname(fileURLToPath(import.meta.url));

const ONE_PIXEL_PNG = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
    "base64",
);

const expectNoPageOverflow = async (page: Page, context: string) => {
    const widths = await page.evaluate(() => ({
        viewport: document.documentElement.clientWidth,
        document: document.documentElement.scrollWidth,
        body: document.body.scrollWidth,
    }));
    expect(widths.document, `${context}: document width ${JSON.stringify(widths)}`)
        .toBeLessThanOrEqual(widths.viewport);
    expect(widths.body, `${context}: body width ${JSON.stringify(widths)}`)
        .toBeLessThanOrEqual(widths.viewport);
};

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

test("SALE can prepare a tender but cannot submit or cancel it", async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
}) => {
    const managerToken = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${managerToken}` };
    const unique = Date.now().toString();
    let customerId = 0;
    let projectId = 0;
    let tenderId = 0;

    try {
        const customerResponse = await api.post("/api/customers", {
            headers,
            data: {
                type: "Individual",
                name: `Tender permission customer ${unique}`,
                sourceCode: "marketing",
                primaryContact: {
                    fullName: `Tender permission contact ${unique}`,
                    phone: `07${unique.slice(-8)}`,
                    isPrimary: true,
                },
            },
        });
        expect(customerResponse.status(), await customerResponse.text()).toBe(201);
        customerId = (await customerResponse.json()).id as number;

        const projectResponse = await api.post("/api/operational-projects", {
            headers,
            data: { name: `Tender permission project ${unique}`, customerId },
        });
        expect(projectResponse.status(), await projectResponse.text()).toBe(201);
        projectId = (await projectResponse.json()).id as number;

        const tenderResponse = await api.post("/api/tenders", {
            headers,
            data: {
                name: `Tender permission boundary ${unique}`,
                customerId,
                operationalProjectId: projectId,
                invitationReceivedAt: new Date(Date.now() - 24 * 60 * 60 * 1_000).toISOString(),
                invitationSource: "Cổng đấu thầu quốc gia",
                submissionDeadline: new Date(Date.now() + 14 * 24 * 60 * 60 * 1_000).toISOString(),
            },
        });
        expect(tenderResponse.status(), await tenderResponse.text()).toBe(201);
        tenderId = (await tenderResponse.json()).id as number;

        await loginInBrowserAs(page, TEST_USERS.sale);
        await page.goto(`/admin/tenders/${tenderId}`, { waitUntil: "networkidle" });
        await page.getByTestId("tender-estimate-tab").click();

        await expect(page.getByTestId("tender-lifecycle")).toBeVisible();
        await expect(page.getByTestId("tender-submit")).toHaveCount(0);
        await expect(page.getByTestId("tender-cancel")).toHaveCount(0);
        await expect(page.getByText(/Nhập dự toán|Import estimate/)).toBeVisible();
    } finally {
        if (tenderId) await hardDeleteBusinessRoot(api, headers, `/api/tenders/${tenderId}`);
        if (projectId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
        if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
    }
});

test("tender plan assigns owners and deadlines and manages checklist files", async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
}, testInfo) => {
    test.slow();
    const token = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${token}` };
    const unique = Date.now().toString();
    let customerId = 0;
    let projectId = 0;
    let tenderId = 0;
    let uploadedFilePath: string | null = null;

    try {
        const customerResponse = await api.post("/api/customers", {
            headers,
            data: {
                type: "Individual",
                name: `Tender checklist customer ${unique}`,
                sourceCode: "marketing",
                primaryContact: {
                    fullName: `Tender checklist contact ${unique}`,
                    phone: `07${unique.slice(-8)}`,
                    isPrimary: true,
                },
            },
        });
        expect(customerResponse.status(), await customerResponse.text()).toBe(201);
        customerId = (await customerResponse.json()).id as number;

        const projectResponse = await api.post("/api/operational-projects", {
            headers,
            data: {
                name: `Tender shared project ${unique}`,
                customerId,
                note: "Dự án có trước gói thầu theo quy trình NICON",
            },
        });
        expect(projectResponse.status(), await projectResponse.text()).toBe(201);
        projectId = (await projectResponse.json()).id as number;

        const tenderResponse = await api.post("/api/tenders", {
            headers,
            data: {
                name: `Tender checklist actions ${unique}`,
                customerId,
                operationalProjectId: projectId,
                invitationReceivedAt: new Date(Date.now() - 24 * 60 * 60 * 1_000).toISOString(),
                invitationSource: "Email khách hàng",
                submissionDeadline: new Date(Date.now() + 14 * 24 * 60 * 60 * 1_000).toISOString(),
            },
        });
        expect(tenderResponse.status(), await tenderResponse.text()).toBe(201);
        const tender = await tenderResponse.json();
        tenderId = tender.id as number;
        const checklistItems = tender.checklistItems as Array<{
            id: number;
            templateCode: string | null;
            title: string;
        }>;
        expect(checklistItems.length).toBeGreaterThan(1);

        await loginInBrowserAs(page, TEST_USERS.salesManager);
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto("/admin/tenders", { waitUntil: "networkidle" });
        await expect(page.getByTestId("tender-list-cards")).toBeVisible();
        await expect(page.getByTestId("tender-list-table")).toBeHidden();
        await expectNoPageOverflow(page, "tender list at 390px");
        if (process.env.PLAYWRIGHT_VISUAL_EVIDENCE) {
            await page.screenshot({ path: testInfo.outputPath("tender-list-mobile.png"), fullPage: true });
        }

        await page.setViewportSize({ width: 768, height: 1024 });
        await expect(page.getByTestId("tender-list-cards")).toBeVisible();
        await expect(page.getByTestId("tender-list-table")).toBeHidden();
        await expectNoPageOverflow(page, "tender list at 768px");

        await page.setViewportSize({ width: 1280, height: 800 });
        await page.goto(`/admin/tenders/${tenderId}`, { waitUntil: "networkidle" });

        await page.getByTestId("tender-plan-tab-trigger").click();
        await expect(page.getByTestId("tender-plan-tab")).toContainText(/Kế hoạch đấu thầu|Tender plan/);
        const processOverview = page.getByTestId("tender-process-overview");
        await expect(processOverview).toContainText(/Nhận HSMT|Invitation received/);
        await expect(processOverview).toContainText(/Lập kế hoạch|Plan/);
        await expect(processOverview).toContainText(/Lập hồ sơ dự thầu|Prepare bid/);
        await expect(processOverview).toContainText(/Đánh giá|Review/);
        await expect(processOverview).toContainText(/Nộp thầu|Submit/);
        await expect(processOverview).toContainText(/Kết quả|Result/);
        const assignDepartmentLink = page.getByRole("link", { name: /Giao hồ sơ cho phòng ban|Assign a department/ });
        await expect(assignDepartmentLink).toBeVisible();
        await assignDepartmentLink.click();
        await expect(page).toHaveURL(new RegExp(
            `/admin/work-packages\\?create=1&purpose=TenderPlanning&projectId=${projectId}&tenderId=${tenderId}`,
        ));
        const packageDialog = page.getByRole("dialog");
        await expect(packageDialog).toBeVisible();
        await expect(packageDialog.getByTestId("work-package-purpose"))
            .toContainText(/Kế hoạch đấu thầu|Tender planning/);
        await expect(packageDialog.getByTestId("work-package-project"))
            .toContainText(`Tender shared project ${unique}`);
        const departmentSelect = packageDialog.getByTestId("work-package-department");
        await expect(departmentSelect).toBeEnabled();
        await departmentSelect.click();
        expect(await page.getByRole("option").count()).toBeGreaterThan(2);
        await page.keyboard.press("Escape");
        await page.keyboard.press("Escape");
        await page.goBack({ waitUntil: "networkidle" });
        await page.getByRole("tab", { name: /Hồ sơ dự thầu|Checklist/ }).click();

        for (const item of checklistItems) {
            const key = item.templateCode ?? item.id;
            await expect(page.getByTestId(`tender-checklist-row-${key}-desktop`)).toBeVisible();
            if (item.templateCode === "capability") {
                await expect(page.getByTestId(`tender-checklist-library-${key}-desktop`)).toBeVisible();
                await expect(page.getByTestId(`tender-checklist-row-${key}-desktop`))
                    .toContainText(/Phải chọn từ thư viện dùng chung|Select from shared library/);
                await expect(page.getByTestId(`tender-checklist-upload-${key}-desktop`)).toHaveCount(0);
            } else {
                await expect(page.getByTestId(`tender-checklist-library-${key}-desktop`)).toHaveCount(0);
                await expect(page.getByTestId(`tender-checklist-upload-${key}-desktop`)).toBeVisible();
            }
        }

        const createCustomResponse = page.waitForResponse((response) =>
            response.request().method() === "POST"
            && response.url().endsWith(`/api/tenders/${tenderId}/checklist`),
        );
        await page.getByTestId("tender-checklist-add").click();
        await page.getByTestId("tender-checklist-custom-title").fill("Bản vẽ phối cảnh theo HSMT");
        await page.getByTestId("tender-checklist-custom-required").uncheck();
        await page.getByTestId("tender-checklist-custom-save").click();
        const createCustom = await createCustomResponse;
        expect(createCustom.status(), await createCustom.text()).toBe(200);
        const customItem = (await createCustom.json()).checklistItems.find(
            (item: { title: string }) => item.title === "Bản vẽ phối cảnh theo HSMT",
        ) as { id: number };
        const customRow = page.getByTestId(`tender-checklist-row-${customItem.id}-desktop`);
        await expect(customRow).toBeVisible();

        const markNotApplicableResponse = page.waitForResponse((response) =>
            response.request().method() === "PATCH"
            && response.url().endsWith(`/api/tenders/${tenderId}/checklist/${customItem.id}`),
        );
        await customRow.getByRole("combobox").first().click();
        await page.getByRole("option", { name: /Không áp dụng|Not applicable/ }).click();
        await page.getByTestId("tender-checklist-na-reason").fill("HSMT không yêu cầu bản vẽ phối cảnh.");
        await page.getByTestId("tender-checklist-na-save").click();
        expect((await markNotApplicableResponse).status()).toBe(200);
        await expect(customRow).toContainText(/Không áp dụng|Not applicable/);

        const deleteCustomResponse = page.waitForResponse((response) =>
            response.request().method() === "DELETE"
            && response.url().endsWith(`/api/tenders/${tenderId}/checklist/${customItem.id}`),
        );
        await customRow.getByRole("button", { name: /Xóa mục hồ sơ|Delete document item/ }).click();
        expect((await deleteCustomResponse).status()).toBe(200);
        await expect(customRow).toHaveCount(0);

        const capabilityItem = checklistItems.find((item) => item.templateCode === "capability");
        expect(capabilityItem).toBeTruthy();
        const capabilityKey = capabilityItem!.templateCode ?? capabilityItem!.id;
        await page.getByTestId(`tender-checklist-library-${capabilityKey}-desktop`).click();
        const libraryDialog = page.getByRole("dialog");
        await expect(libraryDialog).toBeVisible();
        await expect(
            libraryDialog.getByRole("heading", {
                name: /thư viện dùng chung|shared document library|共享文档库|共有文書ライブラリ/i,
            }),
        ).toBeVisible();
        await expect(libraryDialog.getByTestId("tender-library-target")).toHaveText(capabilityItem!.title);
        await page.keyboard.press("Escape");
        await expect(libraryDialog).toBeHidden();

        const assigneeOptionsResponse = await api.get("/api/tenders/assignee-options", { headers });
        expect(assigneeOptionsResponse.status(), await assigneeOptionsResponse.text()).toBe(200);
        const assignee = (await assigneeOptionsResponse.json() as Array<{ id: number; fullName: string }>)[0];
        expect(assignee).toBeTruthy();
        const plannedItem = checklistItems[0];
        const plannedKey = plannedItem.templateCode ?? plannedItem.id;
        const ownerResponsePromise = page.waitForResponse((response) =>
            response.request().method() === "PATCH"
            && response.url().includes(`/api/tenders/${tenderId}/checklist/${plannedItem.id}`),
        );
        await page.getByTestId(`tender-checklist-owner-${plannedKey}-desktop`).click();
        await page.getByRole("option", { name: assignee.fullName, exact: true }).click();
        const ownerResponse = await ownerResponsePromise;
        expect(ownerResponse.status(), await ownerResponse.text()).toBe(200);
        expect((await ownerResponse.json()).checklistItems.find(
            (item: { id: number }) => item.id === plannedItem.id,
        )?.ownerUserId).toBe(assignee.id);

        const deadline = new Date(Date.now() + 7 * 24 * 60 * 60 * 1_000).toISOString().slice(0, 10);
        const deadlineResponsePromise = page.waitForResponse((response) =>
            response.request().method() === "PATCH"
            && response.url().includes(`/api/tenders/${tenderId}/checklist/${plannedItem.id}`),
        );
        await page.getByTestId(`tender-checklist-row-${plannedKey}-desktop`)
            .locator('input[type="date"]')
            .fill(deadline);
        expect((await deadlineResponsePromise).status()).toBe(200);

        const target = checklistItems.find((item) => item.templateCode === "legal") ?? checklistItems[1];
        const targetKey = target.templateCode ?? target.id;
        await expect(page.getByTestId(`tender-checklist-row-${targetKey}-desktop`))
            .toContainText(/Hồ sơ theo gói thầu|Tender-specific document/);
        await expect(page.getByTestId(`tender-checklist-library-${targetKey}-desktop`)).toHaveCount(0);

        const uploadResponsePromise = page.waitForResponse((response) =>
            response.request().method() === "POST"
            && response.url().includes(`/api/tenders/${tenderId}/checklist/${target.id}/upload`),
        );
        const fileChooserPromise = page.waitForEvent("filechooser");
        await page.getByTestId(`tender-checklist-upload-${targetKey}-desktop`).click();
        const fileChooser = await fileChooserPromise;
        await fileChooser.setFiles({
            name: `tender-checklist-${unique}.png`,
            mimeType: "image/png",
            buffer: ONE_PIXEL_PNG,
        });
        const uploadResponse = await uploadResponsePromise;
        expect(uploadResponse.status()).toBe(200);
        const uploadedTender = await uploadResponse.json();
        uploadedFilePath = uploadedTender.checklistItems.find(
            (item: { id: number }) => item.id === target.id,
        )?.filePath ?? null;

        const previewTestId = `tender-checklist-preview-${targetKey}-desktop`;
        await expect(page.getByTestId(previewTestId)).toBeVisible();
        await page.getByTestId(previewTestId).click();
        await expect(page.getByTestId(`${previewTestId}-dialog`)).toBeVisible();
        const previewImage = page.getByTestId(`${previewTestId}-image`);
        await expect(previewImage).toBeVisible();
        await expect(previewImage).toHaveAttribute("src", /^blob:/);
        await page.keyboard.press("Escape");

        await page.setViewportSize({ width: 390, height: 844 });
        const mobileOwner = page.getByTestId(`tender-checklist-owner-${plannedKey}-mobile`);
        await expect(mobileOwner).toBeVisible();
        await expect(mobileOwner).toContainText(assignee.fullName);
        const mobileDeadline = page.getByTestId(`tender-checklist-row-${plannedKey}-mobile`)
            .locator('input[type="date"]');
        await expect(mobileDeadline).toHaveValue(deadline);
        await expect(mobileDeadline).toHaveAttribute("max", tender.submissionDeadline.slice(0, 10));
        expect((await mobileOwner.boundingBox())?.height).toBeGreaterThanOrEqual(36);
        const viewport = await page.evaluate(() => ({
            bodyWidth: document.body.scrollWidth,
            viewportWidth: window.innerWidth,
        }));
        expect(viewport.bodyWidth).toBeLessThanOrEqual(viewport.viewportWidth);
        await expectNoPageOverflow(page, "tender checklist at 390px");
        await expect(page.getByTestId("tender-detail-tabs")).toHaveJSProperty("scrollWidth",
            await page.getByTestId("tender-detail-tabs").evaluate((element) => element.clientWidth));

        await page.getByTestId("tender-plan-tab-trigger").click();
        await expect(page.getByTestId("tender-process-overview")).toBeVisible();
        const mobileProcessWidths = await page.getByTestId("tender-process-overview").evaluate((element) => ({
            client: element.clientWidth,
            scroll: element.scrollWidth,
        }));
        expect(mobileProcessWidths.scroll).toBeLessThanOrEqual(mobileProcessWidths.client);
        await expectNoPageOverflow(page, "tender plan at 390px");
        if (process.env.PLAYWRIGHT_VISUAL_EVIDENCE) {
            await page.screenshot({ path: testInfo.outputPath("tender-plan-mobile.png"), fullPage: true });
        }

        await page.setViewportSize({ width: 768, height: 1024 });
        await page.getByRole("tab", { name: /Hồ sơ dự thầu|Checklist/ }).click();
        await expect(page.getByTestId(`tender-checklist-row-${plannedKey}-mobile`)).toBeVisible();
        await expect(page.getByTestId(`tender-checklist-row-${plannedKey}-desktop`)).toBeHidden();
        await expectNoPageOverflow(page, "tender checklist at 768px");
        if (process.env.PLAYWRIGHT_VISUAL_EVIDENCE) {
            await page.screenshot({ path: testInfo.outputPath("tender-checklist-tablet.png"), fullPage: true });
        }
    } finally {
        if (uploadedFilePath?.startsWith("/files/tenders/")) {
            await rm(
                path.resolve(specDirectory, "../../../nihomebackend/wwwroot", uploadedFilePath.slice(1)),
                { force: true },
            );
        }
        if (tenderId) await hardDeleteBusinessRoot(api, headers, `/api/tenders/${tenderId}`);
        if (projectId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
        if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
    }
});
