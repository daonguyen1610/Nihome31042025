import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const mailSinkUrl = process.env.MAIL_SINK_URL;

test("approved opportunity quote sends the edited preview through SMTP before customer confirmation", async ({ api, loginAs, loginInBrowserAs, page }) => {
  test.skip(!mailSinkUrl, "Run with docker-compose.e2e.yaml and MAIL_SINK_URL=http://127.0.0.1:8025");

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
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Khách hàng cải tạo văn phòng ${suffix}`,
        sourceCode: "marketing",
        primaryContact: { fullName: "Nguyễn Minh Châu", phone: `0912${Math.floor(100000 + Math.random() * 899999)}`, email: recipient, isPrimary: true },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = (await customerResponse.json()).id as number;

    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: { name: `Cải tạo văn phòng Minh Châu ${suffix}`, customerId, estimatedValue: 750_000_000, winProbability: 45 },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = (await opportunityResponse.json()).id as number;
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
    await contractDialog.getByTestId("contract-quick-project-open").click();
    await contractDialog.getByTestId("contract-quick-project-name").fill(`Dự án cải tạo văn phòng Minh Châu ${suffix}`);
    const projectResponsePromise = page.waitForResponse((response) =>
      response.url().includes("/api/operational-projects") && response.request().method() === "POST");
    await contractDialog.getByTestId("contract-quick-project-create").click();
    const projectResponse = await projectResponsePromise;
    expect(projectResponse.status(), await projectResponse.text()).toBe(201);
    projectId = (await projectResponse.json()).id as number;
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
    if (projectId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    if (opportunityId) await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
