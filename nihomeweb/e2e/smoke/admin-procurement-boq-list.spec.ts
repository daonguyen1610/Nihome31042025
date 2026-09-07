import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 710001;

const revisions = Array.from({ length: 12 }, (_, index) => {
  const revisionNumber = index + 1;
  return {
    id: 720000 + revisionNumber,
    operationalProjectId: projectId,
    revisionNumber,
    currency: "VND",
    status: revisionNumber % 2 === 0 ? "Approved" : "Draft",
    sourceTenderEstimateRevisionId: null,
    sourceContractAppendixId: null,
    costTotal: revisionNumber * 1_000_000,
    preparedByUserId: 1,
    preparedByName: `Preparer ${revisionNumber}`,
    submittedAt: null,
    approvedAt: revisionNumber % 2 === 0 ? "2026-09-01T00:00:00Z" : null,
    rejectedAt: null,
    decisionReason: null,
    isFinal: revisionNumber === 12,
    createdAt: `2026-08-${String(revisionNumber).padStart(2, "0")}T00:00:00Z`,
    updatedAt: `2026-09-${String(revisionNumber).padStart(2, "0")}T00:00:00Z`,
    rowVersion: `row-${revisionNumber}`,
    lines: [{
      id: 730000 + revisionNumber,
      itemCode: `MAT-${revisionNumber}`,
      description: `Material ${revisionNumber}`,
      unit: "m2",
      approvedQuantity: revisionNumber,
      budgetUnitPrice: 1_000_000,
      amount: revisionNumber * 1_000_000,
    }],
  };
});

const project = {
  id: projectId,
  code: "PJ-BOQ-001",
  name: "BOQ Browser Project",
  customerId: 1,
  customerName: "NICON",
  projectManagerUserId: 1,
  projectManagerName: "Project Manager",
  status: "Active",
  startDate: null,
  endDate: null,
  opportunityCount: 0,
  quoteCount: 0,
  contractCount: 0,
  updatedAt: "2026-09-01T00:00:00Z",
};

const projectDetail = {
  ...project,
  note: null,
  designProjectId: null,
  designProjectCode: null,
  rowVersion: "project-row-version",
  createdAt: "2026-08-01T00:00:00Z",
  contractSummary: {
    activeContractCount: 2,
    upstreamContractCount: 1,
    upstreamCurrentValue: 20_000_000,
    downstreamContractCount: 1,
    downstreamCurrentValue: 8_000_000,
    scheduledPaymentAmount: 0,
    paidPaymentAmount: 0,
    paymentProgressPercent: 0,
  },
  opportunities: [],
  quotes: [],
  contracts: [
    { id: 740001, contractNumber: "HD-CUSTOMER-001", direction: "Upstream", type: "DesignAndBuild", vendorId: null, vendorCode: null, vendorName: null, status: "InProgress", value: 20_000_000, approvedVoTotal: 0, currentValue: 20_000_000, paymentMilestoneCount: 0, paidMilestoneCount: 0, scheduledPaymentAmount: 0, paidPaymentAmount: 0, paymentProgressPercent: 0, signedDate: null, startDate: null, endDate: null },
    { id: 740002, contractNumber: "PO-SUPPLIER-002", direction: "Downstream", type: "Supply", vendorId: 8, vendorCode: "V-008", vendorName: "Supplier Eight", status: "Draft", value: 8_000_000, approvedVoTotal: 0, currentValue: 8_000_000, paymentMilestoneCount: 0, paidMilestoneCount: 0, scheduledPaymentAmount: 0, paidPaymentAmount: 0, paymentProgressPercent: 0, signedDate: null, startDate: null, endDate: null },
  ],
};

test("BOQ list filters, sorts, paginates, and exports within the selected project", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(/\/api\/(?:v1\/)?operational-projects(?:\?.*)?$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ total: 1, page: 1, pageSize: 200, items: [project] }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ operationalProjectId: projectId, members: [], assignments: [] }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ boqRevisions: revisions, materialRequests: [], contractLines: [], receipts: [], issues: [], vendorRatings: [] }),
  }));
  await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(/\/api\/(?:v1\/)?contracts(?:\?.*)?$/, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/boq-revisions/export(?:\\?.*)?$`), route => route.fulfill({
    status: 200,
    contentType: "text/csv; charset=utf-8",
    headers: { "Content-Disposition": `attachment; filename="project-${projectId}-boq.csv"` },
    body: "Revision,Status,ItemCodes\r\n12,Approved,MAT-12\r\n",
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/boq-revisions(?:\\?.*)?$`), route => {
    const url = new URL(route.request().url());
    const search = (url.searchParams.get("search") ?? "").toLowerCase();
    const status = url.searchParams.get("status");
    const sortDirection = url.searchParams.get("sortDirection") ?? "desc";
    const pageNumber = Number(url.searchParams.get("page") ?? 1);
    const pageSize = Number(url.searchParams.get("pageSize") ?? 10);
    let filtered = revisions.filter(item =>
      (!status || item.status === status) &&
      (!search || `R${item.revisionNumber} ${item.preparedByName} ${item.lines[0].itemCode} ${item.lines[0].description}`.toLowerCase().includes(search)),
    );
    filtered = [...filtered].sort((left, right) => sortDirection === "asc"
      ? left.revisionNumber - right.revisionNumber
      : right.revisionNumber - left.revisionNumber);
    const items = filtered.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map(item => ({
      ...item,
      lineCount: item.lines.length,
      itemCodes: item.lines.map(line => line.itemCode),
      lines: undefined,
    }));
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: filtered.length, page: pageNumber, pageSize, items }),
    });
  });

  await page.goto(`${baseURL}/admin/procurement-control`, { waitUntil: "networkidle" });
  await page.locator("#procurement-project").click();
  await page.getByRole("option", { name: /PJ-BOQ-001/ }).click();

  const table = page.getByRole("table");
  await expect(page.locator("#boq-search")).toBeVisible();
  await expect(page.getByText(/Trang 1\/2|Page 1\/2|第 1\/2 页|1\/2 ページ/i)).toBeVisible();
  await expect(table.getByRole("cell", { name: /^R12(?:\s|$)/ })).toBeVisible();
  await expect(table.getByText("R1", { exact: true })).toHaveCount(0);

  await page.getByRole("button", { name: /Sau|Next|下一页|次へ/i }).click();
  await expect(table.getByText("R1", { exact: true })).toBeVisible();

  await page.locator("#boq-search").fill("MAT-12");
  await expect(table.getByRole("cell", { name: /^R12(?:\s|$)/ })).toBeVisible();
  await expect(table.getByRole("cell", { name: "R11", exact: true })).toHaveCount(0);

  await page.locator("#boq-search").fill("");
  await page.locator("#boq-status").click();
  await page.getByRole("option", { name: /Đã duyệt|Approved|已批准|承認済/i }).click();
  await expect(table.locator("tbody tr")).toHaveCount(6);
  await expect(table.getByText(/Nháp|Draft|草稿|下書き/i)).toHaveCount(0);

  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: /Xuất CSV|Export CSV|导出 CSV|CSVエクスポート/i }).click();
  await expect((await download).suggestedFilename()).toContain("project-");
});

test("BOQ detail keeps project context and safely edits a draft", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  let currentRevision = { ...revisions[0], updatedAt: "2026-09-01T08:00:00Z" };
  let updatePayload: Record<string, unknown> | null = null;

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(projectDetail),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/boq-revisions/${currentRevision.id}$`), async route => {
    if (route.request().method() === "PUT") {
      updatePayload = route.request().postDataJSON() as Record<string, unknown>;
      expect(route.request().headers()["idempotency-key"]).toBeTruthy();
      expect(route.request().headers()["if-match"]).toContain(currentRevision.rowVersion);
      const lines = (updatePayload.lines as typeof currentRevision.lines).map((line, index) => ({
        ...line,
        id: currentRevision.lines[index]?.id ?? 739000 + index,
        amount: line.approvedQuantity * line.budgetUnitPrice,
      }));
      currentRevision = { ...currentRevision, currency: String(updatePayload.currency), lines, costTotal: lines.reduce((sum, line) => sum + line.amount, 0), rowVersion: "row-updated" };
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(currentRevision) });
  });

  await page.goto(`${baseURL}/admin/procurement-control/projects/${projectId}/boq/${currentRevision.id}`, { waitUntil: "networkidle" });

  await expect(page.getByRole("heading", { name: /Chi tiết BOQ R1|BOQ R1 details|BOQ R1 详情|BOQ R1 詳細/i })).toBeVisible();
  await expect(page.getByRole("navigation", { name: /Ngữ cảnh BOQ|BOQ context|BOQ 上下文|BOQコンテキスト/i })).toContainText("NICON");
  await expect(page.getByRole("link", { name: "HD-CUSTOMER-001" })).toBeVisible();
  await expect(page.getByRole("link", { name: "PO-SUPPLIER-002" })).toBeVisible();
  await expect(page.getByText("contracts.status.InProgress")).toHaveCount(0);
  await expect(page.getByText("contracts.status.Draft")).toHaveCount(0);

  await page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i }).click();
  await page.getByRole("button", { name: /Thêm dòng|Add line|添加明细|明細を追加/i }).click();
  await page.locator("#boq-edit-code-1").fill("mat-1");
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
  await expect(page.getByRole("alert")).toContainText(/Mã vật tư phải duy nhất|Item codes must be unique|物料编码必须唯一|品目コードは一意/i);
  await expect(page.locator("#boq-edit-code-1")).toHaveValue("mat-1");

  await page.locator("#boq-edit-code-1").fill("MAT-NEW");
  await page.locator("#boq-edit-description-1").fill("New material");
  await page.locator("#boq-edit-unit-1").fill("item");
  await page.locator("#boq-edit-quantity-1").fill("2");
  await page.locator("#boq-edit-price-1").fill("500000");
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();

  await expect.poll(() => updatePayload).not.toBeNull();
  await expect(page.locator("#boq-edit-code-1")).toHaveCount(0);
  await expect(page.getByRole("cell", { name: "MAT-NEW" })).toBeVisible();

  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ role: "PROCUREMENT_VIEWER", roleId: null, permissions: ["proc.boq.view"] }),
  }));
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("button", { name: /Sửa|Edit|编辑|編集/i })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /Trình duyệt|Submit|提交|提出/i })).toHaveCount(0);

  for (const viewport of [{ width: 768, height: 1024 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: /Chi tiết BOQ R1|BOQ R1 details|BOQ R1 详情|BOQ R1 詳細/i })).toBeVisible();
    await expect(page.getByRole("navigation", { name: /Ngữ cảnh BOQ|BOQ context|BOQ 上下文|BOQコンテキスト/i })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
});
