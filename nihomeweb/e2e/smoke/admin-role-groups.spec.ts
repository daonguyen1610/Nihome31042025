import { test, expect, TEST_USERS } from "../fixtures/auth";

test("administrator manages a role group across mobile and tablet views", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const code = `E2E_TEAM_${Date.now()}`;
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.setViewportSize({ width: 390, height: 844 });

  try {
    await page.goto("/admin/roles");
    await page.getByTestId("rbac-view-groups").click();
    await expect(page.getByTestId("rbac-groups-editor")).toBeVisible();

    await page.getByTestId("rbac-group-CRM").click();
    await page.getByTestId("role-group-delete").click();
    await expect(page.getByRole("alertdialog").getByRole("alert")).toBeVisible();
    await page.getByRole("alertdialog").getByRole("button").first().click();

    await page.getByTestId("role-group-create").click();
    await page.getByTestId("role-group-code").fill(code);
    await page.getByTestId("role-group-name-vi").fill("Ban Điều phối dự án");
    await page.getByTestId("role-group-name-en").fill("Project Coordination");
    await page.getByTestId("role-group-name-zh").fill("项目协调团队");
    await page.getByTestId("role-group-name-ja").fill("プロジェクト調整チーム");
    await page.getByTestId("role-group-order").fill("10000");
    await expect(page.getByTestId("role-group-order")).toHaveValue("10.000");
    await page.getByTestId("role-group-save").click();
    await expect(page.getByTestId(`rbac-group-${code}`)).toBeVisible();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath("role-group-mobile.png"), fullPage: true });

    await page.setViewportSize({ width: 768, height: 1024 });
    await page.getByTestId(`rbac-group-${code}`).click();
    await page.getByTestId("role-group-edit").click();
    await expect(page.getByTestId("role-group-order")).toHaveValue("10.000");
    await page.getByTestId("role-group-name-en").fill("Project Delivery Coordination");
    await page.getByTestId("role-group-save").click();
    await expect(page.getByTestId(`rbac-group-${code}`)).toContainText("Ban Điều phối dự án");
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath("role-group-tablet.png"), fullPage: true });

    await page.getByTestId("role-group-delete").click();
    await page.locator("#deletion-confirmation").fill(code);
    await page.getByRole("alertdialog").getByRole("button").last().click();
    await expect(page.getByTestId(`rbac-group-${code}`)).toHaveCount(0);
  } finally {
    const groups = await api.get("/api/admin/rbac/role-groups", { headers });
    if (groups.ok()) {
      const group = (await groups.json()).find((item: { code: string }) => item.code === code);
      if (group) {
        const preview = await api.get(`/api/admin/rbac/role-groups/${group.id}/deletion-impact`, {
          headers,
        });
        if (preview.ok()) {
          const impact = await preview.json();
          if (impact.canDelete)
            await api.delete(`/api/admin/rbac/role-groups/${group.id}`, {
              headers,
              data: { confirmation: code, planToken: impact.planToken },
            });
        }
      }
    }
  }
});
