import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("team member picker stays scrollable on a short screen", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const projects = await api.get("/api/design-projects?page=1&pageSize=1", { headers });
  const projectId = (await projects.json()).items[0]?.id;
  expect(projectId).toBeGreaterThan(0);
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.setViewportSize({ width: 390, height: 380 });
  await page.goto(`/admin/design-projects/${projectId}?tab=team`);
  await page.getByRole("button", { name: "Add member" }).click();
  await page.getByRole("dialog").getByRole("combobox", { name: "User" }).click();
  const picker = page.locator('[data-people-picker="true"]');
  await expect(picker).toBeVisible();
  const bounds = await picker.boundingBox();
  expect(bounds).not.toBeNull();
  expect(bounds!.y).toBeGreaterThanOrEqual(0);
  expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(380);
  const list = picker.locator("[cmdk-list]");
  const movement = await list.evaluate((element) => {
    const before = element.scrollTop;
    element.scrollTop = element.scrollHeight;
    return element.scrollTop - before;
  });
  expect(movement).toBeGreaterThan(0);
});

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
    dialog.getByText("Choose a user and assign roles for this design contract."),
  ).toBeVisible();
  await expect(dialog.getByText("Roles in this design contract")).toBeVisible();
  await expect(dialog.getByText("Design module")).toBeVisible();
  await expect(dialog.getByText("End date")).toHaveCount(0);
  await expect(dialog.getByText("Roles / RACI / scope")).toHaveCount(0);
  await expect(dialog.getByTestId("design-team-role-row").getByRole("combobox")).toHaveCount(1);
  await expect(dialog.getByTestId("design-team-role-row").getByRole("combobox")).toHaveText(
    "Select a role",
  );
  const roleBounds = await dialog.getByTestId("design-team-role-row").evaluate((row) => {
    const select = row.querySelector('[role="combobox"]')?.getBoundingClientRect();
    const remove = row.querySelector('button[aria-label="Remove role"]')?.getBoundingClientRect();
    return select && remove
      ? {
          select: { x: select.x, y: select.y, width: select.width, height: select.height },
          remove: { x: remove.x, y: remove.y },
        }
      : null;
  });
  expect(roleBounds).not.toBeNull();
  expect(roleBounds!.remove.x).toBeGreaterThan(
    roleBounds!.select.x + roleBounds!.select.width - 10,
  );
  expect(roleBounds!.remove.y).toBeLessThan(roleBounds!.select.y + roleBounds!.select.height);
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
  await expect(page.locator('aside nav > a[href="/admin/work-packages"]')).toBeVisible();
  await expect(page.locator('aside nav > a[href="/admin/work-packages"]')).toHaveText(
    "Work packages",
  );
  await expect(page.locator("aside nav > a").first()).toHaveAttribute("href", "/admin");
  await expect(page.locator("aside nav > a").nth(1)).toHaveAttribute(
    "href",
    "/admin/notifications",
  );
  await expect(page.locator("aside nav > a").nth(2)).toHaveAttribute(
    "href",
    "/admin/work-packages",
  );
  await expect(page.locator('#admin-nav-group-crm a[href="/admin/work-packages"]')).toHaveCount(0);
  await expect(
    page.getByRole("columnheader", { name: "Roles in this design contract" }),
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
      `/api/operational-projects/${operationalProjectId}/team/candidates?designProjectId=${designProjectId}`,
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
    const peoplePicker = page.locator('[data-people-picker="true"]');
    await peoplePicker.locator("input[cmdk-input]").fill(candidate.email);
    await peoplePicker.locator("[cmdk-item]").filter({ hasText: candidate.name }).click();
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
    expect(member.designProjectId).toBe(designProjectId);
    expect(member.id).not.toBe(sharedMember.id);
    const memberRow = page.locator("li").filter({ hasText: candidate.email });
    await memberRow.getByRole("button", { name: "Edit member" }).click();
    const editDialog = page.getByRole("dialog");
    await expect(editDialog.getByText("Update roles in this design contract.")).toBeVisible();
    const currentUser = editDialog.getByRole("combobox", { name: "User", exact: true });
    await expect(currentUser).toBeDisabled();
    await expect(currentUser).toContainText(candidate.name);
    await editDialog.locator('input[maxlength="150"]').fill("Concept design coordinator");
    await editDialog.getByRole("button", { name: "Save changes" }).click();
    await expect(editDialog).toHaveCount(0);
    await expect(memberRow).toContainText("Concept design coordinator");
    const scopedResponse = await api.get(
      `/api/operational-projects/${operationalProjectId}/team?designProjectId=${designProjectId}`,
      { headers },
    );
    expect(scopedResponse.ok(), await scopedResponse.text()).toBeTruthy();
    const scoped = await scopedResponse.json();
    expect(
      scoped.members.every(
        (item: { designProjectId: number }) => item.designProjectId === designProjectId,
      ),
    ).toBe(true);
    expect(scoped.members.find((item: { id: number }) => item.id === member.id).position).toBe(
      "Concept design coordinator",
    );
    const afterEdit = await api.get(`/api/operational-projects/${operationalProjectId}/team`, {
      headers,
    });
    expect(afterEdit.ok(), await afterEdit.text()).toBeTruthy();
    expect((await afterEdit.json()).members).toEqual((await team.json()).members);

    await page.goto(`/admin/design-projects/${designProjectId}?tab=concept`);
    await page.getByRole("button", { name: "New option" }).click();
    const owner = page.getByRole("dialog").getByRole("combobox", { name: "Owner" });
    await expect(owner).toBeEnabled();
    await owner.click();
    const names = await page.locator("[cmdk-item]").allTextContents();
    expect(names.some((name) => name.includes(candidate.name))).toBe(true);
    const procurementUser = (await users.json()).items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.procurement.phoneNumber,
    );
    const procurementCandidate = (await candidates.json()).find(
      (item: { userId: number }) => item.userId === procurementUser.id,
    );
    expect(procurementCandidate).toBeTruthy();
    expect(names.some((name) => name.includes(procurementCandidate.name))).toBe(false);
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
