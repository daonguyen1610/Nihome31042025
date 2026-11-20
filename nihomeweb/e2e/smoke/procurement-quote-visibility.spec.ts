import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { approveProjectBoq } from "../fixtures/projectBoq";

/**
 * NICON: the whole Procurement department, head and staff, sees every quote.
 * Only the chosen approver (the Procurement head) decides, and is told in-app
 * with a link to the quote, not only by email.
 */
test("Procurement sees every quote and the chosen approver is notified with a link", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(180_000);
  const sales = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const head = { Authorization: `Bearer ${await loginAs(TEST_USERS.procurementManager)}` };
  const officer = { Authorization: `Bearer ${await loginAs(TEST_USERS.procurementOfficer)}` };
  const suffix = Math.random().toString(36).slice(2, 10);
  let customerId = 0;
  let opportunityId = 0;
  let projectId = 0;
  let quoteId = 0;

  try {
    const lead = await api.post("/api/leads", {
      headers: sales,
      data: {
        name: `Nhà xưởng cơ khí Tân Uyên ${suffix}`,
        phone: `0913${Math.floor(100000 + Math.random() * 899999)}`,
        sourceCode: "marketing",
      },
    });
    expect(lead.status(), await lead.text()).toBe(201);
    const conversion = await api.post(`/api/leads/${(await lead.json()).id}/convert`, {
      headers: sales,
      data: { address: "KCN Nam Tân Uyên, Bình Dương", representativeName: "Trần Văn Hùng" },
    });
    expect(conversion.status(), await conversion.text()).toBe(200);
    ({ convertedCustomerId: customerId, convertedOpportunityId: opportunityId } =
      await conversion.json());
    projectId = (
      await (await api.get(`/api/opportunities/${opportunityId}`, { headers: sales })).json()
    ).operationalProjectId;
    for (const targetStage of ["Qualification", "Proposal", "Negotiation"]) {
      const moved = await api.patch(`/api/opportunities/${opportunityId}/stage`, {
        headers: sales,
        data: { targetStage },
      });
      expect(moved.status(), await moved.text()).toBe(200);
    }
    const boq = await approveProjectBoq(api, admin, projectId, [
      { name: "Kết cấu thép nhà xưởng", unit: "tấn", quantity: 40, unitPrice: 32_000_000 },
    ]);
    const created = await api.post("/api/quotes", {
      headers: sales,
      data: {
        opportunityId,
        method: "Boq",
        packageDescription: "Kết cấu thép và mái nhà xưởng Tân Uyên",
        projectBoqRevisionId: boq.id,
        vatPercent: 8,
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    const quote = (await created.json()) as { id: number; code: string; rowVersion: string };
    quoteId = quote.id;

    // Sales sends it for approval to the Procurement head.
    const candidates = (await (
      await api.get(`/api/quotes/${quoteId}/approval-candidates`, { headers: sales })
    ).json()) as { id: number; roleCode: string }[];
    const approver = candidates.find((candidate) => candidate.roleCode === "PROCUREMENT_MANAGER")!;
    expect(approver, "the Procurement head can be chosen").toBeTruthy();
    const preview = await api.post(`/api/quotes/${quoteId}/approval-email-preview`, {
      headers: sales,
      data: { approverUserId: approver.id, rowVersion: quote.rowVersion },
    });
    expect(preview.status(), await preview.text()).toBe(200);
    const submitted = await api.post(`/api/quotes/${quoteId}/submit`, {
      headers: sales,
      data: {
        approverUserId: approver.id,
        previewToken: (await preview.json()).previewToken,
        rowVersion: quote.rowVersion,
      },
    });
    expect(submitted.status(), await submitted.text()).toBe(200);

    // A Procurement officer sees the quote but cannot decide on it.
    const officerList = await api.get(`/api/quotes?search=${quote.code}`, { headers: officer });
    expect(officerList.status(), await officerList.text()).toBe(200);
    expect((await officerList.json()).items.map((item: { id: number }) => item.id)).toContain(
      quoteId,
    );
    const officerApprove = await api.post(`/api/quotes/${quoteId}/approve`, {
      headers: officer,
      data: { rowVersion: (await submitted.json()).rowVersion },
    });
    expect(officerApprove.status()).toBe(403);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.procurementOfficer);
    await page.goto("/admin/quotes", { waitUntil: "networkidle" });
    await page.getByRole("link", { name: quote.code }).first().click();
    await expect(page).toHaveURL(new RegExp(`/admin/quotes/${quoteId}$`));
    await expect(page.getByRole("button", { name: "Duyệt", exact: true })).toBeDisabled();
    // The disabled decision says who can take it.
    await expect(page.getByTestId("quote-approval-email-status")).toContainText(
      "(người duyệt được chọn) được Duyệt hoặc Từ chối duyệt báo giá này",
    );

    // The head gets an in-app notice that opens the quote, ready to approve.
    const notices = await (await api.get("/api/notifications?take=20", { headers: head })).json();
    const notice = (
      (Array.isArray(notices) ? notices : notices.items) as {
        title: string;
        linkUrl?: string;
      }[]
    ).find((item) => item.linkUrl === `/admin/quotes/${quoteId}`);
    expect(notice?.title).toContain(quote.code);
    await loginInBrowserAs(page, TEST_USERS.procurementManager);
    await page.goto("/admin/notifications", { waitUntil: "networkidle" });
    await page.getByText(notice!.title).first().click();
    await expect(page).toHaveURL(new RegExp(`/admin/quotes/${quoteId}$`));
    await expect(page.getByRole("button", { name: "Duyệt", exact: true })).toBeEnabled();

    // A rejection is confirmed and needs a reason; it goes back to Sales with it.
    await page.getByRole("button", { name: "Từ chối duyệt", exact: true }).click();
    const decision = page.getByRole("dialog");
    await expect(decision).toContainText("quay về Kinh doanh để sửa");
    const confirmReject = decision.getByRole("button", { name: "Từ chối duyệt", exact: true });
    await expect(confirmReject).toBeDisabled();
    await decision.getByRole("textbox").fill("Đơn giá kết cấu thép chưa cập nhật tháng 11");
    await confirmReject.click();
    await expect(decision).toBeHidden();
    await expect(
      page.getByText(`Đã trả báo giá ${quote.code} về Kinh doanh để sửa`).first(),
    ).toBeVisible();
    const rejected = await (await api.get(`/api/quotes/${quoteId}`, { headers: sales })).json();
    expect(rejected.status).toBe("Draft");
    expect(
      (rejected.approvalLogs as { note?: string }[]).some(
        (log) => log.note === "Đơn giá kết cấu thép chưa cập nhật tháng 11",
      ),
    ).toBe(true);
  } finally {
    if (quoteId) await hardDeleteBusinessRoot(api, admin, `/api/quotes/${quoteId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, admin, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, admin, `/api/customers/${customerId}`);
  }
});
