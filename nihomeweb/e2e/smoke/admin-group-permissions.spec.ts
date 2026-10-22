import { test, expect, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

// These journeys temporarily edit the seeded CRM baseline and restore it.
test.describe.configure({ mode: "serial" });

for (const width of [390, 768, 1440]) {
  test(`group common permissions can be inspected, reset and saved at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const groups = await (await api.get("/api/admin/rbac/role-groups", { headers })).json();
    const group = groups.find((item: { code: string }) => item.code === "CRM");
    const original: string[] = group.baselinePermissions;
    const roles = await (await api.get("/api/admin/rbac/roles", { headers })).json();
    const sale = roles.find((item: { code: string }) => item.code === "SALE");
    const roleBefore = await (
      await api.get(`/api/admin/rbac/roles/${sale.id}/permissions`, { headers })
    ).json();
    try {
      await page.setViewportSize({ width, height: 900 });
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.goto("/admin/roles");
      await page.getByTestId("rbac-view-groups").click();
      await page.getByTestId("rbac-group-CRM").click();
      const trigger = page.getByTestId("rbac-open-group-permissions");
      await expect(trigger).toHaveText("Xem/chỉnh quyền mẫu");
      await trigger.focus();
      await page.keyboard.press("Enter");
      const dialog = page.getByTestId("rbac-group-permissions-dialog");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("heading", { level: 2 })).toContainText("Quyền chung của nhóm");
      const save = page.getByTestId("rbac-save-baseline");
      await expect(save).toBeDisabled();
      const search = page.locator("#rbac-baseline-search");
      await search.fill("no-such-nicon-permission");
      await expect(dialog.getByRole("checkbox")).toHaveCount(0);
      await expect(dialog).toContainText("Không tìm thấy quyền phù hợp với bộ lọc.");
      await search.fill("dashboard.view");
      const checkbox = dialog.getByRole("checkbox", { name: "CRM dashboard.view", exact: true });
      expect(await checkbox.isChecked()).toBe(original.includes("dashboard.view"));
      await checkbox.click();
      await expect(save).toBeEnabled();
      await page.getByTestId("rbac-reset-baseline").click();
      await expect(save).toBeDisabled();
      await checkbox.click();
      await page.keyboard.press("Escape");
      const discard = page.getByRole("alertdialog");
      await expect(discard).toBeVisible();
      await discard.getByRole("button", { name: "Tiếp tục chỉnh sửa", exact: true }).click();
      await expect(discard).toHaveCount(0);
      await expect(dialog).toBeVisible();
      await expect(save).toBeEnabled();
      await page.keyboard.press("Escape");
      await expect(discard).toBeVisible();
      await discard.getByRole("button", { name: "Bỏ thay đổi", exact: true }).click();
      await expect(dialog).toHaveCount(0);
      await expect(trigger).toBeFocused();
      await trigger.click();
      await search.fill("dashboard.view");
      expect(await checkbox.isChecked()).toBe(original.includes("dashboard.view"));
      await checkbox.click();
      // Search and module filters must not drop other selected permissions.
      await page.locator("#rbac-baseline-module").selectOption("crm");
      await expect(dialog.getByRole("checkbox")).toHaveCount(0);
      await page.locator("#rbac-baseline-module").selectOption("");
      const response = page.waitForResponse(
        (res) =>
          res.url().includes(`/role-groups/${group.id}/baseline`) &&
          res.request().method() === "PUT",
      );
      await save.click();
      expect((await response).status()).toBe(200);
      await expect(save).toBeDisabled();
      await page.reload();
      await page.getByTestId("rbac-view-groups").click();
      await page.getByTestId("rbac-group-CRM").click();
      await trigger.click();
      await search.fill("dashboard.view");
      expect(await checkbox.isChecked()).toBe(!original.includes("dashboard.view"));
      const saved = await (await api.get("/api/admin/rbac/role-groups", { headers })).json();
      const expected = original.includes("dashboard.view")
        ? original.filter((code) => code !== "dashboard.view")
        : [...original, "dashboard.view"];
      expect(
        saved.find((item: { id: number }) => item.id === group.id).baselinePermissions.sort(),
      ).toEqual(expected.sort());
      const roleAfter = await (
        await api.get(`/api/admin/rbac/roles/${sale.id}/permissions`, { headers })
      ).json();
      expect(roleAfter.permissions).toEqual(roleBefore.permissions);
      await search.fill("");
      const dimensions = await dialog.evaluate((element) => ({
        width: element.clientWidth,
        scroll: element.scrollWidth,
        height: element.clientHeight,
        scrollHeight: element.scrollHeight,
      }));
      expect(dimensions.scroll).toBeLessThanOrEqual(dimensions.width);
      expect(dimensions.scrollHeight).toBeLessThanOrEqual(dimensions.height);
      await expect(save).toBeInViewport();
      await dialog.screenshot({ path: testInfo.outputPath(`group-permissions-${width}.png`) });
      await page.keyboard.press("Escape");
      await expect(trigger).toBeFocused();
      await page.getByTestId("rbac-group-DESIGN").click();
      await trigger.click();
      await expect(dialog.getByRole("heading", { level: 2 })).toContainText("Thiết kế");
      await expect(save).toBeDisabled();
    } finally {
      const restored = await api.put(`/api/admin/rbac/role-groups/${group.id}/baseline`, {
        headers,
        data: { permissions: original },
      });
      expect(restored.status()).toBe(200);
    }
  });
}

for (const editable of [false, true]) {
  test(`group permission dialog handles ${editable ? "rejected saves without losing edits" : "view-only access in four languages"}`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const code = `BASELINE_UI_${Date.now()}`;
    const user = {
      phoneNumber: `09${String(Date.now()).slice(-8)}`,
      password: "Admin@123",
      role: code,
    };
    const created = await api.post("/api/admin/rbac/roles", {
      headers,
      data: {
        code,
        name: "NICON quyền chung kiểm thử",
        permissions: [
          "dashboard.view",
          "rbac.roles.view",
          ...(editable ? ["rbac.roles.manage"] : []),
        ],
      },
    });
    expect(created.status()).toBe(201);
    const { id: roleId } = await created.json();
    let userId: number | undefined;
    try {
      const createdUser = await api.post("/api/users", {
        headers,
        data: {
          ...user,
          fullName: "NICON kiểm thử quyền chung",
          email: `${code.toLowerCase()}@e2e.nihome.local`,
        },
      });
      expect(createdUser.status()).toBe(201);
      userId = (await createdUser.json()).id;
      await loginInBrowserAs(page, user);
      await page.goto("/admin/roles");
      await page.getByTestId("rbac-view-groups").click();
      await page.getByTestId("rbac-group-CRM").click();
      await page.getByTestId("rbac-open-group-permissions").click();
      const dialog = page.getByTestId("rbac-group-permissions-dialog");
      if (editable) {
        const baselineBefore = await (
          await api.get("/api/admin/rbac/role-groups", { headers })
        ).json();
        await page.locator("#rbac-baseline-search").fill("users.manage");
        const checkbox = dialog.getByRole("checkbox", { name: "CRM users.manage", exact: true });
        await expect(checkbox).not.toBeChecked();
        await checkbox.click();
        const response = page.waitForResponse(
          (res) => res.url().includes("/baseline") && res.request().method() === "PUT",
        );
        await page.getByTestId("rbac-save-baseline").click();
        expect((await response).status()).toBe(403);
        await expect(dialog.getByRole("alert")).toContainText("Bạn không được cấp thêm quyền");
        const baselineAfter = await (
          await api.get("/api/admin/rbac/role-groups", { headers })
        ).json();
        expect(baselineAfter).toEqual(baselineBefore);
        await expect(checkbox).toBeChecked();
        await expect(page.getByTestId("rbac-save-baseline")).toBeEnabled();
        await page.getByTestId("rbac-reset-baseline").click();
        await expect(checkbox).not.toBeChecked();
      } else {
        for (const checkbox of await dialog.getByRole("checkbox").all())
          await expect(checkbox).toBeDisabled();
        await expect(page.getByTestId("rbac-save-baseline")).toHaveCount(0);
        await expect(page.getByTestId("rbac-reset-baseline")).toHaveCount(0);
        for (const [language, label] of [
          ["vi", "Xem quyền mẫu"],
          ["en", "View baseline permissions"],
          ["zh", "查看权限基线"],
          ["ja", "権限基準を表示"],
        ]) {
          await page.evaluate((lang) => localStorage.setItem("nicon_lang", lang), language);
          await page.reload();
          await page.getByTestId("rbac-view-groups").click();
          await expect(page.getByTestId("rbac-open-group-permissions")).toHaveText(label);
          await page.getByTestId("rbac-open-group-permissions").click();
          await expect(dialog).not.toContainText("adminRbac.groups.");
          await page.keyboard.press("Escape");
        }
      }
    } finally {
      if (Number.isInteger(userId)) {
        execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${userId}`);
        execSql(`DELETE FROM refresh_tokens WHERE UserId = ${userId}`);
        execSql(`DELETE FROM users WHERE Id = ${userId}`);
      }
      expect((await api.delete(`/api/admin/rbac/roles/${roleId}`, { headers })).status()).toBe(200);
    }
  });
}
