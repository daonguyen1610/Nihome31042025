import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";

test("superseded QS blocks payment until the same request is revised and approved", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(180_000);
  const suffix = randomUUID().slice(0, 8);
  const adminToken = await loginAs(TEST_USERS.superAdmin);
  const qsToken = await loginAs(TEST_USERS.qs);
  const pmToken = await loginAs(TEST_USERS.pm);
  const accountantToken = await loginAs(TEST_USERS.accountant);
  const bgdToken = await loginAs(TEST_USERS.bgd);
  const headers = (token: string) => ({
    Authorization: `Bearer ${token}`,
    "Idempotency-Key": randomUUID(),
  });
  const adminHeaders = headers(adminToken);
  const customerId = await createOwnCustomer(api, adminHeaders, "Payment revision");
  const vendorResponse = await api.post("/api/vendors", {
    headers: headers(adminToken),
    data: {
      vendorCode: `E2E-REV-${suffix}`,
      companyName: `E2E structural supplier ${suffix}`,
      vendorType: "Supplier",
      phone: `090${Math.floor(1_000_000 + Math.random() * 8_999_999)}`,
    },
  });
  expect(vendorResponse.status(), await vendorResponse.text()).toBe(201);
  const vendorId = (await vendorResponse.json()).id as number;
  async function userId(role: string, phoneNumber: string) {
    const response = await api.get(`/api/users?role=${role}&search=${phoneNumber}&take=10`, {
      headers: adminHeaders,
    });
    expect(response.ok(), await response.text()).toBe(true);
    const user = (await response.json()).items.find(
      (item: { phoneNumber: string }) => item.phoneNumber === phoneNumber,
    );
    expect(user, `${role} account ${phoneNumber}`).toBeTruthy();
    return user.id as number;
  }
  const pmId = await userId("PM", TEST_USERS.pm.phoneNumber);
  const qsId = await userId("QS", TEST_USERS.qs.phoneNumber);
  const accountantId = await userId("ACCOUNTANT", TEST_USERS.accountant.phoneNumber);
  const projectResponse = await api.post("/api/operational-projects", {
    headers: headers(adminToken),
    data: { name: `Payment revision ${suffix}`, customerId, projectManagerUserId: pmId },
  });
  expect(projectResponse.status(), await projectResponse.text()).toBe(201);
  const projectId = (await projectResponse.json()).id as number;
  const memberResponse = await api.post(`/api/operational-projects/${projectId}/team/members`, {
    headers: headers(adminToken),
    data: {
      userId: qsId,
      position: "QS",
      startedAt: new Date().toISOString(),
      roles: [{ roleCode: "Observer", scope: "Project" }],
    },
  });
  expect(memberResponse.status(), await memberResponse.text()).toBe(201);
  const contractResponse = await api.post("/api/contracts", {
    headers: headers(adminToken),
    data: {
      customerId,
      operationalProjectId: projectId,
      direction: "Downstream",
      type: "Supply",
      vendorId,
      value: 100_000,
      startDate: new Date(Date.now() + 86_400_000).toISOString().slice(0, 10),
      endDate: new Date(Date.now() + 181 * 86_400_000).toISOString().slice(0, 10),
      scopeOfWork: "Factory structural supply package",
    },
  });
  expect(contractResponse.status(), await contractResponse.text()).toBe(201);
  const contract = (await contractResponse.json()) as { id: number; rowVersion: string };
  const signedResponse = await api.post(`/api/contracts/${contract.id}/transition`, {
    headers: headers(adminToken),
    data: { newStatus: "Signed", rowVersion: contract.rowVersion },
  });
  expect(signedResponse.status(), await signedResponse.text()).toBe(200);

  async function approveQs(supersedesBatchId?: number) {
    const qsResponse = await api.post(
      `/api/operational-projects/${projectId}/construction/qs-batches`,
      {
        headers: headers(qsToken),
        data: {
          direction: "Downstream",
          contractId: contract.id,
          wbsCode: "STRUCTURE-01",
          periodFrom: "2026-08-01T00:00:00Z",
          periodTo: "2026-08-31T00:00:00Z",
          certifiedAmount: 1_000,
          description: "Accepted structural supply quantity",
          lines: [
            {
              itemCode: "STRUCTURE-CONCRETE",
              description: "Concrete works",
              unit: "m3",
              quantity: 1,
              unitPrice: 1_000,
            },
          ],
          evidenceDocumentIds: [],
          supersedesBatchId,
        },
      },
    );
    expect(qsResponse.status(), await qsResponse.text()).toBe(201);
    const qs = (await qsResponse.json()) as { id: number; code: string; rowVersion: string };
    const submittedResponse = await api.post(
      `/api/operational-projects/${projectId}/construction/qs-batches/${qs.id}/submit`,
      { headers: headers(qsToken), data: { rowVersion: qs.rowVersion } },
    );
    expect(submittedResponse.status(), await submittedResponse.text()).toBe(200);
    const submitted = (await submittedResponse.json()) as { rowVersion: string };
    const approvedResponse = await api.post(
      `/api/operational-projects/${projectId}/construction/qs-batches/${qs.id}/cm-decision`,
      {
        headers: headers(pmToken),
        data: { approved: true, reason: "Independent CM review", rowVersion: submitted.rowVersion },
      },
    );
    expect(approvedResponse.status(), await approvedResponse.text()).toBe(200);
    return qs;
  }

  const originalQs = await approveQs();
  const invoiceNumber = `INV-REV-${suffix}`;
  const paymentResponse = await api.post("/api/finance/payment-requests", {
    headers: headers(accountantToken),
    data: {
      contractId: contract.id,
      vendorId,
      constructionQsBatchId: originalQs.id,
      supplierInvoiceNumber: invoiceNumber,
      invoiceDate: new Date().toISOString().slice(0, 10),
      invoiceAmount: 1_000,
      currency: "VND",
      receivedAt: new Date().toISOString(),
      assignedAccountantUserId: accountantId,
      attachments: [{ fileName: "supplier-invoice.pdf", filePath: "/documents/invoice.pdf" }],
    },
  });
  expect(paymentResponse.status(), await paymentResponse.text()).toBe(201);
  let payment = (await paymentResponse.json()) as { id: number; rowVersion: string };
  for (const action of ["submit", "validate"] as const) {
    const response = await api.post(`/api/finance/payment-requests/${payment.id}/${action}`, {
      headers: headers(accountantToken),
      data: { rowVersion: payment.rowVersion },
    });
    expect(response.status(), await response.text()).toBe(200);
    payment = await response.json();
  }
  const approvedPayment = await api.post(`/api/finance/payment-requests/${payment.id}/decision`, {
    headers: headers(bgdToken),
    data: { approved: true, rowVersion: payment.rowVersion },
  });
  expect(approvedPayment.status(), await approvedPayment.text()).toBe(200);
  const replacementQs = await approveQs(originalQs.id);

  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  const pageErrors: string[] = [];
  page.on("pageerror", (error) => pageErrors.push(error.message));
  await loginInBrowserAs(page, TEST_USERS.accountant);
  await page.goto("/admin/finance-control");
  const invoice = page.locator("article").filter({ hasText: invoiceNumber });
  await expect(invoice).toContainText("Approved");
  await invoice.getByRole("button", { name: "Mark paid", exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Confirm paid", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toBeVisible();
  await expect(invoice).toContainText("Approved");
  await page.getByRole("dialog").getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(page.getByRole("dialog")).not.toBeVisible();

  await invoice.getByRole("button", { name: "Create revision", exact: true }).click();
  await page.locator("#finance-action-reason").fill("CM superseded the accepted QS batch");
  await page.getByRole("dialog").getByRole("button", { name: "Create revision" }).click();
  await expect(invoice).toContainText("Draft");
  await expect(invoice).toContainText("Revision 2");
  await expect(invoice).toContainText(invoiceNumber);
  await invoice.locator("details").first().locator("summary").click();
  await expect(invoice).toContainText("Revision 1");
  await expect(invoice).toContainText("CM superseded the accepted QS batch");

  await invoice.getByRole("button", { name: "Edit draft", exact: true }).click();
  const editor = page.getByRole("dialog");
  await editor.locator("#finance-downstream-qs").click();
  await page.getByRole("option", { name: new RegExp(replacementQs.code) }).click();
  await editor.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(editor).not.toBeVisible();
  await expect(invoice).toContainText(replacementQs.code);
  await invoice.getByRole("button", { name: "Submit", exact: true }).click();
  await invoice.getByRole("button", { name: "Validate", exact: true }).click();
  await expect(invoice).toContainText("Ready for approval");

  await loginInBrowserAs(page, TEST_USERS.bgd);
  await page.goto("/admin/finance-control");
  await invoice.getByRole("button", { name: "Approve", exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Approve", exact: true }).click();
  await expect(invoice).toContainText("Approved");
  await loginInBrowserAs(page, TEST_USERS.accountant);
  await page.goto("/admin/finance-control");
  await invoice.getByRole("button", { name: "Mark paid", exact: true }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Confirm paid", exact: true }).click();
  await expect(invoice).toContainText("Paid");
  await expect(invoice).toHaveCount(1);
  expect(pageErrors).toEqual([]);
});
