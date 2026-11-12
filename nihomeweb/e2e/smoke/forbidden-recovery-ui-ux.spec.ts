import { test, expect, TEST_USERS } from "../fixtures/auth";

const localizedRecovery = [
  {
    language: "vi",
    recovery:
      "Bạn chưa có quyền truy cập khu vực này. Hãy đăng xuất rồi đăng nhập bằng tài khoản có quyền phù hợp, hoặc nhờ quản trị viên xem xét quyền truy cập.",
    switchAccount: "Đăng xuất và đổi tài khoản",
  },
  {
    language: "en",
    recovery:
      "You do not have access to this area. Sign out and use an account with the right access, or ask an administrator to review your permissions.",
    switchAccount: "Sign out and switch account",
  },
  {
    language: "zh",
    recovery:
      "您没有访问此区域的权限。请先退出，再使用具有相应权限的账户登录；或者请管理员检查您的访问权限。",
    switchAccount: "退出并切换账户",
  },
  {
    language: "ja",
    recovery:
      "このエリアへのアクセス権がありません。ログアウトして適切な権限を持つアカウントでログインするか、管理者にアクセス権を確認してもらってください。",
    switchAccount: "ログアウトしてアカウントを切り替える",
  },
];

test("forbidden access explains recovery in every language and lets staff switch accounts", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.warehouse);
  await page.goto("/admin/users");
  await expect(page.getByRole("heading", { name: "403" })).toBeVisible();

  await page.goto("/forbidden");
  for (const copy of localizedRecovery) {
    await page.evaluate((language) => localStorage.setItem("nicon_lang", language), copy.language);
    await page.reload();
    await expect(page.getByText(copy.recovery, { exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: copy.switchAccount })).toBeVisible();
  }

  await page.getByRole("button", { name: "ログアウトしてアカウントを切り替える" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.locator("input").first()).toBeVisible();
});
