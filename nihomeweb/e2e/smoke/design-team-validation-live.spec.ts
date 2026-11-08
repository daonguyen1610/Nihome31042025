import { test, expect, TEST_USERS } from "../fixtures/auth";

test("empty member submission identifies and focuses the required user field", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/design-projects?pageSize=100", { headers });
  expect(response.ok()).toBe(true);
  const project = (await response.json()).items.find(
    (item: { projectCode: string }) => item.projectCode === "DP-SAMPLE-002",
  );
  expect(project, "seeded design dossier DP-SAMPLE-002").toBeTruthy();

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/admin/design-projects/${project.id}?tab=team`);
  await page.getByRole("button", { name: "Thêm thành viên", exact: true }).click();

  const check = expect.configure({ soft: true, timeout: 1000 });
  const dialog = page.getByRole("dialog", { name: "Thêm thành viên dự án" });
  const user = dialog.getByRole("combobox", { name: "Người dùng", exact: true });
  const mutations: string[] = [];
  page.on("request", (request) => {
    if (
      !["GET", "HEAD", "OPTIONS"].includes(request.method()) &&
      new URL(request.url()).pathname.includes("/team")
    )
      mutations.push(`${request.method()} ${new URL(request.url()).pathname}`);
  });

  await dialog.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
  await expect(dialog.getByText("Vui lòng chọn người dùng đang hoạt động.")).toBeVisible();
  await check(user).toBeFocused();
  await check(user).toHaveAttribute("aria-invalid", "true");
  await check(user).toHaveAccessibleDescription("Vui lòng chọn người dùng đang hoạt động.");
  await check(dialog.getByRole("alert")).toHaveText("Vui lòng chọn người dùng đang hoạt động.");
  await check(user).toHaveAttribute("aria-required", "true");
  await check(dialog.getByLabel("Chức danh dự án", { exact: false })).toHaveAttribute(
    "required",
    "",
  );
  await check(dialog.getByLabel("Ngày bắt đầu", { exact: false })).toHaveAttribute("required", "");
  await check(dialog.getByRole("combobox", { name: "Vai trò", exact: true })).toHaveAttribute(
    "aria-required",
    "true",
  );
  // Repeating the same invalid submission must restore focus even when the
  // validation message and invalid field have not changed.
  await dialog.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
  await check(user).toBeFocused();
  expect(mutations, "empty form must not send team write requests").toEqual([]);
  await page.screenshot({ path: testInfo.outputPath("invalid-member-dialog-390.png") });

  await dialog.getByRole("button", { name: "Huỷ", exact: true }).click();
  await expect(dialog).toHaveCount(0);
});
