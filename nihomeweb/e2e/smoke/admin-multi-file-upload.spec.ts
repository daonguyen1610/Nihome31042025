/// <reference types="node" />
import { test, expect, TEST_USERS } from "../fixtures/auth";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const samplePdf = path.resolve(
  __dirname,
  "../../../nihomebackend/wwwroot/process-assets/files/501e798356d44d8792986b936ac2d100.pdf",
);

const uploadButton = /Tải lên|Upload|上传|アップロード/i;
const documentsTab = /Tài liệu|Documents|文档|資料/i;

test("multi-file upload stores every quote and customer document", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(120_000);

  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  const customerName = `Multi-file ${unique}`;
  const quoteFileNames = [`quote-a-${unique}.pdf`, `quote-b-${unique}.pdf`];
  const customerFileNames = [`customer-a-${unique}.pdf`, `customer-b-${unique}.pdf`];
  let customerId = 0;
  let operationalProjectId = 0;
  let opportunityId = 0;
  let quoteId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: customerName,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "Multi-file contact",
          phone: `09${unique.slice(-8)}`,
          email: `multi-${unique}@example.com`,
          isPrimary: true,
        },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = (await customerResponse.json()).id as number;

    const projectResponse = await api.post("/api/operational-projects", {
      headers,
      data: { name: `Multi-file project ${unique}`, customerId },
    });
    expect(projectResponse.status(), await projectResponse.text()).toBe(201);
    operationalProjectId = (await projectResponse.json()).id as number;

    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: {
        name: `Multi-file opportunity ${unique}`,
        customerId,
        operationalProjectId,
        estimatedValue: 100_000_000,
        winProbability: 40,
      },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = (await opportunityResponse.json()).id as number;

    const quoteResponse = await api.post("/api/quotes", {
      headers,
      data: { opportunityId, draftOnly: true },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    quoteId = (await quoteResponse.json()).id as number;

    const pdf = fs.readFileSync(samplePdf);
    const files = (names: string[]) =>
      names.map((name) => ({ name, mimeType: "application/pdf", buffer: pdf }));

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`/admin/customers?open=${customerId}`, { waitUntil: "networkidle" });
    const customerDialog = page.getByTestId("customer-detail-dialog");
    await expect(customerDialog).toBeVisible();
    await expect(customerDialog.getByText(customerName)).toBeVisible({ timeout: 15_000 });
    await customerDialog.getByRole("tab", { name: documentsTab }).click();
    await customerDialog.locator("#customer-document-file").setInputFiles(files(customerFileNames));
    await customerDialog.locator("#customer-document-label").fill("Multi-file evidence");
    await customerDialog.getByRole("button", { name: uploadButton }).click();
    for (const name of customerFileNames) {
      await expect(customerDialog.locator("li").filter({ hasText: name })).toBeVisible({
        timeout: 15_000,
      });
    }
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: documentsTab }).click();
    await page.locator("#quote-document-file").setInputFiles(files(quoteFileNames));
    await page.locator("#quote-document-label").fill("Multi-file evidence");
    await page.getByRole("button", { name: uploadButton }).click();
    for (const name of quoteFileNames) {
      await expect(page.locator("li").filter({ hasText: name })).toBeVisible({ timeout: 15_000 });
    }
  } finally {
    const deleteDocuments = async (base: string) => {
      const list = await api.get(base, { headers });
      if (!list.ok()) return;
      for (const document of (await list.json()) as { id: number }[]) {
        await api.delete(`${base}/${document.id}`, { headers });
      }
    };
    if (quoteId) await deleteDocuments(`/api/quotes/${quoteId}/documents`);
    if (customerId) await deleteDocuments(`/api/customers/${customerId}/documents`);
    if (quoteId) await hardDeleteBusinessRoot(api, headers, `/api/quotes/${quoteId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (operationalProjectId)
      await hardDeleteBusinessRoot(
        api,
        headers,
        `/api/operational-projects/${operationalProjectId}`,
      );
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
