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

test("the new contract form keeps new milestones Pending", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/contracts");
  await page.locator("#c-project").click();
  await page.getByRole("option").filter({ hasText: /PJ-/ }).first().click();
  await page.getByRole("button", { name: /Thêm hợp đồng|New contract|新增合同|契約を追加/i }).click();
  await page.getByRole("button", { name: /Thêm đợt|Add milestone|添加里程碑|マイルストーンを追加/i }).click();

  const milestone = page.getByTestId("c-milestone-0");
  await expect(milestone.getByText(/Chưa yêu cầu|Pending|待处理|未請求/i)).toBeVisible();
  await expect(milestone.getByRole("combobox")).toHaveCount(0);
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
