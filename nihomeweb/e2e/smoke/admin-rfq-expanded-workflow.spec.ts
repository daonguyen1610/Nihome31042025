import { expect, test, TEST_USERS } from "../fixtures/auth";

const payloadLines = (payload: unknown): unknown[] => {
  if (!payload || typeof payload !== "object" || !("lines" in payload) || !Array.isArray(payload.lines)) return [];
  return payload.lines;
};

test("authorized users score bids and submit a split award plan", async ({ page, loginInBrowserAs, baseURL }) => {
  const projectId = 166900;
  const rfqId = 166901;
  const captured: { evaluation?: Record<string, unknown>; award?: Record<string, unknown> } = {};
  const header = { id: rfqId, operationalProjectId: projectId, code: "RFQ-SPLIT-001", title: "Split cable supply",
    projectCode: "PJ-SPLIT", projectName: "Split Award Project", customerName: "NICON Customer",
    sourceBoqRevisionId: 10, boqRevision: 1, status: "UnderEvaluation", ownerUserId: 1, ownerName: "Procurement Owner",
    invitedCount: 2, receivedCount: 2, issuedAt: "2026-09-01T00:00:00Z", dueAt: "2035-01-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z", overdue: false, rowVersion: "row-1" };
  const detail = { header, currency: "VND", note: null,
    lines: [{ id: 11, projectBoqLineId: 21, itemCode: "CABLE-01", description: "Power cable", unit: "m", quantity: 100, budgetUnitPrice: 200000, lowestUnitPrice: 175000 }],
    vendors: [{ id: 31, name: "Vendor A", type: "Supplier", isActive: true }, { id: 32, name: "Vendor B", type: "Supplier", isActive: true }],
    bids: [
      { id: 41, vendorId: 31, revision: 1, leadTimeDays: 7, paymentTerms: "Net 30", validUntil: "2035-01-02T00:00:00Z", note: null, submittedAt: "2026-09-01T00:00:00Z", submittedBy: "Procurement Owner", withdrawnAt: null, isCurrent: true, isComplete: true, isEligible: true, isLowest: false, total: 18000000, currency: "VND", exchangeRateToVnd: 1, subtotal: 18000000, freightAmount: 0, discountPercent: 0, discountAmount: 0, vatPercent: 0, totalOriginal: 18000000, commercialScore: null, weightedScore: null, evaluationNote: null, submittedViaPortal: false, lines: [{ rfqLineId: 11, unitPrice: 180000, amount: 18000000, unitPriceVnd: 180000, amountVnd: 18000000 }] },
      { id: 42, vendorId: 32, revision: 1, leadTimeDays: 10, paymentTerms: "Net 15", validUntil: "2035-01-02T00:00:00Z", note: null, submittedAt: "2026-09-01T00:00:00Z", submittedBy: "Procurement Owner", withdrawnAt: null, isCurrent: true, isComplete: true, isEligible: true, isLowest: true, total: 17500000, currency: "VND", exchangeRateToVnd: 1, subtotal: 17500000, freightAmount: 0, discountPercent: 0, discountAmount: 0, vatPercent: 0, totalOriginal: 17500000, commercialScore: 80, weightedScore: 84, evaluationNote: "Commercial review", submittedViaPortal: false, lines: [{ rfqLineId: 11, unitPrice: 175000, amount: 17500000, unitPriceVnd: 175000, amountVnd: 17500000 }] },
    ], events: [], documents: [], selectedBidId: null, contractId: null, contractNumber: null, awardedAt: null, awardedBy: null, awardReason: null, awardSnapshotJson: null,
    scoring: { priceWeight: 50, leadTimeWeight: 20, vendorRatingWeight: 20, commercialWeight: 10 }, awards: [],
    materialRequests: [{ lineId: 51, materialRequestId: 52, materialRequestCode: "MR-001", projectBoqLineId: 21, requestedQuantity: 100, alreadyAllocatedQuantity: 0, remainingQuantity: 100 }],
  };
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(baseURL ?? "http://localhost:5043");
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ role: "SUPER_ADMIN", permissions: ["proc.rfqs.view", "proc.rfqs.manage", "proc.rfqs.award", "proc.rfqs.export"] }) }));
  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 100, items: [{ id: projectId, code: "PJ-SPLIT", name: "Split Award Project", customerName: "NICON Customer", status: "Active" }] }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/rfqs/references$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ revisions: [], vendors: detail.vendors, owners: [], documents: [] }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/rfqs/${rfqId}$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/rfqs(?:\\?.*)?$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 20, items: [header] }) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/rfqs/${rfqId}/bids/evaluate$`), route => { captured.evaluation = route.request().postDataJSON(); return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) }); });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/rfqs/${rfqId}/award-batch$`), route => { captured.award = route.request().postDataJSON(); return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ...detail, header: { ...header, status: "Awarded", rowVersion: "row-2" } }) }); });

  await page.goto(`${baseURL ?? "http://localhost:5043"}/admin/procurement-control/rfqs?projectId=${projectId}&rfqId=${rfqId}`);
  await page.getByRole("button", { name: "Evaluate bids" }).click();
  const evaluation = page.getByRole("dialog");
  await evaluation.getByLabel("Commercial score (0-100)").fill("88");
  await evaluation.getByLabel("Evaluation evidence").fill("Commercial terms and delivery evidence reviewed.");
  await evaluation.getByRole("button", { name: "Save evaluation" }).click();
  await expect.poll(() => captured.evaluation).toBeDefined();

  await page.getByRole("button", { name: "Award and create draft contract" }).click();
  const award = page.getByRole("dialog");
  await award.getByRole("button", { name: "Add allocation" }).click();
  await award.getByLabel("Quantity").nth(0).fill("40");
  await award.getByLabel("Quantity").nth(1).fill("60");
  await award.getByLabel("Vendor").nth(1).selectOption("42");
  await award.getByLabel("Material Request demand").nth(1).selectOption("51");
  await award.getByLabel("Decision / withdrawal reason").fill("Split capacity across qualified suppliers.");
  await award.getByLabel("Reason for not selecting the highest score").fill("Delivery capacity requires two suppliers.");
  await award.getByRole("button", { name: "Award and create draft contract" }).click();
  await expect.poll(() => captured.award).toBeDefined();
  expect(payloadLines(captured.award)).toHaveLength(2);
});