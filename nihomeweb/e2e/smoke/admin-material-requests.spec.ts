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
  let createPayload: Record<string, unknown> | null = null;
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
    if (route.request().method() === "POST") {
      createPayload = route.request().postDataJSON() as Record<string, unknown>;
      return route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          ...request,
          id: 180103,
          code: "MR-0181",
          status: "Draft",
          siteRequesterUserId: 1,
          siteRequesterName: "Super Admin",
          responsibleSiteUserId: 1,
          responsibleSiteUserName: "Super Admin",
          requestedAt: null,
          createdAt: "2026-09-08T08:00:00Z",
          updatedAt: "2026-09-08T08:00:00Z",
          rowVersion: "create-row",
        }),
      });
    }
    listQueries.push(new URL(route.request().url()));
    if (listFails) return route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ message: "Material request list unavailable" }) });
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        total: 21,
        page: Number(new URL(route.request().url()).searchParams.get("page") ?? 1),
        pageSize: 20,
        items: [request],
        currentApprovedBoq: {
          revisionId: 180401,
          revisionNumber: 1,
          lines: [{
            id: request.lines[0].projectBoqLineId,
            itemCode: request.lines[0].itemCode,
            description: request.lines[0].description,
            unit: request.lines[0].unit,
            approvedQuantity: request.lines[0].boqApprovedQuantity,
            remainingQuantity: request.lines[0].boqRemainingQuantity,
          }],
        },
      }),
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
  await expect(requestCard.getByRole("link", { name: "View details" })).toHaveAttribute(
    "href",
    `/admin/procurement-control/projects/${projectId}/material-requests/${request.id}`,
  );
  await expect(requestCard.getByRole("button", { name: "Submit" })).toHaveCount(0);
  await page.getByRole("button", { name: "Create request" }).click();
  await page.getByText("BOQ item", { exact: true }).locator("..").getByRole("combobox").click();
  await expect(page.getByRole("option", { name: /MAT-CEMENT.*60 remaining/ })).toBeVisible();
  await page.getByRole("option", { name: /MAT-CEMENT.*60 remaining/ }).click();
  await page.locator("#request-site-user").click();
  await page.getByRole("option", { name: "Super Admin", exact: true }).click();
  await page.locator("#request-proc-user").click();
  const procurementOwnerOptions = page.getByRole("listbox");
  await expect(procurementOwnerOptions.getByRole("option", { name: "Super Admin", exact: true })).toHaveCount(0);
  await procurementOwnerOptions.getByRole("option", { name: "Procurement Owner", exact: true }).click();
  const requiredAt = page.locator("#request-required-at");
  expect(new Date(await requiredAt.inputValue()).getTime()).toBeGreaterThan(Date.now());
  const createQuantity = page.getByRole("dialog").getByRole("spinbutton");
  await createQuantity.fill("61");
  await page.getByRole("button", { name: "Create draft" }).click();
  await expect(page.getByRole("alert")).toContainText(/not exceed the remaining BOQ allowance/i);
  await expect(createQuantity).toHaveValue("61");
  await createQuantity.fill("30");
  await page.getByRole("button", { name: "Create draft" }).click();
  await expect.poll(() => createPayload).not.toBeNull();
  expect(createPayload).toMatchObject({
    responsibleSiteUserId: 1,
    assignedProcurementUserId: request.assignedProcurementUserId,
    lines: [{ projectBoqLineId: request.lines[0].projectBoqLineId, requestedQuantity: 30 }],
  });
  await expect(page.getByRole("dialog")).toBeHidden();
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

test("MR-only user creates from limited BOQ context without BOQ access", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  let boqEndpointRequests = 0;
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      role: "MR_ONLY",
      roleId: 901,
      permissions: ["proc.material-requests.view", "proc.material-requests.manage"],
    }),
  }));
  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ total: 1, page: 1, pageSize: 100, items: [{ id: projectId, code: "PJ-0180", name: "Foundation Project" }] }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/boq-revisions`), route => {
    boqEndpointRequests += 1;
    return route.fulfill({ status: 403, contentType: "application/json", body: JSON.stringify({ message: "Forbidden" }) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ operationalProjectId: projectId, canManage: false, roleDefinitions: [], moduleOptions: [], disciplineOptions: [], members: [], assignments: [] }),
  }));
  await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(/\/api\/(?:v1\/)?contracts(?:\?.*)?$/, route => route.fulfill({ status: 403, contentType: "application/json", body: "{}" }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests.*`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      total: 0,
      page: 1,
      pageSize: 20,
      items: [],
      currentApprovedBoq: {
        revisionId: 180401,
        revisionNumber: 1,
        lines: [{ id: 180301, itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", approvedQuantity: 100, remainingQuantity: 60 }],
      },
    }),
  }));

  await page.goto(`${appUrl}/admin/procurement-control`, { waitUntil: "networkidle" });
  await page.locator("#procurement-project").click();
  await page.getByRole("option", { name: "PJ-0180 · Foundation Project" }).click();

  await expect(page.getByRole("tab", { name: /Yêu cầu vật tư|Material requests|材料申请|資材依頼/i })).toHaveAttribute("data-state", "active");
  await expect(page.getByRole("tab", { name: /Phiên bản BOQ|BOQ revisions|BOQ 版本|BOQ改訂/i })).toHaveCount(0);
  await page.getByRole("button", { name: /Tạo yêu cầu|Create request|创建申请|依頼を作成/i }).click();
  await page.getByText(/Hạng mục BOQ|BOQ item|BOQ 项目|BOQ品目/i, { exact: true }).locator("..").getByRole("combobox").click();
  await expect(page.getByRole("option", { name: /MAT-CEMENT.*(?:Còn 60|60 remaining|剩余 60|残り 60)/i })).toBeVisible();
  expect(boqEndpointRequests).toBe(0);
});

test("material request detail edits safely and completes its workflow", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const requestId = 180102;
  let failUpdate = true;
  let detail = {
    ...request,
    id: requestId,
    code: "MR-DETAIL-0180",
    status: "Draft",
    siteRequesterUserId: 1,
    siteRequesterName: "Super Admin",
    requiredAt: "2026-09-20T08:00:00Z",
    submittedAt: null,
    submittedByUserId: null,
    submittedByName: null,
    approvedAt: null,
    approvedByUserId: null,
    approvedByName: null,
    rejectedAt: null,
    rejectedByUserId: null,
    rejectedByName: null,
    fulfilledAt: null,
    cancelledAt: null,
    createdAt: "2026-09-08T08:00:00Z",
    updatedAt: "2026-09-08T08:00:00Z",
    rowVersion: "detail-row-1",
    operationalProjectCode: "PJ-0180",
    operationalProjectName: "Foundation Project",
    customerId: 1,
    customerName: "NICON",
    contracts: [
      { id: 880001, contractNumber: "HD-CUSTOMER-0180", direction: "Upstream", type: "DesignAndBuild", vendorId: null, vendorName: null, status: "InProgress" },
      { id: 880002, contractNumber: "PO-SUPPLIER-0180", direction: "Downstream", type: "Supply", vendorId: 8, vendorName: "Supplier Eight", status: "Draft" },
    ],
    lines: [{ ...request.lines[0], id: 180202, requestedQuantity: 20, receivedQuantity: 0 }],
  };

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests/${requestId}$`), async route => {
    const method = route.request().method();
    if (method === "PUT") {
      if (failUpdate) {
        await route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ message: "Update unavailable" }) });
        return;
      }
      const payload = route.request().postDataJSON() as { requiredAt: string; note: string; lines: Array<{ requestedQuantity: number }> };
      expect(route.request().headers()["idempotency-key"]).toBeTruthy();
      expect(route.request().headers()["if-match"]).toContain(detail.rowVersion);
      detail = { ...detail, requiredAt: payload.requiredAt, note: payload.note, rowVersion: "detail-row-2", lines: [{ ...detail.lines[0], requestedQuantity: payload.lines[0].requestedQuantity }] };
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests/${requestId}/submit$`), async route => {
    detail = { ...detail, status: "Submitted", submittedAt: "2026-09-08T09:00:00Z", submittedByUserId: 1, submittedByName: "Super Admin", rowVersion: "detail-row-3" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests/${requestId}/decision$`), async route => {
    detail = { ...detail, status: "Approved", approvedAt: "2026-09-08T10:00:00Z", approvedByUserId: 1, approvedByName: "Super Admin", rowVersion: "detail-row-4" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests/${requestId}/cancel$`), async route => {
    const payload = route.request().postDataJSON() as { reason: string };
    detail = { ...detail, status: "Cancelled", cancelledAt: "2026-09-08T11:00:00Z", decisionReason: payload.reason, rowVersion: "detail-row-5" };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-requests(?:\\?.*)?$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ total: 1, page: 1, pageSize: 1, items: [detail], currentApprovedBoq: { revisionId: 180401, revisionNumber: 1, lines: [{ id: 180301, itemCode: "MAT-CEMENT", description: "Portland cement", unit: "kg", approvedQuantity: 100, remainingQuantity: 60 }] } }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ operationalProjectId: projectId, members: [], assignments: [] }),
  }));
  await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([{ userId: 13, userName: "Procurement Owner" }]) }));

  await page.goto(`${baseURL}/admin/procurement-control/projects/${projectId}/material-requests/${requestId}`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { level: 1, name: "MR-DETAIL-0180" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: /Ngữ cảnh yêu cầu vật tư|Material request context|材料申请上下文|資材依頼コンテキスト/i })).toContainText("NICON");
  await expect(page.getByRole("link", { name: "HD-CUSTOMER-0180" })).toBeVisible();
  await expect(page.getByRole("link", { name: "PO-SUPPLIER-0180" })).toBeVisible();

  await page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).click();
  const quantity = page.locator("#request-detail-quantity-0");
  await quantity.fill("61");
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
  await expect(page.getByRole("alert")).toContainText(/không vượt hạn mức|not exceed the remaining|不得超过|超えない/i);
  await expect(quantity).toHaveValue("61");

  await quantity.fill("30");
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
  await expect(page.getByRole("alert")).toContainText("Update unavailable");
  await expect(quantity).toHaveValue("30");
  failUpdate = false;
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
  await expect(page.getByRole("dialog")).toBeHidden();
  await expect(page.getByRole("cell", { name: "30", exact: true })).toBeVisible();

  await page.getByRole("button", { name: /Trình duyệt|Submit|提交|提出/i }).click();
  await expect(page.getByText(/Đã trình|Submitted|已提交|提出済み/i).first()).toBeVisible();
  await page.getByRole("button", { name: /Phê duyệt|Approve|批准|承認/i }).click();
  await page.getByRole("dialog").getByRole("button", { name: /Phê duyệt|Approve|批准|承認/i }).click();
  await expect(page.getByText(/Đã duyệt|Approved|已批准|承認済み/i).first()).toBeVisible();

  await page.getByRole("button", { name: /Hủy yêu cầu|Cancel request|取消申请|依頼をキャンセル/i }).click();
  await page.locator("#material-request-reason").fill("No");
  await page.getByRole("dialog").getByRole("button", { name: /Hủy yêu cầu|Cancel request|取消申请|依頼をキャンセル/i }).click();
  await expect(page.getByRole("alert")).toContainText(/ít nhất 3|at least 3|至少需要 3|3文字以上/i);
  await page.locator("#material-request-reason").fill("Scope changed");
  await page.getByRole("dialog").getByRole("button", { name: /Hủy yêu cầu|Cancel request|取消申请|依頼をキャンセル/i }).click();
  await expect(page.getByText(/Đã huỷ|Đã hủy|Cancelled|已取消|キャンセル済み/i).first()).toBeVisible();

  detail = { ...detail, status: "Draft", cancelledAt: null, decisionReason: null };
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ role: "MR_VIEWER", roleId: null, permissions: ["proc.material-requests.view"] }),
  }));
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /Trình duyệt|Submit|提交|提出/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /Hủy yêu cầu|Cancel request|取消申请|依頼をキャンセル/i })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "HD-CUSTOMER-0180" })).toHaveCount(0);
  await expect(page.getByText("HD-CUSTOMER-0180", { exact: true })).toBeVisible();

  for (const viewport of [{ width: 768, height: 1024 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.reload({ waitUntil: "networkidle" });
    await expect(page.locator('[data-testid^="material-request-line-card-"]').first()).toBeVisible();
    await expect(page.getByRole("table")).toBeHidden();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
});
