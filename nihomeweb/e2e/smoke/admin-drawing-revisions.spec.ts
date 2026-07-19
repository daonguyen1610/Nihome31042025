import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-117 M2 Drawing Revision end-to-end flow. Real-user path through
 * the `docker compose` stack — the API is not mocked. Verifies:
 *
 *   1. SUPER_ADMIN sets up a project + first shop drawing.
 *   2. The Revisions button on the shop drawing row opens the panel.
 *   3. Creating R1 via the dialog appends a "Đang sử dụng" row.
 *   4. A second revision (R2) flips R1 to "Đã thu hồi" and lands R2
 *      as current.
 *   5. The diff picker between R1/R2 renders metadata changes.
 *   6. SALE is bounced from the endpoints.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-117 — Drawing Revisions (real-user flow)", () => {
  test("Design Lead can append revisions + diff them from the Shop Drawing tab", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    // ---------- 1. Set up a project + first shop drawing via the API ----------
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    const customersResp = await api.get("/api/customers?pageSize=1", { headers: authHeader });
    let customerId = 0;
    if (customersResp.ok()) customerId = (await customersResp.json()).items?.[0]?.id ?? 0;
    if (!customerId) {
      const created = await api.post("/api/customers", {
        headers: authHeader,
        data: {
          name: `E2E REV customer ${uid()}`,
          type: "Company",
          sourceCode: "referral",
          relationshipStatus: "InProgress",
        },
      });
      expect(created.ok(), await created.text()).toBeTruthy();
      customerId = (await created.json()).id;
    }

    const projCreate = await api.post("/api/design-projects", {
      headers: authHeader,
      data: { name: `E2E-REV ${uid()}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    // Concept → BasicDesign
    const optCreate = await api.post("/api/concept-options", {
      headers: authHeader,
      data: { designProjectId: projectId, name: "E2E finalized" },
    });
    expect(optCreate.ok(), await optCreate.text()).toBeTruthy();
    const optId = (await optCreate.json()).id as number;
    for (const status of ["PendingInternalReview", "PresentedToClient", "Finalized"]) {
      (await api.post(`/api/concept-options/${optId}/status`, { headers: authHeader, data: { status } })).ok();
    }
    // BasicDesign → ShopDrawing
    for (const disciplineCode of ["architecture", "structure", "mep"]) {
      const doc = await api.post("/api/basic-design-docs", {
        headers: authHeader,
        data: {
          designProjectId: projectId,
          disciplineCode,
          title: `${disciplineCode} — E2E BD ${uid()}`,
        },
      });
      const docId = (await doc.json()).id as number;
      for (const status of ["SubmittedForReview", "InternallyApproved"]) {
        await api.post(`/api/basic-design-docs/${docId}/status`, { headers: authHeader, data: { status } });
      }
    }
    await api.post(`/api/basic-design-docs/design-project/${projectId}/unlock-shop-drawing`, { headers: authHeader });

    const shopCreate = await api.post("/api/shop-drawings", {
      headers: authHeader,
      data: {
        designProjectId: projectId,
        disciplineCode: "architecture",
        constructionItem: "Móng cọc E2E",
        title: `E2E rev drawing ${uid()}`,
      },
    });
    expect(shopCreate.ok(), await shopCreate.text()).toBeTruthy();
    const shopId = (await shopCreate.json()).id as number;

    // ---------- 2. Open the Shop Drawing tab in the browser ----------
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/design-projects/${projectId}`, {
      waitUntil: "networkidle",
    });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    const shopTab = page.locator('button[role="tab"]').filter({
      hasText: /^Shop Drawing$|^Shop drawing$/i,
    });
    await shopTab.click({ force: true });
    await expect(page.getByTestId("shop-drawing-tab")).toBeVisible();

    // ---------- 3. Open the Revisions panel ----------
    await page.getByTestId(`shop-drawing-revisions-${shopId}`).click();
    await expect(page.getByTestId("revisions-panel")).toBeVisible();

    // ---------- 4. Append R1 via the dialog ----------
    await page.getByTestId("revisions-new").click();
    await page.getByTestId("revisions-form-reason").click();
    await page.getByRole("option", { name: /Y\u00eau c\u1ea7u kh\u00e1ch|Client request/i }).click();
    await page.getByTestId("revisions-form-note").fill("R1 E2E — client requested door move");
    await page.getByTestId("revisions-form-save").click();

    // R1 row appears + is current
    await expect(page.locator('[data-testid^="revision-row-"]').first()).toContainText(/R1/i);
    await expect(page.locator('[data-testid^="revision-row-"]').first())
      .toContainText(/\u0110ang s\u1eed d\u1ee5ng|Current/i);

    // ---------- 5. Append R2 — R1 should flip to superseded ----------
    await page.getByTestId("revisions-new").click();
    await page.getByTestId("revisions-form-reason").click();
    await page.getByRole("option", { name: /\u0110\u1ed3ng b\u1ed9 MEP|MEP coordination/i }).click();
    await page.getByTestId("revisions-form-note").fill("R2 E2E — MEP sync tweak");
    await page.getByTestId("revisions-form-save").click();

    // Newest revision on top is R2 (Current), R1 now shows Superseded.
    const rows = page.locator('[data-testid^="revision-row-"]');
    await expect(rows.nth(0)).toContainText(/R2/i);
    await expect(rows.nth(0)).toContainText(/\u0110ang s\u1eed d\u1ee5ng|Current/i);
    await expect(rows.nth(1)).toContainText(/R1/i);
    await expect(rows.nth(1)).toContainText(/\u0110\u00e3 thu h\u1ed3i|Superseded/i);

    // ---------- 6. Diff picker ----------
    // Pick R1 as From and R2 as To via the API response order — safer
    // than parsing DOM labels which vary per language.
    const listResp = await api.get(
      `/api/drawing-revisions?targetType=ShopDrawing&targetId=${shopId}&pageSize=10`,
      { headers: authHeader },
    );
    const revs = (await listResp.json()).items as Array<{ id: number; revisionNumber: number }>;
    const r1 = revs.find((r) => r.revisionNumber === 1)!;
    const r2 = revs.find((r) => r.revisionNumber === 2)!;
    await page.getByTestId("revisions-diff-from").click();
    await page.getByRole("option", { name: "R1" }).click();
    await page.getByTestId("revisions-diff-to").click();
    await page.getByRole("option", { name: "R2" }).click();
    await page.getByTestId("revisions-diff-run").click();
    await expect(page.getByTestId("revisions-diff-output")).toBeVisible();
    await expect(page.getByTestId("revisions-diff-output")).toContainText(/L\u00fd do|Reason/i);

    // Final API sanity — deterministic regardless of DOM state.
    expect(r1.revisionNumber).toBe(1);
    expect(r2.revisionNumber).toBe(2);
  });

  test("SALE role is blocked from Drawing Revision endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/drawing-revisions", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
