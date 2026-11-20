import type { APIRequestContext } from "@playwright/test";
import { execSql } from "../fixtures/db";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { approveProjectBoq } from "../fixtures/projectBoq";

// Quotes follow the approval chain in Cấu hình → Luồng duyệt: the sender chooses one
// approver per step and the steps decide in turn. Rules and edge cases are covered by
// QuotesControllerTests; this checks the screens people use for it.

// Both tests set the shared quote workflow, so they run one after the other.
test.describe.configure({ mode: "serial" });

type Headers = { Authorization: string };
type Step = { approverRoleCode: string; name: string };
type WorkflowRow = { id: number; module: string; action: string };

/** Applies a quote approval chain and returns the workflow as it was, to put back. */
async function applyQuoteChain(api: APIRequestContext, admin: Headers, steps: Step[]) {
  const workflows = (await (
    await api.get("/api/workflows?includeInactive=true", { headers: admin })
  ).json()) as WorkflowRow[];
  const original = workflows.find((w) => w.module === "quotes" && w.action === "approve")!;
  const saved = await api.put(`/api/workflows/${original.id}`, {
    headers: admin,
    data: {
      ...original,
      steps: steps.map((step, index) => ({
        order: index + 1,
        ...step,
        slaHours: 24,
        requireAllApprovers: false,
      })),
    },
  });
  expect(saved.status(), await saved.text()).toBe(200);
  return original;
}

type Created = {
  customerId: number;
  opportunityId: number;
  projectId: number;
  quoteId: number;
  code: string;
};

/**
 * A Sales lead converted to a project with an approved BOQ, and its draft BOQ quote. Each id
 * is recorded in `created` as soon as it exists, so a failure part-way still cleans up.
 */
async function createSalesQuote(
  api: APIRequestContext,
  admin: Headers,
  sale: Headers,
  created: Created,
) {
  const suffix = Math.random().toString(36).slice(2, 8);
  const lead = await api.post("/api/leads", {
    headers: sale,
    data: {
      name: `Nhà xưởng Tân Uyên ${suffix}`,
      phone: `0913${Math.floor(100000 + Math.random() * 899999)}`,
      sourceCode: "marketing",
    },
  });
  expect(lead.status(), await lead.text()).toBe(201);
  const converted = await (
    await api.post(`/api/leads/${(await lead.json()).id}/convert`, {
      headers: sale,
      data: { address: "KCN Tân Uyên, Bình Dương", representativeName: "Võ Thanh Tâm" },
    })
  ).json();
  created.customerId = converted.convertedCustomerId;
  created.opportunityId = converted.convertedOpportunityId;
  created.projectId = (
    await (await api.get(`/api/opportunities/${created.opportunityId}`, { headers: sale })).json()
  ).operationalProjectId;
  for (const targetStage of ["Qualification", "Proposal", "Negotiation"])
    await api.patch(`/api/opportunities/${created.opportunityId}/stage`, {
      headers: sale,
      data: { targetStage },
    });
  const boq = await approveProjectBoq(api, admin, created.projectId, [
    { name: "Thi công kết cấu thép nhà xưởng", unit: "tấn", quantity: 120, unitPrice: 28_500_000 },
  ]);
  const quote = await api.post("/api/quotes", {
    headers: sale,
    data: {
      opportunityId: created.opportunityId,
      method: "Boq",
      packageDescription: "Nhà xưởng thép Tân Uyên",
      projectBoqRevisionId: boq.id,
      vatPercent: 8,
    },
  });
  expect(quote.status(), await quote.text()).toBe(201);
  const body = (await quote.json()) as { id: number; code: string };
  created.quoteId = body.id;
  created.code = body.code;
}

async function removeSalesQuote(api: APIRequestContext, admin: Headers, created: Created) {
  if (created.quoteId) await hardDeleteBusinessRoot(api, admin, `/api/quotes/${created.quoteId}`);
  if (created.opportunityId)
    await hardDeleteBusinessRoot(api, admin, `/api/opportunities/${created.opportunityId}`);
  if (created.projectId)
    await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${created.projectId}`);
  if (created.customerId)
    await hardDeleteBusinessRoot(api, admin, `/api/customers/${created.customerId}`);
}

test("a quote is approved step by step along the configured chain", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(240_000);
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const sale = { Authorization: `Bearer ${await loginAs(TEST_USERS.sale)}` };
  const original = await applyQuoteChain(api, admin, [
    { name: "Trưởng phòng Cung ứng duyệt", approverRoleCode: "PROCUREMENT_MANAGER" },
    { name: "Trưởng phòng Kinh doanh duyệt", approverRoleCode: "SALES_MANAGER" },
  ]);
  const created: Created = { customerId: 0, opportunityId: 0, projectId: 0, quoteId: 0, code: "" };

  try {
    await createSalesQuote(api, admin, sale, created);
    const { quoteId, code } = created;

    // Sales chooses one approver for each step.
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.sale);
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Gửi duyệt" }).click();
    const send = page.getByRole("dialog");
    await expect(send).toContainText("Bước 1 · Trưởng phòng Cung ứng");
    await expect(send).toContainText("Bước 2 · Trưởng phòng Kinh doanh");
    await expect(send.getByRole("button", { name: "Xem trước email" })).toBeDisabled();
    await send.getByTestId("quote-approval-approver").click();
    await page.getByRole("option", { name: /Nguyễn Thị Thu Trang/ }).click();
    await send.getByTestId("quote-approval-approver-1").click();
    await page.getByRole("option", { name: /Trần Quốc Huy/ }).click();
    await send.getByRole("button", { name: "Xem trước email" }).click();
    await expect(send.getByTestId("quote-approval-preview")).toBeVisible();
    await send.getByRole("button", { name: "Gửi duyệt" }).click();
    await expect(send).toBeHidden();
    await expect(page.getByTestId("quote-approval-steps")).toContainText("Đang chờ duyệt");

    // Step 1 confirms its decision; the quote passes to step 2.
    await loginInBrowserAs(page, TEST_USERS.procurementManager);
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Duyệt", exact: true }).click();
    const confirmStep = page.getByRole("dialog");
    await expect(confirmStep).toContainText(`Duyệt bước 1/2 của báo giá ${code}`);
    await confirmStep.getByRole("button", { name: "Duyệt", exact: true }).click();
    await expect(
      page.getByText(`Báo giá ${code} chuyển cho Trần Quốc Huy duyệt tiếp`).first(),
    ).toBeVisible();
    await expect(page.getByTestId("quote-progress")).toContainText("bước duyệt 2/2");

    // Step 2 approves the quote.
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Duyệt", exact: true }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Duyệt", exact: true }).click();
    await expect(page.getByText(`Đã duyệt báo giá ${code}`).first()).toBeVisible();
    const approved = await (await api.get(`/api/quotes/${quoteId}`, { headers: admin })).json();
    expect(approved.status).toBe("Approved");
    expect((approved.approvalSteps as { state: string }[]).map((step) => step.state)).toEqual([
      "Approved",
      "Approved",
    ]);
  } finally {
    await api.put(`/api/workflows/${original.id}`, { headers: admin, data: original });
    await removeSalesQuote(api, admin, created);
  }
});

// When the person a waiting quote is with can no longer approve it, the quote says so and
// Sales takes it back to choose someone else, instead of it waiting forever.
test("a quote whose approver can no longer decide it is taken back by Sales", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(240_000);
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const sale = { Authorization: `Bearer ${await loginAs(TEST_USERS.sale)}` };
  const original = await applyQuoteChain(api, admin, [
    { name: "Trưởng phòng Cung ứng duyệt", approverRoleCode: "PROCUREMENT_MANAGER" },
  ]);
  const stamp = Date.now().toString().slice(-7);
  let approverId = 0;
  const created: Created = { customerId: 0, opportunityId: 0, projectId: 0, quoteId: 0, code: "" };

  try {
    // A Head of Procurement of its own, so the seeded one stays active for other tests.
    // Administrators here cannot manage users through the API, so it copies the seeded
    // one's role in SQL, as other fixtures do for setup without a public API.
    const inserted = execSql(
      `SET NOCOUNT ON; INSERT INTO users (PhoneNumber, FullName, Email, PasswordHash, Role, IsActive, RoleEntityId)
       OUTPUT INSERTED.Id
       SELECT N'0917${stamp}', N'Lâm Quốc Bảo ${stamp}', N'quoc.bao.${stamp}@e2e.nihome.local',
              PasswordHash, Role, 1, RoleEntityId
       FROM users WHERE PhoneNumber = N'${TEST_USERS.procurementManager.phoneNumber}'`,
    );
    approverId = Number(inserted.trim().split(/\s+/)[0]);
    expect(approverId, inserted).toBeGreaterThan(0);

    await createSalesQuote(api, admin, sale, created);
    const { quoteId, code } = created;
    const draft = await (await api.get(`/api/quotes/${quoteId}`, { headers: sale })).json();
    const preview = await api.post(`/api/quotes/${quoteId}/approval-email-preview`, {
      headers: sale,
      data: { approverUserIds: [approverId], rowVersion: draft.rowVersion },
    });
    expect(preview.status(), await preview.text()).toBe(200);
    const submitted = await api.post(`/api/quotes/${quoteId}/submit`, {
      headers: sale,
      data: {
        approverUserIds: [approverId],
        rowVersion: draft.rowVersion,
        previewToken: (await preview.json()).previewToken,
      },
    });
    expect(submitted.status(), await submitted.text()).toBe(200);
    // The approver leaves NICON while the quote waits.
    execSql(`UPDATE users SET IsActive = 0 WHERE Id = ${approverId}`);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.sale);
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    const card = page.getByTestId("quote-approval-email-status");
    await expect(card).toContainText(
      `Lâm Quốc Bảo ${stamp} không còn đủ điều kiện duyệt báo giá này`,
    );
    await card.getByTestId("quote-withdraw-approval").click();
    // Taking back is not a deletion: the confirmation says what it does.
    const confirm = page.getByRole("alertdialog");
    await expect(confirm.getByRole("button", { name: "Xoá" })).toHaveCount(0);
    await confirm.getByRole("button", { name: "Rút lại báo giá" }).click();
    await expect(page.getByText(`Đã rút lại báo giá ${code} về Nháp`).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Gửi duyệt" })).toBeVisible();
    const back = await (await api.get(`/api/quotes/${quoteId}`, { headers: sale })).json();
    expect(back.status).toBe("Draft");
    expect(back.approvalApproverUserId ?? null).toBeNull();
  } finally {
    await api.put(`/api/workflows/${original.id}`, { headers: admin, data: original });
    await removeSalesQuote(api, admin, created);
    if (approverId) execSql(`DELETE FROM users WHERE Id = ${approverId}`);
  }
});
