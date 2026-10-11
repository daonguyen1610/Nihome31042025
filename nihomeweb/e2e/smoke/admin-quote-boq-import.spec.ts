/// <reference types="node" />
import type { APIRequestContext } from "@playwright/test";
import JSZip from "jszip";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const uniqueId = () => `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const escapeXml = (value: string) => value
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;");

async function createMultiSheetBoqWorkbook() {
  const zip = new JSZip();
  zip.file("[Content_Types].xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
      <Default Extension="xml" ContentType="application/xml"/>
      <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
      <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
      <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
      <Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
    </Types>`);
  zip.file("_rels/.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
    </Relationships>`);
  zip.file("xl/workbook.xml", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
      <sheets>
        <sheet name="Tổng hợp" sheetId="1" r:id="rId1"/>
        <sheet name="Nhà xưởng" sheetId="2" r:id="rId2"/>
        <sheet name="Hạ tầng ẩn" sheetId="3" state="hidden" r:id="rId3"/>
      </sheets>
    </workbook>`);
  zip.file("xl/_rels/workbook.xml.rels", `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
      <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
      <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>
    </Relationships>`);

  const cell = (reference: string, value: string | number) => typeof value === "number"
    ? `<c r="${reference}"><v>${value}</v></c>`
    : `<c r="${reference}" t="inlineStr"><is><t>${escapeXml(value)}</t></is></c>`;
  const sheet = (rows: Array<Array<string | number>>) => `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
    <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
      ${rows.map((values, rowIndex) => `<row r="${rowIndex + 1}">${values.map((value, columnIndex) => cell(`${String.fromCharCode(65 + columnIndex)}${rowIndex + 1}`, value)).join("")}</row>`).join("")}
    </sheetData></worksheet>`;
  zip.file("xl/worksheets/sheet1.xml", sheet([["TỔNG GIÁ TRỊ"]]));
  zip.file("xl/worksheets/sheet2.xml", sheet([
    ["Mã hạng mục", "NỘI DUNG CÔNG VIỆC", "ĐVT", "KHỐI LƯỢNG", "ĐƠN GIÁ"],
    ["NX-001", "Bê tông móng nhà xưởng", "m3", 10, 1_500_000],
  ]));
  zip.file("xl/worksheets/sheet3.xml", sheet([
    ["Mã hạng mục", "Mô tả", "Đơn vị", "Khối lượng", "Đơn giá"],
    ["HT-001", "Đường bê tông nội bộ", "m2", 20, 750_000],
  ]));
  return zip.generateAsync({ type: "nodebuffer" });
}

async function createQuoteFixture(api: APIRequestContext, headers: { Authorization: string }) {
  const suffix = uniqueId();
  const customerResponse = await api.post("/api/customers", {
    headers,
    data: {
      type: "Individual",
      name: `BOQ import customer ${suffix}`,
      sourceCode: "marketing",
      primaryContact: {
        fullName: "BOQ Import Contact",
        phone: `07${Date.now().toString().slice(-8)}`,
        email: `boq-import-${suffix}@test.example`,
        isPrimary: true,
      },
    },
  });
  expect(customerResponse.status(), await customerResponse.text()).toBe(201);
  const customerId = (await customerResponse.json()).id as number;
  const opportunityResponse = await api.post("/api/opportunities", {
    headers,
    data: { name: `BOQ import opportunity ${suffix}`, customerId, estimatedValue: 500_000_000, winProbability: 50 },
  });
  expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
  const opportunityId = (await opportunityResponse.json()).id as number;
  const quoteResponse = await api.post("/api/quotes", {
    headers,
    data: {
      opportunityId,
      method: "Boq",
      items: [{ itemCode: "OLD-001", name: "Dòng BOQ cũ", unit: "item", quantity: 1, unitPrice: 1 }],
      discountPercent: 0,
      vatPercent: 10,
    },
  });
  expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
  const quote = await quoteResponse.json();
  return { customerId, opportunityId, quoteId: quote.id as number };
}

test.describe.serial("quote BOQ workbook import", () => {
  test("downloads the template, validates mappings, previews hidden sheets, and imports all Detail sheets", async ({
    api, page, loginAs, loginInBrowserAs,
  }) => {
    test.setTimeout(90_000);
    const token = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${token}` };
    const fixture = await createQuoteFixture(api, headers);
    try {
      await loginInBrowserAs(page, TEST_USERS.salesManager);
      await page.goto(`/admin/quotes/${fixture.quoteId}`, { waitUntil: "networkidle" });
      await page.getByTestId("quote-import-boq").click();
      const dialog = page.getByTestId("quote-boq-import-dialog");
      await expect(dialog).toBeVisible();

      const downloadPromise = page.waitForEvent("download");
      await page.getByTestId("quote-boq-template-download").click();
      expect((await downloadPromise).suggestedFilename()).toBe("NICON-quote-BOQ-template.xlsx");

      const workbook = await createMultiSheetBoqWorkbook();
      await page.getByTestId("quote-boq-import-file").setInputFiles({
        name: "nicon-multi-sheet-boq.xlsx",
        mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        buffer: workbook,
      });
      const previewResponse = page.waitForResponse((response) => response.url().includes("/boq-import/preview") && response.request().method() === "POST");
      await page.getByTestId("quote-boq-import-preview").click();
      expect((await previewResponse).status()).toBe(200);

      await expect(page.getByTestId("quote-boq-sheet-0")).toContainText("Tổng hợp");
      await expect(page.getByTestId("quote-boq-sheet-1")).toContainText("Nhà xưởng");
      await expect(page.getByTestId("quote-boq-sheet-2")).toContainText("Hạ tầng ẩn");
      await expect(page.getByTestId("quote-boq-sheet-2")).toContainText(/Sheet ẩn|Hidden sheet|非表示|隐藏/);

      const detailRow = page.getByTestId("quote-boq-sheet-1");
      const descriptionInput = detailRow.locator('input[type="number"]').nth(1);
      await descriptionInput.fill("3");
      await expect(page.getByRole("alert")).toBeVisible();
      await expect(page.getByTestId("quote-boq-import-confirm")).toBeDisabled();
      await descriptionInput.fill("2");
      await expect(page.getByTestId("quote-boq-import-confirm")).toBeEnabled();

      for (const viewport of [{ width: 390, height: 844 }, { width: 768, height: 1024 }, { width: 1440, height: 900 }]) {
        await page.setViewportSize(viewport);
        const box = await dialog.boundingBox();
        expect(box).not.toBeNull();
        expect(box!.x).toBeGreaterThanOrEqual(0);
        expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width + 1);
        await expect(page.getByTestId("quote-boq-import-confirm")).toBeVisible();
      }

      const confirmResponse = page.waitForResponse((response) => response.url().includes("/boq-import/confirm") && response.request().method() === "POST");
      await page.getByTestId("quote-boq-import-confirm").click();
      expect((await confirmResponse).status()).toBe(200);
      await expect(dialog).toBeHidden();
      await expect(page.locator('input[value="Bê tông móng nhà xưởng"]')).toBeVisible();
      await expect(page.locator('input[value="Đường bê tông nội bộ"]')).toBeVisible();
      await expect(page.locator('input[value="Dòng BOQ cũ"]')).toHaveCount(0);
    } finally {
      await hardDeleteBusinessRoot(api, headers, `/api/quotes/${fixture.quoteId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${fixture.opportunityId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/customers/${fixture.customerId}`);
    }
  });

  test("locks edit and import controls while the submitted quote is pending approval", async ({
    api, page, loginAs, loginInBrowserAs,
  }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${token}` };
    const fixture = await createQuoteFixture(api, headers);
    try {
      const submitResponse = await api.post(`/api/quotes/${fixture.quoteId}/submit`, { headers, data: {} });
      expect(submitResponse.status(), await submitResponse.text()).toBe(200);
      expect((await submitResponse.json()).status).toBe("PendingApproval");

      await loginInBrowserAs(page, TEST_USERS.salesManager);
      await page.goto(`/admin/quotes/${fixture.quoteId}`, { waitUntil: "networkidle" });
      await expect(page.getByTestId("quote-import-boq")).toHaveCount(0);
      await expect(page.getByTestId("quote-edit")).toHaveCount(0);
    } finally {
      await hardDeleteBusinessRoot(api, headers, `/api/quotes/${fixture.quoteId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${fixture.opportunityId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/customers/${fixture.customerId}`);
    }
  });

  test("imports over an approved quote as the next Draft version", async ({
    api, page, loginAs, loginInBrowserAs,
  }) => {
    test.setTimeout(60_000);
    const token = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${token}` };
    const fixture = await createQuoteFixture(api, headers);
    try {
      const submitResponse = await api.post(`/api/quotes/${fixture.quoteId}/submit`, { headers, data: {} });
      expect(submitResponse.status(), await submitResponse.text()).toBe(200);
      const approveResponse = await api.post(`/api/quotes/${fixture.quoteId}/approve`, { headers, data: {} });
      expect(approveResponse.status(), await approveResponse.text()).toBe(200);
      expect((await approveResponse.json()).status).toBe("Approved");

      await loginInBrowserAs(page, TEST_USERS.salesManager);
      await page.goto(`/admin/quotes/${fixture.quoteId}`, { waitUntil: "networkidle" });
      await page.getByTestId("quote-import-boq").click();
      await page.getByTestId("quote-boq-import-file").setInputFiles({
        name: "approved-quote-boq.xlsx",
        mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        buffer: await createMultiSheetBoqWorkbook(),
      });
      await page.getByTestId("quote-boq-import-preview").click();
      await expect(page.getByTestId("quote-boq-import-confirm")).toBeEnabled();
      const confirmResponsePromise = page.waitForResponse((response) => response.url().includes("/boq-import/confirm") && response.request().method() === "POST");
      await page.getByTestId("quote-boq-import-confirm").click();
      const confirmResponse = await confirmResponsePromise;
      expect(confirmResponse.status(), await confirmResponse.text()).toBe(200);
      const result = await confirmResponse.json();
      expect(result.quoteVersion).toBe(2);
      expect(result.quote.status).toBe("Draft");
      await expect(page.getByText("V2", { exact: true })).toBeVisible();
      await expect(page.getByTestId("quote-import-boq")).toBeVisible();
    } finally {
      await hardDeleteBusinessRoot(api, headers, `/api/quotes/${fixture.quoteId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${fixture.opportunityId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/customers/${fixture.customerId}`);
    }
  });

  test("does not expose quote BOQ import to a role without quote management access", async ({
    api, page, loginAs, loginInBrowserAs,
  }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const headers = { Authorization: `Bearer ${token}` };
    const fixture = await createQuoteFixture(api, headers);
    try {
      await loginInBrowserAs(page, TEST_USERS.warehouse);
      await page.goto(`/admin/quotes/${fixture.quoteId}`, { waitUntil: "networkidle" });
      await expect(page.getByTestId("quote-import-boq")).toHaveCount(0);
      await expect(page.getByTestId("quote-boq-import-dialog")).toHaveCount(0);
    } finally {
      await hardDeleteBusinessRoot(api, headers, `/api/quotes/${fixture.quoteId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${fixture.opportunityId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/customers/${fixture.customerId}`);
    }
  });
});
