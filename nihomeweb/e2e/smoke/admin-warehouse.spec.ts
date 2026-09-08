import { expect, test, TEST_USERS } from "../fixtures/auth";
import type { Page } from "@playwright/test";

const projectId = 169001;
const boqLineId = 169101;
const requestLineId = 169201;
const receiptId = 169301;
const issueId = 169302;

const stock = [{
  projectBoqLineId: boqLineId,
  itemCode: "MAT-CEMENT",
  description: "Portland cement",
  unit: "kg",
  boqApprovedQuantity: 100,
  receivedQuantity: 50,
  issuedQuantity: 20,
  onHandQuantity: 30,
}];

const receiptListItem = {
  id: receiptId,
  operationalProjectId: projectId,
  type: "Receipt",
  code: "WR-0001",
  status: "Draft",
  occurredAt: "2026-09-08T08:00:00Z",
  postedAt: null,
  actorUserId: 1,
  actorName: "Super Admin",
  responsibleUserId: null,
  responsibleUserName: null,
  workItemCode: null,
  reversalOfId: null,
  reversalReason: null,
  lineCount: 1,
  totalQuantity: 25,
  itemCodes: ["MAT-CEMENT"],
  createdAt: "2026-09-08T08:00:00Z",
  updatedAt: "2026-09-08T08:00:00Z",
  rowVersion: "receipt-list-row",
};

const issueListItem = {
  ...receiptListItem,
  id: issueId,
  type: "Issue",
  code: "WI-0001",
  status: "Posted",
  occurredAt: "2026-09-08T09:00:00Z",
  postedAt: "2026-09-08T09:05:00Z",
  responsibleUserId: 1,
  responsibleUserName: "Super Admin",
  workItemCode: "FOUNDATION-A",
  totalQuantity: 20,
  rowVersion: "issue-list-row",
};

const project = { id: projectId, code: "PJ-0169", name: "Warehouse Project" };
const permissions = ["crm.contracts.view", "proc.boq.view", "proc.material-requests.view", "proc.warehouse.view", "proc.warehouse.post"];

const mockProjectContext = async (page: Page) => {
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ role: "WAREHOUSE", roleId: 169, permissions }),
  }));
  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ total: 1, page: 1, pageSize: 100, items: [project] }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ operationalProjectId: projectId, members: [{ id: 1, userId: 1, userName: "Super Admin", position: "WAREHOUSE", isActive: true, roles: [], rowVersion: "team-row" }], assignments: [] }),
  }));
  await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(/\/api\/(?:v1\/)?contracts(?:\?.*)?$/, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }) }));
};

test("warehouse list filters, exports, validates creates, and stays responsive", async ({ page, loginInBrowserAs, baseURL }) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  const listQueries: URL[] = [];
  let receiptPayload: Record<string, unknown> | null = null;
  let issuePayload: Record<string, unknown> | null = null;
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
  await mockProjectContext(page);

  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      boqRevisions: [{ id: 169401, operationalProjectId: projectId, revisionNumber: 1, currency: "VND", status: "Approved", costTotal: 1000, preparedByUserId: 1, isFinal: true, createdAt: "2026-09-01T08:00:00Z", updatedAt: "2026-09-01T08:00:00Z", rowVersion: "boq-row", lines: [{ id: boqLineId, itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", approvedQuantity: 100, budgetUnitPrice: 10, amount: 1000 }] }],
      materialRequests: [{ id: 169501, operationalProjectId: projectId, code: "MR-0001", status: "PartiallyFulfilled", siteRequesterUserId: 1, responsibleSiteUserId: 1, assignedProcurementUserId: 1, requiredAt: "2026-09-10T08:00:00Z", createdAt: "2026-09-01T08:00:00Z", updatedAt: "2026-09-01T08:00:00Z", rowVersion: "mr-row", lines: [{ id: requestLineId, projectBoqLineId: boqLineId, itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", requestedQuantity: 50, receivedQuantity: 10, boqApprovedQuantity: 100, boqRemainingQuantity: 50 }] }],
      contractLines: [], receipts: [], issues: [], vendorRatings: [],
    }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/boq-revisions(?:\\?.*)?$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 20, items: [] }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests(?:\\?.*)?$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 20, items: [], currentApprovedBoq: { revisionId: 169401, revisionNumber: 1, lines: [{ id: boqLineId, itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", approvedQuantity: 100, remainingQuantity: 50 }] } }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/warehouse-transactions(?:\\?.*)?$`), route => {
    listQueries.push(new URL(route.request().url()));
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 2, page: Number(new URL(route.request().url()).searchParams.get("page") ?? 1), pageSize: 20, items: [issueListItem, receiptListItem], stock }) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/receipts$`), route => {
    receiptPayload = route.request().postDataJSON() as Record<string, unknown>;
    return route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify({ ...receiptListItem, operationalProjectId: projectId, receivedByUserId: 1, inspectedAt: "2026-09-08T08:00:00Z", lines: [] }) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/issues$`), route => {
    issuePayload = route.request().postDataJSON() as Record<string, unknown>;
    return route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify({ ...issueListItem, operationalProjectId: projectId, issuedByUserId: 1, responsibleSiteUserId: 1, issuedAt: "2026-09-08T09:00:00Z", lines: [] }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${appUrl}/admin/procurement-control?projectId=${projectId}&tab=warehouse`, { waitUntil: "networkidle" });
  await expect(page.getByRole("tab", { selected: true })).toContainText(/Warehouse receipts \/ issues/i);
  await expect(page.getByRole("heading", { name: "Site stock" })).toBeVisible();
  await expect(page.locator("article").filter({ hasText: "MAT-CEMENT" }).getByText("30", { exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "WI-0001" })).toBeVisible();
  await expect(page.getByRole("link", { name: "View details" }).first()).toHaveAttribute("href", `/admin/procurement-control/projects/${projectId}/warehouse/issue/${issueId}`);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

  await page.getByRole("textbox", { name: "Search warehouse transactions" }).fill("cement");
  await page.getByRole("combobox", { name: "Transaction type" }).click();
  await page.getByRole("option", { name: "Receipt", exact: true }).click();
  await page.getByRole("combobox", { name: "Status" }).click();
  await page.getByRole("option", { name: "Draft", exact: true }).click();
  await page.getByLabel("Occurred from").fill("2026-09-01");
  await page.getByLabel("Occurred to").fill("2026-09-30");
  await expect.poll(() => Object.fromEntries(listQueries.at(-1)?.searchParams ?? [])).toMatchObject({ search: "cement", type: "receipt", status: "Draft", occurredFrom: "2026-09-01", occurredTo: "2026-09-30" });

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export CSV" }).click();
  expect((await downloadPromise).suggestedFilename()).toContain(`warehouse-transactions-project-${projectId}`);

  await page.getByRole("button", { name: "Create receipt" }).click();
  const receiptDialog = page.getByRole("dialog");
  await receiptDialog.getByText("Material request line", { exact: true }).locator("..").getByRole("combobox").click();
  await page.getByRole("option", { name: /MR-0001.*MAT-CEMENT/ }).click();
  const receiptQuantity = receiptDialog.getByRole("spinbutton");
  await receiptQuantity.fill("41");
  await receiptDialog.getByRole("button", { name: "Create draft" }).click();
  await expect(receiptDialog.getByRole("alert")).toBeVisible();
  await expect(receiptQuantity).toHaveValue("41");
  await receiptQuantity.fill("30");
  await receiptDialog.getByRole("button", { name: "Create draft" }).click();
  await expect.poll(() => receiptPayload).not.toBeNull();
  expect(receiptPayload).toMatchObject({ receivedByUserId: 1, lines: [{ materialRequestLineId: requestLineId, receivedQuantity: 30 }] });

  await page.getByRole("button", { name: "Create issue" }).click();
  const issueDialog = page.getByRole("dialog");
  await issueDialog.locator("#issue-site-user").click();
  await page.getByRole("option", { name: "Super Admin", exact: true }).click();
  await issueDialog.getByText("BOQ item", { exact: true }).locator("..").getByRole("combobox").click();
  await page.getByRole("option", { name: /MAT-CEMENT/ }).click();
  const issueQuantity = issueDialog.getByRole("spinbutton");
  await issueQuantity.fill("31");
  await issueDialog.getByRole("button", { name: "Create draft" }).click();
  await expect(issueDialog.getByRole("alert")).toBeVisible();
  await issueQuantity.fill("20");
  await issueDialog.getByRole("button", { name: "Create draft" }).click();
  await expect.poll(() => issuePayload).not.toBeNull();
  expect(issuePayload).toMatchObject({ responsibleSiteUserId: 1, issuedByUserId: 1, lines: [{ projectBoqLineId: boqLineId, issuedQuantity: 20 }] });
  expect(errors).toEqual([]);
});

test("warehouse receipt detail edits, posts, reverses, and preserves context", async ({ page, loginInBrowserAs, baseURL }) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  let failUpdate = true;
  let detail = {
    id: receiptId,
    operationalProjectId: projectId,
    code: "WR-0001",
    status: "Draft",
    reversalOfReceiptId: null,
    reversalTransactionId: null as number | null,
    receivedByUserId: 1,
    receivedByName: "Super Admin",
    inspectedAt: "2026-09-08T08:00:00Z",
    postedAt: null as string | null,
    postedByUserId: null as number | null,
    postedByName: null as string | null,
    reversalReason: null as string | null,
    createdAt: "2026-09-08T08:00:00Z",
    updatedAt: "2026-09-08T08:00:00Z",
    rowVersion: "receipt-row-1",
    operationalProjectCode: "PJ-0169",
    operationalProjectName: "Warehouse Project",
    customerId: 1,
    customerName: "NICON",
    contracts: [{ id: 169601, contractNumber: "PO-0169", direction: "Downstream", type: "Supply", vendorId: 9, vendorName: "Cement Supplier", status: "InProgress" }],
    lines: [{ id: 169701, materialRequestLineId: requestLineId, contractLineId: null, materialRequestCode: "MR-0001", itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", requestedQuantity: 50, netReceivedQuantity: 25, contractNumber: null, vendorName: null, receivedQuantity: 25 }],
  };

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
  await mockProjectContext(page);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/warehouse-transactions(?:\\?.*)?$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 1, items: [receiptListItem], stock }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/receipts/${receiptId}$`), async route => {
    if (route.request().method() === "PUT") {
      if (failUpdate) return route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ message: "Update unavailable" }) });
      const payload = route.request().postDataJSON() as { lines: Array<{ receivedQuantity: number }> };
      expect(route.request().headers()["idempotency-key"]).toBeTruthy();
      expect(route.request().headers()["if-match"]).toContain(detail.rowVersion);
      detail = { ...detail, rowVersion: "receipt-row-2", lines: [{ ...detail.lines[0], receivedQuantity: payload.lines[0].receivedQuantity }] };
    }
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/receipts/${receiptId}/post$`), async route => {
    detail = { ...detail, status: "Posted", postedAt: "2026-09-08T09:00:00Z", postedByUserId: 1, postedByName: "Super Admin", rowVersion: "receipt-row-3" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/receipts/${receiptId}/reverse$`), async route => {
    const payload = route.request().postDataJSON() as { reason: string };
    detail = { ...detail, status: "Reversed", reversalTransactionId: 169399, reversalReason: payload.reason, updatedAt: "2026-09-08T10:00:00Z", rowVersion: "receipt-row-4" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ...detail, id: 169399, code: "WR-0002", reversalOfReceiptId: receiptId, status: "Posted" }) });
  });

  await page.goto(`${appUrl}/admin/procurement-control/projects/${projectId}/warehouse/receipt/${receiptId}`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { level: 1, name: "WR-0001" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Warehouse transaction context" })).toContainText("NICON");
  await expect(page.getByRole("link", { name: "PO-0169" })).toBeVisible();

  await page.getByRole("button", { name: "Edit" }).click();
  const quantity = page.locator("#warehouse-receipt-quantity-0");
  await quantity.fill("30");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByRole("alert")).toContainText("Update unavailable");
  await expect(quantity).toHaveValue("30");
  failUpdate = false;
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(page.getByRole("cell", { name: "30", exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Post" }).click();
  await expect(page.getByText("Posted", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit" })).toHaveCount(0);
  await page.getByRole("button", { name: "Reverse transaction" }).click();
  const reason = page.locator("#warehouse-reversal-reason");
  await reason.fill("No");
  await page.getByRole("dialog").getByRole("button", { name: "Reverse transaction" }).click();
  await expect(page.getByRole("alert")).toContainText(/at least 3/i);
  await expect(reason).toHaveValue("No");
  await reason.fill("Receipt entered in error");
  await page.getByRole("dialog").getByRole("button", { name: "Reverse transaction" }).click();
  await expect(page.getByText("Reversed", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Reverse transaction" })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "View reversal transaction" })).toHaveAttribute("href", `/admin/procurement-control/projects/${projectId}/warehouse/receipt/169399`);

  for (const viewport of [{ width: 768, height: 1024 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.reload({ waitUntil: "networkidle" });
    await expect(page.locator("article").filter({ hasText: "MAT-CEMENT" })).toBeVisible();
    await expect(page.getByRole("table")).toBeHidden();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
  await expect(page.getByRole("link", { name: "Back" })).toHaveAttribute("href", `/admin/procurement-control?projectId=${projectId}&tab=warehouse`);
});

test("warehouse issue detail preserves allocation and enforces stock and permissions", async ({ page, loginInBrowserAs, baseURL }) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  let failUpdate = true;
  let detail = {
    id: issueId,
    operationalProjectId: projectId,
    code: "WI-0001",
    status: "Draft",
    reversalOfIssueId: null,
    reversalTransactionId: null as number | null,
    responsibleSiteUserId: 1,
    responsibleSiteUserName: "Super Admin",
    issuedByUserId: 1,
    issuedByName: "Super Admin",
    issuedAt: "2026-09-08T08:00:00Z",
    postedAt: null as string | null,
    postedByUserId: null as number | null,
    postedByName: null as string | null,
    workItemCode: "FOUNDATION-A",
    reversalReason: null as string | null,
    createdAt: "2026-09-08T08:00:00Z",
    updatedAt: "2026-09-08T08:00:00Z",
    rowVersion: "issue-row-1",
    operationalProjectCode: "PJ-0169",
    operationalProjectName: "Warehouse Project",
    customerId: 1,
    customerName: "NICON",
    contracts: [],
    lines: [{ id: 169702, projectBoqLineId: boqLineId, itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", boqApprovedQuantity: 100, stockOnHand: 30, issuedQuantity: 20 }],
  };

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
  await mockProjectContext(page);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/warehouse-transactions(?:\\?.*)?$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 1, items: [issueListItem], stock }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/issues/${issueId}$`), async route => {
    if (route.request().method() === "PUT") {
      if (failUpdate) return route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ message: "Issue update unavailable" }) });
      const payload = route.request().postDataJSON() as { responsibleSiteUserId: number; workItemCode: string; lines: Array<{ issuedQuantity: number }> };
      expect(route.request().headers()["idempotency-key"]).toBeTruthy();
      expect(route.request().headers()["if-match"]).toContain(detail.rowVersion);
      detail = { ...detail, responsibleSiteUserId: payload.responsibleSiteUserId, workItemCode: payload.workItemCode, rowVersion: "issue-row-2", lines: [{ ...detail.lines[0], issuedQuantity: payload.lines[0].issuedQuantity }] };
    }
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/issues/${issueId}/post$`), async route => {
    detail = { ...detail, status: "Posted", postedAt: "2026-09-08T09:00:00Z", postedByUserId: 1, postedByName: "Super Admin", rowVersion: "issue-row-3" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/issues/${issueId}/reverse$`), async route => {
    const payload = route.request().postDataJSON() as { reason: string };
    detail = { ...detail, status: "Reversed", reversalTransactionId: 169398, reversalReason: payload.reason, updatedAt: "2026-09-08T10:00:00Z", rowVersion: "issue-row-4" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ...detail, id: 169398, code: "WI-0002", status: "Posted", reversalOfIssueId: issueId }) });
  });

  await page.goto(`${appUrl}/admin/procurement-control/projects/${projectId}/warehouse/issue/${issueId}`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { level: 1, name: "WI-0001" })).toBeVisible();
  await expect(page.getByText("FOUNDATION-A", { exact: true }).first()).toBeVisible();
  await page.getByRole("button", { name: "Edit" }).click();
  const quantity = page.locator("#warehouse-issue-quantity-0");
  await quantity.fill("31");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByRole("alert")).toContainText(/on-hand stock/i);
  await expect(quantity).toHaveValue("31");
  await quantity.fill("18");
  await page.locator("#warehouse-work-item").fill("FOUNDATION-B");
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByRole("alert")).toContainText("Issue update unavailable");
  await expect(quantity).toHaveValue("18");
  await expect(page.locator("#warehouse-work-item")).toHaveValue("FOUNDATION-B");
  failUpdate = false;
  await page.getByRole("button", { name: "Save" }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(page.getByText("FOUNDATION-B", { exact: true }).first()).toBeVisible();

  await page.getByRole("button", { name: "Post" }).click();
  await expect(page.getByText("Posted", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit" })).toHaveCount(0);
  await page.getByRole("button", { name: "Reverse transaction" }).click();
  await page.locator("#warehouse-reversal-reason").fill("Wrong receiving crew");
  await page.getByRole("dialog").getByRole("button", { name: "Reverse transaction" }).click();
  await expect(page.getByText("Reversed", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("link", { name: "View reversal transaction" })).toHaveAttribute("href", `/admin/procurement-control/projects/${projectId}/warehouse/issue/169398`);

  detail = { ...detail, status: "Draft", reversalTransactionId: null, reversalReason: null };
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ role: "WAREHOUSE_VIEWER", roleId: 170, permissions: ["proc.warehouse.view"] }),
  }));
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("button", { name: "Edit" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Post" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Reverse transaction" })).toHaveCount(0);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.locator("article").filter({ hasText: "MAT-CEMENT" })).toBeVisible();
  await expect(page.getByRole("table")).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});
