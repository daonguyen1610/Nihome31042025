import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Design first, contract later: the create dialog asks for the operational
 * project directly instead of failing silently when no contract is chosen.
 */
test("design project dialog requires an operational project when no contract is linked", async ({ loginInBrowserAs, page }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/design-projects");
  await page.getByRole("button", { name: /Tạo dự án thiết kế|New design project/i }).first().click();

  const dialog = page.getByRole("dialog");
  const projectField = page.getByTestId("design-project-operational-project");
  await expect(projectField).toBeVisible();
  await expect(projectField).toContainText(/Dự án vận hành|Operational project/);
  await expect(dialog.getByText("Chủ nhiệm thiết kế", { exact: true })).toBeVisible();

  let posted = false;
  page.on("request", (request) => {
    if (request.method() === "POST" && new URL(request.url()).pathname === "/api/design-projects") posted = true;
  });
  await dialog.locator("input").first().fill("Thiết kế ý tưởng nhà xưởng");
  await dialog.getByRole("button", { name: /Lưu|Save/ }).last().click();
  await expect(page.getByTestId("design-project-form-error")).toBeVisible();
  expect(posted).toBe(false);

  // The taller form still fits a phone: the dialog scrolls instead of
  // pushing its header off screen.
  const box = await dialog.boundingBox();
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});

test("Design Lead cannot create the project root from the Design list", async ({ loginInBrowserAs, page }) => {
  await loginInBrowserAs(page, TEST_USERS.designLead);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin/design-projects", { waitUntil: "networkidle" });

  await expect(page.getByRole("heading", { name: /Dự án thiết kế|Design projects/i })).toBeVisible();
  await expect(page.getByRole("button", { name: /Tạo dự án thiết kế|New design project/i })).toHaveCount(0);
});

test("Project Manager can view shared projects but cannot create the Sales root", async ({ loginInBrowserAs, page }) => {
  await loginInBrowserAs(page, TEST_USERS.pm);
  await page.goto("/admin/operational-projects", { waitUntil: "networkidle" });

  await expect(page.getByRole("heading", { name: /dự án vận hành|Operational project management/i })).toBeVisible();
  await expect(page.getByRole("button", { name: /Tạo dự án vận hành|New operational project/i })).toHaveCount(0);
  await page.goto("/admin/operational-projects?create=1", { waitUntil: "networkidle" });
  await expect(page.getByRole("dialog")).toHaveCount(0);
});
