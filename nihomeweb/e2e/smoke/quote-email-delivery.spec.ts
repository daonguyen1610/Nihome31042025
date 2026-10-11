import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const mailSinkUrl = process.env.MAIL_SINK_URL;

test("approved opportunity quote sends the edited preview through SMTP before customer confirmation", async ({ api, loginAs, loginInBrowserAs, page }) => {
  test.skip(!mailSinkUrl, "Run with docker-compose-dev.yaml and MAIL_SINK_URL=http://127.0.0.1:8025");

  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const suffix = Math.random().toString(36).slice(2, 10);
  const recipient = `quote-${suffix}@example.test`;
  const marker = `E2E-QUOTE-${suffix}`;
  let customerId = 0;
  let opportunityId = 0;
  let quoteId = 0;
  let projectId = 0;
  let contractId = 0;

  try {
    // Converting the lead creates the customer contact, the Opportunity and the
    // operational project the quotation inherits, so the pre-contract department
    // work packages have a project to work in and the send dialog can prefill
    // the contact address captured from the lead.
    const leadResponse = await api.post("/api/leads", {
      headers,
      data: {
        name: `Cải tạo văn phòng Minh Châu ${suffix}`,
        phone: `0912${Math.floor(100000 + Math.random() * 899999)}`,
        email: recipient,
        sourceCode: "marketing",
      },
    });
    expect(leadResponse.status(), await leadResponse.text()).toBe(201);
    const leadId = (await leadResponse.json()).id as number;

    const conversionResponse = await api.post(`/api/leads/${leadId}/convert`, {
      headers,
      data: { address: "12 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh", representativeName: "Nguyễn Minh Châu" },
    });
    expect(conversionResponse.status(), await conversionResponse.text()).toBe(200);
    const conversion = await conversionResponse.json() as { convertedCustomerId: number; convertedOpportunityId: number };
    customerId = conversion.convertedCustomerId;
    opportunityId = conversion.convertedOpportunityId;

    const opportunityResponse = await api.get(`/api/opportunities/${opportunityId}`, { headers });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(200);
    projectId = (await opportunityResponse.json()).operationalProjectId as number;
    expect(projectId, "converting a lead must create the operational project").toBeGreaterThan(0);
    for (const targetStage of ["Qualification", "Proposal", "Negotiation"]) {
      const response = await api.patch(`/api/opportunities/${opportunityId}/stage`, { headers, data: { targetStage } });
      expect(response.status(), await response.text()).toBe(200);
    }

    const quoteResponse = await api.post("/api/quotes", {
      headers,
      data: {
        opportunityId,
        method: "Boq",
        packageDescription: "Cải tạo văn phòng và hệ thống MEP Minh Châu",
        items: [{ itemCode: "CT-01", name: "Thi công hoàn thiện văn phòng", unit: "m2", quantity: 500, unitPrice: 1_500_000 }],
        vatPercent: 8,
      },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    let quote = await quoteResponse.json() as { id: number; code: string; rowVersion: string; status: string };
    quoteId = quote.id;
    const submitted = await api.post(`/api/quotes/${quoteId}/submit`, { headers, data: { rowVersion: quote.rowVersion } });
    expect(submitted.status(), await submitted.text()).toBe(200);
    quote = await submitted.json();
    const approved = await api.post(`/api/quotes/${quoteId}/approve`, { headers, data: { rowVersion: quote.rowVersion } });
    expect(approved.status(), await approved.text()).toBe(200);

    // Tài chính – Kế toán reviews the commercial terms before the quotation may
    // reach the customer. The server only opens the send gate for an approved
    // review of this exact quotation version, and the reviewer cannot approve
    // their own dossier, so an independent approver clears it.
    const optionsResponse = await api.get("/api/work-packages/options", { headers });
    expect(optionsResponse.status(), await optionsResponse.text()).toBe(200);
    const options = await optionsResponse.json() as {
      departments: { id: number; code: string }[];
      users: { userId: number; name: string; roleGroupId: number; canLead: boolean }[];
    };
    const financeGroup = options.departments.find(group => group.code === "FINANCE")!;
    const accountant = options.users.find(user => user.roleGroupId === financeGroup.id && user.canLead)!;
    const financePackageResponse = await api.post("/api/work-packages", {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      data: {
        kind: "Project",
        purpose: "FinanceCommercialReview",
        name: `Rà soát thương mại báo giá ${quote.code}`,
        operationalProjectId: projectId,
        sourceOpportunityId: opportunityId,
        sourceQuoteId: quoteId,
        departmentRoleGroupId: financeGroup.id,
        leadUserId: accountant.userId,
      },
    });
    expect(financePackageResponse.status(), await financePackageResponse.text()).toBe(201);
    const financePackageId = (await financePackageResponse.json()).id as number;

    const accountantHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.accountant)}`, "Idempotency-Key": randomUUID() };
    let reviewResponse = await api.put(`/api/work-packages/${financePackageId}/finance-review`, {
      headers: accountantHeaders,
      data: {
        contractConditionsReviewed: true,
        paymentTermsReviewed: true,
        guaranteesReviewed: true,
        findings: "Điều kiện thanh toán theo tiến độ, giữ lại 5% bảo hành trong 12 tháng.",
      },
    });
    expect(reviewResponse.status(), await reviewResponse.text()).toBe(200);
    let review = await reviewResponse.json() as { rowVersion: string };
    reviewResponse = await api.post(`/api/work-packages/${financePackageId}/finance-review/submit`, {
      headers: { ...accountantHeaders, "Idempotency-Key": randomUUID() },
      data: { rowVersion: review.rowVersion },
    });
    expect(reviewResponse.status(), await reviewResponse.text()).toBe(200);
    review = await reviewResponse.json();
    reviewResponse = await api.post(`/api/work-packages/${financePackageId}/finance-review/decision`, {
      headers: { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}`, "Idempotency-Key": randomUUID() },
      data: { approved: true, rowVersion: review.rowVersion },
    });
    expect(reviewResponse.status(), await reviewResponse.text()).toBe(200);
    expect((await reviewResponse.json() as { status: string }).status).toBe("Approved");

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Gửi khách" }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByLabel("Email khách hàng")).toHaveValue(recipient);
    await expect(dialog.getByLabel("Tiêu đề email")).toHaveValue(/QT-/);
    await expect(dialog.getByLabel("Nội dung email sắp gửi")).toHaveValue(/Tổng giá trị/);
    await expect(dialog.getByTestId("quote-email-attachment")).toContainText(`${quote.code}.xlsx`);
    await expect(dialog.frameLocator('iframe[title="Xem trước email hiển thị"]').locator("body")).toContainText("NICON");
    await expect(dialog.frameLocator('iframe[title="Xem trước email hiển thị"]').locator("body")).toContainText("810.000.000");
    await dialog.getByLabel("Email khách hàng").fill("invalid@domain");
    await expect(dialog.getByRole("button", { name: "Gửi khách" })).toBeDisabled();
    await dialog.getByLabel("Email khách hàng").fill(recipient);
    await dialog.getByLabel("Tiêu đề email").fill(`NICON quote ${marker}`);
    await dialog.getByLabel("Nội dung email sắp gửi").fill(`<div>Dear customer, please review <strong>${marker}</strong>. Total: 810000000 VND.</div>`);
    await expect(dialog.frameLocator('iframe[title="Xem trước email hiển thị"]').locator("body")).toContainText(marker);
    await dialog.getByRole("button", { name: "Gửi khách" }).click();
    await expect(dialog).not.toBeVisible();

    await expect.poll(async () => {
      const response = await api.get(`${mailSinkUrl}/messages`);
      expect(response.status()).toBe(200);
      const messages = await response.json() as Array<{ recipient: string; raw: string }>;
      return messages.filter((message) => message.recipient === recipient);
    }, { timeout: 10_000 }).toHaveLength(1);
    const mailbox = await api.get(`${mailSinkUrl}/messages`);
    const messages = await mailbox.json() as Array<{ recipient: string; raw: string }>;
    const raw = messages.find((message) => message.recipient === recipient)!.raw;
    expect(raw).toContain(`Subject: NICON quote ${marker}`);
    expect(raw).toContain("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    expect(raw).toContain(`${quote.code}.xlsx`);
    expect(raw).toContain(`<strong>${marker}</strong>`);

    const sentResponse = await api.get(`/api/quotes/${quoteId}`, { headers });
    expect(sentResponse.status()).toBe(200);
    quote = await sentResponse.json();
    expect(quote.status).toBe("SentToCustomer");
    await page.getByRole("button", { name: "Khách duyệt" }).click();
    await expect.poll(async () => {
      const response = await api.get(`/api/quotes/${quoteId}`, { headers });
      return (await response.json() as { status: string }).status;
    }).toBe("CustomerApproved");

    await page.goto(`/admin/opportunities/${opportunityId}`, { waitUntil: "networkidle" });
    await page.getByTestId("opportunity-create-contract").click();
    await expect(page).toHaveURL(new RegExp(`fromQuote=${quoteId}`));
    const contractDialog = page.getByRole("dialog");
    await expect(contractDialog.getByText(`#${quoteId}`)).toBeVisible();
    // The contract dialog prefills the project the quote already belongs to.
    // Contract, Opportunity and quote must share one project, so this step links
    // that project instead of creating a second one for the same customer.
    await expect(contractDialog.locator("#c-project-form")).toContainText(`PJ-`);
    await contractDialog.locator("#c-value").fill("810000000");
    const contractResponsePromise = page.waitForResponse((response) =>
      /\/api\/contracts(?:\?.*)?$/.test(response.url()) && response.request().method() === "POST");
    await contractDialog.getByRole("button", { name: "Lưu" }).click();
    const contractResponse = await contractResponsePromise;
    expect(contractResponse.status(), await contractResponse.text()).toBe(201);
    const contract = await contractResponse.json() as { id: number; quoteId: number | null; opportunityId: number | null; value: number };
    contractId = contract.id;
    expect(contract.quoteId).toBe(quoteId);
    expect(contract.opportunityId).toBe(opportunityId);
    expect(contract.value).toBe(810_000_000);
  } finally {
    if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
    if (quoteId) await hardDeleteBusinessRoot(api, headers, `/api/quotes/${quoteId}`);
    if (opportunityId) await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (projectId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
