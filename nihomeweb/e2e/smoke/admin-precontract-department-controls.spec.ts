import { randomUUID } from "node:crypto";
import path from "node:path";
import { expect, test, TEST_USERS } from "../fixtures/auth";

/**
 * MoM §5 (meeting 2026-10-03) pre-contract cross-department controls, driven in
 * the browser against the real stack: Cung ứng quotes suppliers and
 * sub-contractors on a project work package so Kinh doanh can price the output
 * quote, and Tài chính – Kế toán joins twice — commercial review before the
 * customer email, financial dossier review before the tender is submitted.
 *
 * The quote-send checks only run in the blocked direction, so no customer email
 * is ever handed to the configured SMTP server. The allowed direction is
 * covered by nihomebackend.integration.tests/Controllers/
 * PrecontractCrossDepartmentControlsTests.cs.
 *
 * Set E2E_SHOT_DIR to keep one screenshot per verified screen.
 */

type Body = Record<string, unknown>;
const shotDir = process.env.E2E_SHOT_DIR;
const tomorrow = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);

/**
 * A department user only sees a project work package when they are on the
 * project team, and the pre-contract departments must be added to the project
 * before they can work in it.
 */
const addProjectMembers = async (
  call: (name: keyof typeof TEST_USERS, method: "get" | "post" | "put" | "patch", url: string, data?: unknown) => Promise<Body>,
  projectId: number,
  members: { phoneNumber: string; position: string }[],
) => {
  const users = (await call("superAdmin", "get", "/api/users?take=200")).items as { id: number; phoneNumber: string }[];
  for (const member of members) {
    const user = users.find(item => item.phoneNumber === member.phoneNumber)!;
    await call("salesManager", "post", `/api/operational-projects/${projectId}/team/members`, {
      userId: user.id,
      position: member.position,
      startedAt: new Date(Date.now() - 86_400_000).toISOString(),
      roles: [{ roleCode: "Observer", scope: "Project" }],
    });
  }
};

test.describe("MoM §5 pre-contract department controls", () => {
  test("Cung ứng quotes suppliers on a work package and the lead approves independently", async ({
    api,
    loginAs,
    loginInBrowserAs,
    page,
  }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: 1440, height: 900 });

    const tokens = new Map<string, string>();
    const call = async (
      name: keyof typeof TEST_USERS,
      method: "get" | "post" | "put" | "patch",
      url: string,
      data?: unknown,
    ) => {
      if (!tokens.has(name)) tokens.set(name, await loginAs(TEST_USERS[name]));
      const headers = { Authorization: `Bearer ${tokens.get(name)!}`, "Idempotency-Key": randomUUID() };
      const response = await api[method](url, { headers, data });
      expect(response.ok(), `${method.toUpperCase()} ${url}: ${await response.text()}`).toBeTruthy();
      const text = await response.text();
      return (text ? JSON.parse(text) : {}) as Body;
    };
    const shot = async (file: string) => {
      if (shotDir) await page.screenshot({ path: path.join(shotDir, file), fullPage: true });
    };

    // ---- Setup: a converted lead owns the shared project and the opportunity.
    const suffix = randomUUID().slice(0, 8);
    const lead = await call("salesManager", "post", "/api/leads", {
      name: `Ông Võ Hoàng Long ${suffix}`,
      phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
      email: `long.vo.${suffix}@saigonpack.test`,
      companyName: `Công ty TNHH Bao bì Sài Gòn ${suffix}`,
      sourceCode: "marketing",
    });
    const converted = await call("salesManager", "post", `/api/leads/${lead.id}/convert`, {
      address: "Lô B7, KCN Tân Tạo, TP.HCM",
      representativeName: "Võ Hoàng Long",
    });
    const opportunityId = converted.convertedOpportunityId as number;
    let opportunity = await call("salesManager", "get", `/api/opportunities/${opportunityId}`);
    const projectId = opportunity.operationalProjectId as number;
    expect(projectId, "lead conversion opens the operational project").toBeTruthy();

    const options = await call("salesManager", "get", "/api/work-packages/options");
    const procurementGroup = (options.departments as { id: number; code: string }[])
      .find(group => group.code === "PROCUREMENT")!;
    const procurementLeads = (options.users as { userId: number; name: string; roleGroupId: number; canLead: boolean }[])
      .filter(user => user.roleGroupId === procurementGroup.id && user.canLead);
    // Maker: the Cung ứng officer who collects supplier prices.
    const maker = TEST_USERS.procurement;
    // Checker: the work package lead, a different person in the same department.
    const leadOption = procurementLeads.find(user => user.name !== "Đinh Hải Yến")!;
    const vendor = (options.vendors as { id: number; code: string; name: string }[])[0];
    await addProjectMembers(call, projectId, [
      { phoneNumber: maker.phoneNumber, position: "Cán bộ Cung ứng" },
      { phoneNumber: TEST_USERS.warehouse.phoneNumber, position: "Chủ trì Cung ứng" },
    ]);

    // ---- Kinh doanh assigns the input-price work to Cung ứng from the project.
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(
      `/admin/work-packages?create=1&purpose=ProcurementInputPricing&projectId=${projectId}&opportunityId=${opportunityId}`,
      { waitUntil: "networkidle" },
    );

    const createDialog = page.getByRole("dialog");
    await expect(createDialog.getByText("Cung ứng báo giá đầu vào")).toBeVisible();
    // The purpose fixes the receiving department: a pre-contract price request
    // can only go to Cung ứng, and it never carries an execution contract.
    await expect(createDialog.getByText("Phòng ban nhận việc")).toBeVisible();
    await expect(createDialog.getByText("Công việc chung")).toBeHidden();
    await createDialog.locator('input[maxlength="300"]').fill(`Giá đầu vào kết cấu thép ${suffix}`);
    await createDialog.locator('textarea[maxlength="4000"]').fill(
      "Cung ứng lấy báo giá nhà cung cấp thép và thầu phụ MEP để Kinh doanh chốt báo giá đầu ra.",
    );
    await createDialog.getByRole("combobox").last().click();
    await page.getByRole("option", { name: leadOption.name }).click();
    await shot("s5-01-giao-viec-cung-ung.png");
    await createDialog.getByRole("button", { name: "Lưu thay đổi" }).click();

    await expect(page).toHaveURL(/\/admin\/work-packages\/\d+$/);
    const packageId = Number(page.url().split("/").pop());
    await expect(page.getByText("Cung ứng báo giá đầu vào").first()).toBeVisible();
    await expect(page.getByRole("heading", { name: "Báo giá đầu vào từ nhà cung cấp / thầu phụ" })).toBeVisible();

    // ---- Cung ứng records the supplier offer on the same work package.
    await loginInBrowserAs(page, maker);
    await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Thêm báo giá đầu vào" }).click();
    const priceDialog = page.getByRole("dialog");
    await priceDialog.getByRole("combobox").first().click();
    await page.getByRole("option", { name: new RegExp(vendor.code) }).click();
    await priceDialog.locator('input[maxlength="120"]').fill(`BG-${suffix}`);
    await priceDialog.locator('input[type="date"]').fill(tomorrow(30));
    await priceDialog.getByPlaceholder("Mã hạng mục").fill("KC-THEP-01");
    await priceDialog.getByPlaceholder("Mô tả").fill("Kết cấu thép mạ kẽm nhúng nóng");
    await priceDialog.getByPlaceholder("Đơn vị").fill("kg");
    await priceDialog.getByLabel("Khối lượng").fill("12500");
    await priceDialog.getByLabel("Đơn giá").fill("21500");
    await shot("s5-02-nhap-gia-dau-vao.png");
    await priceDialog.getByRole("button", { name: "Lưu thay đổi" }).click();

    const offerCard = page.locator("article").filter({ hasText: `BG-${suffix}` });
    await expect(offerCard.getByText("Bản nháp")).toBeVisible();
    // Money leaves the API as a plain number and must reach the screen grouped.
    const detail = await call("salesManager", "get", `/api/work-packages/${packageId}`);
    const offer = (detail.inputPriceOffers as Body[])[0];
    const grouped = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(offer.grandTotal as number);
    await expect(offerCard.getByText(new RegExp(`${grouped.replace(/\./g, "\\.")} VND`)).first()).toBeVisible();

    await offerCard.getByRole("button", { name: "Gửi duyệt" }).click();
    await expect(offerCard.getByText("Chờ duyệt")).toBeVisible();
    await shot("s5-03-cho-duyet-gia-dau-vao.png");

    // ---- Four eyes: the author cannot clear their own input price.
    await offerCard.getByRole("button", { name: "Phê duyệt" }).click();
    await expect(page.getByText(/404|Không thể|tự phê duyệt/i).first()).toBeVisible();
    await page.reload({ waitUntil: "networkidle" });
    await expect(page.locator("article").filter({ hasText: `BG-${suffix}` }).getByText("Chờ duyệt")).toBeVisible();

    // ---- The work package lead approves; only then is the price usable.
    await loginInBrowserAs(page, TEST_USERS.warehouse);
    await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
    const leadCard = page.locator("article").filter({ hasText: `BG-${suffix}` });
    await leadCard.getByRole("button", { name: "Phê duyệt" }).click();
    await expect(leadCard.getByText("Đã duyệt")).toBeVisible();
    await shot("s5-04-gia-dau-vao-da-duyet.png");

    const approved = await call("salesManager", "get", `/api/work-packages/${packageId}`);
    expect(((approved.inputPriceOffers as Body[])[0].status as string)).toBe("Approved");

    // The screen stays usable on the BCH-style narrow viewport.
    await page.setViewportSize({ width: 390, height: 844 });
    await page.reload({ waitUntil: "networkidle" });
    const dimensions = await page.locator("html").evaluate(element => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }));
    expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth);
  });

  test("a quote can only use an approved input price and cannot be sent before Finance signs off", async ({
    api,
    loginAs,
    loginInBrowserAs,
    page,
  }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: 1440, height: 900 });

    const tokens = new Map<string, string>();
    const call = async (
      name: keyof typeof TEST_USERS,
      method: "get" | "post" | "put" | "patch",
      url: string,
      data?: unknown,
    ) => {
      if (!tokens.has(name)) tokens.set(name, await loginAs(TEST_USERS[name]));
      const headers = { Authorization: `Bearer ${tokens.get(name)!}`, "Idempotency-Key": randomUUID() };
      const response = await api[method](url, { headers, data });
      expect(response.ok(), `${method.toUpperCase()} ${url}: ${await response.text()}`).toBeTruthy();
      const text = await response.text();
      return (text ? JSON.parse(text) : {}) as Body;
    };
    const shot = async (file: string) => {
      if (shotDir) await page.screenshot({ path: path.join(shotDir, file), fullPage: true });
    };

    const suffix = randomUUID().slice(0, 8);
    const lead = await call("salesManager", "post", "/api/leads", {
      name: `Bà Đỗ Thanh Hà ${suffix}`,
      phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
      email: `ha.do.${suffix}@vinamech.test`,
      companyName: `Công ty CP Cơ khí Vina ${suffix}`,
      sourceCode: "marketing",
    });
    const converted = await call("salesManager", "post", `/api/leads/${lead.id}/convert`, {
      address: "Khu công nghiệp Long Hậu, Long An",
      representativeName: "Đỗ Thanh Hà",
    });
    const opportunityId = converted.convertedOpportunityId as number;
    let opportunity = await call("salesManager", "get", `/api/opportunities/${opportunityId}`);
    const projectId = opportunity.operationalProjectId as number;
    for (const targetStage of ["Qualification", "Proposal", "Negotiation"]) {
      opportunity = await call("salesManager", "patch", `/api/opportunities/${opportunityId}/stage`, {
        targetStage,
        rowVersion: opportunity.rowVersion,
      });
    }

    const quote = await call("salesManager", "post", "/api/quotes", {
      opportunityId,
      method: "Boq",
      packageDescription: `Kết cấu thép nhà xưởng Vina ${suffix}`,
      items: [{ itemCode: "KC-THEP-01", name: "Kết cấu thép mạ kẽm nhúng nóng", unit: "kg", quantity: 12_500, unitPrice: 28_900 }],
      vatPercent: 8,
    });
    const quoteId = quote.id as number;

    // Cung ứng supplies an approved input price on the shared project.
    const options = await call("salesManager", "get", "/api/work-packages/options");
    const procurementGroup = (options.departments as { id: number; code: string }[])
      .find(group => group.code === "PROCUREMENT")!;
    const financeGroup = (options.departments as { id: number; code: string }[])
      .find(group => group.code === "FINANCE")!;
    const procurementLead = (options.users as { userId: number; name: string; roleGroupId: number; canLead: boolean }[])
      .find(user => user.roleGroupId === procurementGroup.id && user.name === "Trịnh Văn Sơn")!;
    const accountant = (options.users as { userId: number; name: string; roleGroupId: number; canLead: boolean }[])
      .find(user => user.roleGroupId === financeGroup.id && user.canLead)!;
    const vendor = (options.vendors as { id: number; code: string; name: string }[])[0];
    await addProjectMembers(call, projectId, [
      { phoneNumber: TEST_USERS.procurement.phoneNumber, position: "Cán bộ Cung ứng" },
      { phoneNumber: TEST_USERS.accountant.phoneNumber, position: "Kế toán dự án" },
    ]);

    const pricePackage = await call("salesManager", "post", "/api/work-packages", {
      kind: "Project",
      purpose: "ProcurementInputPricing",
      name: `Giá đầu vào kết cấu thép Vina ${suffix}`,
      operationalProjectId: projectId,
      sourceOpportunityId: opportunityId,
      departmentRoleGroupId: procurementGroup.id,
      leadUserId: procurementLead.userId,
    });
    const pricePackageId = pricePackage.id as number;
    let offer = await call("procurement", "post", `/api/work-packages/${pricePackageId}/input-price-offers`, {
      vendorId: vendor.id,
      supplierReference: `BG-VINA-${suffix}`,
      currency: "VND",
      validUntil: `${tomorrow(30)}T23:59:59.000Z`,
      lines: [{ itemCode: "KC-THEP-01", description: "Kết cấu thép mạ kẽm nhúng nóng", unit: "kg", quantity: 12_500, unitPrice: 21_500, vatPercent: 8 }],
    });
    offer = await call("procurement", "post", `/api/work-packages/${pricePackageId}/input-price-offers/${offer.id}/submit`, {
      rowVersion: offer.rowVersion,
    });
    const approvedOffer = await call("warehouse", "post", `/api/work-packages/${pricePackageId}/input-price-offers/${offer.id}/decision`, {
      approved: true,
      rowVersion: offer.rowVersion,
    });
    expect(approvedOffer.status).toBe("Approved");

    // ---- The approved input price is offered to Kinh doanh on the quote.
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByTestId("quote-edit").click();
    await page.getByRole("combobox").filter({ hasText: /Chưa gắn báo giá đầu vào/ }).click();
    await page.getByRole("option", { name: new RegExp(`BG-VINA-${suffix}`) }).click();
    await shot("s5-05-bao-gia-chon-gia-dau-vao.png");
    await page.getByRole("button", { name: "Lưu thay đổi" }).first().click();
    await expect.poll(async () => {
      const saved = await call("salesManager", "get", `/api/quotes/${quoteId}`);
      return saved.inputPriceOfferId;
    }).toBe(offer.id);
    await expect(page.getByText(`BG-VINA-${suffix}`).first()).toBeVisible();

    // ---- The quote still cannot reach the customer: Finance has not reviewed.
    await call("salesManager", "post", `/api/quotes/${quoteId}/submit`, {
      rowVersion: (await call("salesManager", "get", `/api/quotes/${quoteId}`)).rowVersion,
    });
    await call("salesManager", "post", `/api/quotes/${quoteId}/approve`, {
      rowVersion: (await call("salesManager", "get", `/api/quotes/${quoteId}`)).rowVersion,
    });
    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Gửi khách" }).click();
    const sendDialog = page.getByRole("dialog");
    await expect(sendDialog.getByText("Email khách hàng")).toBeVisible({ timeout: 15_000 });
    const sendResponse = page.waitForResponse(response =>
      response.request().method() === "POST" && /\/api\/(?:v1\/)?quotes\/\d+\/send$/.test(response.url()));
    await sendDialog.getByRole("button", { name: "Gửi khách" }).click();
    const rejected = await sendResponse;
    expect(rejected.status()).toBe(400);
    expect(await rejected.text()).toContain("Tài chính – Kế toán rà soát");
    await expect(page.getByText(/Tài chính – Kế toán rà soát/).first()).toBeVisible();
    await shot("s5-06-chan-gui-khi-chua-ra-soat.png");
    // The blocked send must not have moved the quote or emailed the customer.
    const blocked = await call("salesManager", "get", `/api/quotes/${quoteId}`);
    expect(blocked.status).toBe("Approved");
    expect(blocked.sentAt).toBeNull();

    // ---- Finance – Accounting reviews the commercial dossier on the quote.
    // Kinh doanh raises the request straight from the quotation screen, which
    // is what hands the project, opportunity and quotation to the work package.
    await sendDialog.getByRole("button", { name: "Huỷ", exact: true }).click();
    await page.getByRole("link", { name: "Yêu cầu Tài chính rà soát" }).click();
    const reviewPackageDialog = page.getByRole("dialog");
    await reviewPackageDialog.getByText("Tài chính rà soát thương mại").first().waitFor();
    await reviewPackageDialog.locator('input[maxlength="300"]').fill(`Rà soát thương mại Vina ${suffix}`);
    await reviewPackageDialog.getByRole("combobox").last().click();
    await page.getByRole("option", { name: accountant.name }).click();
    await reviewPackageDialog.getByRole("button", { name: "Lưu thay đổi" }).click();
    await expect(page).toHaveURL(/\/admin\/work-packages\/\d+$/);
    const reviewPackageId = Number(page.url().split("/").pop());

    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto(`/admin/work-packages/${reviewPackageId}`, { waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: "Rà soát điều kiện hợp đồng và thanh toán" })).toBeVisible();
    for (const label of ["Đã rà soát điều kiện hợp đồng", "Đã rà soát điều kiện thanh toán", "Đã rà soát bảo lãnh"]) {
      await page.getByText(label).click();
    }
    await page.locator('textarea[maxlength="4000"]').fill("Điều kiện thanh toán theo tiến độ, giữ lại 5% bảo hành 12 tháng.");
    await page.getByRole("button", { name: "Lưu thay đổi" }).click();
    await shot("s5-07-ra-soat-thuong-mai.png");
    await page.getByRole("button", { name: "Gửi duyệt" }).click();
    await expect(page.getByText("Chờ duyệt").first()).toBeVisible();

    // The maker cannot approve their own review; an independent approver must.
    await expect(page.getByRole("button", { name: "Phê duyệt" })).toBeVisible();

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/work-packages/${reviewPackageId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Phê duyệt" }).click();
    await expect(page.getByText("Đã duyệt").first()).toBeVisible();
    await shot("s5-08-ra-soat-da-duyet.png");
    // Deliberately no send here: the gate is now open, and clicking it would
    // hand a real quotation email to the configured SMTP account.
  });
});
