import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

/**
 * NICON: any Procurement member prepares input prices, and whoever sends them
 * for approval picks the approver from NICON's users (searchable), who alone
 * decides. Not limited to the work package's assigner.
 */
test("the sender picks the input price approver, who alone approves it", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(180_000);
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const officer = { Authorization: `Bearer ${await loginAs(TEST_USERS.procurementOfficer)}` };
  const writes = (headers: Record<string, string>) => ({
    ...headers,
    "Idempotency-Key": crypto.randomUUID(),
  });
  const unique = Date.now();
  let customerId = 0,
    projectId = 0,
    opportunityId = 0,
    packageId = 0;

  try {
    customerId = await createOwnCustomer(api, admin, `NICON giá đầu vào ${unique}`);
    let response = await api.post("/api/operational-projects", {
      headers: writes(admin),
      data: { name: `Nhà xưởng Long An ${unique}`, customerId },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    projectId = (await response.json()).id;
    response = await api.post("/api/opportunities", {
      headers: writes(admin),
      data: {
        name: `Hệ thống PCCC nhà xưởng Long An ${unique}`,
        customerId,
        operationalProjectId: projectId,
        estimatedValue: 800_000_000,
        winProbability: 50,
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    opportunityId = (await response.json()).id;
    const options = await (await api.get("/api/work-packages/options", { headers: admin })).json();
    const procurement = options.departments.find(
      (item: { code: string }) => item.code === "PROCUREMENT",
    );
    const manager = options.users.find(
      (item: { roleGroupId: number; roleCode: string }) =>
        item.roleGroupId === procurement.id && item.roleCode === "PROCUREMENT_MANAGER",
    );
    response = await api.post("/api/work-packages", {
      headers: writes(admin),
      data: {
        kind: "Project",
        purpose: "ProcurementInputPricing",
        name: `Báo giá đầu vào PCCC ${unique}`,
        operationalProjectId: projectId,
        sourceOpportunityId: opportunityId,
        departmentRoleGroupId: procurement.id,
        leadUserId: manager.userId,
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    packageId = (await response.json()).id;

    // The whole Procurement department hears about the request (and is emailed),
    // not only the assigner.
    const notices = await (
      await api.get("/api/notifications?take=50", { headers: officer })
    ).json();
    const items = (Array.isArray(notices) ? notices : notices.items) as {
      title: string;
      linkUrl?: string;
    }[];
    expect(
      items.some((item) => item.linkUrl === `/admin/work-packages/${packageId}`),
      "the officer is notified of the input price request",
    ).toBeTruthy();

    // A Procurement officer who is not the assigner adds the supplier quote.
    response = await api.post(`/api/work-packages/${packageId}/input-price-offers`, {
      headers: writes(officer),
      data: {
        vendorId: options.vendors[0].id,
        supplierReference: `BG-PCCC-${unique}`,
        currency: "VND",
        validUntil: new Date(Date.now() + 30 * 86_400_000).toISOString(),
        lines: [
          {
            itemCode: "PCCC-01",
            description: "Đầu phun sprinkler",
            unit: "cái",
            quantity: 300,
            unitPrice: 120_000,
            vatPercent: 10,
          },
        ],
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    const authorId = (await response.json()).createdByUserId as number;
    const author = options.users.find((item: { userId: number }) => item.userId === authorId) as {
      name: string;
    };
    const approvers = (await (
      await api.get(`/api/work-packages/${packageId}/approvers`, { headers: officer })
    ).json()) as { userId: number; name: string; email: string; isDepartmentLead: boolean }[];
    const accountant = approvers.find((item) => item.email.includes("accounting"))!;
    expect(accountant, "an accountant who may approve is required").toBeTruthy();
    expect(approvers[0].userId).toBe(manager.userId);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.procurementOfficer);
    await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
    const offerCard = page.locator("article").filter({ hasText: `BG-PCCC-${unique}` });
    await offerCard.getByRole("button", { name: "Gửi duyệt" }).click();

    // Step 1: confirm. Cancelling sends nothing.
    const confirm = page.getByRole("alertdialog");
    await expect(confirm).toContainText("chọn người duyệt");
    await confirm.getByRole("button", { name: "Huỷ" }).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await offerCard.getByRole("button", { name: "Gửi duyệt" }).click();
    await page.getByRole("alertdialog").getByRole("button", { name: "Tiếp tục" }).click();

    // Step 2: the approver defaults to the assigner; search to pick another person.
    const dialog = page.getByRole("dialog", { name: "Xem trước email gửi duyệt" });
    const picker = dialog.getByRole("combobox", { name: "Người duyệt" });
    await expect(picker).toContainText(manager.name);
    await expect(dialog.getByLabel("Người nhận", { exact: true })).toHaveValue(manager.email);
    await picker.click();
    const search = page.getByPlaceholder("Gõ tên, email hoặc phòng ban để tìm");
    await search.fill("không-có-người-này");
    await expect(page.getByText("Không tìm thấy người duyệt phù hợp")).toBeVisible();
    await search.fill(accountant.name.split(" ").slice(-2).join(" "));
    await page.getByRole("option", { name: new RegExp(accountant.name) }).click();
    // The author is never offered as their own approver.
    await picker.click();
    await search.fill(author.name);
    await expect(page.getByRole("option", { name: new RegExp(author.name) })).toHaveCount(0);
    await page.keyboard.press("Escape");
    await expect(dialog.getByLabel("Người nhận", { exact: true })).toHaveValue(accountant.email);
    await expect(dialog.frameLocator("iframe").locator("body")).toContainText(accountant.name);

    // CC suggests NICON people while typing, like a mail client; outside
    // addresses can still be typed. Accents are optional when searching.
    const cc = dialog.getByRole("combobox", { name: "CC (không bắt buộc)" });
    const send = dialog.getByRole("button", { name: "Gửi duyệt và thông báo", exact: true });
    const managerSurname = manager.name.split(" ").pop()!;
    await cc.fill(managerSurname.normalize("NFD").replace(/[\u0300-\u036f]/g, ""));
    await expect(page.getByRole("option", { name: new RegExp(manager.email) })).toBeVisible();
    await cc.press("Enter");
    const chips = dialog.getByTestId("email-recipient-chip");
    await expect(chips).toHaveCount(1);
    await expect(chips.first()).toHaveText(manager.name);
    // Someone already in CC is not suggested again.
    await cc.fill(managerSurname);
    await expect(page.getByRole("option", { name: new RegExp(manager.email) })).toHaveCount(0);
    await cc.fill("doitac.pccc@example.com,");
    await expect(chips).toHaveCount(2);
    // A malformed address is flagged and blocks sending until removed.
    await cc.fill("sai-email");
    await cc.press("Enter");
    await expect(chips).toHaveCount(3);
    await expect(dialog.getByRole("alert")).toContainText("Nhập To và CC là email hợp lệ");
    await expect(send).toBeDisabled();
    await dialog.getByRole("button", { name: "Bỏ sai-email khỏi CC" }).click();
    await expect(chips).toHaveCount(2);
    await dialog.getByRole("button", { name: "Cập nhật xem trước" }).click();
    await expect(send).toBeEnabled();
    await expect(chips).toHaveCount(2);
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      expect(
        await dialog.evaluate((element) => element.scrollWidth <= element.clientWidth),
      ).toBeTruthy();
      await dialog.screenshot({ path: testInfo.outputPath(`chosen-approver-${width}.png`) });
    }
    await dialog.getByRole("button", { name: "Gửi duyệt và thông báo", exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await expect(offerCard.getByText(`Chờ ${accountant.name} duyệt`)).toBeVisible();

    // The assigner was not chosen: no decision for them.
    await loginInBrowserAs(page, TEST_USERS.procurementManager);
    await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
    const managerCard = page.locator("article").filter({ hasText: `BG-PCCC-${unique}` });
    await expect(managerCard.getByText(`Chờ ${accountant.name} duyệt`)).toBeVisible();
    await expect(managerCard.getByRole("button", { name: "Phê duyệt" })).toHaveCount(0);

    // The chosen approver, outside Procurement and the project, opens it and approves.
    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
    const approverCard = page.locator("article").filter({ hasText: `BG-PCCC-${unique}` });
    await approverCard.getByRole("button", { name: "Phê duyệt" }).click();
    await expect(approverCard.getByText("Đã duyệt")).toBeVisible();
    const detail = await (
      await api.get(`/api/work-packages/${packageId}`, { headers: admin })
    ).json();
    expect(detail.inputPriceOffers[0]).toMatchObject({
      status: "Approved",
      approvalApproverUserId: accountant.userId,
    });
  } finally {
    if (packageId) await hardDeleteBusinessRoot(api, admin, `/api/work-packages/${packageId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, admin, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, admin, `/api/customers/${customerId}`);
  }
});
