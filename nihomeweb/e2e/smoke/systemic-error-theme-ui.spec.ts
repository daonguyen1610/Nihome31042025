import { expect, test, TEST_USERS } from "../fixtures/auth";

for (const [lang, message, retry] of [
  ["vi", "Lỗi kết nối mạng. Vui lòng kiểm tra internet.", "Thử lại"],
  ["en", "Network error. Please check your connection.", "Retry"],
  ["zh", "网络错误，请检查您的网络连接。", "重试"],
  ["ja", "ネットワークエラー。接続を確認してください。", "再試行"],
]) {
  test(`list failure is localized and recovers in ${lang}`, async ({ page, loginInBrowserAs }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript((language) => localStorage.setItem("nicon_lang", language), lang);
    await page.emulateMedia({ colorScheme: "dark" });
    await page.goto("/admin/customers");
    await expect(page.locator("table tbody tr").first()).toBeVisible();
    let fail = true;
    await page.route("**/api/customers?**", (route) =>
      fail ? route.abort("connectionfailed") : route.continue(),
    );
    await page.reload();
    await expect(page.getByText(message, { exact: true })).toBeVisible();
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).colorScheme)).toBe(
      "light",
    );
    fail = false;
    await page.getByRole("button", { name: retry, exact: true }).click();
    await expect(page.locator("table tbody tr").first()).toBeVisible();
    await expect(page.getByText(message, { exact: true })).toHaveCount(0);
  });
}
