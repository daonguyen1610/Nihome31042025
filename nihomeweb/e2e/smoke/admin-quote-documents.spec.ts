/// <reference types="node" />
import { test, expect, TEST_USERS } from "../fixtures/auth";
import JSZip from "jszip";

const DOCX_CONTENT = "NIH-430 DOCX preview evidence";
const UNSAFE_LINK_TEXT = "Unsafe DOCX link";

async function createDocxFixture() {
  const archive = new JSZip();
  archive.file(
    "[Content_Types].xml",
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
      <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
        <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
        <Default Extension="xml" ContentType="application/xml" />
        <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
      </Types>`,
  );
  archive.file(
    "_rels/.rels",
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
      <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
        <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" />
      </Relationships>`,
  );
  archive.file(
    "word/_rels/document.xml.rels",
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
      <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
        <Relationship Id="rIdUnsafe" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink" Target="javascript:document.body.dataset.compromised='true'" TargetMode="External" />
      </Relationships>`,
  );
  archive.file(
    "word/document.xml",
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
      <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
        <w:body>
          <w:p><w:r><w:t>${DOCX_CONTENT}</w:t></w:r></w:p>
          <w:p>
            <w:hyperlink r:id="rIdUnsafe" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <w:r><w:t>${UNSAFE_LINK_TEXT}</w:t></w:r>
            </w:hyperlink>
          </w:p>
          <w:sectPr>
            <w:pgSz w:w="12240" w:h="15840" />
            <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" />
          </w:sectPr>
        </w:body>
      </w:document>`,
  );
  return archive.generateAsync({ type: "nodebuffer" });
}

test("sales manager uploads, renders, persists, and deletes a DOCX quote document", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(90_000);

  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  const fileName = `nih-430-${unique}.docx`;
  let customerId = 0;
  let opportunityId = 0;
  let quoteId = 0;
  let quoteCode = "";
  const packageDescription = `NIH-430 complete quote view ${unique}`;
  const currentPackageDescription = `NIH-430 current quote view ${unique}`;

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
        packageDescription,
        discountPercent: 0,
        vatPercent: 8,
        note: `NIH-430 quote note ${unique}`,
      },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    const quote = await quoteResponse.json();
    quoteId = quote.id as number;
    quoteCode = quote.code as string;

    const submitResponse = await api.post(`/api/quotes/${quoteId}/submit`, {
      headers,
      data: {},
    });
    expect(submitResponse.status(), await submitResponse.text()).toBe(200);

    const approveResponse = await api.post(`/api/quotes/${quoteId}/approve`, {
      headers,
      data: {},
    });
    expect(approveResponse.status(), await approveResponse.text()).toBe(200);

    const updateResponse = await api.put(`/api/quotes/${quoteId}`, {
      headers,
      data: {
        areaSqm: 120,
        unitPricePerSqm: 4_500_000,
        packageDescription: currentPackageDescription,
        items: [],
        discountPercent: 0,
        vatPercent: 8,
        note: `NIH-430 current quote note ${unique}`,
      },
    });
    expect(updateResponse.status(), await updateResponse.text()).toBe(200);
    expect((await updateResponse.json()).version).toBe(2);

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/quotes", { waitUntil: "networkidle" });

    const quoteRecord = page.locator("tr:visible, li:visible").filter({ hasText: quoteCode }).first();
    await expect(quoteRecord).toBeVisible();
    await quoteRecord.click();
    await expect(page).toHaveURL(new RegExp(`/admin/quotes/${quoteId}$`));
    await expect(page.getByRole("heading", { name: new RegExp(quoteCode) })).toBeVisible();

    const quoteDescription = page.locator("textarea").first();
  await expect(quoteDescription).toHaveValue(currentPackageDescription);
    await expect(quoteDescription).toBeDisabled();
    await expect(page.getByRole("button", { name: /Gửi duyệt|Submit for approval|提交审批|承認申請/i })).toBeVisible();
    await page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).click();
    await expect(quoteDescription).toBeEnabled();
    await page.getByRole("button", { name: /Huỷ|Cancel|取消|キャンセル/i }).first().click();
    await expect(quoteDescription).toBeDisabled();

    await page.getByRole("tab", { name: /Phiên bản|Versions|版本|バージョン/i }).click();
    await page.getByRole("button", { name: /V1$/ }).click();
    const versionOneDialog = page.getByRole("dialog").filter({ hasText: packageDescription });
    await expect(versionOneDialog).toBeVisible();
    await expect(versionOneDialog.getByText(packageDescription, { exact: true })).toBeVisible();
    await expect(versionOneDialog.getByText("100", { exact: true })).toBeVisible();
    await versionOneDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();

    await page.getByRole("button", { name: /V2$/ }).click();
    const versionTwoDialog = page.getByRole("dialog").filter({ hasText: currentPackageDescription });
    await expect(versionTwoDialog).toBeVisible();
    await expect(versionTwoDialog.getByText(currentPackageDescription, { exact: true })).toBeVisible();
    await expect(versionTwoDialog.getByText("120", { exact: true })).toBeVisible();
    await versionTwoDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();

    await page.getByRole("tab", { name: /Tài liệu|Documents|文档|資料/i }).click();
    await expect(page.getByText(/Chưa có tài liệu|No documents|尚未上传|まだありません/i)).toBeVisible();

    await page.locator("#quote-document-file").setInputFiles({
      name: fileName,
      mimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      buffer: await createDocxFixture(),
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
    await expect(previewDialog.getByText(DOCX_CONTENT)).toBeVisible({ timeout: 10_000 });
    await expect(previewDialog.getByText(UNSAFE_LINK_TEXT)).not.toHaveAttribute("href");

    const popupPromise = page.waitForEvent("popup");
    await previewDialog.getByRole("button", { name: /Mở trong tab mới|Open in new tab|新标签页|新しいタブ/i }).click();
    const previewPage = await popupPromise;
    await expect(previewPage.getByText(DOCX_CONTENT)).toBeVisible({ timeout: 10_000 });
    await expect(previewPage.getByText(UNSAFE_LINK_TEXT)).not.toHaveAttribute("href");
    await expect(previewPage.locator("body")).not.toHaveAttribute("data-compromised");
    await previewPage.close();

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

test("sales manager opens BOQ version history from the mobile quote list", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  let customerId = 0;
  let opportunityId = 0;
  let quoteId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `NIH-430 BOQ Customer ${unique}`,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "BOQ Version Contact",
          phone: `07${unique.slice(-8)}`,
          isPrimary: true,
        },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = ((await customerResponse.json()).id as number);

    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: {
        name: `NIH-430 BOQ Opportunity ${unique}`,
        customerId,
        estimatedValue: 600_000_000,
        winProbability: 60,
      },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = ((await opportunityResponse.json()).id as number);

    const quoteResponse = await api.post("/api/quotes", {
      headers,
      data: {
        opportunityId,
        method: "Boq",
        items: [
          { itemCode: "FOUND-V1", name: "Foundation V1", unit: "m3", quantity: 2, unitPrice: 1_000_000, sortOrder: 1 },
          { itemCode: "STEEL-V1", name: "Steel V1", unit: "kg", quantity: 10, unitPrice: 20_000, sortOrder: 2 },
        ],
        discountPercent: 0,
        vatPercent: 8,
      },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    const quote = await quoteResponse.json();
    quoteId = quote.id as number;
    const quoteCode = quote.code as string;

    expect((await api.post(`/api/quotes/${quoteId}/submit`, { headers, data: {} })).status()).toBe(200);
    expect((await api.post(`/api/quotes/${quoteId}/approve`, { headers, data: {} })).status()).toBe(200);

    const updateResponse = await api.put(`/api/quotes/${quoteId}`, {
      headers,
      data: {
        items: [
          { itemCode: "FOUND-V2", name: "Foundation V2", unit: "m3", quantity: 3, unitPrice: 1_100_000, sortOrder: 1 },
        ],
        discountPercent: 5,
        vatPercent: 10,
      },
    });
    expect(updateResponse.status(), await updateResponse.text()).toBe(200);
    expect((await updateResponse.json()).version).toBe(2);

    await page.setViewportSize({ width: 390, height: 844 });
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/quotes", { waitUntil: "networkidle" });

    const quoteCard = page.locator("li:visible").filter({ hasText: quoteCode }).first();
    await expect(quoteCard).toBeVisible();
    await quoteCard.click();
    await expect(page).toHaveURL(new RegExp(`/admin/quotes/${quoteId}$`));

    await page.getByRole("tab", { name: /Phiên bản|Versions|版本|バージョン/i }).click();
    await page.getByRole("button", { name: /V1$/ }).click();
    const versionOneDialog = page.getByRole("dialog").filter({ hasText: "Foundation V1" });
    await expect(versionOneDialog).toBeVisible();
    await expect(versionOneDialog.locator("li:visible").filter({ hasText: "FOUND-V1" })).toContainText("Foundation V1");
    await expect(versionOneDialog.locator("li:visible").filter({ hasText: "STEEL-V1" })).toContainText("Steel V1");
    await expect(versionOneDialog.locator("input, textarea, select")).toHaveCount(0);
    await versionOneDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();

    await page.getByRole("button", { name: /V2$/ }).click();
    const versionTwoDialog = page.getByRole("dialog").filter({ hasText: "Foundation V2" });
    await expect(versionTwoDialog).toBeVisible();
    await expect(versionTwoDialog.locator("li:visible").filter({ hasText: "FOUND-V2" })).toContainText("Foundation V2");
    await expect(versionTwoDialog.getByText(/Hiện tại|Current|当前|現行/i)).toBeVisible();
    await expect(versionTwoDialog.locator("input, textarea, select")).toHaveCount(0);
  } finally {
    if (quoteId) await api.delete(`/api/quotes/${quoteId}`, { headers });
    if (opportunityId) await api.delete(`/api/opportunities/${opportunityId}`, { headers });
    if (customerId) await api.delete(`/api/customers/${customerId}`, { headers });
  }
});
