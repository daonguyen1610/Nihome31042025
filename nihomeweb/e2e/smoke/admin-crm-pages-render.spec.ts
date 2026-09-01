import { expect } from "@playwright/test";
import { test, TEST_USERS } from "../fixtures/auth";

/**
 * A render-time throw blanks the page while lint and tsc both stay quiet — a
 * dependency array reading a state variable declared further down the component
 * is enough to do it, and that is exactly how /admin/design-projects went white.
 *
 * These walk every CRM and design list page and fail on a blank body or a
 * console error, so the next one is caught before a person finds it.
 */
const PAGES = [
  "/admin/leads",
  "/admin/customers",
  "/admin/opportunities",
  "/admin/quotes",
  "/admin/material-rates",
  "/admin/contracts",
  "/admin/design-projects",
  "/admin/tenders",
  "/admin/surveys",
];

for (const path of PAGES) {
  test(`${path} renders without a runtime error`, async ({ page, loginInBrowserAs }) => {
    // The dev bundle keeps Vite's HMR client, whose socket cannot reach anything
    // here; that noise is not the app failing.
    const isEnvironmentNoise = (text: string) =>
      text.includes("WebSocket") || text.includes("ws://") || text.includes("[vite]");

    const errors: string[] = [];
    page.on("pageerror", (e) => errors.push(`uncaught: ${e.message}`));
    page.on("console", (m) => {
      if (m.type() !== "error") return;
      const text = m.text();
      if (!isEnvironmentNoise(text)) errors.push(text);
    });

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(path);

    // The admin shell always renders a sidebar; a thrown render leaves nothing.
    await expect(page.locator("main, [role=main], nav").first()).toBeVisible({ timeout: 15_000 });

    const bodyText = (await page.locator("body").innerText()).trim();
    expect(bodyText.length, `${path} rendered an empty page`).toBeGreaterThan(50);

    expect(errors, `${path} logged: ${errors.join(" | ")}`).toEqual([]);
  });
}

/**
 * NIH-444 — the matrix is long, and scrolling used to carry the role names off
 * screen, leaving no way to tell which column a checkbox belonged to.
 */
test("the role matrix keeps its header visible and can be filtered down", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/roles");

  const header = page.locator("[data-testid^=rbac-col-]").first();
  await expect(header).toBeVisible({ timeout: 15_000 });

  // Scroll the matrix itself; the header has to survive it.
  const matrix = page.locator("table").first().locator("xpath=..");
  await matrix.evaluate((el) => el.scrollBy(0, 800));
  await expect(header).toBeInViewport();

  // Filtering by module cuts the rows down.
  const before = await page.locator("tbody tr").count();
  await page.locator("#rbac-search").fill("construction");
  await expect
    .poll(async () => page.locator("tbody tr").count(), { timeout: 10_000 })
    .toBeLessThan(before);

  // Hiding a role removes its column.
  await page.locator("#rbac-search").fill("");
  const columnsBefore = await page.locator("[data-testid^=rbac-col-]").count();
  await page.locator("label:has(input[type=checkbox])").first().locator("input").uncheck();
  await expect
    .poll(async () => page.locator("[data-testid^=rbac-col-]").count(), { timeout: 10_000 })
    .toBeLessThan(columnsBefore);
});
