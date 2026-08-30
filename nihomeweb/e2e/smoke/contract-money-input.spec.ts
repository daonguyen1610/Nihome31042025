import { expect } from "@playwright/test";
import { test, TEST_USERS } from "../fixtures/auth";

test("the new contract form suggests an editable contract number", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/contracts");

  await page.getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|契約を追加/i }).click();

  const contractNumber = page.locator("#c-number");
  await expect(contractNumber).toHaveValue(/^HD-\d{4}-\d{4,}$/, { timeout: 15_000 });
  await contractNumber.fill("HD-CUSTOM-EDITABLE");
  await expect(contractNumber).toHaveValue("HD-CUSTOM-EDITABLE");
});

test("the new contract form proposes an editable date for a Paid milestone", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/contracts");
  await page.getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|契約を追加/i }).click();
  await page.getByRole("button", { name: /Thêm đợt|Add milestone|添加里程碑|マイルストーンを追加/i }).click();

  const milestone = page.getByTestId("c-milestone-0");
  await milestone.getByRole("combobox").click();
  await page.getByRole("option", { name: /Đã thanh toán|Paid|已付款|支払済/i }).click();

  const actualDate = milestone.locator('input[type="date"]').last();
  const localToday = await page.evaluate(() => {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${now.getFullYear()}-${month}-${day}`;
  });
  await expect(actualDate).toHaveValue(localToday);
  await actualDate.fill("2026-08-30");
  await expect(actualDate).toHaveValue("2026-08-30");

  await milestone.locator("input").nth(0).fill("Paid milestone");
  await milestone.locator('input[type="number"]').fill("100");
  await page.locator("#c-customer-form").click();
  await page.getByRole("option").first().click();
  await actualDate.fill("");
  let createRequests = 0;
  await page.route(new RegExp("/api/(?:v1/)?contracts$"), async route => {
    if (route.request().method() === "POST") createRequests += 1;
    await route.continue();
  });
  await page.getByRole("button", { name: /Lưu|Save|保存/i }).click();
  await expect(page.getByText(/Ngày thanh toán thực tế là bắt buộc|actual payment date is required|必须填写实际付款日期|実際の支払日が必要/i)).toBeVisible();
  await expect(page.getByRole("dialog")).toBeVisible();
  expect(createRequests).toBe(0);

  await milestone.getByRole("combobox").click();
  await page.getByRole("option", { name: /Chưa yêu cầu|Pending|待处理|未請求/i }).click();
  await expect(milestone.locator('input[type="date"]')).toHaveCount(1);
});

/**
 * The contract value field was a bare number input. It now groups thousands, and
 * the grouping must not fight the caret: reformatting on every keystroke pushes
 * the caret to the end whenever the length changes.
 */
test("the contract value field groups thousands without stealing the caret", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/contracts");

  await page.getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|契約を追加/i }).click();

  const value = page.locator("#c-value");
  await expect(value).toBeVisible({ timeout: 15_000 });

  await value.click();
  await value.pressSequentially("1500000000", { delay: 40 });

  // Typing stays raw so the caret is left alone.
  await expect(value).toBeFocused();

  // Blur formats it.
  await page.locator("#c-scope").click();
  await expect(value).toHaveValue("1.500.000.000", { timeout: 15_000 });

  // Clearing leaves it empty, not showing the display em dash.
  // fill("") rather than a select-all chord: Control+A is not select-all on
  // macOS, so the chord silently did nothing there.
  await value.fill("");
  await page.locator("#c-scope").click();
  await expect(value).toHaveValue("");
});
