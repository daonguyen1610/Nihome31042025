import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { uploadSignedContractScan } from "../fixtures/contracts";
import { createOwnCustomer } from "../fixtures/designProjects";
import { fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// CSA form: 6 sheets; "Bill 2-Main building works" (sheet5.xml) prices each item as material plus
// labour & equipment, with the bill total in Q6.
const building = "xl/worksheets/sheet5.xml";

test("a CSA BOQ reads quantities from the measured rows and adds both prices", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `Chủ đầu tư nhà kho ${stamp}`);
  const created = await api.post("/api/operational-projects", {
    headers,
    data: { name: `Nhà kho CSA ${stamp}`, customerId },
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
      value: 595_671_000,
      scopeOfWork: "Phần xây dựng nhà kho",
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
    .selectOption({ label: "Civil, structural and architectural (CSA) quotation" });
  const downloading = page.waitForEvent("download");
  await dialog.getByTestId("boq-workbook-template-download").click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("BOQ-CSA-Work.xlsx");

  // Natural soil compaction, 11,913.42 m² summed from its measured rows, at 40,000 material + 10,000 labour.
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-csa.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(
      await readFile((await download.path())!),
      {
        L30: 40_000,
        N30: 10_000,
        Q30: 595_671_000,
        Q6: 595_671_000,
      },
      building,
    ),
  });
  await dialog.getByTestId("boq-workbook-validate").click();
  const preview = page.getByTestId("boq-workbook-preview");
  await expect(preview).toContainText("595.671.000");
  await dialog.screenshot({ path: testInfo.outputPath("csa-boq-preview.png") });
  await page.getByTestId("boq-workbook-confirm").click();
  await page.waitForURL(/\/boq\/\d+$/);
  const sheets = page.getByTestId("boq-workbook-sheets");
  await expect(sheets).toContainText("Source file: boq-csa.xlsx");
  await expect(sheets).toContainText("Bill 2-Main building works");
  await expect(page.locator("body")).not.toContainText("boqWorkbook.");

  const boqs = await api.get(`/api/operational-projects/${projectId}/procurement/boq-revisions`, {
    headers,
  });
  expect((await boqs.json()).items[0].costTotal).toBe(595_671_000);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(sheets).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: testInfo.outputPath("csa-boq-mobile.png"), fullPage: true });
});
