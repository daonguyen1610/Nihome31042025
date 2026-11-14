import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { uploadSignedContractScan } from "../fixtures/contracts";
import { createOwnCustomer } from "../fixtures/designProjects";
import { fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// Lump-sum form: 21 sheets; "10 THOÁT NƯỚC MƯA" (sheet28.xml) prices each row as material, labour and
// machines, with the sheet total above the rows.
const drainage = "xl/worksheets/sheet28.xml";

test("a lump-sum BOQ stores material, labour and machines as one unit price", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `Chủ đầu tư trọn gói ${stamp}`);
  const created = await api.post("/api/operational-projects", {
    headers,
    data: { name: `Nhà máy trọn gói ${stamp}`, customerId },
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
      value: 358_800_000,
      scopeOfWork: "Thiết kế, xin phép và thi công trọn gói",
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
    .selectOption({ label: "Lump-sum design, permit and construction quotation" });
  const downloading = page.waitForEvent("download");
  await dialog.getByTestId("boq-workbook-template-download").click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("BOQ-Tron-goi.xlsx");

  // Drain trench digging, 717.6 m³ (the base quantity) at 300,000 material + 150,000 labour + 50,000 machines.
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-tron-goi.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(
      await readFile((await download.path())!),
      {
        G15: 300_000,
        H15: 150_000,
        I15: 50_000,
        J15: 500_000,
        K15: 358_800_000,
        K8: 358_800_000,
      },
      drainage,
    ),
  });
  await dialog.getByTestId("boq-workbook-validate").click();
  const preview = page.getByTestId("boq-workbook-preview");
  await expect(preview).toContainText("358.800.000");
  await dialog.screenshot({ path: testInfo.outputPath("tron-goi-boq-preview.png") });
  await page.getByTestId("boq-workbook-confirm").click();
  await page.waitForURL(/\/boq\/\d+$/);
  const sheets = page.getByTestId("boq-workbook-sheets");
  await expect(sheets).toContainText("Source file: boq-tron-goi.xlsx");
  await expect(sheets).toContainText("10 THOÁT NƯỚC MƯA");
  await expect(page.locator("body")).not.toContainText("boqWorkbook.");

  const boqs = await api.get(`/api/operational-projects/${projectId}/procurement/boq-revisions`, {
    headers,
  });
  expect((await boqs.json()).items[0].costTotal).toBe(358_800_000);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(sheets).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: testInfo.outputPath("tron-goi-boq-mobile.png"), fullPage: true });
});
