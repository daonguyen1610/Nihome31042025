import { expect, test, TEST_USERS } from "../fixtures/auth";

/**
 * NIH-165 browser coverage stays intentionally UI-focused. CRUD, owner scoping,
 * document, evaluation, and permission contracts are covered by backend
 * integration tests.
 */
test.describe("NIH-165 — Procurement vendors", () => {
  test("SUPER_ADMIN can render list, detail, and create validation", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (error) => jsErrors.push(error.message));

    const token = await loginAs(TEST_USERS.superAdmin);
    const response = await api.get("/api/procurement/vendors?page=1&pageSize=1", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    const firstVendor = (await response.json()).items?.[0] as { id: number; companyName: string } | undefined;
    expect(firstVendor, "Expected the procurement vendor sample data to be seeded").toBeTruthy();

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/procurement/vendors`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("vendor-list-page")).toBeVisible();
    await expect(page.getByRole("heading", { level: 1 })).not.toContainText("vendors.title");

    await page.goto(`${baseURL}/admin/procurement/vendors/${firstVendor!.id}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("vendor-detail-page")).toBeVisible();
    await expect(page.getByRole("heading", { level: 1, name: firstVendor!.companyName })).toBeVisible();

    await page.goto(`${baseURL}/admin/procurement/vendors/new`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("vendor-form-page")).toBeVisible();
    await page.getByRole("button", { name: /Save|Lưu|保存|儲存/i }).click();
    await expect(page.locator("#vendor-code")).toHaveAttribute("aria-invalid", "true");
    await expect(page.locator("#company-name")).toHaveAttribute("aria-invalid", "true");

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
  });
});
