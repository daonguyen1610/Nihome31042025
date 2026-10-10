import { randomUUID } from "node:crypto";
import path from "node:path";
import { expect, test, TEST_USERS } from "../fixtures/auth";

/**
 * Business pipeline behind the 2026-10 demo fixes, driven through the real
 * stack: lead → opportunity (+ project) → approved quote → contract, design
 * before contract (scenario B), and a won tender continuing through an
 * approved customer quote to a contract (scenario C).
 *
 * The records are kept (realistic names) so they can be shown live, so the
 * spec only runs with DEMO_FLOW=1. Set DEMO_SHOT_DIR to save one screenshot
 * per step for the customer report.
 */

type Body = Record<string, unknown>;
const shotDir = process.env.DEMO_SHOT_DIR;

test("sales, design-first and tender flows reach a contract without dead ends", async ({
  api,
  loginAs,
  loginInBrowserAs,
  page,
}) => {
  test.skip(!process.env.DEMO_FLOW, "Creates demo records that are kept; run with DEMO_FLOW=1.");
  test.setTimeout(300_000);
  await page.setViewportSize({ width: 1440, height: 900 });

  const tokens = new Map<string, string>();
  const token = async (name: keyof typeof TEST_USERS) => {
    if (!tokens.has(name)) tokens.set(name, await loginAs(TEST_USERS[name]));
    return tokens.get(name)!;
  };
  const call = async (
    name: keyof typeof TEST_USERS,
    method: "get" | "post" | "put" | "patch",
    url: string,
    data?: unknown,
    multipart?: Record<string, { name: string; mimeType: string; buffer: Buffer }>,
  ) => {
    const headers = { Authorization: `Bearer ${await token(name)}`, "Idempotency-Key": randomUUID() };
    const response = await api[method](url, multipart ? { headers, multipart } : { headers, data });
    expect(response.ok(), `${method.toUpperCase()} ${url}: ${await response.text()}`).toBeTruthy();
    const text = await response.text();
    return (text ? JSON.parse(text) : {}) as Body;
  };
  const shot = async (file: string, clip?: { x: number; y: number; width: number; height: number }) => {
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(400);
    if (shotDir) await page.screenshot({ path: path.join(shotDir, file), clip });
  };
  const phone = () => `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`;
  const convertLead = async (name: string, company: string) => {
    const lead = await call("salesManager", "post", "/api/leads", {
      name, phone: phone(), companyName: company, sourceCode: "marketing",
    });
    const converted = await call("salesManager", "post", `/api/leads/${lead.id}/convert`, {
      address: "KCN Tân Uyên, Bình Dương", representativeName: name,
    });
    const opportunity = await call("salesManager", "get", `/api/opportunities/${converted.convertedOpportunityId}`);
    return {
      customerId: converted.convertedCustomerId as number,
      opportunityId: converted.convertedOpportunityId as number,
      projectId: opportunity.operationalProjectId as number,
    };
  };

  await loginInBrowserAs(page, TEST_USERS.salesManager);

  // ---- 1. Lead conversion opens the project -------------------------------
  const lead = await call("salesManager", "post", "/api/leads", {
    name: "Ông Lê Văn Thành",
    phone: phone(),
    email: `thanh.le.${Date.now()}@saomaifood.vn`,
    companyName: "Công ty TNHH Thực phẩm Sao Mai",
    sourceCode: "marketing",
  });
  await page.goto(`/admin/leads/${lead.id}`);
  await page.getByRole("button", { name: "Chuyển thành Khách hàng" }).first().click();
  await page.locator("#convert-address").fill("Lô C3, KCN Tân Uyên, Bình Dương");
  await page.locator("#convert-representative").fill("Lê Văn Thành");
  await shot("01-chuyen-doi-lead.png");
  await page.getByRole("dialog").getByRole("button", { name: "Chuyển thành Khách hàng" }).click();
  await expect.poll(async () => (await call("salesManager", "get", `/api/leads/${lead.id}`)).status).toBe("Converted");
  const convertedLead = await call("salesManager", "get", `/api/leads/${lead.id}`);
  const customerId = convertedLead.convertedCustomerId as number;
  const opportunityId = convertedLead.convertedOpportunityId as number;
  let opportunity = await call("salesManager", "get", `/api/opportunities/${opportunityId}`);
  const projectId = opportunity.operationalProjectId as number;
  expect(projectId, "conversion opens the operational project").toBeTruthy();

  await page.goto(`/admin/opportunities/${opportunityId}`);
  await expect(page.getByTestId("opportunity-create-quote")).toBeVisible();
  await shot("02-co-hoi-tao-bao-gia.png");

  await page.goto(`/admin/operational-projects/${projectId}`);
  await expect(page.getByText("Công ty TNHH Thực phẩm Sao Mai").first()).toBeVisible();
  await shot("03-du-an-tu-sinh.png");

  // ---- 2. Quote without a catalog is guided, not blocked ------------------
  await page.route(/\/api\/material-rate-catalogs\?/, (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.goto(`/admin/quotes?create=1&opportunityId=${opportunityId}`);
  await expect(page.getByTestId("quote-catalog-empty-InvestmentRate")).toBeVisible();
  await shot("04-bao-gia-huong-dan-tao-danh-muc.png");
  await page.getByTestId("quote-catalog-setup-InvestmentRate").click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await shot("05-tao-danh-muc-tu-bao-gia.png");
  await page.keyboard.press("Escape");
  await expect(page.getByTestId("material-rates-quote-return")).toBeVisible();
  await shot("06-quay-lai-bao-gia.png");
  await page.unroute(/\/api\/material-rate-catalogs\?/);

  // ---- 3. Approved quote → contract -----------------------------------------
  for (const targetStage of ["Qualification", "Proposal"]) {
    opportunity = await call("salesManager", "patch", `/api/opportunities/${opportunityId}/stage`, {
      targetStage, rowVersion: opportunity.rowVersion,
    });
  }
  let quote = await call("salesManager", "post", "/api/quotes", {
    opportunityId,
    method: "Boq",
    packageDescription: "Thiết kế và thi công nhà máy chế biến Sao Mai — giai đoạn 1",
    items: [
      { itemCode: "MONG-01", name: "Móng bê tông cốt thép M250", unit: "m3", quantity: 320, unitPrice: 2_850_000 },
      { itemCode: "KC-02", name: "Kết cấu thép tiền chế nhà xưởng", unit: "kg", quantity: 54_000, unitPrice: 21_500 },
      { itemCode: "MAI-03", name: "Mái tôn cách nhiệt PU 50mm", unit: "m2", quantity: 4_800, unitPrice: 385_000 },
    ],
    discountPercent: 2,
    vatPercent: 8,
  });
  quote = await call("salesManager", "post", `/api/quotes/${quote.id}/submit`, { rowVersion: quote.rowVersion });
  quote = await call("salesManager", "post", `/api/quotes/${quote.id}/approve`, { rowVersion: quote.rowVersion });

  await page.goto(`/admin/quotes/${quote.id}`);
  await expect(page.getByTestId("quote-link-contract")).toBeVisible();
  await shot("07-bao-gia-da-duyet.png");

  await page.goto(`/admin/opportunities/${opportunityId}`);
  await page.getByTestId("opportunity-quotes-tab").click();
  await expect(page.getByRole("tabpanel")).toContainText(quote.code as string);
  await shot("08-tab-bao-gia-cua-co-hoi.png");

  await page.goto(`/admin/quotes/${quote.id}`);
  await page.getByRole("button", { name: "Tạo hợp đồng từ báo giá" }).click();
  await expect(page).toHaveURL(/fromQuote=/);
  await expect(page.getByRole("dialog")).toBeVisible();
  await shot("09-hop-dong-tao-tu-bao-gia.png");
  await page.keyboard.press("Escape");

  // Customer contracts keep their approved source quotation at creation.
  const drafted = await call("salesManager", "post", "/api/contracts", {
    customerId,
    opportunityId,
    quoteId: quote.id,
    direction: "Upstream",
    type: "DesignAndBuild",
    value: quote.grandTotal,
    scopeOfWork: "Thiết kế và thi công nhà máy chế biến Sao Mai — giai đoạn 1",
  });
  await page.goto(`/admin/contracts/${drafted.id}`);
  await expect(page.getByRole("link", { name: quote.code as string })).toBeVisible();
  await shot("11-hop-dong-co-bao-gia-nguon.png");
  const linked = await call("salesManager", "get", `/api/contracts/${drafted.id}`);
  expect(linked.quoteId).toBe(quote.id);
  expect(linked.operationalProjectId).toBe(projectId);

  await page.goto(`/admin/operational-projects/${projectId}`);
  const contractRow = page.getByText(drafted.contractNumber as string).first();
  await expect(contractRow).toBeVisible();
  await contractRow.scrollIntoViewIfNeeded();
  await page.mouse.wheel(0, -250);
  await shot("12-du-an-du-lich-su-ban-hang.png");
  const project = await call("salesManager", "get", `/api/operational-projects/${projectId}`);
  expect([project.opportunityCount, project.quoteCount, project.contractCount]).toEqual([1, 1, 1]);

  await page.goto(`/admin/opportunities/${opportunityId}`);
  await page.getByRole("tab", { name: /Audit log/ }).click();
  await expect(page.getByRole("tabpanel")).toContainText(/Lê Văn Thành|Quốc Huy|Trần/);
  await shot("13-lich-su-thay-doi-co-hoi.png");

  await page.goto("/admin/opportunities");
  await page.getByRole("button", { name: /Pipeline|Phễu/ }).first().click();
  await expect(page.getByText("Tiếp cận").first()).toBeVisible();
  // Column headers only: the cards below include other teams' test records.
  await shot("14-pheu-ban-hang-thuat-ngu-khach-hang.png", { x: 330, y: 95, width: 1070, height: 320 });

  // ---- 4. Design before contract (scenario B) ------------------------------
  const pharma = await convertLead("Bà Nguyễn Thu Hà", "Công ty CP Dược phẩm An Khang");
  const users = await call("superAdmin", "get", "/api/users?take=200");
  const designLeadId = ((users.items as Body[]).find((user) => user.phoneNumber === TEST_USERS.designLead.phoneNumber)!).id;
  await call("salesManager", "post", `/api/operational-projects/${pharma.projectId}/team/members`, {
    userId: designLeadId,
    position: "Chủ trì thiết kế",
    startedAt: new Date(Date.now() - 86_400_000).toISOString(),
    roles: [{ roleCode: "DesignLead", scope: "Project" }],
  });
  const design = await call("salesManager", "post", "/api/design-projects", {
    name: "Thiết kế ý tưởng nhà máy dược An Khang",
    customerId: pharma.customerId,
    operationalProjectId: pharma.projectId,
    designLeadUserId: designLeadId,
  });
  expect(design.contractId).toBeNull();
  await loginInBrowserAs(page, TEST_USERS.designLead);
  await page.goto(`/admin/design-projects/${design.id}`);
  await shot("16-du-an-thiet-ke-chua-co-hop-dong.png");

  // ---- 5. Won tender continues to a contract (scenario C) ------------------
  await loginInBrowserAs(page, TEST_USERS.salesManager);
  const investor = await call("salesManager", "post", "/api/customers", {
    type: "Company",
    name: "Công ty CP Đầu tư Hạ tầng Long Hậu",
    taxId: `0316${Math.floor(100_000 + Math.random() * 899_999)}`,
    address: "KCN Long Hậu, Cần Giuộc, Long An",
    representativeName: "Phạm Quốc Bảo",
    sourceCode: "referral",
    primaryContact: { fullName: "Ông Phạm Quốc Bảo", phone: phone(), email: `bao.pham.${Date.now()}@longhau.vn`, isPrimary: true },
  });
  let tender = await call("salesManager", "post", "/api/tenders", {
    name: "Gói thầu thiết kế và thi công kho lạnh Long Hậu",
    customerId: investor.id,
    openingDate: new Date(Date.now() + 2 * 86_400_000).toISOString(),
    submissionDeadline: new Date(Date.now() + 10 * 86_400_000).toISOString(),
    infoSource: "Thư mời thầu",
  });
  for (const item of tender.checklistItems as Body[]) {
    await call("salesManager", "patch", `/api/tenders/${tender.id}/checklist/${item.id}`, { status: "Done" });
  }
  const csv = [
    "ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note",
    "KL-01,Panel cách nhiệt kho lạnh 150mm,m2,3200,610000,720000,8,",
    "KL-02,Hệ thống máy lạnh trung tâm,bộ,2,1450000000,1680000000,8,",
    "KL-03,Nền bê tông chịu tải chống thấm,m2,2400,520000,610000,8,",
  ].join("\r\n");
  const imported = await call("salesManager", "post", `/api/tenders/${tender.id}/estimates/import`, undefined, {
    file: { name: "du-toan-du-thau-long-hau.csv", mimeType: "text/csv", buffer: Buffer.from(csv, "utf8") },
  });
  const revisionId = (imported.revision as Body).id;
  await call("salesManager", "post", `/api/tenders/${tender.id}/estimates/${revisionId}/submit`);
  await call("salesManager", "post", `/api/tenders/${tender.id}/estimates/${revisionId}/approve`);
  tender = await call("salesManager", "post", `/api/tenders/${tender.id}/transition`, { status: "Submitted" });

  await page.goto(`/admin/tenders/${tender.id}`);
  await page.getByRole("tab", { name: /Kết quả/ }).click();
  await page.getByRole("button", { name: /Đánh dấu Trúng/ }).first().click();
  await expect(page.getByTestId("tender-won-mode-new")).toHaveAttribute("aria-checked", "true");
  await shot("17-trung-thau-tao-co-hoi.png");
  await page.getByTestId("tender-won-confirm").click();
  await expect(page.getByRole("link", { name: tender.name as string })).toBeVisible();
  await shot("18-ket-qua-trung-thau.png");
  tender = await call("salesManager", "get", `/api/tenders/${tender.id}`);
  const tenderOpportunityId = tender.wonOpportunityId as number;
  const tenderOpportunity = await call("salesManager", "get", `/api/opportunities/${tenderOpportunityId}`);
  expect(tenderOpportunity.stage).toBe("Negotiation");
  expect(tenderOpportunity.operationalProjectId).toBeTruthy();

  let tenderQuote = await call("salesManager", "post", "/api/quotes", {
    opportunityId: tenderOpportunityId,
    method: "Boq",
    packageDescription: "Thi công kho lạnh Long Hậu theo kết quả trúng thầu",
    items: [{ itemCode: "KL-01", name: "Panel cách nhiệt kho lạnh", unit: "m2", quantity: 3200, unitPrice: 720000 }],
    discountPercent: 0,
    vatPercent: 8,
  });
  tenderQuote = await call("salesManager", "post", `/api/quotes/${tenderQuote.id}/submit`, { rowVersion: tenderQuote.rowVersion });
  tenderQuote = await call("salesManager", "post", `/api/quotes/${tenderQuote.id}/approve`, { rowVersion: tenderQuote.rowVersion });

  await page.getByRole("link", { name: tender.name as string }).click();
  await expect(page.getByTestId("opportunity-create-contract")).toBeVisible();
  await shot("19-co-hoi-tu-goi-thau.png");
  await page.getByTestId("opportunity-create-contract").click();
  await expect(page).toHaveURL(/fromQuote=/);
  await expect(page.getByRole("dialog")).toBeVisible();
  await shot("20-hop-dong-tu-goi-thau.png");

  await page.goto("/admin/tenders");
  await shot("21-danh-sach-goi-thau.png");
});
