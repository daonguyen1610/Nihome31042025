import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { uploadSignedContractScan } from "../fixtures/contracts";
import { createOwnCustomer } from "../fixtures/designProjects";
import { fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// GNF factory form: 23 sheets, totals at the top of each sheet; the first building sheet is sheet4.xml.
const temporaryWorks = "xl/worksheets/sheet4.xml";

test("a filled factory BOQ is read from its own sheet and adds up to the sheet total", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `Chủ đầu tư nhà xưởng ${stamp}`);
  const created = await api.post("/api/operational-projects", {
    headers,
    data: { name: `Nhà xưởng ${stamp}`, customerId },
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
      value: 35_000_000,
      scopeOfWork: "Xây dựng nhà xưởng",
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
  await dialog.getByLabel("BOQ form").selectOption({ label: "Factory construction quotation" });
  const downloading = page.waitForEvent("download");
  await dialog.getByTestId("boq-workbook-template-download").click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("BOQ-Nha-xuong.xlsx");

  // Electricity setup (1 lot) and 8 months of temporary power, as Excel would store them.
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-nha-xuong.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(
      await readFile((await download.path())!),
      {
        E7: 1,
        F7: 15_000_000,
        I7: 15_000_000,
        J7: 15_000_000,
        E8: 8,
        G8: 2_500_000,
        I8: 2_500_000,
        J8: 20_000_000,
        J6: 35_000_000,
      },
      temporaryWorks,
    ),
  });
  await dialog.getByTestId("boq-workbook-validate").click();
  const preview = page.getByTestId("boq-workbook-preview");
  await expect(preview).toContainText("35.000.000");
  await dialog.screenshot({ path: testInfo.outputPath("factory-boq-preview.png") });
  await page.getByTestId("boq-workbook-confirm").click();
  await page.waitForURL(/\/boq\/\d+$/);
  const sheets = page.getByTestId("boq-workbook-sheets");
  await expect(sheets).toContainText("Source file: boq-nha-xuong.xlsx");
  await expect(sheets).toContainText("1.Công Tác Tạm");
  await expect(page.locator("body")).not.toContainText("boqWorkbook.");

  const boqs = await api.get(`/api/operational-projects/${projectId}/procurement/boq-revisions`, {
    headers,
  });
  expect((await boqs.json()).items[0].costTotal).toBe(35_000_000);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(sheets).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: testInfo.outputPath("factory-boq-mobile.png"), fullPage: true });
});
