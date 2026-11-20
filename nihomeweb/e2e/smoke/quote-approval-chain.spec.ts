import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { approveProjectBoq } from "../fixtures/projectBoq";

// Quotes follow the approval chain in Cấu hình → Luồng duyệt: the sender chooses one
// approver per step and the steps decide in turn. Rules and edge cases are covered by
// QuotesControllerTests; this checks the screens people use for it.
test("a quote is approved step by step along the configured chain", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(240_000);
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const sale = { Authorization: `Bearer ${await loginAs(TEST_USERS.sale)}` };
  const suffix = Math.random().toString(36).slice(2, 8);
  const workflows = (await (
    await api.get("/api/workflows?includeInactive=true", { headers: admin })
  ).json()) as { id: number; module: string; action: string }[];
  const original = workflows.find((w) => w.module === "quotes" && w.action === "approve")!;
  let customerId = 0;
  let opportunityId = 0;
  let projectId = 0;
  let quoteId = 0;

  try {
    const chain = await api.put(`/api/workflows/${original.id}`, {
      headers: admin,
      data: {
        ...original,
        steps: [
          {
            order: 1,
            name: "Trưởng phòng Cung ứng duyệt",
            approverRoleCode: "PROCUREMENT_MANAGER",
            slaHours: 24,
            requireAllApprovers: false,
          },
          {
            order: 2,
            name: "Trưởng phòng Kinh doanh duyệt",
            approverRoleCode: "SALES_MANAGER",
            slaHours: 24,
            requireAllApprovers: false,
          },
        ],
      },
    });
    expect(chain.status(), await chain.text()).toBe(200);

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
    customerId = converted.convertedCustomerId;
    opportunityId = converted.convertedOpportunityId;
    projectId = (
      await (await api.get(`/api/opportunities/${opportunityId}`, { headers: sale })).json()
    ).operationalProjectId;
    for (const targetStage of ["Qualification", "Proposal", "Negotiation"])
      await api.patch(`/api/opportunities/${opportunityId}/stage`, {
        headers: sale,
        data: { targetStage },
      });
    const boq = await approveProjectBoq(api, admin, projectId, [
      {
        name: "Thi công kết cấu thép nhà xưởng",
        unit: "tấn",
        quantity: 120,
        unitPrice: 28_500_000,
      },
    ]);
    const created = await api.post("/api/quotes", {
      headers: sale,
      data: {
        opportunityId,
        method: "Boq",
        packageDescription: "Nhà xưởng thép Tân Uyên",
        projectBoqRevisionId: boq.id,
        vatPercent: 8,
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    const code = ((await created.json()) as { id: number; code: string }).code;
    quoteId = (await created.json()).id;

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
    if (quoteId) await hardDeleteBusinessRoot(api, admin, `/api/quotes/${quoteId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, admin, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, admin, `/api/customers/${customerId}`);
  }
});
