import { test, expect, TEST_USERS } from "../fixtures/auth";
import { rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const specDirectory = path.dirname(fileURLToPath(import.meta.url));

const ONE_PIXEL_PNG = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
    "base64",
);

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

test("every tender checklist item offers library and direct upload actions", async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
}) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${token}` };
    const unique = Date.now().toString();
    let customerId = 0;
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

        const tenderResponse = await api.post("/api/tenders", {
            headers,
            data: {
                name: `Tender checklist actions ${unique}`,
                customerId,
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
        await page.goto(`/admin/tenders/${tenderId}`, { waitUntil: "networkidle" });

        for (const item of checklistItems) {
            const key = item.templateCode ?? item.id;
            await expect(page.getByTestId(`tender-checklist-row-${key}-desktop`)).toBeVisible();
            await expect(page.getByTestId(`tender-checklist-library-${key}-desktop`)).toBeVisible();
            await expect(page.getByTestId(`tender-checklist-upload-${key}-desktop`)).toBeVisible();
        }

        const target = checklistItems.find((item) => item.templateCode === "legal") ?? checklistItems[1];
        const targetKey = target.templateCode ?? target.id;
        await page.getByTestId(`tender-checklist-library-${targetKey}-desktop`).click();
        const libraryDialog = page.getByRole("dialog");
        await expect(libraryDialog).toBeVisible();
        await expect(
            libraryDialog.getByRole("heading", {
                name: /thư viện dùng chung|shared document library|共享文档库|共有文書ライブラリ/i,
            }),
        ).toBeVisible();
        await expect(libraryDialog.getByTestId("tender-library-target")).toHaveText(target.title);
        await page.keyboard.press("Escape");
        await expect(libraryDialog).toBeHidden();

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
    } finally {
        if (uploadedFilePath?.startsWith("/files/tenders/")) {
            await rm(
                path.resolve(specDirectory, "../../../nihomebackend/wwwroot", uploadedFilePath.slice(1)),
                { force: true },
            );
        }
        if (tenderId) await api.delete(`/api/tenders/${tenderId}`, { headers });
        if (customerId) await api.delete(`/api/customers/${customerId}`, { headers });
    }
});
