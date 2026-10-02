import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Design first, contract later: the create dialog asks for the operational
 * project directly instead of failing silently when no contract is chosen.
 */
test("design project dialog requires an operational project when no contract is linked", async ({ loginInBrowserAs, page }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/design-projects");
  await page.getByRole("button", { name: /Tạo dự án thiết kế|New design project/i }).first().click();

  const dialog = page.getByRole("dialog");
  const projectField = page.getByTestId("design-project-operational-project");
  await expect(projectField).toBeVisible();
  await expect(projectField).toContainText(/Dự án vận hành|Operational project/);

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
