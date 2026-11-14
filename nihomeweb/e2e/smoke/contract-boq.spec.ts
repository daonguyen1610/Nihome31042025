import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { uploadSignedContractScan } from "../fixtures/contracts";
import { createOwnCustomer } from "../fixtures/designProjects";
import { approveProjectBoq, fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// 31/10 meeting: the won tender BOQ becomes the contract BOQ without a new file; a contract BOQ
// that differs (or a later change) is uploaded on the project page against the signed contract.
test("the PM turns the approved tender BOQ into the contract BOQ, then a revised contract BOQ is uploaded", async ({
  page,
  api,
  baseURL,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `Chủ đầu tư ${stamp}`);
  const managers = await api.get(`/api/users?role=PM&search=${TEST_USERS.pm.phoneNumber}&take=10`, {
    headers,
  });
  const created = await api.post("/api/operational-projects", {
    headers,
    data: {
      name: `Căn hộ dịch vụ ${stamp}`,
      customerId,
      projectManagerUserId: (await managers.json()).items[0].id,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const projectId = (await created.json()).id as number;
  const tender = await approveProjectBoq(api, headers, projectId, [
    { name: "Đầu báo khói", unit: "cái", quantity: 50, unitPrice: 420_000 },
  ]);

  const contractResponse = await api.post("/api/contracts", {
    headers,
    data: {
      customerId,
      operationalProjectId: projectId,
      direction: "Upstream",
      type: "Construction",
      startDate: new Date(Date.now() + 86_400_000).toISOString().slice(0, 10),
      endDate: new Date(Date.now() + 181 * 86_400_000).toISOString().slice(0, 10),
      value: 25_000_000,
      scopeOfWork: "PCCC căn hộ dịch vụ",
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
  await loginInBrowserAs(page, TEST_USERS.pm);
  await page.goto(`${baseURL}/admin/procurement-control/projects/${projectId}/boq/${tender.id}`, {
    waitUntil: "networkidle",
  });
  await expect(page.getByText("Approved (tender)").first()).toBeVisible();
  await page.getByTestId("boq-contract-convert").click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("keeps its file and lines");
  await expect(dialog.getByRole("combobox")).toContainText(contract.contractNumber);
  await dialog.screenshot({ path: testInfo.outputPath("contract-convert-dialog.png") });
  await dialog.getByTestId("boq-contract-convert-confirm").click();
  await expect(dialog).toBeHidden();
  await expect(page.getByTestId("boq-contract-convert")).toHaveCount(0);
  await expect(page.locator("main")).toContainText(contract.contractNumber);
  await expect(page.locator("body")).not.toContainText("boqWorkbook.");

  // The same revision, file unchanged, is now the material allowance.
  const requests = await api.get(
    `/api/operational-projects/${projectId}/procurement/material-requests`,
    { headers },
  );
  expect((await requests.json()).currentApprovedBoq.revisionId).toBe(tender.id);

  // A revised contract BOQ is uploaded on the project page against the signed contract.
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`${baseURL}/admin/operational-projects/${projectId}`, {
    waitUntil: "networkidle",
  });
  const projectBoq = page.getByTestId("project-boq");
  await expect(projectBoq.getByRole("listitem").first()).toContainText(contract.contractNumber);
  await projectBoq.getByTestId("boq-contract-upload").click();
  const upload = page.getByRole("dialog");
  await expect(upload.getByLabel("Upstream contract")).toHaveValue(String(contract.id));
  const downloading = page.waitForEvent("download");
  await upload.getByTestId("boq-workbook-template-download").click();
  const template = await (await downloading).path();
  await upload.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-hop-dong-dieu-chinh.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(await readFile(template!), {
      E17: 60,
      F17: 420000,
      G17: 25200000,
      G30: 25200000,
      G101: 25200000,
    }),
  });
  await upload.getByTestId("boq-workbook-validate").click();
  await expect(page.getByTestId("boq-workbook-preview")).toContainText("25.200.000");
  await page.getByTestId("boq-workbook-confirm").click();
  await page.waitForURL(/\/boq\/\d+$/);
  await expect(page.getByTestId("boq-workbook-sheets")).toContainText(
    "Source file: boq-hop-dong-dieu-chinh.xlsx",
  );
  await expect(page.locator("main")).toContainText(contract.contractNumber);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByTestId("boq-workbook-sheets")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({ path: testInfo.outputPath("contract-boq-mobile.png"), fullPage: true });
});
