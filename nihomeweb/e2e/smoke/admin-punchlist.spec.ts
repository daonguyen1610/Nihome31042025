import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-146 M4 Punch list end-to-end. Real-user path through the running
 * `docker compose` stack — the API is not mocked. Verifies:
 *
 *   1. SUPER_ADMIN provisions a fresh DesignProject.
 *   2. The `/admin/construction/punchlist` page renders + accepts the
 *      "New punch" dialog.
 *   3. Row click opens the detail sheet; walking Open → InProgress →
 *      Fixed → Verified via the toolbar buttons hits the correct
 *      endpoints (Verify goes through the dedicated /verify route).
 *   4. Reopen from Verified flips the row back to Open and bumps the
 *      reopen counter.
 *   5. SALE is bounced from the endpoints.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-146 — Punch list (real-user flow)", () => {
  test("Super Admin walks a punch item through Open → InProgress → Fixed → Verified → Reopen", async ({
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
          name: `E2E Punch customer ${uid()}`,
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
      data: { name: `E2E-PUNCH ${projSuffix}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    // Open the page + filter to this project
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/punchlist`, { waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await expect(page.getByTestId("punch-page")).toBeVisible();

    const projectFilter = page.locator('button[role="combobox"]').first();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/punch-items") &&
          r.url().includes(`designProjectId=${projectId}`),
      ),
      (async () => {
        await projectFilter.click();
        await page
          .getByRole("option", { name: new RegExp(`E2E-PUNCH ${projSuffix}`) })
          .click();
      })(),
    ]);

    // Create a punch via the dialog
    await page.getByTestId("punch-new").click();
    await page.waitForSelector('[data-testid="punch-new-title"]');
    const titleText = `E2E punch ${uid()}`;
    await page.getByTestId("punch-new-title").fill(titleText);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/punch-items") &&
          r.request().method() === "POST" &&
          r.status() === 201,
      ),
      page.getByTestId("punch-new-save").click({ force: true }),
    ]);
    await page.waitForResponse(
      (r) => r.url().includes("/api/punch-items?") && r.request().method() === "GET",
    );
    const row = page.locator('[data-testid^="punch-row-"]').filter({ hasText: titleText });
    await expect(row).toBeVisible();

    // Walk the workflow via the detail sheet toolbar
    await row.click();
    await expect(page.getByTestId("punch-start")).toBeVisible();

    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/punch-items\/\d+\/status$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("punch-start").click(),
    ]);
    await expect(page.getByTestId("punch-fix")).toBeVisible();

    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/punch-items\/\d+\/status$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("punch-fix").click(),
    ]);
    await expect(page.getByTestId("punch-verify")).toBeVisible();

    await page.getByTestId("punch-verify").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/punch-items\/\d+\/verify$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("punch-action-confirm").click({ force: true }),
    ]);

    // API sanity — Verified with reopenCount=0.
    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/punch-items?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return { status: "err" };
          const items = (await list.json()).items as Array<{ title: string; status: string; reopenCount: number }>;
          const match = items.find((i) => i.title === titleText);
          return match ?? { status: "missing" };
        },
        { timeout: 5_000 },
      )
      .toMatchObject({ status: "Verified", reopenCount: 0 });

    // Reopen — from Verified back to Open, counter should bump.
    await page.getByTestId("punch-reopen").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/punch-items\/\d+\/status$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("punch-action-confirm").click({ force: true }),
    ]);

    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/punch-items?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return { status: "err" };
          const items = (await list.json()).items as Array<{ title: string; status: string; reopenCount: number }>;
          const match = items.find((i) => i.title === titleText);
          return match ?? { status: "missing" };
        },
        { timeout: 5_000 },
      )
      .toMatchObject({ status: "Open", reopenCount: 1 });
  });

  test("SALE role is blocked from Punch List endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/punch-items", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
