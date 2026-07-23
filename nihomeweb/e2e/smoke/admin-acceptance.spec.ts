import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-143 M4 Partial Acceptance — real-user path through the running
 * docker stack. Verifies:
 *
 *   1. SUPER_ADMIN provisions a DesignProject.
 *   2. The /admin/construction/acceptance page renders + accepts the
 *      New record dialog.
 *   3. Detail-sheet workflow walks Draft → Submitted → Approved via
 *      the dedicated /approve endpoint.
 *   4. Reject + Revise increments the revision counter.
 *   5. SALE is blocked from the API.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-143 — Partial acceptance (real-user flow)", () => {
  test("SUPER_ADMIN drafts, submits and approves an acceptance record", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    const customersResp = await api.get("/api/customers?pageSize=1", { headers: authHeader });
    let customerId = 0;
    if (customersResp.ok()) customerId = (await customersResp.json()).items?.[0]?.id ?? 0;
    if (!customerId) {
      const created = await api.post("/api/customers", {
        headers: authHeader,
        data: {
          name: `E2E Acc customer ${uid()}`,
          type: "Company",
          sourceCode: "referral",
          relationshipStatus: "InProgress",
        },
      });
      expect(created.ok(), await created.text()).toBeTruthy();
      customerId = (await created.json()).id;
    }

    const projSuffix = uid();
    const projCreate = await api.post("/api/design-projects", {
      headers: authHeader,
      data: { name: `E2E-ACC ${projSuffix}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/acceptance`, { waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await expect(page.getByTestId("acceptance-page")).toBeVisible();

    const projectFilter = page.locator('button[role="combobox"]').first();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/acceptance-records") &&
          r.url().includes(`designProjectId=${projectId}`),
      ),
      (async () => {
        await projectFilter.click();
        await page
          .getByRole("option", { name: new RegExp(`E2E-ACC ${projSuffix}`) })
          .click();
      })(),
    ]);

    await page.getByTestId("acceptance-new").click();
    await page.waitForSelector('[data-testid="acceptance-form-title"]');
    const titleText = `E2E acceptance ${uid()}`;
    await page.getByTestId("acceptance-form-title").fill(titleText);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/acceptance-records") &&
          r.request().method() === "POST" &&
          r.status() === 201,
      ),
      page.getByTestId("acceptance-form-save").click({ force: true }),
    ]);
    await page.waitForResponse(
      (r) => r.url().includes("/api/acceptance-records?") && r.request().method() === "GET",
    );

    const row = page.locator('[data-testid^="acceptance-row-"]').filter({ hasText: titleText });
    await expect(row).toBeVisible();
    await row.click();

    // Draft -> Submitted
    await page.getByTestId("acceptance-submit").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/acceptance-records\/\d+\/status$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("acceptance-action-confirm").click({ force: true }),
    ]);
    await expect(page.getByTestId("acceptance-approve")).toBeVisible();

    // Submitted -> Approved via /approve
    await page.getByTestId("acceptance-approve").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/acceptance-records\/\d+\/approve$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("acceptance-action-confirm").click({ force: true }),
    ]);

    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/acceptance-records?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return { status: "err" };
          const items = (await list.json()).items as Array<{
            title: string;
            status: string;
            revisionCount: number;
          }>;
          const match = items.find((i) => i.title === titleText);
          return match ?? { status: "missing" };
        },
        { timeout: 5_000 },
      )
      .toMatchObject({ status: "Approved" });
  });

  test("Reject then revise bumps revisionCount", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    const customersResp = await api.get("/api/customers?pageSize=1", { headers: authHeader });
    const customerId = customersResp.ok()
      ? (await customersResp.json()).items?.[0]?.id ?? 0
      : 0;
    expect(customerId).toBeGreaterThan(0);

    const projCreate = await api.post("/api/design-projects", {
      headers: authHeader,
      data: { name: `E2E-ACC-REV ${uid()}`, customerId },
    });
    const projectId = (await projCreate.json()).id as number;

    const create = await api.post("/api/acceptance-records", {
      headers: authHeader,
      data: {
        designProjectId: projectId,
        title: `Rev ${uid()}`,
        acceptanceDate: "2026-06-15",
      },
    });
    const id = (await create.json()).id as number;

    await api.post(`/api/acceptance-records/${id}/status`, {
      headers: authHeader,
      data: { status: "Submitted" },
    });
    await api.post(`/api/acceptance-records/${id}/status`, {
      headers: authHeader,
      data: { status: "Rejected", resolutionNote: "fix" },
    });
    const revised = await api.post(`/api/acceptance-records/${id}/status`, {
      headers: authHeader,
      data: { status: "Draft" },
    });
    expect(revised.ok()).toBeTruthy();
    expect((await revised.json()).revisionCount).toBe(1);
  });

  test("SALE role is blocked from acceptance endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/acceptance-records", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
