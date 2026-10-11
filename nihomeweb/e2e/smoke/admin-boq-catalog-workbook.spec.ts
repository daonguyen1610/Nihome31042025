/// <reference types="node" />
import JSZip from "jszip";
import { expect, test, TEST_USERS } from "../fixtures/auth";

async function workbook() {
  const zip = new JSZip();
  zip.file("[Content_Types].xml", `<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>`);
  zip.file("_rels/.rels", `<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>`);
  zip.file("xl/workbook.xml", `<?xml version="1.0"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Summary" sheetId="1" r:id="rId1"/><sheet name="Factory" sheetId="2" r:id="rId2"/></sheets></workbook>`);
  zip.file("xl/_rels/workbook.xml.rels", `<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/></Relationships>`);
  const sheet = (rows: string[][]) => `<?xml version="1.0"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${rows.map((row, ri) => `<row r="${ri + 1}">${row.map((value, ci) => `<c r="${String.fromCharCode(65 + ci)}${ri + 1}" t="inlineStr"><is><t>${value}</t></is></c>`).join("")}</row>`).join("")}</sheetData></worksheet>`;
  zip.file("xl/worksheets/sheet1.xml", sheet([["TOTAL"]]));
  zip.file("xl/worksheets/sheet2.xml", sheet([["Item code", "Description", "Unit", "Quantity", "Unit price"], ["NX-01", "Foundation concrete", "m3", "10", "1500000"]]));
  return zip.generateAsync({ type: "nodebuffer" });
}

test("imports a multi-sheet workbook into a Draft BOQ rate revision", async ({ api, page, loginAs, loginInBrowserAs }) => {
  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const suffix = `${Date.now()}`;
  const templateName = `Factory mapping ${suffix}`;
  let savedTemplate: Record<string, unknown> | undefined;
  await page.route("**/api/material-rate-catalogs/boq-import-templates", async route => {
    if (route.request().method() !== "POST") return route.continue();
    savedTemplate = route.request().postDataJSON() as Record<string, unknown>;
    await route.fulfill({
      status: 201,
      contentType: "application/json",
      body: JSON.stringify({ id: 2_147_480_000, isSystem: false, ...savedTemplate }),
    });
  });
  const catalogResponse = await api.post("/api/material-rate-catalogs", { headers, data: { catalogType: "Boq", code: `E2E-BOQ-${suffix}`, name: `Factory BOQ ${suffix}`, currency: "VND", isActive: true } });
  expect(catalogResponse.status(), await catalogResponse.text()).toBe(201);
  const catalogId = (await catalogResponse.json()).id as number;
  const revisionResponse = await api.post(`/api/material-rate-catalogs/${catalogId}/revisions`, { headers, data: { effectiveFrom: "2042-01-01" } });
  expect(revisionResponse.status(), await revisionResponse.text()).toBe(201);
  try {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/material-rates/boq", { waitUntil: "networkidle" });
    await page.getByTestId("material-rates-search").fill(`E2E-BOQ-${suffix}`);
    await page.getByRole("button", { name: new RegExp(`Factory BOQ ${suffix}.*E2E-BOQ-${suffix}`) }).click();
    await page.getByTestId("boq-workbook-open").click();
    await page.getByTestId("boq-workbook-file").setInputFiles({ name: "factory-boq.xlsx", mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", buffer: await workbook() });
    await page.getByTestId("boq-workbook-preview").click();
    await expect(page.getByTestId("boq-workbook-sheet-0")).toContainText("Summary");
    await expect(page.getByTestId("boq-workbook-sheet-1")).toContainText("Factory");
    await expect(page.getByTestId("boq-workbook-include-0")).not.toBeChecked();
    await expect(page.getByTestId("boq-workbook-include-1")).toBeChecked();
    await page.getByTestId("boq-workbook-template-name").fill(templateName);
    await page.getByTestId("boq-workbook-template-save").click();
    await expect.poll(() => savedTemplate).toMatchObject({ name: templateName, headerRow: 1, descriptionColumn: 2, unitColumn: 3, quantityColumn: 4, unitPriceColumn: 5 });
    await page.getByTestId("boq-workbook-descriptionColumn-1").fill("5");
    await expect(page.getByTestId("boq-workbook-confirm")).toBeDisabled();
    await page.getByTestId("boq-workbook-template-1").click();
    await page.getByRole("option", { name: templateName }).click();
    await expect(page.getByTestId("boq-workbook-descriptionColumn-1")).toHaveValue("2");
    await expect(page.getByTestId("boq-workbook-confirm")).toBeEnabled();
    await page.getByTestId("boq-workbook-confirm").click();
    await expect(page.getByTestId("boq-catalog-workbook-dialog")).toBeHidden();
    await expect(page.getByRole("cell", { name: "Foundation concrete", exact: true })).toBeVisible();
  } finally {
    await api.delete(`/api/material-rate-catalogs/${catalogId}`, { headers });
  }
});
