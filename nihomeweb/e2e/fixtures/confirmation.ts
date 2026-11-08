import { expect, type Page } from "@playwright/test";

/** Complete the application's accessible confirmation, including popup gestures. */
export async function confirmAction(page: Page, destructive = true): Promise<void> {
  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await dialog
    .getByRole("button", {
      name: destructive
        ? /^(Delete|Xóa|Xoá|删除|削除)$/i
        : /^(Confirm action|Xác nhận thao tác|确认操作|操作を確認)$/i,
    })
    .click();
  await expect(dialog).toBeHidden();
}
