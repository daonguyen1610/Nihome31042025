import { expect, test } from "../fixtures/auth";

test("finance control renders payments, periods, corrections, and payment history", async ({
  page,
  baseURL,
}) => {
  const tokens = {
    accessToken: "finance-smoke-access",
    refreshToken: "finance-smoke-refresh",
    roleId: 1,
  };
  const frontendUrl = new URL(baseURL!);
  await page.context().addCookies([
    {
      name: "nicon_access_token",
      value: tokens.accessToken,
      domain: frontendUrl.hostname,
      path: "/",
      sameSite: "Strict",
    },
    {
      name: "nicon_refresh_token",
      value: tokens.refreshToken,
      domain: frontendUrl.hostname,
      path: "/",
      sameSite: "Strict",
    },
  ]);

  await page.route(/\/api\/(?:v1\/)?auth\/refresh$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(tokens),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?users\/me$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: 1,
        phoneNumber: "0335240370",
        fullName: "Administrator",
        role: "SUPER_ADMIN",
        roleId: 1,
        isActive: true,
      }),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        role: "SUPER_ADMIN",
        roleId: tokens.roleId ?? 1,
        permissions: [
          "dashboard.view",
          "finance.payments.view",
          "finance.payments.manage",
          "finance.payments.approve",
          "finance.payments.pay",
          "finance.periods.view",
          "finance.periods.close",
          "finance.corrections.view",
          "finance.corrections.manage",
          "finance.corrections.approve",
          "finance.corrections.reverse",
        ],
      }),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?notifications\/unread-count$/, (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: '{"count":0}' }),
  );
  await page.route(/\/api\/(?:v1\/)?translations\/vi$/, (route) =>
    route.fulfill({
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
          "finance.action.edit": "Edit draft",
          "finance.action.save": "Save draft",
          "finance.action.paymentRevise": "Create revision",
          "finance.dialog.payment.editTitle": "Edit draft payment request",
          "finance.actionDialog.paymentRevise.title": "Create payment request revision",
          "finance.field.timeline": "Status timeline",
          "finance.payments.referencesEmpty":
            "A signed downstream contract and an active accountant are required to create a payment request.",
        },
      }),
    }),
  );

  await page.route(/\/api\/(?:v1\/)?finance\/payment-requests$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: 1,
          code: "PAY-000001",
          contractId: 10,
          contractNumber: "CT-2026-010",
          vendorId: 20,
          vendorName: "Nicon Materials",
          contractPaymentMilestoneId: null,
          constructionQsBatchId: 12,
          constructionQsCode: "QS-2026-012",
          supplierInvoiceNumber: "INV-2026-001",
          invoiceDate: "2026-09-01",
          invoiceAmount: 125000000,
          currency: "VND",
          status: "UnderValidation",
          revisionNumber: 2,
          revisions: [
            {
              revisionNumber: 1,
              status: "Rejected",
              constructionQsBatchId: 12,
              supplierInvoiceNumber: "INV-2026-001",
              invoiceAmount: 125000000,
              currency: "VND",
              capturedAt: "2026-09-01T00:00:00Z",
              revisionReason: "Corrected QS",
              attachments: [],
            },
          ],
          receivedAt: "2026-09-02T03:00:00Z",
          assignedAccountantUserId: 7,
          assignedAccountantName: "Finance Accountant",
          submittedAt: "2026-09-02T04:00:00Z",
          rowVersion: "AAAAAAAAB9M=",
          attachments: [{ id: 1, fileName: "invoice.pdf", filePath: "/documents/invoice.pdf" }],
          events: [
            {
              id: 1,
              fromStatus: null,
              toStatus: "Draft",
              changedByUserId: 1,
              changedByName: "Administrator",
              changedAt: "2026-09-02T03:00:00Z",
            },
            {
              id: 2,
              fromStatus: "Draft",
              toStatus: "UnderValidation",
              changedByUserId: 1,
              changedByName: "Administrator",
              changedAt: "2026-09-02T04:00:00Z",
            },
          ],
        },
        {
          id: 4,
          code: "PAY-000002",
          contractId: 10,
          contractNumber: "CT-2026-010",
          vendorId: 20,
          vendorName: "Nicon Materials",
          contractPaymentMilestoneId: null,
          constructionQsBatchId: null,
          supplierInvoiceNumber: "INV-2026-002",
          invoiceDate: "2026-09-02",
          invoiceAmount: 85000000,
          currency: "VND",
          status: "Draft",
          revisionNumber: 1,
          revisions: [],
          receivedAt: "2026-09-03T03:00:00Z",
          assignedAccountantUserId: 7,
          assignedAccountantName: "Finance Accountant",
          rowVersion: "AAAAAAAAB9V=",
          attachments: [{ id: 2, fileName: "draft.pdf", filePath: "/documents/draft.pdf" }],
          events: [],
        },
      ]),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?finance\/periods$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: 2,
          year: 2026,
          month: 8,
          periodStartUtc: "2026-07-31T17:00:00Z",
          periodEndUtc: "2026-08-31T17:00:00Z",
          status: "Closed",
          closedAt: "2026-09-01T02:00:00Z",
          closeReason: "Monthly close",
          rowVersion: "AAAAAAAAB9Q=",
        },
      ]),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?finance\/corrections$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        {
          id: 3,
          code: "AC-000001",
          operationalProjectId: 4,
          projectName: "Office Fit-out",
          accountingPeriodId: 2,
          periodLabel: "08/2026",
          sourceEntityType: "PaymentRequest",
          sourceEntityId: 1,
          reasonCode: "AMOUNT_ERROR",
          originalValue: 125000000,
          correctedValue: 120000000,
          currency: "VND",
          responsibleAccountantUserId: 7,
          responsibleAccountantName: "Finance Accountant",
          status: "Submitted",
          recordedByUserId: 1,
          submittedAt: "2026-09-03T02:00:00Z",
          rowVersion: "AAAAAAAAB9U=",
        },
      ]),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?contracts(?:\?.*)?$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?vendors(?:\?.*)?$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?operational-projects(?:\?.*)?$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }),
    }),
  );
  await page.route(/\/api\/(?:v1\/)?users(?:\?.*)?$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, items: [] }),
    }),
  );

  // Deliberate lookup failure exercises recovery separately from the unmocked
  // RFQ business pipeline. General CRM/user lists remain empty above.
  let referencesAvailable = false;
  await page.route(/\/api\/(?:v1\/)?finance\/payment-references$/, (route) => {
    return route.fulfill(
      referencesAvailable
        ? {
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
              contracts: [
                {
                  id: 10,
                  contractNumber: "CT-2026-010",
                  vendorId: 20,
                  vendorName: "Nicon Materials",
                  paymentMilestones: [],
                },
              ],
              vendors: [{ id: 20, vendorCode: "V-020", companyName: "Nicon Materials" }],
              accountants: [{ id: 7, fullName: "Finance Accountant" }],
              qsBatches: [],
            }),
          }
        : {
            status: 503,
            contentType: "application/json",
            body: JSON.stringify({ message: "Payment references temporarily unavailable" }),
          },
    );
  });

  await page.goto(`${baseURL}/admin/finance-control`, { waitUntil: "networkidle" });

  await expect(
    page.getByRole("heading", { name: /Kiểm soát tài chính|Finance control|财务管控|財務管理/i }),
  ).toBeVisible();
  await expect(page.getByText("PAY-000001")).toBeVisible();
  await page
    .getByText(/Lịch sử trạng thái|Status timeline|状态时间线|ステータス履歴/i)
    .first()
    .click();
  await expect(page.getByText("Administrator").first()).toBeVisible();

  await page.getByRole("tab", { name: /Kỳ kế toán|Periods|会计期间|会計期間/i }).click();
  await expect(page.getByText("08/2026")).toBeVisible();
  await page.getByRole("tab", { name: /Điều chỉnh|Corrections|调整|修正/i }).click();
  await expect(page.getByText("AC-000001")).toBeVisible();

  await page.getByRole("tab", { name: /Thanh toán|Payments|付款|支払/i }).click();
  await page
    .getByRole("button", { name: /Tạo đề nghị|Create request|创建申请|申請を作成/i })
    .click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("alert")).toBeVisible();
  await expect(dialog.locator("#finance-invoice-number")).toHaveCount(0);
  await expect(
    dialog.getByRole("button", { name: /Tạo bản nháp|Create draft|finance.action.create/i }),
  ).toBeDisabled();
  referencesAvailable = true;
  await dialog.getByRole("button", { name: /Thử lại|Retry|common.retry/i }).click();
  await expect(dialog.locator("#finance-invoice-number")).toBeVisible();
  await expect(dialog.locator("#finance-received")).toBeVisible();

  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  let updatedInvoice = "";
  let updatedRowVersion = "";
  await page.route(/\/api\/(?:v1\/)?finance\/payment-requests\/4$/, (route) => {
    const body = route.request().postDataJSON();
    updatedInvoice = body.supplierInvoiceNumber;
    updatedRowVersion = body.rowVersion;
    return route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });
  await page.getByRole("button", { name: "Edit draft" }).click();
  await expect(dialog.getByRole("heading", { name: "Edit draft payment request" })).toBeVisible();
  await expect(dialog.locator("#finance-invoice-number")).toHaveValue("INV-2026-002");
  await dialog.locator("#finance-invoice-number").fill("INV-2026-002-REV");
  await dialog.getByRole("button", { name: "Save draft" }).click();
  await expect(dialog).not.toBeVisible();
  expect(updatedInvoice).toBe("INV-2026-002-REV");
  expect(updatedRowVersion).toBe("AAAAAAAAB9V=");
  let revisionReason = "";
  await page.route(/\/api\/(?:v1\/)?finance\/payment-requests\/1\/revise$/, (route) => {
    revisionReason = route.request().postDataJSON().reason;
    return route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });
  await page.getByRole("button", { name: "Create revision" }).click();
  await page.locator("#finance-action-reason").fill("Corrected QS after submission");
  await page.getByRole("dialog").getByRole("button", { name: "Create revision" }).click();
  await expect(page.getByRole("dialog")).not.toBeVisible();
  expect(revisionReason).toBe("Corrected QS after submission");
  await page.route(/\/api\/(?:v1\/)?finance\/payment-references$/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ contracts: [], vendors: [], accountants: [], qsBatches: [] }),
    }),
  );
  await page
    .getByRole("button", { name: /Tạo đề nghị|Create request|创建申请|申請を作成/i })
    .click();
  await expect(
    dialog.getByText(
      "A signed downstream contract and an active accountant are required to create a payment request.",
    ),
  ).toBeVisible();
  await expect(
    dialog.getByRole("button", { name: /Tạo bản nháp|Create draft|finance.action.create/i }),
  ).toBeDisabled();
  await expect(dialog.locator("#finance-contract")).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(dialog).toBeVisible();
  await page.setViewportSize({ width: 768, height: 1024 });
  await expect(dialog).toBeVisible();
});
