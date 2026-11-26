import type { Page } from "@playwright/test";
import { expect, test, TEST_USERS } from "../fixtures/auth";

// The preview is stubbed: a real move needs a real Google Drive. No test here moves files.
const PREVIEW_URL = /\/api\/(?:v1\/)?site-settings\/google-drive\/tree-migration\/preview$/;

const preview = {
  changes: [
    {
      category: "DesignShopDrawing",
      from: "02_Thiet_ke/03_Chi_tiet_ShopDrawing",
      to: "03_ThietKe/03_ChiTiet_ShopDrawing",
    },
  ],
  projectCount: 4,
  documentCount: 1250,
  untrackedFileCount: 0,
  blockers: ["DRIVE_SYNC_ENABLED"],
  latestMigration: null,
};

const START =
  /^(Chuyển sang cây thư mục PRD v2|Move to the PRD v2 folder tree|迁移到 PRD v2 文件夹结构|PRD v2 フォルダー構成に移行)$/;
const RESUME = /^(Tiếp tục chuyển|Continue move|继续迁移|移行を再開)$/;
const UNDO = /^(Hoàn tác|Undo|撤销|元に戻す)$/;

async function openDriveSettings(page: Page, body: unknown) {
  await page.route(PREVIEW_URL, (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) }),
  );
  await page.goto("/admin/settings?tab=drive");
  return page.getByTestId("drive-tree-migration");
}

test("sync being enabled locks the move and says how to unlock it on a phone", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.setViewportSize({ width: 390, height: 844 });
  const panel = await openDriveSettings(page, preview);

  await expect(panel.getByRole("table")).toContainText("02_Thiet_ke/03_Chi_tiet_ShopDrawing");
  await expect(panel.getByRole("table")).toContainText("03_ThietKe/03_ChiTiet_ShopDrawing");
  await expect(panel.getByRole("button", { name: START })).toBeDisabled();
  await expect(panel).toContainText(
    /Tắt đồng bộ trước khi chuyển|Turn off synchronization before moving|迁移或撤销前请先关闭同步|移行や取り消しの前に同期をオフ/,
  );
  await expect(panel.getByRole("button", { name: UNDO })).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    ),
  ).toBeLessThanOrEqual(0);
});

test("a half-done undo only offers to finish the undo, after a confirmation", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let moves = 0;
  await page.route(/\/site-settings\/google-drive\/tree-migration(?:\/\d+\/undo)?$/, (route) => {
    moves++;
    return route.abort();
  });
  const panel = await openDriveSettings(page, {
    ...preview,
    changes: [],
    blockers: [],
    latestMigration: { id: 5, status: "Failed", undoStarted: true, error: "Drive unavailable" },
  });

  await expect(panel.getByRole("button", { name: UNDO })).toBeEnabled();
  await expect(panel.getByRole("button", { name: START })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: RESUME })).toHaveCount(0);

  await panel.getByRole("button", { name: UNDO }).click();
  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: /^(Hủy|Huỷ|Cancel|取消|キャンセル)$/ }).click();
  await expect(dialog).toBeHidden();
  expect(moves).toBe(0);
});
