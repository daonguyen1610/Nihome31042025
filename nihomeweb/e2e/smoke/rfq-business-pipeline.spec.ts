import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";

test.describe.configure({ mode: "serial" });

// Real stack and seeded business roles. APIs establish only the project/team
// foundation; every procurement, contract and payment state below comes from UI.
for (const outcome of ["Paid", "Rejected", "Cancelled"] as const) {
test(`business roles carry an approved BOQ through RFQ, warehouse and invoice ${outcome}`, async ({ page, api, loginAs, loginInBrowserAs }, testInfo) => {
  test.setTimeout(180_000);
  const suffix = randomUUID().slice(0, 8);
  const adminToken = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${adminToken}`, "Idempotency-Key": randomUUID() };
  const projects = await api.get("/api/operational-projects?pageSize=100", { headers });
  const customerId = (await projects.json()).items.find((x: { code: string }) => x.code === "PJ-SAMPLE-RFQ").customerId;
  const userNames = new Map<string, string>();
  async function userId(role: string) {
    const response = await api.get(`/api/users?role=${role}&skip=0&take=100`, { headers });
    expect(response.ok()).toBe(true);
    const user = (await response.json()).items.find((x: { isActive: boolean }) => x.isActive);
    userNames.set(role, user.fullName);
    return user.id as number;
  }
  const pmId = await userId("PM");
  const procurementId = await userId("PROCUREMENT");
  await userId("ACCOUNTANT");
  const warehouseId = await userId("WAREHOUSE");
  const created = await api.post("/api/operational-projects", { headers, data: { name: `RFQ pipeline ${suffix}`, customerId, projectManagerUserId: pmId } });
  expect(created.status(), await created.text()).toBe(201);
  const project = await created.json();
  for (const [userId, position] of [[procurementId, "Procurement"], [warehouseId, "Warehouse"]] as const) {
    const member = await api.post(`/api/operational-projects/${project.id}/team/members`, {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      data: { userId, position, startedAt: new Date().toISOString(), roles: [{ roleCode: "Observer", scope: "Project" }] },
    });
    expect(member.status(), await member.text()).toBe(201);
  }
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  const workspace = `/admin/procurement-control?projectId=${project.id}`;
  let rfqUrl = "";
  let contractNumber = "";

  await test.step("Procurement prepares the BOQ and BGD approves its budget", async () => {
    await loginInBrowserAs(page, TEST_USERS.procurement);
    await page.goto(`${workspace}&tab=boq`);
    await page.getByRole("button", { name: "Create BOQ revision", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel("Item code", { exact: true }).fill("PIPE-CABLE");
    await dialog.getByLabel("Description", { exact: true }).fill("Factory power cable");
    await dialog.getByLabel("Unit", { exact: true }).fill("m");
    await dialog.getByLabel("Quantity", { exact: true }).fill("100");
    await dialog.getByLabel("Budget unit price", { exact: true }).fill("200000");
    await dialog.getByRole("button", { name: "Create draft", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await page.getByRole("button", { name: "Submit", exact: true }).click();
    await expect(page.getByRole("cell", { name: "Submitted", exact: true })).toBeVisible();
    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto(`${workspace}&tab=boq`);
    await page.getByRole("button", { name: "Approve", exact: true }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Approve", exact: true }).click();
    await expect(page.getByRole("cell", { name: /Approved/ })).toBeVisible();
  });

  await test.step("Procurement requests prices and compares vendor revisions", async () => {
    await loginInBrowserAs(page, TEST_USERS.procurement);
    await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}`);
    await page.getByRole("button", { name: "Create RFQ", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel("Package title", { exact: true }).fill(`Factory cable package ${suffix}`);
    await dialog.getByRole("combobox", { name: "Source BOQ", exact: true }).selectOption({ index: 1 });
    await dialog.getByRole("combobox", { name: "Procurement owner", exact: true }).selectOption(String(procurementId));
    await dialog.getByLabel("Quotation deadline", { exact: true }).fill("2035-01-01T09:00");
    await dialog.getByLabel("Quantity PIPE-CABLE", { exact: true }).fill("20");
    await dialog.getByRole("checkbox", { name: "[SAMPLE] Alternative electrical supplier", exact: true }).check();
    await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    rfqUrl = page.url();
    await page.getByRole("button", { name: "Issue RFQ", exact: true }).click();
    for (const price of [190000, 180000]) {
      await page.getByRole("button", { name: "Submit quote revision", exact: true }).click();
      await dialog.getByRole("combobox", { name: "Vendor", exact: true }).selectOption({ index: 1 });
      await dialog.getByRole("spinbutton", { name: /PIPE-CABLE/ }).fill(String(price));
      await dialog.getByLabel("Delivery lead time (days)", { exact: true }).fill("7");
      await dialog.getByLabel("Quote valid until", { exact: true }).fill("2035-02-01T09:00");
      await dialog.getByLabel("Payment terms", { exact: true }).fill("100% after delivery and invoice validation");
      await dialog.getByRole("button", { name: "Submit quote revision", exact: true }).click();
      await expect(dialog).not.toBeVisible();
    }
    await expect(page.getByRole("region", { name: "Bid comparison matrix", exact: true })).toContainText("3,600,000 VND");
    await page.getByRole("button", { name: "Start evaluation / stop bids", exact: true }).click();
    await expect(page.getByRole("button", { name: "Submit quote revision", exact: true })).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath("01-quote-comparison.png"), fullPage: true });
  });

  await test.step("BGD awards the package and Sales Manager signs the generated contract", async () => {
    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto(rfqUrl);
    await page.getByLabel("Decision / withdrawal reason", { exact: true }).fill("Revised price meets budget, full scope and delivery confirmed");
    await page.getByRole("combobox", { name: "Vendor", exact: true }).selectOption({ index: 1 });
    await page.getByRole("button", { name: "Award and create draft contract", exact: true }).click();
    const contractLink = page.locator("section").getByRole("link", { name: /^PO-RFQ-/ });
    await expect(contractLink).toBeVisible();
    contractNumber = (await contractLink.textContent())!;
    const contractUrl = (await contractLink.getAttribute("href"))!;
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(contractUrl);
    await expect(page.getByRole("heading", { name: contractNumber, exact: true })).toBeVisible();
    await expect(page.getByRole("main")).toContainText(/3[.,]600[.,]000/);
    await page.getByRole("button", { name: "Mark as Signed", exact: true }).click();
    await expect(page.getByRole("button", { name: "Move to In progress", exact: true })).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath("02-signed-contract.png"), fullPage: true });
  });

  await test.step("Site demand is approved and Warehouse receives and issues the cable", async () => {
    const dialog = page.getByRole("dialog");
    await loginInBrowserAs(page, TEST_USERS.pm);
    await page.goto(`${workspace}&tab=requests`);
    await page.getByRole("button", { name: "Create request", exact: true }).click();
    await dialog.getByRole("combobox", { name: "Responsible site user", exact: true }).click();
    await page.getByRole("option", { name: userNames.get("PM")!, exact: true }).click();
    await dialog.getByRole("combobox", { name: "Procurement owner", exact: true }).click();
    await page.getByRole("option", { name: userNames.get("PROCUREMENT")!, exact: true }).click();
    await dialog.getByLabel("Required at", { exact: true }).fill("2035-01-01T09:00");
    await dialog.getByRole("combobox", { name: "BOQ item", exact: true }).click();
    await page.getByRole("option", { name: /PIPE-CABLE/ }).click();
    await dialog.getByLabel("Quantity", { exact: true }).fill("20");
    await dialog.getByRole("button", { name: "Create draft", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await page.getByRole("button", { name: "Submit", exact: true }).click();
    await expect(page.getByRole("cell", { name: "Submitted", exact: true })).toBeVisible();
    await loginInBrowserAs(page, TEST_USERS.procurement);
    await page.goto(`${workspace}&tab=requests`);
    await page.getByRole("button", { name: "Approve", exact: true }).click();
    await dialog.getByRole("button", { name: "Approve", exact: true }).click();
    await expect(page.getByRole("cell", { name: "Approved", exact: true })).toBeVisible();
    await loginInBrowserAs(page, TEST_USERS.warehouse);
    await page.goto(`${workspace}&tab=warehouse`);
    await page.getByRole("button", { name: "Create receipt", exact: true }).click();
    // Existing receipt fields lack accessible label associations. Scope by the
    // displayed form and native role; this test does not bypass the form/API.
    await dialog.getByRole("combobox").nth(0).click();
    await page.getByRole("option", { name: /PIPE-CABLE/ }).click();
    await dialog.getByRole("spinbutton").fill("20");
    await dialog.getByRole("button", { name: "Create draft", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await page.locator('a[href*="/warehouse/receipt/"]:visible').click();
    await page.getByRole("button", { name: "Post", exact: true }).click();
    await expect(page.getByRole("button", { name: "Reverse transaction", exact: true })).toBeVisible();
    await page.getByRole("link", { name: "Back", exact: true }).click();
    await expect(page.getByRole("cell", { name: "Posted", exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Create issue", exact: true }).click();
    await dialog.getByRole("combobox", { name: "Responsible site user", exact: true }).click();
    await page.getByRole("option", { name: userNames.get("PM")!, exact: true }).click();
    await dialog.getByLabel("Work item code", { exact: true }).fill("FACTORY-ELECTRICAL");
    await dialog.getByRole("combobox", { name: "BOQ item", exact: true }).click();
    await page.getByRole("option", { name: /PIPE-CABLE/ }).click();
    await dialog.getByLabel("Quantity", { exact: true }).fill("20");
    await dialog.getByRole("button", { name: "Create draft", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await page.locator('a[href*="/warehouse/issue/"]:visible').click();
    await page.getByRole("button", { name: "Post", exact: true }).click();
    await expect(page.getByRole("button", { name: "Reverse transaction", exact: true })).toBeVisible();
    await page.getByRole("link", { name: "Back", exact: true }).click();
    await expect(page.getByRole("cell", { name: "Posted", exact: true })).toHaveCount(2);
    await page.screenshot({ path: testInfo.outputPath("03-received-and-issued.png"), fullPage: true });
  });

  await test.step("Accountant receives the signed vendor contract and prepares the invoice", async () => {
    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto("/admin/finance-control");
    await page.getByRole("button", { name: "Create request", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox", { name: "Downstream contract", exact: true }).click();
    // This is a business handoff assertion. Do not replace it with a direct API
    // payment or elevated-role session if the ordinary accountant cannot select it.
    await expect(page.getByRole("option", { name: new RegExp(contractNumber) })).toBeVisible();
    await page.getByRole("option", { name: new RegExp(contractNumber) }).click();
    await dialog.getByLabel("Supplier invoice number", { exact: true }).fill(`INV-PIPE-${suffix}`);
    await dialog.getByLabel("Amount", { exact: true }).fill("3600000");
    await dialog.getByRole("combobox", { name: "Assigned accountant", exact: true }).click();
    await page.getByRole("option", { name: userNames.get("ACCOUNTANT")!, exact: true }).click();
    await dialog.locator('input[maxlength="260"]').fill("supplier-invoice.pdf");
    await dialog.getByPlaceholder("/documents/invoice.pdf").fill("/documents/supplier-invoice.pdf");
    await dialog.getByRole("button", { name: "Create draft", exact: true }).click();
    await expect(dialog).not.toBeVisible();
  });
  const invoice = () => page.locator("article").filter({ hasText: `INV-PIPE-${suffix}` });
  await test.step("Accountant validates, BGD approves and Accountant records payment", async () => {
    await invoice().getByRole("button", { name: "Submit", exact: true }).click();
    await invoice().getByRole("button", { name: "Validate", exact: true }).click();
    await expect(invoice()).toContainText("Ready for approval");
    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto("/admin/finance-control");
    if (outcome === "Rejected") {
      await invoice().getByRole("button", { name: "Reject", exact: true }).click();
      const decision = page.getByRole("dialog");
      await decision.getByRole("button", { name: "Reject", exact: true }).click();
      await expect(decision.getByRole("alert")).toBeVisible();
      await expect(invoice()).toContainText("Ready for approval");
      await decision.getByLabel("Reason / note", { exact: true }).fill("Invoice scope requires supplier correction");
      await decision.getByRole("button", { name: "Reject", exact: true }).click();
      await expect(decision).not.toBeVisible();
      await expect(invoice()).toContainText("Rejected");
    } else {
    await invoice().getByRole("button", { name: "Approve", exact: true }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Approve", exact: true }).click();
    await expect(invoice()).toContainText("Approved");
      if (outcome === "Cancelled") {
        await invoice().getByRole("button", { name: "Cancel request", exact: true }).click();
        const decision = page.getByRole("dialog");
        await decision.getByRole("button", { name: "Cancel request", exact: true }).click();
        await expect(decision.getByRole("alert")).toBeVisible();
        await expect(invoice()).toContainText("Approved");
        await decision.getByLabel("Reason / note", { exact: true }).fill("Supplier invoice replaced before payment");
        await decision.getByRole("button", { name: "Cancel request", exact: true }).click();
        await expect(decision).not.toBeVisible();
        await expect(invoice()).toContainText("Cancelled");
      }
    }
    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto("/admin/finance-control");
    if (outcome !== "Paid") {
      await expect(invoice()).toContainText(outcome);
      await expect(invoice().getByRole("button", { name: "Mark paid", exact: true })).toHaveCount(0);
      await expect(invoice().getByRole("button", { name: "Submit", exact: true })).toHaveCount(0);
      await page.reload();
      await expect(invoice()).toContainText(outcome);
      await expect(invoice()).toContainText(contractNumber);
      await expect(invoice()).toContainText("3,600,000");
      await page.screenshot({ path: testInfo.outputPath(`04-invoice-${outcome}.png`), fullPage: true });
      return;
    }
    await invoice().getByRole("button", { name: "Mark paid", exact: true }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Confirm paid", exact: true }).click();
    await expect(invoice()).toContainText("Paid");
    await expect(invoice()).toContainText(contractNumber);
    await expect(invoice()).toContainText("3,600,000");
    await page.reload();
    await expect(invoice()).toContainText("Paid");
    await page.screenshot({ path: testInfo.outputPath("03-paid-invoice.png"), fullPage: true });
  });
  expect(errors).toEqual([]);
});
}
