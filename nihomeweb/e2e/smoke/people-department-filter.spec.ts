import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

test("inactive departments retain their roles under the unassigned filter", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/admin/rbac/role-groups", { headers });
  expect(response.ok(), await response.text()).toBeTruthy();
  const procurement = (await response.json()).find(
    (group: { code: string }) => group.code === "PROCUREMENT",
  );
  expect(procurement).toBeTruthy();
  try {
    execSql(
      `SET QUOTED_IDENTIFIER ON; UPDATE role_groups SET IsActive=0 WHERE Id=${procurement.id};`,
    );
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/users");
    await page.getByLabel("Department / Group", { exact: true }).click();
    await expect(page.getByRole("option", { name: "Procurement", exact: true })).toHaveCount(0);
    await page.getByRole("option", { name: "No department assigned", exact: true }).click();
    await page.getByLabel("Role", { exact: true }).click();
    await expect(page.getByRole("option", { name: "Procurement", exact: true })).toBeVisible();
    await page.getByRole("option", { name: "Procurement", exact: true }).click();
    await expect(page.getByText("No department assigned", { exact: true }).first()).toBeVisible();
  } finally {
    execSql(
      `SET QUOTED_IDENTIFIER ON; UPDATE role_groups SET IsActive=1 WHERE Id=${procurement.id};`,
    );
  }
});

test("department filtering clears hidden user selections and preserves form role choices", async ({
  page,
  loginInBrowserAs,
}) => {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/users");
  const checkbox = page.locator('button[role="checkbox"]:visible').first();
  await checkbox.check();
  await expect(page.getByRole("button", { name: "Clear selection", exact: true })).toBeVisible();
  await page.getByLabel("Department / Group", { exact: true }).click();
  await page.getByRole("option", { name: "Procurement", exact: true }).click();
  await expect(page.getByRole("button", { name: "Clear selection", exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Add user", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByRole("combobox").click();
  await expect(page.getByRole("option", { name: "Administrator", exact: true })).toBeVisible();
  await expect(page.getByRole("option", { name: "Procurement", exact: true })).toBeVisible();
  await page.keyboard.press("Escape");
  await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
});

test("person picker identifies roles, filters departments and preserves selection", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/design-projects");
  const manager = page
    .locator("label")
    .filter({ hasText: /^Design manager$/ })
    .locator("..")
    .getByRole("combobox");
  await manager.click();
  const department = page.getByLabel("Department / Group", { exact: true });
  await expect(department).toBeVisible();
  const procurement = department.locator("option").filter({ hasText: /Procurement/i });
  await expect(procurement).toHaveCount(1);
  const departmentId = await procurement.getAttribute("value");
  await department.selectOption(departmentId!);

  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get(`/api/users?departmentId=${departmentId}&take=100`, { headers });
  expect(response.ok(), await response.text()).toBeTruthy();
  const { items } = (await response.json()) as {
    items: { id: number; fullName: string | null; email: string; roleName: string }[];
  };
  expect(items.length).toBeGreaterThan(0);
  const picker = page.locator('[data-people-picker="true"]');
  // One clear-filter option remains available alongside the matching people.
  await expect(picker.locator("[cmdk-list]").getByRole("option")).toHaveCount(items.length + 1);
  for (const person of items) {
    await expect(
      picker.getByRole("option").filter({ hasText: person.fullName || person.email }),
    ).toContainText(/Procurement/i);
  }
  const search = picker.getByRole("combobox", { name: "Search name, role or email…", exact: true });
  await search.fill("nicon-no-matching-person-948372");
  await expect(picker).toContainText("No matching people. Change the filter or search.");
  await search.fill("");
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    const bounds = await picker.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.x).toBeGreaterThanOrEqual(0);
    expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
    await page.screenshot({ path: testInfo.outputPath(`people-picker-${width}.png`) });
  }
  const chosen = items[0];
  await picker
    .getByRole("option")
    .filter({ hasText: chosen.fullName || chosen.email })
    .click();
  await expect(manager).toContainText(chosen.fullName || chosen.email);
  await expect(manager).toContainText(/Procurement/i);
  await manager.click();
  await expect(department).toHaveValue("all");
  await department.selectOption(departmentId!);
  await page.keyboard.press("Escape");
  await expect(manager).toContainText(chosen.fullName || chosen.email);
  expect(errors).toEqual([]);
});

test("design review retains chosen recipients and draft while filtering people", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.designManager)}` };
  const response = await api.get("/api/design-projects?search=DP-SAMPLE-003&pageSize=10", {
    headers,
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const project = (await response.json()).items.find(
    (item: { projectCode: string }) => item.projectCode === "DP-SAMPLE-003",
  );
  expect(project).toBeTruthy();
  await loginInBrowserAs(page, TEST_USERS.designManager);
  await page.goto(`/admin/design-projects/${project.id}?tab=reviews`);
  await page.getByTestId("create-design-review").click();
  const dialog = page.getByRole("dialog");
  const title = dialog.locator("input").first();
  await title.fill("NICON coordination review draft");
  const checklist = dialog.getByTestId("people-checklist");
  const first = checklist.getByRole("checkbox").first();
  await first.check();
  const chip = checklist.getByRole("button", { name: /^Deselect:/ }).first();
  const identity = await chip.textContent();
  expect(identity).toContain(" · ");
  await checklist.getByPlaceholder("Search name, role or email…").fill("nicon-no-recipient-938472");
  await expect(checklist).toContainText("No matching people. Change the filter or search.");
  await expect(chip).toHaveText(identity!);
  await expect(title).toHaveValue("NICON coordination review draft");
  await checklist.getByPlaceholder("Search name, role or email…").fill("");
  const filter = checklist.getByLabel("Department / Group", { exact: true });
  const options = filter.locator("option");
  expect(await options.count()).toBeGreaterThan(1);
  await filter.selectOption((await options.last().getAttribute("value"))!);
  await expect(chip).toHaveText(identity!);
  await filter.selectOption("all");
  await expect(first).toBeChecked();
  await chip.click();
  await expect(first).not.toBeChecked();
});
