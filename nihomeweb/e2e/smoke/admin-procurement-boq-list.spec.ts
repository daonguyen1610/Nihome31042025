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
  await expect(table.getByText("R12", { exact: true })).toBeVisible();
  await expect(table.getByText("R1", { exact: true })).toHaveCount(0);

  await page.getByRole("button", { name: /Tiếp|Next|下一页|次へ/i }).click();
  await expect(table.getByText("R1", { exact: true })).toBeVisible();

  await page.locator("#boq-search").fill("MAT-12");
  await expect(table.getByText("R12", { exact: true })).toBeVisible();
  await expect(page.getByText(/Trang 1\/1|Page 1\/1|第 1\/1 页|1\/1 ページ/i)).toBeVisible();

  await page.locator("#boq-search").fill("");
  await page.locator("#boq-status").click();
  await page.getByRole("option", { name: /Đã duyệt|Approved|已批准|承認済/i }).click();
  await expect(page.getByText(/6 phiên bản|6 revisions|共 6 个版本|全 6 改訂/i)).toBeVisible();

  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: /Xuất CSV|Export CSV|导出 CSV|CSVエクスポート/i }).click();
  await expect((await download).suggestedFilename()).toContain("project-");
});
