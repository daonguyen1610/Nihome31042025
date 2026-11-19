import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const closeNames: Record<string, string> = { vi: "Đóng", en: "Close", zh: "关闭", ja: "閉じる" };

test("work package creation identifies required fields and focuses invalid input", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.goto("/admin/work-packages/projects");
  await page.getByRole("button", { name: "Create work package" }).click();
  const dialog = page.getByRole("dialog", { name: "Create a department work package" });
  await expect(dialog).toBeVisible();
  const name = dialog.getByRole("textbox", { name: "Package name" });
  await expect(name).toHaveAttribute("aria-required", "true");
  await dialog.getByRole("button", { name: "Save changes" }).click();
  await expect(dialog.getByRole("combobox", { name: "Operational project" })).toBeFocused();
  await expect(name).toHaveAttribute("aria-invalid", "true");
  // Work is filed under the project; there is no contract to choose.
  await expect(dialog.getByRole("combobox", { name: "Contract", exact: true })).toHaveCount(0);
  await expect(dialog.getByRole("alert").first()).toBeVisible();
  await name.fill("N");
  await name.blur();
  await expect(name).toHaveAttribute("aria-invalid", "true");
  await name.fill("Hồ sơ thi công NICON");
  await name.blur();
  await expect(name).not.toHaveAttribute("aria-invalid", "true");
  await dialog.getByLabel("Start date").fill("2026-11-10");
  const end = dialog.getByLabel("End date");
  await end.fill("2026-11-09");
  await end.blur();
  await expect(
    dialog.getByText("End date: select a date on or after the start date."),
  ).toBeVisible();
  await end.fill("2026-11-11");
  await end.blur();
  await expect(end).not.toHaveAttribute("aria-invalid", "true");
});

for (const [lang, message] of [
  ["vi", "Tên nhóm công việc: nhập từ 2 đến 300 ký tự."],
  ["en", "Package name: enter between 2 and 300 characters."],
  ["zh", "工作包名称：请输入 2 到 300 个字符。"],
  ["ja", "作業パッケージ名: 2〜300 文字で入力してください。"],
]) {
  test(`create validation explains the name rule in ${lang}`, async ({
    page,
    loginInBrowserAs,
  }, testInfo) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript((language) => localStorage.setItem("nicon_lang", language), lang);
    await page.setViewportSize({ width: lang === "vi" || lang === "zh" ? 390 : 768, height: 900 });
    await page.goto("/admin/work-packages?create=1");
    const dialog = page.getByRole("dialog").filter({ has: page.getByTestId("work-package-name") });
    await expect(dialog).toBeVisible();
    const name = dialog.getByTestId("work-package-name");
    await name.fill("N");
    await name.blur();
    await expect(dialog.getByRole("alert").filter({ hasText: message })).toBeVisible();
    await expect(name).toHaveAttribute("aria-describedby", "create-package-name-error");
    await expect(name).toHaveValue("N");
    await expect
      .poll(async () => {
        const bounds = await dialog.boundingBox();
        return (
          bounds !== null && bounds.x >= 0 && bounds.x + bounds.width <= page.viewportSize()!.width
        );
      })
      .toBe(true);
    const titleBounds = await dialog.getByRole("heading").boundingBox();
    const closeBounds = await dialog
      .getByRole("button", { name: closeNames[lang], exact: true })
      .boundingBox();
    expect(titleBounds!.x + titleBounds!.width).toBeLessThanOrEqual(closeBounds!.x);
    await page.screenshot({ path: testInfo.outputPath(`create-validation-${lang}.png`) });
  });
}

test("failed package save keeps the draft and localizes the network error", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/work-packages/options", { headers });
  expect(response.ok()).toBeTruthy();
  const options = await response.json();
  const departmentIndex = options.departments.findIndex(
    (department: { code: string }) => department.code === "PROCUREMENT",
  );
  expect(departmentIndex).toBeGreaterThanOrEqual(0);
  const lead = options.users.find(
    (user: { roleGroupId: number; canLead: boolean }) =>
      user.roleGroupId === options.departments[departmentIndex].id && user.canLead,
  );
  expect(lead, "seeded procurement lead required").toBeTruthy();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin/work-packages/internal");
  await page.getByRole("button", { name: "Tạo nhóm công việc" }).click();
  const dialog = page.getByRole("dialog", { name: "Tạo nhóm công việc cho phòng ban" });
  const name = dialog.getByTestId("work-package-name");
  const draft = "Hồ sơ bảo trì thiết bị nội bộ NICON";
  await name.fill(draft);
  await dialog.getByTestId("work-package-department").click();
  await page.getByRole("option").nth(departmentIndex).click();
  await dialog.getByTestId("work-package-lead").getByRole("combobox").click();
  await page.getByRole("option", { name: lead.name }).click();
  let saveRequests = 0;
  await page.route("**/api/work-packages", async (route) => {
    if (route.request().method() === "POST") {
      saveRequests++;
      await route.abort("connectionfailed");
    } else await route.continue();
  });
  await dialog.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
  await expect(dialog.getByRole("alert")).toBeVisible();
  await expect(dialog.getByRole("alert")).toHaveText(
    "Lỗi kết nối mạng. Vui lòng kiểm tra internet.",
  );
  await expect(name).toHaveValue(draft);
  await expect(dialog.getByTestId("work-package-lead")).toContainText(lead.name);
  expect(saveRequests).toBe(1);
});

// NICON 19/11/2026: the package's assigner is the receiving department's head
// (TBP) by default, so the requester does not have to guess who leads it.
test("the assigner defaults to the receiving department's head and follows the department", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const options = await (await api.get("/api/work-packages/options", { headers })).json();
  const headOf = (code: string) => {
    const department = options.departments.find((item: { code: string }) => item.code === code);
    return {
      index: options.departments.indexOf(department),
      head: options.users.find(
        (user: { roleGroupId: number; roleCode: string; canLead: boolean }) =>
          user.roleGroupId === department.id && user.canLead && user.roleCode.endsWith("_MANAGER"),
      ) as { name: string },
    };
  };
  const procurement = headOf("PROCUREMENT");
  const crm = headOf("CRM");
  expect(procurement.head && crm.head, "seeded department heads required").toBeTruthy();

  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin/work-packages/projects");
  await page.getByRole("button", { name: "Tạo nhóm công việc" }).click();
  const dialog = page.getByRole("dialog", { name: "Tạo nhóm công việc cho phòng ban" });
  await expect(dialog.getByText("Người phân công (TBP phòng nhận)")).toBeVisible();
  await dialog.getByTestId("work-package-department").click();
  await page.getByRole("option").nth(procurement.index).click();
  await expect(dialog.getByTestId("work-package-lead")).toContainText(procurement.head.name);
  await dialog.getByTestId("work-package-department").click();
  await page.getByRole("option").nth(crm.index).click();
  await expect(dialog.getByTestId("work-package-lead")).toContainText(crm.head.name);
});

// The assigner can be changed after creation, to anyone in the receiving
// department who may assign work.
test("editing a work package hands it to another assigner", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const options = await (await api.get("/api/work-packages/options", { headers })).json();
  const procurement = options.departments.find(
    (item: { code: string }) => item.code === "PROCUREMENT",
  );
  const assigners = options.users.filter(
    (user: { roleGroupId: number; canLead: boolean }) =>
      user.roleGroupId === procurement.id && user.canLead,
  ) as { userId: number; name: string; roleCode: string }[];
  const current = assigners.find((user) => user.roleCode.endsWith("_MANAGER"))!;
  const next = assigners.find((user) => user.userId !== current.userId)!;
  expect(current && next, "two procurement assigners are seeded").toBeTruthy();
  const projects = await (
    await api.get("/api/operational-projects?pageSize=1", { headers })
  ).json();
  const created = await api.post("/api/work-packages", {
    headers: { ...headers, "Idempotency-Key": crypto.randomUUID() },
    data: {
      kind: "Project",
      name: `Chuẩn bị vật tư đổi người phân công ${Date.now()}`,
      operationalProjectId: projects.items[0].id,
      departmentRoleGroupId: procurement.id,
      leadUserId: current.userId,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const packageId = (await created.json()).id as number;

  try {
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`/admin/work-packages/${packageId}`);
    await page.getByTestId("work-package-edit").click();
    const dialog = page.getByRole("dialog");
    const assigner = dialog.getByTestId("work-package-edit-assigner");
    await expect(assigner).toContainText(current.name);
    await assigner.getByRole("combobox").click();
    await page.getByRole("option", { name: new RegExp(next.name) }).click();
    await dialog.getByRole("button", { name: /Lưu/ }).click();
    await expect(dialog).toHaveCount(0);
    const saved = await (await api.get(`/api/work-packages/${packageId}`, { headers })).json();
    expect(saved.leadUserId).toBe(next.userId);
    await expect(page.getByText(next.name).first()).toBeVisible();
  } finally {
    await hardDeleteBusinessRoot(api, headers, `/api/work-packages/${packageId}`);
  }
});
