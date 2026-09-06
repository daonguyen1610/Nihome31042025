import { expect, test, TEST_USERS } from "../fixtures/auth";

test("finance control renders payments, periods, corrections, and payment history", async ({
  page,
  baseURL,
}) => {
  const apiBaseUrl = process.env.API_BASE_URL ?? baseURL!;
  const loginResponse = await page.request.post(`${apiBaseUrl}/api/auth/login`, {
    data: TEST_USERS.superAdmin,
  });
  expect(loginResponse.status(), "browser login as SUPER_ADMIN").toBe(200);
  const tokens = await loginResponse.json();
  const frontendUrl = new URL(baseURL!);
  await page.context().addCookies([
    { name: "nicon_access_token", value: tokens.accessToken, domain: frontendUrl.hostname, path: "/", sameSite: "Strict" },
    { name: "nicon_refresh_token", value: tokens.refreshToken, domain: frontendUrl.hostname, path: "/", sameSite: "Strict" },
  ]);

  await page.route(/\/api\/(?:v1\/)?auth\/refresh$/, (route) => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(tokens),
  }));
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, (route) => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      role: "SUPER_ADMIN",
      roleId: tokens.roleId ?? 1,
      permissions: [
        "dashboard.view",
        "finance.payments.view", "finance.payments.manage", "finance.payments.approve", "finance.payments.pay",
        "finance.periods.view", "finance.periods.close",
        "finance.corrections.view", "finance.corrections.manage", "finance.corrections.approve", "finance.corrections.reverse",
      ],
    }),
  }));
  await page.route(/\/api\/(?:v1\/)?translations\/vi$/, (route) => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({
      languageCode: "vi",
      translations: {
        "finance.title": "Finance control",
        "finance.tabs.payments": "Payments",
        "finance.tabs.periods": "Periods",
        "finance.tabs.corrections": "Corrections",
        "finance.payments.create": "Create request",
        "finance.field.timeline": "Status timeline",
      },
    }),
  }));

  await page.route(/\/api\/(?:v1\/)?finance\/payment-requests$/, (route) => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify([{
      id: 1,
      code: "PAY-000001",
      contractId: 10,
      contractNumber: "CT-2026-010",
      vendorId: 20,
      vendorName: "Nicon Materials",
      contractPaymentMilestoneId: null,
      supplierInvoiceNumber: "INV-2026-001",
      invoiceDate: "2026-09-01",
      invoiceAmount: 125000000,
      currency: "VND",
      status: "UnderValidation",
      receivedAt: "2026-09-02T03:00:00Z",
      assignedAccountantUserId: 7,
      assignedAccountantName: "Finance Accountant",
      submittedAt: "2026-09-02T04:00:00Z",
      rowVersion: "AAAAAAAAB9M=",
      attachments: [{ id: 1, fileName: "invoice.pdf", filePath: "/documents/invoice.pdf" }],
      events: [
        { id: 1, fromStatus: null, toStatus: "Draft", changedByUserId: 1, changedByName: "Administrator", changedAt: "2026-09-02T03:00:00Z" },
        { id: 2, fromStatus: "Draft", toStatus: "UnderValidation", changedByUserId: 1, changedByName: "Administrator", changedAt: "2026-09-02T04:00:00Z" },
      ],
    }]),
  }));
  await page.route(/\/api\/(?:v1\/)?finance\/periods$/, (route) => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify([{ id: 2, year: 2026, month: 8, periodStartUtc: "2026-07-31T17:00:00Z", periodEndUtc: "2026-08-31T17:00:00Z", status: "Closed", closedAt: "2026-09-01T02:00:00Z", closeReason: "Monthly close", rowVersion: "AAAAAAAAB9Q=" }]),
  }));
  await page.route(/\/api\/(?:v1\/)?finance\/corrections$/, (route) => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify([{ id: 3, code: "AC-000001", operationalProjectId: 4, projectName: "Office Fit-out", accountingPeriodId: 2, periodLabel: "08/2026", sourceEntityType: "PaymentRequest", sourceEntityId: 1, reasonCode: "AMOUNT_ERROR", originalValue: 125000000, correctedValue: 120000000, currency: "VND", responsibleAccountantUserId: 7, responsibleAccountantName: "Finance Accountant", status: "Submitted", recordedByUserId: 1, submittedAt: "2026-09-03T02:00:00Z", rowVersion: "AAAAAAAAB9U=" }]),
  }));
  await page.route(/\/api\/(?:v1\/)?contracts(?:\?.*)?$/, (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }) }));
  await page.route(/\/api\/(?:v1\/)?vendors(?:\?.*)?$/, (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }) }));
  await page.route(/\/api\/(?:v1\/)?operational-projects(?:\?.*)?$/, (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }) }));
  await page.route(/\/api\/(?:v1\/)?users(?:\?.*)?$/, (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, items: [] }) }));

  await page.goto(`${baseURL}/admin/finance-control`, { waitUntil: "networkidle" });

  await expect(page.getByRole("heading", { name: /Kiểm soát tài chính|Finance control|财务管控|財務管理/i })).toBeVisible();
  await expect(page.getByText("PAY-000001")).toBeVisible();
  await page.getByText(/Lịch sử trạng thái|Status timeline|状态时间线|ステータス履歴/i).click();
  await expect(page.getByText("Administrator").first()).toBeVisible();

  await page.getByRole("tab", { name: /Kỳ kế toán|Periods|会计期间|会計期間/i }).click();
  await expect(page.getByText("08/2026")).toBeVisible();
  await page.getByRole("tab", { name: /Điều chỉnh|Corrections|调整|修正/i }).click();
  await expect(page.getByText("AC-000001")).toBeVisible();

  await page.getByRole("tab", { name: /Thanh toán|Payments|付款|支払/i }).click();
  await page.getByRole("button", { name: /Tạo đề nghị|Create request|创建申请|申請を作成/i }).click();
  await expect(page.locator("#finance-invoice-number")).toBeVisible();
  await expect(page.locator("#finance-received")).toBeVisible();
});