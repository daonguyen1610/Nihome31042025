import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { uploadSignedContractScan } from "../fixtures/contracts";
import { createOwnCustomer } from "../fixtures/designProjects";
import { fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// ME base form: 18 sheets; "San lấp" (sheet3.xml) prices each row as material plus labour.
const siteLevelling = "xl/worksheets/sheet3.xml";

test("an ME base BOQ stores material plus labour as one unit price", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `Chủ đầu tư nhà máy ME ${stamp}`);
  const created = await api.post("/api/operational-projects", {
    headers,
    data: { name: `Nhà máy ME ${stamp}`, customerId },
  });
  expect(created.status(), await created.text()).toBe(201);
  const projectId = (await created.json()).id as number;
  const contractResponse = await api.post("/api/contracts", {
    headers,
    data: {
      customerId,
      operationalProjectId: projectId,
      direction: "Upstream",
      type: "Construction",
      startDate: new Date(Date.now() + 86_400_000).toISOString().slice(0, 10),
      endDate: new Date(Date.now() + 181 * 86_400_000).toISOString().slice(0, 10),
      value: 385_000_000,
      scopeOfWork: "Xây dựng, cơ điện và PCCC",
    },
  });
  expect(contractResponse.status(), await contractResponse.text()).toBe(201);
  const contract = await contractResponse.json();
  await uploadSignedContractScan(api, headers, contract.id);
  const signed = await api.post(`/api/contracts/${contract.id}/transition`, {
    headers,
    data: { newStatus: "Signed", rowVersion: contract.rowVersion },
  });
  expect(signed.status(), await signed.text()).toBe(200);

  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });
  await page.getByTestId("project-boq").getByTestId("boq-contract-upload").click();
  const dialog = page.getByRole("dialog");
  await dialog
    .getByLabel("BOQ form")
    .selectOption({ label: "Construction, M&E and fire protection quotation" });
  const downloading = page.waitForEvent("download");
  await dialog.getByTestId("boq-workbook-template-download").click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("BOQ-Xay-dung-ME-PCCC.xlsx");

  // Clearing and levelling 11,000 m² (the base quantity) at 20,000 material + 15,000 labour per m².
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-me-pccc.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(
      await readFile((await download.path())!),
      {
        E11: 20_000,
        F11: 15_000,
        G11: 220_000_000,
        H11: 165_000_000,
        I11: 385_000_000,
        I14: 385_000_000,
      },
      siteLevelling,
    ),
  });
  await dialog.getByTestId("boq-workbook-validate").click();
  const preview = page.getByTestId("boq-workbook-preview");
  await expect(preview).toContainText("385.000.000");
  await dialog.screenshot({ path: testInfo.outputPath("me-boq-preview.png") });
  await page.getByTestId("boq-workbook-confirm").click();
  await page.waitForURL(/\/boq\/\d+$/);
  const sheets = page.getByTestId("boq-workbook-sheets");
  await expect(sheets).toContainText("Source file: boq-me-pccc.xlsx");
  await expect(sheets).toContainText("San lấp");
  await expect(page.locator("body")).not.toContainText("boqWorkbook.");

  const boqs = await api.get(`/api/operational-projects/${projectId}/procurement/boq-revisions`, {
    headers,
  });
  expect((await boqs.json()).items[0].costTotal).toBe(385_000_000);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(sheets).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: testInfo.outputPath("me-boq-mobile.png"), fullPage: true });
});
