import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

/**
 * Nicon bug list 30/10/2026 — "Thêm báo giá đầu vào không được".
 *
 * The create dialog is opened from a deep link (`?create=1&purpose=...`). The
 * options used to arrive in two requests, and the auto-open effect re-ran on
 * every arrival, resetting the lead (and any text the user had already typed).
 * These checks drive the real dialog while the heavy options request is delayed
 * so the race is deterministic instead of timing-dependent.
 */

type DepartmentOption = { id: number; code: string; labelKey: string };
type UserOption = { userId: number; name: string; roleGroupId: number; canLead: boolean };

const delayOptionsResponse = async (page: import("@playwright/test").Page) => {
  await page.route("**/api/work-packages/options", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 2_500));
    await route.continue();
  });
};

const loadFixtures = async (
  api: import("@playwright/test").APIRequestContext,
  headers: Record<string, string>,
) => {
  const quotesResponse = await api.get("/api/quotes?search=QT-SAMPLE-001", { headers });
  expect(quotesResponse.status()).toBe(200);
  const quote = (
    (await quotesResponse.json()).items as {
      id: number;
      code: string;
      operationalProjectId: number | null;
      opportunityId: number | null;
    }[]
  ).find((item) => item.code === "QT-SAMPLE-001");
  expect(quote, "the draft sample quote is seeded").toBeTruthy();

  const optionsResponse = await api.get("/api/work-packages/options", { headers });
  expect(optionsResponse.status()).toBe(200);
  const options = await optionsResponse.json();
  const procurement = (options.departments as DepartmentOption[]).find(
    (group) => group.code === "PROCUREMENT",
  );
  const lead = (options.users as UserOption[]).find(
    (user) => user.roleGroupId === procurement?.id && user.canLead,
  );
  expect(procurement, "the procurement department is seeded").toBeTruthy();
  expect(lead, "an active procurement lead is seeded").toBeTruthy();
  return { quote: quote!, procurement: procurement!, lead: lead! };
};

test("the create dialog survives a late options response", async ({
  api,
  loginAs,
  loginInBrowserAs,
  page,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const { quote, lead } = await loadFixtures(api, headers);
  const packageName = `Giá đầu vào giữ form ${Date.now()}`;

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await delayOptionsResponse(page);
  const optionsSettled = page.waitForResponse(
    (response) => response.url().includes("/api/work-packages/options") && response.ok(),
  );
  await page.goto(
    `/admin/work-packages?create=1&purpose=ProcurementInputPricing&projectId=${quote.operationalProjectId}&opportunityId=${quote.opportunityId}&quoteId=${quote.id}`,
  );

  // The department/lead pickers open their own popovers, so anchor on the
  // create dialog by its title instead of the bare dialog role.
  const dialog = page.getByRole("dialog", { name: /Request input pricing/i });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByTestId("work-package-source-quote")).toContainText(String(quote.id));
  await dialog.locator('input[maxlength="300"]').fill(packageName);
  await dialog.getByTestId("work-package-lead").getByRole("combobox").click();
  await page.getByRole("option", { name: lead.name }).click();

  // The delayed options response lands here. Before the fix it re-opened the
  // dialog and cleared both the name and the chosen lead.
  await optionsSettled;
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('input[maxlength="300"]')).toHaveValue(packageName);
  await expect(dialog.getByTestId("work-package-lead")).toContainText(lead.name);
  await expect(dialog.getByTestId("work-package-department")).toContainText(/Procurement/i);
});

test("a project manager can request input pricing for a quote they do not own", async ({
  api,
  loginAs,
  loginInBrowserAs,
  page,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const { quote, lead } = await loadFixtures(api, headers);
  const packageName = `Giá đầu vào PM ${Date.now()}`;

  // The project manager hands work to Cung ứng even though the quote and its
  // opportunity belong to the salesperson. Saving used to answer 400
  // "Cơ hội không thuộc phạm vi của người dùng".
  await loginInBrowserAs(page, TEST_USERS.pm);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  let createdId: number | undefined;
  try {
    await page.goto(
      `/admin/work-packages?create=1&purpose=ProcurementInputPricing&projectId=${quote.operationalProjectId}&opportunityId=${quote.opportunityId}`,
    );
    const dialog = page
      .locator('[role="dialog"][data-state="open"]')
      .filter({ has: page.locator('input[maxlength="300"]') });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByTestId("work-package-department")).toContainText(/Procurement/i);
    await dialog.locator('input[maxlength="300"]').fill(packageName);
    await dialog.getByTestId("work-package-lead").getByRole("combobox").click();
    await page.getByRole("option", { name: lead.name }).click();
    await dialog.getByRole("button", { name: /Save|Lưu/ }).click();

    await expect(page).toHaveURL(/\/admin\/work-packages\/\d+$/, { timeout: 15_000 });
    createdId = Number(page.url().split("/").pop());
  } finally {
    if (createdId) await hardDeleteBusinessRoot(api, headers, `/api/work-packages/${createdId}`);
  }
});
