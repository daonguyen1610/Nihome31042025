import { expect, test, TEST_USERS } from "../fixtures/auth";

test("KPI dashboard calculates, distinguishes missing data, and locks a period", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const year = new Date().getFullYear();
  const month = new Date().getMonth() + 1;
  let locked = false;
  const dashboard = () => ({
    periodId: 91,
    year,
    month,
    timeZoneId: "Asia/Ho_Chi_Minh",
    periodStatus: locked ? "Locked" : "Open",
    periodRowVersion: locked ? "AAAAAAAAB9Q=" : "AAAAAAAAB9M=",
    userId: 3,
    userName: "Sales KPI User",
    roleCode: "SALES",
    totalScore: 20,
    availableWeight: 0.4,
    isComplete: false,
    lastCalculatedAt: "2026-09-06T00:00:00Z",
    scores: [
      { id: 1, definitionId: 1, code: "SALES_CONVERSION", nameKey: "kpi.definition.SALES_CONVERSION", sourceModule: "M1", weight: 0.4, targetValue: null, rawValue: 50, numerator: 1, denominator: 2, score: 50, weightedScore: 20, status: "Available", evidenceJson: '{"EntityType":"Lead","RecordIds":[1,2]}', definitionVersion: 1, calculatedAt: "2026-09-06T00:00:00Z" },
      { id: 2, definitionId: 2, code: "SALES_REVENUE", nameKey: "kpi.definition.SALES_REVENUE", sourceModule: "M6", weight: 0.4, targetValue: null, rawValue: 100000000, score: null, weightedScore: null, status: "MissingConfiguration", evidenceJson: '{"EntityType":"Contract","RecordIds":[1]}', definitionVersion: 1, calculatedAt: "2026-09-06T00:00:00Z" },
      { id: 3, definitionId: 3, code: "SALES_FIRST_RESPONSE", nameKey: "kpi.definition.SALES_FIRST_RESPONSE", sourceModule: "M1", weight: 0.2, targetValue: null, rawValue: null, score: null, weightedScore: null, status: "MissingData", evidenceJson: '{"EntityType":"Lead","RecordIds":[]}', definitionVersion: 1, calculatedAt: "2026-09-06T00:00:00Z" },
    ],
  });

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(/\/api\/(?:v1\/)?kpi\/dashboard/, route => route.fulfill({ status: 404, contentType: "application/json", body: "{}" }));
  await page.route(/\/api\/(?:v1\/)?kpi\/definitions/, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(/\/api\/(?:v1\/)?kpi\/calculate$/, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(dashboard()) }));
  await page.route(/\/api\/(?:v1\/)?kpi\/periods\/\d+\/\d+\/lock$/, route => {
    locked = true;
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(dashboard()) });
  });

  await page.goto(`${baseURL}/admin/kpi`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: /Đánh giá KPI|KPI performance|KPI 绩效|KPI評価/i })).toBeVisible();
  await expect(page.getByRole("heading", { name: /Cấu hình KPI|KPI configuration|KPI 配置|KPI設定/i })).toBeHidden();
  await expect(page.getByText(/Cách sử dụng|How to use this page|使用方法|このページの使い方/i)).toBeVisible();
  await expect(page.getByText(/Khóa kỳ là chốt chính thức|Locking makes the period final|锁定表示期间正式确定|ロックすると期間が確定します/i)).toBeVisible();
  await page.getByRole("button", { name: /Tính KPI|Calculate KPI|计算 KPI|KPIを計算/i }).click();
  await expect(page.getByText(/Tỷ lệ chuyển đổi Lead thành Hợp đồng|Lead-to-contract conversion rate|线索转合同率|リードから契約への転換率/i)).toBeVisible();
  await expect(page.getByText(/Thiếu cấu hình mục tiêu|Missing target configuration|缺少目标配置|目標設定不足/i)).toBeVisible();
  await expect(page.getByText(/Thiếu dữ liệu nguồn|Missing source data|缺少源数据|ソースデータ不足/i)).toBeVisible();
  await expect(page.getByText(/Chưa đủ dữ liệu|Incomplete data|数据不完整|データ不足/i)).toBeVisible();

  await page.locator("#kpi-lock-note").fill("Monthly HR close");
  await page.getByRole("button", { name: /Khóa kỳ|Lock period|锁定期间|期間をロック/i }).click();
  await expect(page.getByText(/Đã khóa|Locked|已锁定|ロック済み/i)).toBeVisible();
  await expect(page.getByRole("button", { name: /Tính KPI|Calculate KPI|计算 KPI|KPIを計算/i })).toBeHidden();
});

test("KPI configuration is a separate management page", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const definition = {
    id: 1,
    code: "SALES_CONVERSION",
    roleCode: "SALES",
    nameKey: "kpi.definition.SALES_CONVERSION",
    sourceModule: "M1",
    metricCode: "SalesLeadConversionRate",
    weight: 0.4,
    requiresTarget: false,
    targetValue: null,
    minimumAcceptableScore: 60,
    targetDirection: "HigherIsBetter",
    version: 1,
    isActive: true,
    rowVersion: "AAAAAAAAB9M=",
  };

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(/\/api\/(?:v1\/)?kpi\/definitions(?:\/\d+)?$/, async route => {
    if (route.request().method() === "PUT") {
      const body = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ ...definition, ...body, version: 2, rowVersion: "AAAAAAAAB9Q=" }),
      });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([definition]) });
  });

  await page.goto(`${baseURL}/admin/kpi/configuration`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: /Cấu hình KPI|KPI configuration|KPI 配置|KPI設定/i })).toBeVisible();
  await expect(page.getByRole("button", { name: /Tính KPI|Calculate KPI|计算 KPI|KPIを計算/i })).toBeHidden();
  await expect(page.getByText(/Cách sử dụng|How to use this page|使用方法|このページの使い方/i)).toBeVisible();
  await expect(page.getByText(/Trọng số 40% · Thiếu 0 mục tiêu|Weight 40% · 0 targets missing|权重 40% · 缺少 0 个目标|重み 40%・目標不足 0 件/i)).toBeVisible();
  await expect(page.getByText(/Tỷ lệ chuyển đổi Lead thành Hợp đồng|Lead-to-contract conversion rate|线索转合同率|リードから契約への転換率/i)).toBeVisible();
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
  await expect(page.getByText(/Đã lưu cấu hình KPI|KPI configuration saved|KPI 配置已保存|KPI設定を保存しました/i)).toBeVisible();
});

test("KPI configuration rejects an evaluation-only user", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.sale);
  await page.goto(`${baseURL}/admin/kpi/configuration`, { waitUntil: "networkidle" });

  await expect(page.getByRole("heading", { name: "403" })).toBeVisible();
  await expect(page.getByRole("heading", { name: /Cấu hình KPI|KPI configuration|KPI 配置|KPI設定/i })).toBeHidden();
});
