import { expect, test, TEST_USERS } from "../fixtures/auth";

for (const [lang, cancel, remove] of [
  ["vi", "Huỷ", "Xoá"],
  ["en", "Cancel", "Delete"],
  ["zh", "取消", "删除"],
  ["ja", "キャンセル", "削除"],
]) {
  test(`confirmation uses localized controls and cancel preserves data in ${lang}`, async ({
    page,
    loginInBrowserAs,
  }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript((language) => localStorage.setItem("nicon_lang", language), lang);
    let nativeDialogs = 0;
    page.on("dialog", async (dialog) => {
      nativeDialogs++;
      await dialog.dismiss();
    });
    let writes = 0;
    page.on("request", (request) => {
      if (request.method() === "DELETE") writes++;
    });
    await page.goto("/admin/master-data");
    const row = page.locator("table tbody tr").first();
    await expect(row).toBeVisible();
    const original = await row.textContent();
    const trigger = row.getByRole("button", { name: new RegExp("Delete|Xóa|Xoá|删除|削除", "i") });
    await trigger.click();
    const confirmation = page.getByRole("alertdialog");
    await expect(confirmation).toBeVisible();
    await expect(confirmation.getByRole("button", { name: remove, exact: true })).toBeVisible();
    await confirmation.getByRole("button", { name: cancel, exact: true }).click();
    await expect(confirmation).not.toBeVisible();
    await expect(trigger).toBeFocused();
    await expect(row).toHaveText(original);
    expect(writes).toBe(0);
    expect(nativeDialogs).toBe(0);
  });
}
