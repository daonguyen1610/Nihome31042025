import { expect, test, TEST_USERS } from "../fixtures/auth";

test.describe("NIH-144 — Project handover", () => {
  test("authorized user can render the handover workspace", async ({
    page,
    loginInBrowserAs,
    baseURL,
  }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/handover`, { waitUntil: "networkidle" });

    await expect(page.getByTestId("handover-page")).toBeVisible();
    await expect(page.locator("body")).not.toContainText("handover.title");
  });
});