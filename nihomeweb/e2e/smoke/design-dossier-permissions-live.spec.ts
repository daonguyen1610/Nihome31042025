import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("leaders save dossier rights while owner rows stay locked in every language", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.slow();
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON dossier permissions");
  let designId: number | undefined;
  let rootId: number | undefined;
  try {
    designId = await createDesignProject(api, {
      headers,
      customerId,
      name: `NICON – Quyền hồ sơ thiết kế ${Date.now()}`,
    });
    const detail = await api.get(`/api/design-projects/${designId}`, { headers });
    expect(detail.ok()).toBe(true);
    rootId = (await detail.json()).operationalProjectId;
    const root = `/api/operational-projects/${rootId}/team`;
    const users = await api.get("/api/users?take=200", { headers });
    expect(users.ok()).toBe(true);
    const architect = (await users.json()).items.find(
      (item: { phoneNumber: string }) => item.phoneNumber === TEST_USERS.architect.phoneNumber,
    );
    expect(architect).toBeTruthy();
    const added = await api.post(`${root}/members`, {
      headers,
      data: {
        designProjectId: designId,
        userId: architect.id,
        position: "Điều phối phương án kiến trúc",
        startedAt: new Date().toISOString(),
        roles: [{ roleCode: "Architect", scope: "Module", scopeValue: "Design" }],
      },
    });
    expect(added.status(), await added.text()).toBe(201);
    const memberId = ((await added.json()) as { id: number }).id;
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/");
    const translations = [
      {
        lang: "vi",
        locked: "Chủ nhiệm và chủ trì thiết kế luôn có toàn quyền trong hồ sơ.",
        action: "Phân quyền hồ sơ",
      },
      {
        lang: "en",
        locked: "The design manager and design lead always have full rights in this dossier.",
        action: "Dossier permissions",
      },
      {
        lang: "zh",
        locked: "设计负责人和设计主管在该档案中始终拥有全部权限。",
        action: "档案权限",
      },
      {
        lang: "ja",
        locked: "設計責任者と設計リーダーはこの図面で常に全権限を持ちます。",
        action: "図面権限",
      },
    ];
    for (const { lang, locked, action } of translations) {
      await page.evaluate((language) => localStorage.setItem("nicon_lang", language), lang);
      await page.setViewportSize({
        width: lang === "en" ? 768 : lang === "zh" ? 1440 : 390,
        height: 844,
      });
      await page.goto(`/admin/design-projects/${designId}?tab=team`);
      await expect(
        page.getByRole("button", { name: locked, exact: true }).filter({ visible: true }),
      ).toBeDisabled();
      await expect(page.getByText(locked, { exact: true }).filter({ visible: true })).toBeVisible();
      const permissions = page
        .getByRole("button", { name: action, exact: true })
        .filter({ visible: true });
      await permissions.click();
      const dialog = page.getByRole("dialog");
      await expect(dialog.getByRole("checkbox")).toHaveCount(4);
      await dialog.evaluate((element) =>
        Promise.all(element.getAnimations().map((animation) => animation.finished)),
      );
      await page.screenshot({ path: testInfo.outputPath(`permissions-${lang}.png`) });
      await page.keyboard.press("Escape");
    }
    await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
    await page.reload();
    await page
      .getByRole("button", { name: "Dossier permissions", exact: true })
      .filter({ visible: true })
      .click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("checkbox", { name: "View", exact: true }).uncheck();
    await dialog.getByRole("button", { name: "Save permissions", exact: true }).click();
    await expect(
      dialog.getByText("View must stay enabled when granting create/update/delete."),
    ).toBeVisible();
    await dialog.getByRole("checkbox", { name: "View", exact: true }).check();
    await dialog.getByRole("checkbox", { name: "Create", exact: true }).uncheck();
    await dialog.getByRole("checkbox", { name: "Update", exact: true }).uncheck();
    let releaseSave!: () => void;
    const saveGate = new Promise<void>((resolve) => {
      releaseSave = resolve;
    });
    const permissionUrl = `**/team/members/${memberId}/design-permissions`;
    await page.route(permissionUrl, async (route) => {
      await saveGate;
      await route.continue();
    });
    const saveRequest = page.waitForRequest(
      (request) =>
        request.method() === "PUT" &&
        request.url().endsWith(`/team/members/${memberId}/design-permissions`),
    );
    try {
      await dialog.getByRole("button", { name: "Save permissions", exact: true }).click();
      await saveRequest;
      await expect(dialog).toHaveAttribute("aria-busy", "true");
      await page.keyboard.press("Escape");
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: "Close", exact: true }).click();
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("button", { name: "Cancel", exact: true })).toBeDisabled();
    } finally {
      releaseSave();
    }
    await expect(dialog).toHaveCount(0);
    await page.unroute(permissionUrl);
    await page.reload();
    await page
      .getByRole("button", { name: "Dossier permissions", exact: true })
      .filter({ visible: true })
      .click();
    await expect(
      page.getByRole("dialog").getByRole("checkbox", { name: "Create", exact: true }),
    ).not.toBeChecked();
    await page.keyboard.press("Escape");
    const memberCard = page
      .getByRole("listitem")
      .filter({ has: page.getByRole("button", { name: "Dossier permissions", exact: true }) })
      .filter({ hasText: architect.fullName })
      .filter({ visible: true });
    await expect(memberCard.getByText("View: Yes", { exact: true })).toBeVisible();
    await expect(memberCard.getByText("Create: No", { exact: true })).toBeVisible();
    const historyResponse = await api.get(`${root}/history?designProjectId=${designId}`, {
      headers,
    });
    expect(historyResponse.ok()).toBe(true);
    const historyEntry = (
      (await historyResponse.json()) as { id: number; entityType: string; entityId: number }[]
    ).find((item) => item.entityType === "MemberPermissions" && item.entityId === memberId);
    expect(historyEntry).toBeTruthy();
    const historyItem = page.getByTestId(`project-history-MemberPermissions-${historyEntry!.id}`);
    await expect(historyItem).toContainText(architect.fullName);
    await expect(historyItem.getByText("View: Yes", { exact: true })).toBeVisible();
    await expect(historyItem.getByText("Create: No", { exact: true })).toBeVisible();
    await loginInBrowserAs(page, TEST_USERS.architect);
    await page.goto(`/admin/design-projects/${designId}?tab=concept`);
    await expect(page.getByRole("tab", { name: "Concept", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "New option", exact: true })).toHaveCount(0);
  } finally {
    if (designId) await hardDeleteBusinessRoot(api, headers, `/api/design-projects/${designId}`);
    if (rootId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${rootId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});

// These seeded dossiers provide real Basic/Shop stage gates. Only the temporary
// scoped assignment is changed; it is ended again even when an assertion fails.
for (const scenario of [
  { code: "DP-SAMPLE-002", tab: "basic", createLabel: "New document" },
  { code: "DP-SAMPLE-003", tab: "shop", createLabel: "New drawing" },
]) {
  test(`${scenario.tab}: create and update rights remain independent in the browser`, async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const projectsResponse = await api.get("/api/design-projects?pageSize=100", { headers });
    expect(projectsResponse.ok()).toBe(true);
    const project = (await projectsResponse.json()).items.find(
      (item: { projectCode: string }) => item.projectCode === scenario.code,
    );
    expect(project, `seeded dossier ${scenario.code}`).toBeTruthy();
    const usersResponse = await api.get("/api/users?take=200", { headers });
    expect(usersResponse.ok()).toBe(true);
    const user = (await usersResponse.json()).items.find(
      (item: { phoneNumber: string }) =>
        item.phoneNumber === TEST_USERS.structuralEngineer.phoneNumber,
    );
    expect(user).toBeTruthy();
    const root = `/api/operational-projects/${project.operationalProjectId}/team`;
    const teamResponse = await api.get(`${root}?designProjectId=${project.id}`, { headers });
    expect(teamResponse.ok()).toBe(true);
    const existing = (await teamResponse.json()).members.find(
      (item: { userId: number; isActive: boolean }) => item.userId === user.id && item.isActive,
    );
    expect(existing, "fixture must not overwrite an existing active assignment").toBeUndefined();
    const memberRequest = {
      designProjectId: project.id,
      userId: user.id,
      position: "NICON – Kiểm tra quyền hồ sơ bộ môn kết cấu",
      startedAt: new Date().toISOString(),
      roles: [{ roleCode: "StructuralEngineer", scope: "Module", scopeValue: "Design" }],
    };
    const added = await api.post(`${root}/members`, { headers, data: memberRequest });
    expect(added.status(), await added.text()).toBe(201);
    let member = await added.json();
    try {
      await loginInBrowserAs(page, TEST_USERS.structuralEngineer);
      await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
      const errors: string[] = [];
      page.on("pageerror", (error) => errors.push(error.message));
      for (const canCreate of [true, false]) {
        const saved = await api.put(`${root}/members/${member.id}/design-permissions`, {
          headers,
          data: { canView: true, canCreate, canUpdate: !canCreate, canDelete: false },
        });
        expect(saved.ok(), await saved.text()).toBe(true);
        for (const width of [390, 768, 1440]) {
          await page.setViewportSize({ width, height: 900 });
          await page.goto(`/admin/design-projects/${project.id}?tab=${scenario.tab}`);
          await expect(page.getByRole("tab", { name: "Team", exact: true })).toBeVisible();
          const create = page.getByRole("button", { name: scenario.createLabel, exact: true });
          if (canCreate) {
            await expect(create).toBeVisible();
            await create.click();
            const dialog = page.getByRole("dialog");
            await expect(dialog).toBeVisible();
            await expect(
              dialog.getByRole("combobox", { name: "Owner", exact: true }),
            ).toBeEnabled();
            await dialog.getByRole("button", { name: "Cancel", exact: true }).click();
            await expect(dialog).toBeHidden();
          } else {
            await expect(create).toHaveCount(0);
          }
          await expect(page.getByRole("button", { name: "Edit member", exact: true })).toHaveCount(
            0,
          );
          expect(
            await page.evaluate(
              () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
            ),
          ).toBe(true);
          await page.screenshot({
            path: testInfo.outputPath(
              `${scenario.tab}-${canCreate ? "create" : "update"}-${width}.png`,
            ),
            fullPage: true,
          });
        }
      }
      expect(errors).toEqual([]);
    } finally {
      const current = await api.get(`${root}?designProjectId=${project.id}`, { headers });
      expect(current.ok()).toBe(true);
      member = (await current.json()).members.find((item: { id: number }) => item.id === member.id);
      const ended = await api.put(`${root}/members/${member.id}`, {
        headers,
        data: {
          ...memberRequest,
          rowVersion: member.rowVersion,
          endedAt: new Date().toISOString(),
        },
      });
      expect(ended.ok(), await ended.text()).toBe(true);
    }
  });
}
