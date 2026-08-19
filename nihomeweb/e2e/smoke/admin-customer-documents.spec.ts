/// <reference types="node" />
import { test, expect, TEST_USERS } from "../fixtures/auth";

test("customer related records, documents, and contract owner inheritance work in the deployed UI", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(90_000);

  const jsErrors: string[] = [];
  const failedResponses: string[] = [];
  page.on("pageerror", (error) => jsErrors.push(error.message));
  page.on("response", (response) => {
    if (response.status() >= 500) failedResponses.push(`${response.status()} ${response.url()}`);
  });

  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  const customerName = `NIH-428 Browser ${unique}`;
  const fileName = `nih-428-${unique}.pdf`;
  let customerId = 0;
  let opportunityId = 0;
  let quoteId = 0;
  let contractId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: customerName,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "Playwright Contact",
          phone: `09${unique.slice(-8)}`,
          isPrimary: true,
        },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    const createdCustomer = await customerResponse.json();
    customerId = createdCustomer.id as number;

    const customerDetailResponse = await api.get(`/api/customers/${customerId}`, { headers });
    expect(customerDetailResponse.status(), await customerDetailResponse.text()).toBe(200);
    const customer = await customerDetailResponse.json();
    const ownerUserId = customer.ownerUserId as number;
    const ownerName = customer.ownerName as string;
    expect(ownerUserId).toBeTruthy();
    expect(ownerName).toBeTruthy();

    const opportunityName = `NIH-428 Opportunity ${unique}`;
    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: {
        name: opportunityName,
        customerId,
        estimatedValue: 428_000_000,
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
        unitPricePerSqm: 4_280_000,
        discountPercent: 0,
        vatPercent: 8,
      },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    const quote = await quoteResponse.json();
    quoteId = quote.id as number;
    const quoteCode = quote.code as string;

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/customers", { waitUntil: "networkidle" });
    await page.locator("#customer-search").fill(customerName);
    const customerRow = page.locator("table tbody tr").filter({ hasText: customerName });
    await expect(customerRow).toBeVisible({ timeout: 10_000 });
    await customerRow.click();

    const customerDialog = page.getByRole("dialog").filter({ hasText: customerName });
    await expect(customerDialog).toBeVisible();
    await customerDialog.getByRole("tab", { name: /Cơ hội.*Báo giá.*Hợp đồng|Opportunities.*Quotes.*Contracts|商机.*报价.*合同|案件.*見積.*契約/i }).click();
    const opportunityLink = customerDialog.getByRole("link", { name: new RegExp(opportunityName) });
    await expect(opportunityLink).toBeVisible();
    await expect(opportunityLink).toHaveAttribute("href", `/admin/opportunities?open=${opportunityId}`);
    await expect(customerDialog.getByRole("link", { name: new RegExp(quoteCode) })).toBeVisible();
    await expect(customerDialog.locator(`a[href="/admin/opportunities?customerId=${customerId}"]`)).toBeVisible();
    await expect(customerDialog.locator(`a[href="/admin/quotes?customerId=${customerId}"]`)).toBeVisible();
    await expect(customerDialog.locator(`a[href="/admin/contracts?customerId=${customerId}"]`)).toBeVisible();
    await expect(customerDialog.getByText(/Khách hàng chưa có hợp đồng|no contracts yet|尚无合同|まだ契約がありません/i)).toBeVisible();

    await customerDialog.getByRole("tab", { name: /Tài liệu|Documents|文档|資料/i }).click();
    await expect(customerDialog.getByText(/Chưa có tài liệu|No documents|尚未关联|まだありません/i)).toBeVisible();

    await customerDialog.locator("#customer-document-file").setInputFiles({
      name: fileName,
      mimeType: "application/pdf",
      buffer: Buffer.from("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF"),
    });
    await customerDialog.locator("#customer-document-label").fill("NIH-428 browser evidence");
    await customerDialog.getByRole("button", { name: /Tải lên|Upload|上传|アップロード/i }).click();

    const documentItem = customerDialog.locator("li").filter({ hasText: fileName });
    await expect(documentItem).toBeVisible({ timeout: 10_000 });
    await expect(documentItem).toContainText("NIH-428 browser evidence");
    await expect(documentItem).toContainText(ownerName);

    await documentItem.getByRole("button", { name: /Xem trước|Preview|预览|プレビュー/i }).click();
    const previewDialog = page.getByRole("dialog", { name: /Xem trước tài liệu|File preview/i });
    await expect(previewDialog).toBeVisible();
    await expect(previewDialog).toContainText(fileName);
    await expect(previewDialog.locator("iframe")).toBeVisible();
    await previewDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();
    await expect(previewDialog).toBeHidden();

    await customerDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();
    await expect(customerDialog).toBeHidden();
    await customerRow.click();
    const reopenedDialog = page.getByRole("dialog").filter({ hasText: customerName });
    await reopenedDialog.getByRole("tab", { name: /Tài liệu|Documents|文档|資料/i }).click();
    const persistedItem = reopenedDialog.locator("li").filter({ hasText: fileName });
    await expect(persistedItem).toBeVisible();

    page.once("dialog", (dialog) => dialog.accept());
    await persistedItem.getByRole("button", { name: /Xoá|Delete|删除|削除/i }).click();
    await expect(persistedItem).toBeHidden();
    await expect(reopenedDialog.getByText(/Chưa có tài liệu|No documents|尚未关联|まだありません/i)).toBeVisible();
    await reopenedDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();

    await page.goto("/admin/contracts", { waitUntil: "networkidle" });
    await page.getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|新規契約/i }).click();
    const contractDialog = page.getByRole("dialog").filter({ hasText: /Thêm hợp đồng|New contract|新增合同|新規契約/i });
    await contractDialog.locator("#c-customer-form").click();
    await page.getByRole("option", { name: customerName, exact: true }).click();
    await expect(contractDialog.getByText(ownerName, { exact: true })).toBeVisible();
    await expect(contractDialog.getByText(/Tự động lấy|Automatically inherited|自动继承|自動的に/i)).toBeVisible();
    await contractDialog.locator("#c-value").fill("428000000");

    const createContractResponse = page.waitForResponse((response) =>
      response.url().endsWith("/api/contracts")
      && response.request().method() === "POST",
    );
    await contractDialog.getByRole("button", { name: /Lưu|Save|保存/i }).click();
    const response = await createContractResponse;
    expect(response.status(), await response.text()).toBe(201);
    const contract = await response.json();
    contractId = contract.id as number;
    expect(contract.customerId).toBe(customerId);
    expect(contract.ownerUserId).toBe(ownerUserId);
    expect(contract.ownerName).toBe(ownerName);
    await expect(contractDialog).toBeHidden();
    await expect(page.locator("table tbody tr").filter({ hasText: customerName })).toBeVisible();

    await page.goto("/admin/customers", { waitUntil: "networkidle" });
    await page.locator("#customer-search").fill(customerName);
    const refreshedCustomerRow = page.locator("table tbody tr").filter({ hasText: customerName });
    await expect(refreshedCustomerRow).toBeVisible({ timeout: 10_000 });
    await refreshedCustomerRow.click();
    const relatedDialog = page.getByRole("dialog").filter({ hasText: customerName });
    await relatedDialog.getByRole("tab", { name: /Cơ hội.*Báo giá.*Hợp đồng|Opportunities.*Quotes.*Contracts|商机.*报价.*合同|案件.*見積.*契約/i }).click();
    await expect(relatedDialog.getByRole("link", { name: new RegExp(contract.contractNumber as string) })).toBeVisible();
    await relatedDialog.getByRole("link", { name: new RegExp(opportunityName) }).click();
    await expect(page).toHaveURL(new RegExp(`/admin/opportunities\\?open=${opportunityId}$`));
    const opportunityDialog = page.getByRole("dialog").filter({ hasText: opportunityName });
    await expect(opportunityDialog).toBeVisible();

    expect(jsErrors, `Unexpected JavaScript errors:\n${jsErrors.join("\n")}`).toHaveLength(0);
    expect(failedResponses, `Unexpected 5xx responses:\n${failedResponses.join("\n")}`).toHaveLength(0);
  } finally {
    if (contractId) {
      const deleteContractResponse = await api.delete(`/api/contracts/${contractId}`, { headers });
      expect(deleteContractResponse.status()).toBe(204);
      expect((await api.get(`/api/contracts/${contractId}`, { headers })).status()).toBe(404);
    }
    if (quoteId) {
      const deleteQuoteResponse = await api.delete(`/api/quotes/${quoteId}`, { headers });
      expect(deleteQuoteResponse.status()).toBe(204);
    }
    if (opportunityId) {
      const deleteOpportunityResponse = await api.delete(`/api/opportunities/${opportunityId}`, { headers });
      expect(deleteOpportunityResponse.status()).toBe(204);
    }
    if (customerId) {
      const deleteCustomerResponse = await api.delete(`/api/customers/${customerId}`, { headers });
      expect(deleteCustomerResponse.status()).toBe(204);
      expect((await api.get(`/api/customers/${customerId}`, { headers })).status()).toBe(404);
    }
  }
});
