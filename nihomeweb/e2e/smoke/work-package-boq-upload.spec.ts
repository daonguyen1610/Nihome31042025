import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { fillWorkbook, xlsxType } from "../fixtures/projectBoq";

// Approval submissions queue an email, so this runs against the isolated SMTP sink.
test.beforeEach(() => {
  test.skip(!process.env.MAIL_SINK_URL, "Approval submissions require the isolated SMTP sink");
});

test("procurement uploads the filled BOQ in its work package and the lead approves the tender BOQ", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(150_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const write = () => ({ ...headers, "Idempotency-Key": crypto.randomUUID() });
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, `PCCC ${stamp}`);
  const project = await api.post("/api/operational-projects", {
    headers: write(),
    data: { name: `Căn hộ dịch vụ ${stamp}`, customerId },
  });
  expect(project.status(), await project.text()).toBe(201);
  const projectId = (await project.json()).id as number;
  const opportunity = await api.post("/api/opportunities", {
    headers: write(),
    data: {
      name: `PCCC căn hộ ${stamp}`,
      customerId,
      operationalProjectId: projectId,
      estimatedValue: 500_000_000,
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
      name: `Yêu cầu báo giá đầu vào PCCC ${stamp}`,
    },
  });
  expect(work.status(), await work.text()).toBe(201);
  const packageId = (await work.json()).id as number;
  // The lead hands the request to a Procurement staff member, which opens the package to them.
  const staff = await api.get(
    `/api/users?role=PROCUREMENT&search=${TEST_USERS.procurement.phoneNumber}&take=10`,
    { headers },
  );
  const today = new Date();
  const task = await api.post(`/api/work-packages/${packageId}/tasks`, {
    headers: write(),
    data: {
      wbsCode: "BOQ-01",
      title: "Lập BOQ báo giá đầu vào PCCC",
      assigneeUserId: (await staff.json()).items[0].id,
      approverUserId: leadUserId,
      plannedStart: today.toISOString(),
      plannedEnd: new Date(today.getTime() + 14 * 86_400_000).toISOString(),
      ccUserIds: [],
      predecessorTaskIds: [],
    },
  });
  expect(task.status(), await task.text()).toBe(201);

  // Procurement works entirely inside its work package.
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.procurement);
  await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
  const section = page.getByTestId("work-package-boq");
  await expect(section).toContainText("Input-price BOQ");
  await expect(section.getByRole("link", { name: "BOQ form library" })).toHaveAttribute(
    "href",
    "/admin/material-rates/boq",
  );
  await section.getByTestId("work-package-boq-upload").click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("BOQ form")).toContainText(
    "Serviced-apartment fire protection quotation",
  );
  // A work-package upload is always a tender BOQ, so there is no contract choice.
  await expect(dialog.getByLabel("Upstream contract")).toHaveCount(0);

  const downloading = page.waitForEvent("download");
  await dialog.getByTestId("boq-workbook-template-download").click();
  const template = await (await downloading).path();
  // A wrong cell is named by sheet and cell, and nothing is saved.
  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-loi.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(await readFile(template!), { E17: "50 cái", F17: 420000 }),
  });
  await dialog.getByTestId("boq-workbook-validate").click();
  const issues = page.getByTestId("boq-workbook-issues");
  await expect(issues).toContainText("nothing was saved");
  await expect(issues).toContainText("Sheet “ptct” · cell E17");
  await expect(page.getByTestId("boq-workbook-confirm")).toBeDisabled();

  await dialog.getByLabel("Completed BOQ file").setInputFiles({
    name: "boq-pccc-dau-vao.xlsx",
    mimeType: xlsxType,
    buffer: await fillWorkbook(await readFile(template!), {
      E17: 50,
      F17: 420000,
      G17: 21000000,
      G30: 21000000,
      G101: 21000000,
    }),
  });
  await dialog.getByTestId("boq-workbook-validate").click();
  await expect(page.getByTestId("boq-workbook-preview")).toContainText("21.000.000");
  await page.getByTestId("boq-workbook-confirm").click();
  await expect(dialog).toBeHidden();
  await expect(section).toContainText("R1");
  await expect(section).toContainText("boq-pccc-dau-vao.xlsx");

  await section.getByTestId("work-package-boq-submit").click();
  await page.getByRole("button", { name: "Submit and notify" }).click();
  await expect(section).toContainText("Submitted");
  await expect(section.getByTestId("work-package-boq-approve")).toHaveCount(0);
  await page.screenshot({
    path: testInfo.outputPath("work-package-boq-submitted.png"),
    fullPage: true,
  });

  // The package lead, not the uploader, approves it as the project's tender BOQ.
  await loginInBrowserAs(page, TEST_USERS.procurementManager);
  await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
  await section.getByTestId("work-package-boq-approve").click();
  await expect(section).toContainText("Approved (tender)");

  const boqs = await api.get(`/api/operational-projects/${projectId}/procurement/boq-revisions`, {
    headers,
  });
  const revision = (await boqs.json()).items[0];
  expect([revision.status, revision.costTotal]).toEqual(["TenderApproved", 21_000_000]);

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(section).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
  await page.screenshot({
    path: testInfo.outputPath("work-package-boq-mobile.png"),
    fullPage: true,
  });
});
