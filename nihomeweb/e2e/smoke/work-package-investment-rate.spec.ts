import { readFile } from "node:fs/promises";
import type { APIRequestContext, Page } from "@playwright/test";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// Factory form: 5,000 m² floor + 600 m² canopy at 50% = 5,300 m²; 200,000 + 1,600,000 = 1,800,000 ₫/m².
const filledFactory = {
  E12: 5000,
  E14: 600,
  E20: 150000,
  F20: 50000,
  E22: 1200000,
  F22: 300000,
  G22: 100000,
};

// An input-price work package for a new project, opened to the Procurement staff member.
async function seedPackage(api: APIRequestContext, headers: Record<string, string>) {
  const write = () => ({ ...headers, "Idempotency-Key": crypto.randomUUID() });
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `Nhà xưởng ${stamp}`);
  const project = await api.post("/api/operational-projects", {
    headers: write(),
    data: { name: `Nhà xưởng An Phú ${stamp}`, customerId },
  });
  expect(project.status(), await project.text()).toBe(201);
  const projectId = (await project.json()).id as number;
  const opportunity = await api.post("/api/opportunities", {
    headers: write(),
    data: {
      name: `Xây dựng nhà xưởng ${stamp}`,
      customerId,
      operationalProjectId: projectId,
      estimatedValue: 9_000_000_000,
      winProbability: 50,
    },
  });
  expect(opportunity.status(), await opportunity.text()).toBe(201);
  const opportunityId = (await opportunity.json()).id as number;
  const options = await (await api.get("/api/work-packages/options", { headers })).json();
  const department = options.departments.find(
    (item: { code: string }) => item.code === "PROCUREMENT",
  );
  const managers = await api.get(
    `/api/users?role=PROCUREMENT_MANAGER&search=${TEST_USERS.procurementManager.phoneNumber}&take=10`,
    { headers },
  );
  const leadUserId = (await managers.json()).items[0].id as number;
  const work = await api.post("/api/work-packages", {
    headers: write(),
    data: {
      kind: "Project",
      purpose: "ProcurementInputPricing",
      operationalProjectId: projectId,
      sourceOpportunityId: opportunityId,
      departmentRoleGroupId: department.id,
      leadUserId,
      name: `Suất đầu tư nhà xưởng ${stamp}`,
    },
  });
  expect(work.status(), await work.text()).toBe(201);
  const packageId = (await work.json()).id as number;
  const staff = await api.get(
    `/api/users?role=PROCUREMENT&search=${TEST_USERS.procurement.phoneNumber}&take=10`,
    { headers },
  );
  const today = new Date();
  const task = await api.post(`/api/work-packages/${packageId}/tasks`, {
    headers: write(),
    data: {
      wbsCode: "SDT-01",
      title: "Lập suất đầu tư nhà xưởng",
      assigneeUserId: (await staff.json()).items[0].id,
      approverUserId: leadUserId,
      plannedStart: today.toISOString(),
      plannedEnd: new Date(today.getTime() + 14 * 86_400_000).toISOString(),
      ccUserIds: [],
      predecessorTaskIds: [],
    },
  });
  expect(task.status(), await task.text()).toBe(201);
  return { packageId, opportunityId, write };
}

// Procurement downloads the factory form in the upload dialog, then uploads the filled copy.
async function uploadFilledForm(page: Page, packageId: number) {
  await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
  const section = page.getByTestId("work-package-investment-rate");
  await expect(section).toContainText("Investment rate (Excel file)");
  await section.getByTestId("work-package-investment-rate-upload").click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Investment-rate form")).toContainText(
    "Investment rate: factory & warehouse (steel frame)",
  );
  const downloading = page.waitForEvent("download");
  await dialog.getByTestId("investment-rate-template-download").click();
  const template = await readFile((await (await downloading).path())!);
  return { section, dialog, template };
}

test("procurement uploads a filled investment-rate form in its work package", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const { packageId } = await seedPackage(api, headers);

  // The forms are listed on the Quote templates page under Investment rate.
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.procurement);
  await page.goto("/admin/material-rates/investment", { waitUntil: "networkidle" });
  const library = page.getByTestId("investment-rate-templates");
  await expect(library).toContainText("Investment rate: office & administration building (RC)");
  await expect(library).toContainText("Investment rate: interior fit-out");

  const { section, dialog, template } = await uploadFilledForm(page, packageId);
  // A wrong cell is named by sheet and cell, and nothing is saved.
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "suat-dau-tu-loi.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(template, { ...filledFactory, E12: "năm nghìn" }),
  });
  await dialog.getByTestId("investment-rate-validate").click();
  const issues = page.getByTestId("boq-workbook-issues");
  await expect(issues).toContainText("nothing was saved");
  await expect(issues).toContainText("Sheet “Suat dau tu” · cell E12");
  await expect(page.getByTestId("investment-rate-confirm")).toBeDisabled();

  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "suat-dau-tu-nha-xuong.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(template, filledFactory),
  });
  await dialog.getByTestId("investment-rate-validate").click();
  const preview = page.getByTestId("investment-rate-preview");
  await expect(preview).toContainText("1.800.000");
  await expect(preview).toContainText("9.540.000.000");
  await page.getByTestId("investment-rate-confirm").click();
  await expect(dialog).toBeHidden();
  await expect(section).toContainText("R1");
  await expect(section).toContainText("suat-dau-tu-nha-xuong.xlsx");
  await expect(section).toContainText("Draft");
  await section.getByRole("button", { name: "Show 12 work groups" }).click();
  await expect(section.getByTestId("investment-rate-groups")).toContainText(
    "NX-03 · Kết cấu thép tiền chế",
  );
  await expect(section.getByTestId("work-package-investment-rate-submit")).toBeEnabled();
  await page.screenshot({ path: testInfo.outputPath("investment-rate-draft.png"), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(section).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: testInfo.outputPath("investment-rate-mobile.png"),
    fullPage: true,
  });
});

test("the lead approves the investment rate and the unit-cost quote applies it", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  // Approval submissions queue an email, so this runs against the isolated SMTP sink.
  test.skip(!process.env.MAIL_SINK_URL, "Approval submissions require the isolated SMTP sink");
  test.setTimeout(180_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const { packageId, opportunityId, write } = await seedPackage(api, headers);
  const draft = await api.post("/api/quotes", {
    headers: write(),
    data: { opportunityId, draftOnly: true },
  });
  expect(draft.status(), await draft.text()).toBe(201);
  const quoteId = (await draft.json()).id as number;

  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.procurement);
  const { section, dialog, template } = await uploadFilledForm(page, packageId);
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "suat-dau-tu-nha-xuong.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(template, filledFactory),
  });
  await dialog.getByTestId("investment-rate-validate").click();
  await page.getByTestId("investment-rate-confirm").click();
  await expect(dialog).toBeHidden();
  await section.getByTestId("work-package-investment-rate-submit").click();
  await page.getByRole("button", { name: "Submit and notify" }).click();
  await expect(section).toContainText("Submitted");
  await expect(section.getByTestId("work-package-investment-rate-approve")).toHaveCount(0);

  // The package lead, not the uploader, approves it.
  await loginInBrowserAs(page, TEST_USERS.procurementManager);
  await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
  await section.getByTestId("work-package-investment-rate-approve").click();
  await expect(section).toContainText("Approved");

  // Sales prices the unit-cost quote from it: area and rate are filled from the form.
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
  await page.getByTestId("quote-edit").click();
  await page.getByTestId("quote-output-method").click();
  await page.getByRole("option", { name: /unit cost|investment rate/i }).click();
  const source = page.getByTestId("quote-project-rate");
  await expect(source).toContainText("R1");
  await expect(source).toContainText("1.800.000");
  await source.getByTestId("quote-project-rate-apply").click();
  await expect(source.getByTestId("quote-project-rate-applied")).toBeVisible();
  await expect(page.getByTestId("quote-rate-catalog")).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath("quote-project-rate.png"), fullPage: true });
  await page
    .getByRole("button", { name: /^(Save|Save changes)$/ })
    .first()
    .click();
  await expect
    .poll(async () => {
      const saved = await (await api.get(`/api/quotes/${quoteId}`, { headers })).json();
      return [saved.rateSource, saved.unitPricePerSqm, saved.subtotal];
    })
    .toEqual(["ProjectRate", 1_800_000, 9_540_000_000]);
});
