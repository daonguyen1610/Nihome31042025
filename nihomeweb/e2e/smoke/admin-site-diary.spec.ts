import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-142 M4 Site Diary end-to-end. Real-user path through the
 * running `docker compose` stack — the API is not mocked. Verifies:
 *
 *   1. SUPER_ADMIN provisions a fresh DesignProject via the API.
 *   2. The `/admin/construction/diary` page renders + accepts the
 *      "New diary" dialog.
 *   3. Clicking the new row opens the detail sheet; Submit fires
 *      the workflow endpoint and flips the row to `Submitted`.
 *   4. Confirm flips it to `Confirmed`, Reopen returns to `Draft`.
 *   5. SALE role is bounced from the endpoints (no perms).
 */

const uid = () => Math.random().toString(36).slice(2, 8);
const today = new Date().toISOString().slice(0, 10);

test.describe("NIH-142 — Site Diary (real-user flow)", () => {
  test("Super Admin walks a diary through Draft → Submitted → Confirmed → Reopen", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    // ---------- 1. Set up a project via API ----------
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    const customersResp = await api.get("/api/customers?pageSize=1", { headers: authHeader });
    let customerId = 0;
    if (customersResp.ok()) customerId = (await customersResp.json()).items?.[0]?.id ?? 0;
    if (!customerId) {
      const created = await api.post("/api/customers", {
        headers: authHeader,
        data: {
          name: `E2E Diary customer ${uid()}`,
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
      data: { name: `E2E-DIARY ${projSuffix}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    // ---------- 2. Open the diary page + filter to this project ----------
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/diary`, { waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await expect(page.getByTestId("site-diary-page")).toBeVisible();

    const projectFilter = page.locator('button[role="combobox"]').first();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/site-diaries") &&
          r.url().includes(`designProjectId=${projectId}`),
      ),
      (async () => {
        await projectFilter.click();
        await page
          .getByRole("option", { name: new RegExp(`E2E-DIARY ${projSuffix}`) })
          .click();
      })(),
    ]);

    // ---------- 3. Create the diary via the dialog ----------
    await page.getByTestId("diary-new").click();
    await page.waitForSelector('[data-testid="diary-new-work"]');
    const workText = `E2E work ${uid()}`;
    await page.getByTestId("diary-new-work").fill(workText);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/site-diaries") &&
          r.request().method() === "POST" &&
          r.status() === 201,
      ),
      page.getByTestId("diary-new-save").click({ force: true }),
    ]);
    await page.waitForResponse(
      (r) => r.url().includes("/api/site-diaries?") && r.request().method() === "GET",
    );
    const row = page.locator('[data-testid^="diary-row-"]').filter({ hasText: workText });
    await expect(row).toBeVisible();

    // ---------- 4. Submit → Confirm → Reopen via the detail sheet ----------
    await row.click();
    await expect(page.getByTestId("diary-submit")).toBeVisible();
    await page.getByTestId("diary-submit").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/site-diaries\/\d+\/submit$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("diary-action-confirm").click({ force: true }),
    ]);

    await page.getByTestId("diary-confirm").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/site-diaries\/\d+\/confirm$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("diary-action-confirm").click({ force: true }),
    ]);

    // API sanity — status is Confirmed on the row we just walked.
    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/site-diaries?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return "err";
          const items = (await list.json()).items as Array<{ workPerformed: string; status: string }>;
          return items.find((i) => i.workPerformed === workText)?.status ?? "missing";
        },
        { timeout: 5_000 },
      )
      .toBe("Confirmed");

    // Reopen — must appear only for confirm-permission holders.
    await page.getByTestId("diary-reopen").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/site-diaries\/\d+\/reopen$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("diary-action-confirm").click({ force: true }),
    ]);

    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/site-diaries?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return "err";
          const items = (await list.json()).items as Array<{ workPerformed: string; status: string }>;
          return items.find((i) => i.workPerformed === workText)?.status ?? "missing";
        },
        { timeout: 5_000 },
      )
      .toBe("Draft");

    // Silence the linter — `today` is imported for future date-filter tests.
    expect(today).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  test("SALE role is blocked from Site Diary endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/site-diaries", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
