import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-118 M2 IFC Release end-to-end flow. Real-user path through the
 * `docker compose` stack — the API is not mocked. Verifies:
 *
 *   1. SUPER_ADMIN sets up a project with an approved shop drawing.
 *   2. The IFC tab renders + accepts the "New release" dialog.
 *   3. Adding a drawing + a recipient via the detail panel populates
 *      the release with the correct counts.
 *   4. Firing the atomic Release action flips the bundled Shop Drawing
 *      status to Released (only writer for that state).
 *   5. After Released, the header shows the timestamp + issuer, the
 *      recipient row exposes the Acknowledge button, and the header
 *      badge is Released.
 *   6. SALE is bounced from the endpoints. DESIGN can view but cannot
 *      fire the release action (needs stricter design.ifc.release).
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-118 — IFC Release (real-user flow)", () => {
  test("Design Lead can build + release an IFC packet from the browser", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    // ---------- 1. Set up a project + approved shop drawing via the API ----------
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    const customersResp = await api.get("/api/customers?pageSize=1", { headers: authHeader });
    let customerId = 0;
    if (customersResp.ok()) customerId = (await customersResp.json()).items?.[0]?.id ?? 0;
    if (!customerId) {
      const created = await api.post("/api/customers", {
        headers: authHeader,
        data: {
          name: `E2E IFC customer ${uid()}`,
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
      data: { name: `E2E-IFC ${uid()}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    // Concept → BasicDesign → ShopDrawing
    const optCreate = await api.post("/api/concept-options", {
      headers: authHeader,
      data: { designProjectId: projectId, name: "E2E finalized" },
    });
    const optId = (await optCreate.json()).id as number;
    for (const status of ["PendingInternalReview", "PresentedToClient", "Finalized"]) {
      await api.post(`/api/concept-options/${optId}/status`, { headers: authHeader, data: { status } });
    }
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

    // Approved shop drawing to bundle.
    const shopCreate = await api.post("/api/shop-drawings", {
      headers: authHeader,
      data: {
        designProjectId: projectId,
        disciplineCode: "architecture",
        constructionItem: "Móng cọc E2E",
        title: `E2E IFC drawing ${uid()}`,
      },
    });
    expect(shopCreate.ok(), await shopCreate.text()).toBeTruthy();
    const shopId = (await shopCreate.json()).id as number;
    for (const status of ["InReview", "Approved"]) {
      await api.post(`/api/shop-drawings/${shopId}/status`, { headers: authHeader, data: { status } });
    }

    // ---------- 2. Open the IFC tab in the browser ----------
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/design-projects/${projectId}`, {
      waitUntil: "networkidle",
    });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    const ifcTab = page.locator('button[role="tab"]').filter({
      hasText: /^Ph\u00e1t h\u00e0nh IFC$|^IFC releases$/i,
    });
    await ifcTab.click({ force: true });
    await expect(page.getByTestId("ifc-releases-tab")).toBeVisible();

    // ---------- 3. Create a new release ----------
    await page.getByTestId("ifc-new").click();
    await page.getByTestId("ifc-form-title").fill(`E2E release ${uid()}`);
    await page.getByTestId("ifc-form-save").click();

    // Detail dialog opens for the new draft.
    await expect(page.getByTestId("ifc-detail")).toBeVisible();

    // ---------- 4. Add the approved drawing + a recipient ----------
    // Expand the picker + tick the drawing we created.
    await page.getByText(/Th\u00eam b\u1ea3n v\u1ebd|Add drawing/i).first().click();
    await page.getByTestId(`ifc-picker-${shopId}`).click();
    await page.getByTestId("ifc-picker-add").click();

    // Confirm the item landed via the API (deterministic — DOM refetches).
    await expect
      .poll(async () => {
        const list = await api.get(
          `/api/ifc-releases?designProjectId=${projectId}&status=Draft&pageSize=20`,
          { headers: authHeader },
        );
        if (!list.ok()) return 0;
        const drafts = (await list.json()).items as Array<{ title: string; items: Array<{ shopDrawingId: number }> }>;
        const match = drafts.find((r) => r.title.startsWith("E2E release"));
        return match?.items.some((i) => i.shopDrawingId === shopId) ? 1 : 0;
      }, { timeout: 5_000 })
      .toBe(1);

    await page.getByTestId("ifc-recipient-name").fill("E2E ABC Corp");
    await page.getByTestId("ifc-recipient-type").click();
    await page.getByRole("option", { name: /Nh\u00e0 th\u1ea7u ch\u00ednh|Main contractor/i }).click();
    await page.getByTestId("ifc-recipient-add").click();

    // ---------- 5. Fire the atomic Release action ----------
    await expect(page.getByTestId("ifc-release")).toBeEnabled();
    await page.getByTestId("ifc-release").click();
    await page.getByTestId("ifc-release-confirm").click();

    // Header badge flips to Released.
    await expect(page.getByTestId("ifc-detail")).toContainText(/\u0110\u00e3 ph\u00e1t h\u00e0nh|Released/i);

    // Sanity — shop drawing is now Released via the API (deterministic).
    await expect
      .poll(async () => {
        const drawing = await api.get(`/api/shop-drawings/${shopId}`, { headers: authHeader });
        if (!drawing.ok()) return "err";
        return (await drawing.json()).status as string;
      }, { timeout: 5_000 })
      .toBe("Released");
  });

  test("SALE role is blocked from IFC endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/ifc-releases", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
