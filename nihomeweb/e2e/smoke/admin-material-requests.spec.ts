import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 180001;
const request = {
  id: 180101,
  operationalProjectId: projectId,
  code: "MR-0180",
  status: "Submitted",
  siteRequesterUserId: 11,
  siteRequesterName: "Site Requester",
  responsibleSiteUserId: 12,
  responsibleSiteUserName: "Site Engineer",
  assignedProcurementUserId: 13,
  assignedProcurementUserName: "Procurement Owner",
  requiredAt: "2026-09-15T08:00:00Z",
  note: "Cement for foundation",
  submittedAt: "2026-09-07T08:00:00Z",
  approvedAt: null,
  fulfilledAt: null,
  decisionReason: null,
  rowVersion: "AAAAAAAAB9M=",
  lines: [{
    id: 180201,
    projectBoqLineId: 180301,
    itemCode: "MAT-CEMENT",
    description: "Portland cement",
    unit: "kg",
    requestedQuantity: 40,
    receivedQuantity: 15,
    boqApprovedQuantity: 100,
    boqRemainingQuantity: 60,
  }],
};

test("material request list filters and exports within the selected project on mobile", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  const listQueries: URL[] = [];
  let workspaceRequests = 0;
  let listFails = false;
  const errors: string[] = [];
  const warnings: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    const text = message.text();
    if (message.type() === "error" && !text.includes("WebSocket") && !text.includes("status of 404")) errors.push(text);
    if (message.type() === "warning" && text.includes("uncontrolled to controlled")) warnings.push(text);
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));

  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, route => {
    const requestedPage = Number(new URL(route.request().url()).searchParams.get("page") ?? 1);
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        total: 101,
        page: requestedPage,
        pageSize: 100,
        items: requestedPage === 1
          ? [{ id: projectId, code: "PJ-0180", name: "Foundation Project" }]
          : [{ id: 180002, code: "PJ-0181", name: "Second Page Project" }],
      }),
    });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement$`), route => {
    workspaceRequests += 1;
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
      boqRevisions: [{
        id: 180401,
        operationalProjectId: projectId,
        revisionNumber: 1,
        currency: "VND",
        status: "Approved",
        costTotal: 1000,
        preparedByUserId: 11,
        preparedByName: "Site Requester",
        isFinal: true,
        createdAt: "2026-09-01T08:00:00Z",
        rowVersion: "AAAAAAAAB9M=",
        lines: request.lines.map(line => ({
          id: line.projectBoqLineId,
          itemCode: line.itemCode,
          description: line.description,
          unit: line.unit,
          approvedQuantity: line.boqApprovedQuantity,
          budgetUnitPrice: 10,
          amount: 1000,
        })),
      }],
      materialRequests: [],
      contractLines: [],
      receipts: [],
      issues: [],
      vendorRatings: [],
      }),
    });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      operationalProjectId: projectId,
      canManage: false,
      roleDefinitions: [],
      moduleOptions: [],
      disciplineOptions: [],
      members: [{
        id: 180501,
        userId: request.assignedProcurementUserId,
        userName: request.assignedProcurementUserName,
        email: "procurement@example.com",
        position: "Procurement",
        startedAt: "2026-09-01T08:00:00Z",
        isActive: true,
        source: "Manual",
        roles: [],
        rowVersion: "AAAAAAAAB9M=",
      }],
      assignments: [],
    }),
  }));
  await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify([{
      userId: request.assignedProcurementUserId,
      userName: request.assignedProcurementUserName,
    }]),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests.*`), route => {
    listQueries.push(new URL(route.request().url()));
    if (listFails) return route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ message: "Material request list unavailable" }) });
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 21, page: Number(new URL(route.request().url()).searchParams.get("page") ?? 1), pageSize: 20, items: [request] }),
    });
  });

  await page.goto(`${appUrl}/admin/procurement-control`, { waitUntil: "networkidle" });
  await page.getByRole("combobox").first().click();
  await expect(page.getByRole("option", { name: "PJ-0181 · Second Page Project" })).toBeVisible();
  await page.getByRole("option", { name: "PJ-0180 · Foundation Project" }).click();
  await page.getByRole("tab", { name: /Material requests/ }).click();

  expect(errors).toEqual([]);
  expect(warnings).toEqual([]);
  await expect(page.getByTestId("material-request-list-toolbar")).toBeVisible();
  await expect(page.getByText("Required from", { exact: true })).toBeVisible();
  await expect(page.getByText("Required to", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Toggle required-date order" })).toContainText("Required at");
  const requestCard = page.locator("article").filter({ hasText: "MR-0180" });
  await expect(requestCard.getByRole("heading", { name: "MR-0180" })).toBeVisible();
  await expect(requestCard.getByText(/Received quantity: 15.*BOQ remaining: 60/)).toBeVisible();
  await page.getByPlaceholder("Search code, note, user, or BOQ item...").fill("cement");
  await page.getByRole("combobox", { name: "Status" }).click();
  await page.getByRole("option", { name: "Submitted" }).click();
  const ownerFilter = page.getByRole("combobox", { name: "Procurement owner" });
  await ownerFilter.click();
  await page.getByRole("listbox").getByRole("option", { name: "Procurement Owner", exact: true }).click();
  await expect(ownerFilter).toHaveText("Procurement Owner");
  await expect.poll(() => listQueries.at(-1)?.searchParams.get("assignedProcurementUserId")).toBe(String(request.assignedProcurementUserId));
  await page.getByLabel("Required from").fill("2026-09-10");
  await page.getByLabel("Required to").fill("2026-09-20");
  await expect.poll(() => Object.fromEntries(listQueries.at(-1)?.searchParams ?? [])).toMatchObject({
    search: "cement",
    status: "Submitted",
    assignedProcurementUserId: String(request.assignedProcurementUserId),
    requiredFrom: "2026-09-10",
    requiredTo: "2026-09-20",
  });

  await page.getByRole("button", { name: "Next" }).click();
  await expect.poll(() => listQueries.at(-1)?.searchParams.get("page")).toBe("2");

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: /Export/i }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toContain(`material-requests-project-${projectId}`);

  const requestsBeforeRefresh = listQueries.length;
  const workspaceRequestsBeforeRefresh = workspaceRequests;
  await page.getByRole("button", { name: "Refresh data" }).click();
  await expect.poll(() => listQueries.length).toBeGreaterThan(requestsBeforeRefresh);
  await expect.poll(() => workspaceRequests).toBeGreaterThan(workspaceRequestsBeforeRefresh);
  expect(Object.fromEntries(listQueries.at(-1)?.searchParams ?? [])).toMatchObject({
    search: "cement",
    status: "Submitted",
    assignedProcurementUserId: String(request.assignedProcurementUserId),
    requiredFrom: "2026-09-10",
    requiredTo: "2026-09-20",
    page: "2",
  });

  listFails = true;
  await page.getByRole("button", { name: "Refresh data" }).click();
  await expect(page.getByRole("button", { name: "Retry" })).toBeVisible();
  listFails = false;
  await page.getByRole("button", { name: "Retry" }).click();
  await expect(requestCard.getByRole("heading", { name: "MR-0180" })).toBeVisible();

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  expect(overflow).toBe(false);
  await page.setViewportSize({ width: 768, height: 1024 });
  const tabletOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  expect(tabletOverflow).toBe(false);
  await expect(page.getByText("Required from", { exact: true })).toBeVisible();
  await expect(page.getByText("Required to", { exact: true })).toBeVisible();
});
