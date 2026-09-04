/// <reference types="node" />
import { test, expect, TEST_USERS } from "../fixtures/auth";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import {
  createApprovedInvestmentRate,
  retireInvestmentRate,
} from "../fixtures/materialRate";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

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
  let investmentRate: Awaited<ReturnType<typeof createApprovedInvestmentRate>> | null = null;

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
          email: `test-${unique}@example.com`,
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
    for (let index = 0; index < 12; index += 1) {
      const activityResponse = await api.post(`/api/opportunities/${opportunityId}/activities`, {
        headers,
        data: {
          type: "Note",
          content: `Opportunity dialog scroll evidence ${index + 1} ${unique}`,
        },
      });
      expect(activityResponse.status(), await activityResponse.text()).toBe(200);
    }

    investmentRate = await createApprovedInvestmentRate(api, headers, unique, 4_280_000);
    const quoteResponse = await api.post("/api/quotes", {
      headers,
      data: {
        opportunityId,
        method: "UnitCost",
        areaSqm: 100,
        unitPricePerSqm: 4_280_000,
        materialRateCatalogId: investmentRate.catalogId,
        pricingEffectiveDate: investmentRate.pricingEffectiveDate,
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

    // Use a real PDF file with actual content for proper preview testing
    const realPdfPath = path.resolve(__dirname, "../../../nihomebackend/wwwroot/process-assets/files/501e798356d44d8792986b936ac2d100.pdf");
    const pdfBuffer = fs.readFileSync(realPdfPath);
    await customerDialog.locator("#customer-document-file").setInputFiles({
      name: fileName,
      mimeType: "application/pdf",
      buffer: pdfBuffer,
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
    await page.setViewportSize({ width: 390, height: 844 });
    await relatedDialog.getByRole("link", { name: new RegExp(opportunityName) }).click();
    await expect(page).toHaveURL(new RegExp(`/admin/opportunities\\?open=${opportunityId}$`));
    const opportunityDialog = page.getByTestId("opportunity-detail-dialog");
    await expect(opportunityDialog).toBeVisible();
    const opportunityDialogHeight = await opportunityDialog.evaluate((element) => element.clientHeight);
    const opportunityTabs = opportunityDialog.getByRole("tab");
    await expect(opportunityTabs).toHaveCount(3);
    for (let index = 0; index < 3; index += 1) {
      await opportunityTabs.nth(index).scrollIntoViewIfNeeded();
      await opportunityTabs.nth(index).click();
      await expect(opportunityTabs.nth(index)).toHaveAttribute("aria-selected", "true");
      expect((await opportunityTabs.nth(index).boundingBox())?.height).toBeGreaterThanOrEqual(44);
      expect(await opportunityDialog.evaluate((element) => element.clientHeight)).toBe(opportunityDialogHeight);
      const activePanel = opportunityDialog.getByRole("tabpanel");
      await expect(activePanel).toBeVisible();
      expect(await activePanel.evaluate((element) => getComputedStyle(element).overflowY)).toBe("auto");
      if (index === 1) {
        const panelSize = await activePanel.evaluate((element) => ({
          clientHeight: element.clientHeight,
          scrollHeight: element.scrollHeight,
        }));
        expect(panelSize.scrollHeight).toBeGreaterThan(panelSize.clientHeight);
        const closeButton = opportunityDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).last();
        const closeButtonTop = await closeButton.evaluate((element) => element.getBoundingClientRect().top);
        expect(await activePanel.evaluate((element) => {
          element.scrollTop = element.scrollHeight;
          return element.scrollTop;
        })).toBeGreaterThan(0);
        await expect(closeButton).toBeVisible();
        expect(await closeButton.evaluate((element) => element.getBoundingClientRect().top)).toBe(closeButtonTop);
      }
    }

    expect(jsErrors, `Unexpected JavaScript errors:\n${jsErrors.join("\n")}`).toHaveLength(0);
    expect(failedResponses, `Unexpected 5xx responses:\n${failedResponses.join("\n")}`).toHaveLength(0);
  } finally {
    if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
    if (quoteId) await hardDeleteBusinessRoot(api, headers, `/api/quotes/${quoteId}`);
    if (opportunityId) await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
    if (investmentRate) await retireInvestmentRate(api, headers, investmentRate);
  }
});

test("mobile customer detail keeps every tab reachable without shrinking touch targets", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  const customerName = `Mobile tabs ${unique}`;
  let customerId = 0;

  try {
    const create = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: customerName,
        sourceCode: "marketing",
        primaryContact: { fullName: "Mobile contact", phone: `08${unique.slice(-8)}`, isPrimary: true },
      },
    });
    expect(create.status(), await create.text()).toBe(201);
    customerId = (await create.json()).id as number;

    await page.setViewportSize({ width: 390, height: 844 });
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/customers", { waitUntil: "networkidle" });
    await page.locator("#customer-search").fill(customerName);
    await page.locator("article").filter({ hasText: customerName }).click();

    const tabs = page.getByTestId("customer-detail-tabs");
    await expect(tabs).toBeVisible();
    const dialog = page.getByTestId("customer-detail-dialog");
    const initialDialogHeight = await dialog.evaluate((element) => element.clientHeight);
    expect(initialDialogHeight).toBeGreaterThan(0);
    const tabButtons = tabs.getByRole("tab");
    await expect(tabButtons).toHaveCount(4);
    for (let index = 0; index < 4; index += 1) {
      expect((await tabButtons.nth(index).boundingBox())?.height).toBeGreaterThanOrEqual(44);
    }
    await tabButtons.last().scrollIntoViewIfNeeded();
    await tabButtons.last().click();
    await expect(tabButtons.last()).toHaveAttribute("aria-selected", "true");
    expect(await dialog.evaluate((element) => element.clientHeight)).toBe(initialDialogHeight);

    await tabButtons.first().scrollIntoViewIfNeeded();
    await tabButtons.first().click();
    const generalPanel = dialog.getByRole("tabpanel");
    await expect(generalPanel).toBeVisible();
    const panelSize = await generalPanel.evaluate((panel) => ({
      clientHeight: panel.clientHeight,
      scrollHeight: panel.scrollHeight,
    }));
    expect(panelSize.scrollHeight).toBeGreaterThan(panelSize.clientHeight);
  } finally {
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
