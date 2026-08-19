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
