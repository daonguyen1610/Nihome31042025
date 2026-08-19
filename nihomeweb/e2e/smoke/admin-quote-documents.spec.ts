/// <reference types="node" />
import { test, expect, TEST_USERS } from "../fixtures/auth";

test("sales manager uploads, previews, persists, and deletes a quote document", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(90_000);

  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  const fileName = `nih-430-${unique}.pdf`;
  let customerId = 0;
  let opportunityId = 0;
  let quoteId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `NIH-430 Customer ${unique}`,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "Quote Document Contact",
          phone: `08${unique.slice(-8)}`,
          isPrimary: true,
        },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = ((await customerResponse.json()).id as number);

    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: {
        name: `NIH-430 Opportunity ${unique}`,
        customerId,
        estimatedValue: 430_000_000,
        winProbability: 60,
      },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = ((await opportunityResponse.json()).id as number);

    const quoteResponse = await api.post("/api/quotes", {
      headers,
      data: {
        opportunityId,
        method: "UnitCost",
        areaSqm: 100,
        unitPricePerSqm: 4_300_000,
        discountPercent: 0,
        vatPercent: 8,
      },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    quoteId = ((await quoteResponse.json()).id as number);

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu|Documents|文档|資料/i }).click();
    await expect(page.getByText(/Chưa có tài liệu|No documents|尚未上传|まだありません/i)).toBeVisible();

    await page.locator("#quote-document-file").setInputFiles({
      name: fileName,
      mimeType: "application/pdf",
      buffer: Buffer.from("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF"),
    });
    await page.locator("#quote-document-label").fill("NIH-430 browser evidence");
    await page.getByRole("button", { name: /Tải lên|Upload|上传|アップロード/i }).click();

    const documentItem = page.locator("li").filter({ hasText: fileName });
    await expect(documentItem).toBeVisible({ timeout: 10_000 });
    await expect(documentItem).toContainText("NIH-430 browser evidence");

    await documentItem.getByRole("button", { name: /Xem trước|Preview|预览|プレビュー/i }).click();
    const previewDialog = page.getByRole("dialog", { name: /Xem trước tài liệu|File preview/i });
    await expect(previewDialog).toBeVisible();
    await expect(previewDialog).toContainText(fileName);
    await expect(previewDialog.locator("iframe")).toBeVisible();
    await previewDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();

    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu|Documents|文档|資料/i }).click();
    const persistedItem = page.locator("li").filter({ hasText: fileName });
    await expect(persistedItem).toBeVisible();

    page.once("dialog", (dialog) => dialog.accept());
    await persistedItem.getByRole("button", { name: /Xoá|Delete|删除|削除/i }).click();
    await expect(persistedItem).toBeHidden();
    await expect(page.getByText(/Chưa có tài liệu|No documents|尚未上传|まだありません/i)).toBeVisible();
  } finally {
    if (quoteId) await api.delete(`/api/quotes/${quoteId}`, { headers });
    if (opportunityId) await api.delete(`/api/opportunities/${opportunityId}`, { headers });
    if (customerId) await api.delete(`/api/customers/${customerId}`, { headers });
  }
});
