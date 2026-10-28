import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("design team assigns roles in the current design project without a scope picker", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const projects = await api.get("/api/design-projects?page=1&pageSize=1", { headers });
  expect(projects.status()).toBe(200);
  const projectId = (await projects.json()).items[0]?.id;
  expect(projectId).toBeGreaterThan(0);
  const detail = await api.get(`/api/design-projects/${projectId}`, { headers });
  expect(detail.status()).toBe(200);
  const operationalProjectId = (await detail.json()).operationalProjectId;
  expect(operationalProjectId).toBeGreaterThan(0);

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/admin/design-projects/${projectId}?tab=team`);
  const teamTab = page.getByRole("tab", { name: "Team" });
  await expect(teamTab).toHaveAttribute("data-state", "active");
  const nextTabArrow = page.getByTestId("scrollable-tabs-next");
  const teamTabBounds = await teamTab.boundingBox();
  const nextArrowBounds = await nextTabArrow.boundingBox();
  expect(teamTabBounds).not.toBeNull();
  expect(nextArrowBounds).not.toBeNull();
  expect(teamTabBounds!.x + teamTabBounds!.width).toBeLessThanOrEqual(nextArrowBounds!.x);
  await page.getByRole("button", { name: "Add member" }).click();

  const dialog = page.getByRole("dialog");
  await expect(
    dialog.getByText("Choose a user and assign roles for this design project."),
  ).toBeVisible();
  await expect(dialog.getByText("Roles in this design project")).toBeVisible();
  await expect(dialog.getByText("Design module")).toBeVisible();
  await expect(dialog.getByText("End date")).toHaveCount(0);
  await expect(dialog.getByText("Roles / RACI / scope")).toHaveCount(0);
  await expect(dialog.getByTestId("design-team-role-row").getByRole("combobox")).toHaveCount(1);
  await expect(dialog.getByTestId("design-team-role-row").getByRole("combobox")).toHaveText(
    "Select a role",
  );
  const roleSelect = await dialog
    .getByTestId("design-team-role-row")
    .getByRole("combobox")
    .boundingBox();
  const removeRole = await dialog.getByRole("button", { name: "Remove role" }).boundingBox();
  expect(roleSelect).not.toBeNull();
  expect(removeRole).not.toBeNull();
  expect(removeRole!.x).toBeGreaterThan(roleSelect!.x + roleSelect!.width - 10);
  expect(removeRole!.y).toBeLessThan(roleSelect!.y + roleSelect!.height);
  await page.screenshot({ path: testInfo.outputPath("design-team-role-mobile.png") });
  await page.setViewportSize({ width: 768, height: 900 });
  const dialogBounds = await dialog.boundingBox();
  expect(dialogBounds?.width).toBeLessThanOrEqual(768);
  await dialog.getByRole("button", { name: "Cancel" }).click();
  await expect(
    page.getByTestId("design-project-team-tab").locator("ul li p.font-semibold").first(),
  ).toHaveCSS("text-overflow", "clip");
  await expect(
    page.getByTestId("design-project-team-tab").locator("ul li p").filter({ hasText: "@" }).first(),
  ).not.toContainText(" · ");
  await page.setViewportSize({ width: 1440, height: 900 });
  await expect(
    page.getByRole("columnheader", { name: "Roles in this design project" }),
  ).toBeVisible();

  await page.goto(`/admin/operational-projects/${operationalProjectId}`);
  await page.getByRole("button", { name: "Add member" }).click();
  const sharedProjectDialog = page.getByRole("dialog");
  await expect(sharedProjectDialog.getByText("Roles / RACI / scope")).toBeVisible();
  await expect(
    sharedProjectDialog.getByTestId("design-team-role-row").getByRole("combobox"),
  ).toHaveCount(3);
});

test("design team saves a role scoped to its current design project", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "design role");
  let designProjectId: number | undefined;
  let operationalProjectId: number | undefined;
  try {
    designProjectId = await createDesignProject(api, {
      headers,
      name: `E2E design role ${Date.now()}`,
      customerId,
    });
    const design = await api.get(`/api/design-projects/${designProjectId}`, { headers });
    expect(design.status()).toBe(200);
    operationalProjectId = (await design.json()).operationalProjectId;
    const users = await api.get("/api/users?take=200", { headers });
    expect(users.status()).toBe(200);
    const architect = (await users.json()).items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.architect.phoneNumber,
    );
    expect(architect?.id).toBeGreaterThan(0);
    const candidates = await api.get(
      `/api/operational-projects/${operationalProjectId}/team/candidates`,
      { headers },
    );
    expect(candidates.status()).toBe(200);
    const candidate = (await candidates.json()).find(
      (item: { userId: number }) => item.userId === architect.id,
    );
    expect(candidate).toBeTruthy();

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/admin/design-projects/${designProjectId}?tab=team`);
    await page.getByRole("button", { name: "Add member" }).click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox").first().click();
    await page.getByRole("option").filter({ hasText: candidate.email }).click();
    await dialog.locator('input[maxlength="150"]').fill("Architectural concept coordinator");
    await dialog.getByTestId("design-team-role-row").getByRole("combobox").click();
    await page.getByRole("option").filter({ hasText: "Architect · R" }).click();
    const created = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/operational-projects/${operationalProjectId}/team/members` &&
        response.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: "Save changes" }).click();
    const response = await created;
    expect(response.status()).toBe(201);
    const member = await response.json();
    expect(member.roles).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ roleCode: "Architect", scope: "Module", scopeValue: "Design" }),
      ]),
    );
    expect(member.roles.some((role: { scope: string }) => role.scope === "Project")).toBe(false);
    await expect(
      page
        .locator("li")
        .filter({ hasText: candidate.email })
        .getByRole("button", { name: "End member assignment" }),
    ).toBeVisible();

    const team = await api.get(`/api/operational-projects/${operationalProjectId}/team`, {
      headers,
    });
    expect(team.status()).toBe(200);
    const sharedMember = (await team.json()).members.find(
      (item: { roles: Array<{ scope: string }> }) =>
        item.roles.some((role) => role.scope === "Project"),
    );
    expect(sharedMember, "the shared project manager role must remain").toBeTruthy();
    await expect(
      page
        .locator("li")
        .filter({ hasText: sharedMember.email })
        .getByRole("button", { name: "End member assignment" }),
    ).toHaveCount(0);
    await page
      .locator("li")
      .filter({ hasText: sharedMember.email })
      .getByRole("button", { name: "Edit member" })
      .click();
    const editDialog = page.getByRole("dialog");
    await expect(editDialog.getByText("Update roles in this design project.")).toBeVisible();
    const sharedRole = editDialog
      .getByTestId("design-team-role-row")
      .filter({ hasText: "Project" });
    await expect(sharedRole.getByRole("combobox")).toHaveCount(0);
    await editDialog
      .locator('input[maxlength="150"]')
      .fill("Project manager for design coordination");
    await editDialog.getByRole("button", { name: "Save changes" }).click();
    await expect(editDialog).toHaveCount(0);
    const afterEdit = await api.get(`/api/operational-projects/${operationalProjectId}/team`, {
      headers,
    });
    expect(afterEdit.status()).toBe(200);
    const retained = (await afterEdit.json()).members.find(
      (item: { id: number }) => item.id === sharedMember.id,
    );
    expect(retained.roles).toEqual(
      expect.arrayContaining([expect.objectContaining({ scope: "Project", scopeValue: null })]),
    );
    const sharedMemberRow = page.locator("li").filter({ hasText: sharedMember.email });
    await sharedMemberRow.getByRole("button", { name: "Edit member" }).click();
    const addDesignRoleDialog = page.getByRole("dialog");
    await addDesignRoleDialog.getByRole("button", { name: "Add role" }).click();
    await addDesignRoleDialog
      .getByTestId("design-team-role-row")
      .last()
      .getByRole("combobox")
      .click();
    await page.getByRole("option", { name: /Architect.*R/ }).click();
    await addDesignRoleDialog.getByRole("button", { name: "Save changes" }).click();
    await expect(addDesignRoleDialog).toHaveCount(0);
    await expect(
      sharedMemberRow.getByRole("button", { name: "End member assignment" }),
    ).toHaveCount(0);
    await sharedMemberRow.getByRole("button", { name: "Edit member" }).click();
    const removeDesignRoleDialog = page.getByRole("dialog");
    await removeDesignRoleDialog
      .getByTestId("design-team-role-row")
      .filter({ hasText: "Architect" })
      .getByRole("button", { name: "Remove role" })
      .click();
    await removeDesignRoleDialog.getByRole("button", { name: "Save changes" }).click();
    await expect(removeDesignRoleDialog).toHaveCount(0);
    const afterRemoval = await api.get(`/api/operational-projects/${operationalProjectId}/team`, {
      headers,
    });
    expect(afterRemoval.status()).toBe(200);
    const finalMember = (await afterRemoval.json()).members.find(
      (item: { id: number }) => item.id === sharedMember.id,
    );
    expect(finalMember.isActive).toBe(true);
    expect(finalMember.roles).toEqual(
      expect.arrayContaining([expect.objectContaining({ scope: "Project", scopeValue: null })]),
    );
    expect(
      finalMember.roles.some((role: { roleCode: string }) => role.roleCode === "Architect"),
    ).toBe(false);
  } finally {
    if (designProjectId)
      await hardDeleteBusinessRoot(api, headers, `/api/design-projects/${designProjectId}`);
    if (operationalProjectId)
      await hardDeleteBusinessRoot(
        api,
        headers,
        `/api/operational-projects/${operationalProjectId}`,
      );
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
