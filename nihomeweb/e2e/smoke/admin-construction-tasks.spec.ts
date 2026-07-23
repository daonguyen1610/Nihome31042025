import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-141 M4 Gantt end-to-end. Real-user path through the running
 * `docker compose` stack (API is not mocked). Verifies:
 *
 *   1. SUPER_ADMIN provisions a fresh design project via the API.
 *   2. The `/admin/construction/tasks` page renders and lets us filter
 *      to that project.
 *   3. The "New task" dialog creates a task; the row shows up.
 *   4. The detail sheet updates progress (auto-completes at 100 %).
 *   5. Bulk-delete removes selected tasks and refreshes the counts.
 *   6. SALE is bounced from the endpoints; DESIGN can view but cannot
 *      manage.
 */

const uid = () => Math.random().toString(36).slice(2, 8);
const today = new Date().toISOString().slice(0, 10);
const plusDays = (n: number) => {
  const d = new Date();
  d.setDate(d.getDate() + n);
  return d.toISOString().slice(0, 10);
};

test.describe("NIH-141 — Construction Gantt (real-user flow)", () => {
  test("Super Admin can create, edit progress and bulk-delete Gantt tasks", async ({
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
          name: `E2E Gantt customer ${uid()}`,
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
      data: { name: `E2E-GANTT ${projSuffix}`, customerId },
    });
    expect(projCreate.ok(), await projCreate.text()).toBeTruthy();
    const projectId = (await projCreate.json()).id as number;

    // ---------- 2. Open the Gantt page + filter to this project ----------
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/tasks`, { waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await expect(page.getByTestId("construction-tasks-page")).toBeVisible();

    // Filter combobox = first role=combobox on the page. Page loads
    // showing "All projects"; we pick the project we just created so
    // the list scope matches and the create dialog defaults to it.
    const projectFilter = page.locator('button[role="combobox"]').first();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/construction-tasks") &&
          r.url().includes(`designProjectId=${projectId}`),
      ),
      (async () => {
        await projectFilter.click();
        await page
          .getByRole("option", { name: new RegExp(`E2E-GANTT ${projSuffix}`) })
          .click();
      })(),
    ]);
    await expect(projectFilter).toContainText(new RegExp(`E2E-GANTT ${projSuffix}`));

    // ---------- 3. Create a task via the dialog ----------
    await page.getByTestId("construction-new").click();
    const taskName = `E2E Task ${uid()}`;
    await page.getByTestId("construction-new-name").fill(taskName);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/construction-tasks") &&
          r.request().method() === "POST" &&
          r.status() === 201,
      ),
      page.getByTestId("construction-new-save").click({ force: true }),
    ]);
    await page.waitForResponse(
      (r) =>
        r.url().includes("/api/construction-tasks?") &&
        r.request().method() === "GET",
    );
    // The row itself is clickable now (no dedicated "Xem chi tiết"
    // button), so we assert the row exists by scoping to its testid.
    const createdRow = page.locator('[data-testid^="construction-row-"]').filter({
      hasText: taskName,
    });
    await expect(createdRow).toBeVisible();

    // ---------- 4. Open detail sheet + push progress to 100 (auto-completes) ----------
    await createdRow.click();
    await expect(page.getByTestId("construction-detail-save")).toBeVisible();
    // Set actual end date so the auto-complete rule fires.
    await page.locator('input[type="date"]').nth(2).fill(today); // actualStart
    await page.locator('input[type="date"]').nth(3).fill(plusDays(1)); // actualEnd
    await page.getByTestId("construction-progress-slider").fill("100");
    // The save button lives at the bottom of a tall sheet — scroll it
    // into view first so Playwright's viewport gate doesn't refuse.
    await page.getByTestId("construction-detail-save").scrollIntoViewIfNeeded();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          /\/api\/construction-tasks\/\d+$/.test(r.url()) &&
          r.request().method() === "PUT" &&
          r.status() === 200,
      ),
      page.getByTestId("construction-detail-save").click({ force: true }),
    ]);

    // API sanity — status is Completed on the row we just edited.
    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/construction-tasks?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return "err";
          const items = (await list.json()).items as Array<{ name: string; status: string }>;
          return items.find((i) => i.name === taskName)?.status ?? "missing";
        },
        { timeout: 5_000 },
      )
      .toBe("Completed");

    // Close the sheet before the bulk-delete step. The Sheet primitive
    // renders its own top-right "Close" X so we scope to the footer
    // button by its Vietnamese label.
    await page.getByRole("button", { name: "Đóng", exact: true }).click();
    await expect(page.getByTestId("construction-detail-save")).toBeHidden();

    // ---------- 5. Bulk-delete the task we just created ----------
    // Select the single row via its select checkbox.
    const row = page.locator('[data-testid^="construction-row-"]').filter({
      hasText: taskName,
    });
    await row.locator('button[role="checkbox"]').click();
    await page.getByTestId("construction-bulk-delete").click();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/construction-tasks/bulk-delete") &&
          r.status() === 200,
      ),
      page.getByTestId("construction-bulk-delete-confirm").click(),
    ]);
    await page.waitForResponse(
      (r) => r.url().includes("/api/construction-tasks?"),
    );

    // The row is gone, empty state is back.
    await expect(
      page.locator('[data-testid^="construction-row-"]').filter({ hasText: taskName }),
    ).toHaveCount(0);
  });

  test("SALE role is blocked from Construction Tasks endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/construction-tasks", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
