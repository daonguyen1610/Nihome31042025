import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-145 M4 As-built dossier end-to-end. Real path through the
 * running docker stack. Verifies:
 *
 *   1. SUPER_ADMIN provisions a DesignProject.
 *   2. The /admin/construction/asbuilt page renders + accepts the
 *      New document dialog.
 *   3. Workflow toolbar walks Draft → Submitted → Approved via the
 *      dedicated /approve endpoint.
 *   4. Completeness roll-up reflects Approved category coverage.
 *   5. SALE is blocked from the API.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-145 — As-built dossier (real-user flow)", () => {
  test("SUPER_ADMIN drafts, submits, approves and completeness updates", async ({
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
          name: `E2E AB customer ${uid()}`,
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
      data: { name: `E2E-AB ${projSuffix}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/asbuilt`, { waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await expect(page.getByTestId("asbuilt-page")).toBeVisible();

    const projectFilter = page.locator('button[role="combobox"]').first();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/as-built-documents") &&
          r.url().includes(`designProjectId=${projectId}`),
      ),
      (async () => {
        await projectFilter.click();
        await page
          .getByRole("option", { name: new RegExp(`E2E-AB ${projSuffix}`) })
          .click();
      })(),
    ]);

    // Create a Drawing document
    await page.getByTestId("asbuilt-new").click();
    await page.waitForSelector('[data-testid="asbuilt-form-title"]');
    const titleText = `E2E as-built ${uid()}`;
    await page.getByTestId("asbuilt-form-title").fill(titleText);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/as-built-documents") &&
          r.request().method() === "POST" &&
          r.status() === 201,
      ),
      page.getByTestId("asbuilt-form-save").click({ force: true }),
    ]);
    await page.waitForResponse(
      (r) => r.url().includes("/api/as-built-documents?") && r.request().method() === "GET",
    );

    const row = page.locator('[data-testid^="asbuilt-row-"]').filter({ hasText: titleText });
    await expect(row).toBeVisible();
    await row.click();

    // Draft → Submitted
    await page.getByTestId("asbuilt-submit").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/as-built-documents\/\d+\/status$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("asbuilt-action-confirm").click({ force: true }),
    ]);
    await expect(page.getByTestId("asbuilt-approve")).toBeVisible();

    // Submitted → Approved via /approve
    await page.getByTestId("asbuilt-approve").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/as-built-documents\/\d+\/approve$/.test(r.url()) && r.status() === 200,
      ),
      page.getByTestId("asbuilt-action-confirm").click({ force: true }),
    ]);

    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/as-built-documents?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return { status: "err", completedRequiredCategories: 0 };
          const body = await list.json();
          const match = (body.items as Array<{ title: string; status: string }>).find((i) => i.title === titleText);
          return {
            status: match?.status ?? "missing",
            completedRequiredCategories: body.completedRequiredCategories,
          };
        },
        { timeout: 5_000 },
      )
      .toMatchObject({ status: "Approved", completedRequiredCategories: 1 });
  });

  test("SALE role is blocked from as-built endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/as-built-documents", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
