import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { personOption } from "../fixtures/people";

test("stale designer selection shows localized guidance and preserves the verification draft", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.slow();
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "Design responsibility");
  const projectName = `NICON Design responsibility ${Date.now()}`;
  const projectId = await createDesignProject(api, { headers, customerId, name: projectName });
  const projectResponse = await api.get(`/api/design-projects/${projectId}`, { headers });
  expect(projectResponse.ok(), await projectResponse.text()).toBeTruthy();
  const { operationalProjectId } = await projectResponse.json();
  const people = await api.get(
    `/api/users?role=ARCHITECT&search=${TEST_USERS.architect.phoneNumber}`,
    { headers },
  );
  expect(people.ok(), await people.text()).toBeTruthy();
  const architect = (await people.json()).items.find(
    (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.architect.phoneNumber,
  );
  expect(architect).toBeTruthy();
  const memberBody = {
    designProjectId: projectId,
    userId: architect.id,
    position: "NICON – Factory architect",
    startedAt: new Date(Date.now() - 86400000).toISOString(),
    roles: [{ roleCode: "Architect", scope: "Module", scopeValue: "Design" }],
  };
  const createdMember = await api.post(
    `/api/operational-projects/${operationalProjectId}/team/members`,
    {
      headers: { ...headers, "Idempotency-Key": `punch-member-${projectId}` },
      data: memberBody,
    },
  );
  expect(createdMember.status(), await createdMember.text()).toBe(201);
  const member = await createdMember.json();
  const createdPunch = await api.post("/api/punch-items", {
    headers,
    data: { designProjectId: projectId, title: "NICON – Drawing coordination", severity: "Medium" },
  });
  expect(createdPunch.status(), await createdPunch.text()).toBe(201);
  const punch = await createdPunch.json();
  for (const status of ["InProgress", "Fixed"]) {
    const transition = await api.post(`/api/punch-items/${punch.id}/status`, {
      headers,
      data: { status },
    });
    expect(transition.ok(), await transition.text()).toBeTruthy();
  }

  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/construction/punchlist", { waitUntil: "networkidle" });
  await page.getByRole("combobox").first().click();
  await page.getByRole("option").filter({ hasText: projectName }).click();
  await page.locator(`[data-testid="punch-view-${punch.id}"]:visible`).click();
  await page.getByTestId("punch-verify").click();
  const dialog = page.getByRole("alertdialog");
  await page.getByTestId("punch-verify-root-cause").click();
  await page.getByRole("option", { name: "Design", exact: true }).click();
  await dialog.getByRole("combobox", { name: "Responsible designer", exact: true }).click();
  const option = personOption(page, architect.fullName);
  await expect(option).toContainText(/Architect/i);
  await option.click();
  const note = "Drawing coordination evidence is ready for independent review.";
  await dialog.getByRole("textbox").fill(note);
  const ended = await api.put(
    `/api/operational-projects/${operationalProjectId}/team/members/${member.id}`,
    {
      headers: { ...headers, "Idempotency-Key": `punch-member-end-${projectId}` },
      data: { ...memberBody, endedAt: new Date().toISOString(), rowVersion: member.rowVersion },
    },
  );
  expect(ended.ok(), await ended.text()).toBeTruthy();
  const rejected = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/punch-items/${punch.id}/verify`) && response.status() === 400,
  );
  await page.getByTestId("punch-action-confirm").click();
  expect((await (await rejected).json()).code).toBe("PUNCH_DESIGN_MEMBER_INVALID");
  await expect(
    page.getByText(
      "The responsible designer must be active in this Design flow or hold a project-wide Design role. Select an eligible designer.",
      { exact: true },
    ),
  ).toBeVisible();
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole("textbox")).toHaveValue(note);
  const persisted = await api.get(`/api/punch-items/${punch.id}`, { headers });
  const unchanged = await persisted.json();
  expect(unchanged.status).toBe("Fixed");
  expect(unchanged.responsibleDesignUserId).toBeNull();
});

// API integration tests prove the real legal membership gate. This isolated UI
// check supplies that documented rejection to verify localization and recovery.
test("legal member rejection preserves the task form and renders localized guidance", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/work-packages?purpose=LegalPermitting&pageSize=100", {
    headers,
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const packageId = (await response.json()).items.find(
    (item: { status: string }) => item.status === "Active",
  )?.id;
  expect(packageId, "active seeded legal package").toBeTruthy();
  const detailResponse = await api.get(`/api/work-packages/${packageId}`, { headers });
  expect(detailResponse.ok(), await detailResponse.text()).toBeTruthy();
  const task = (await detailResponse.json()).tasks.find((item: { status: string }) =>
    ["Planned", "InProgress"].includes(item.status),
  );
  expect(task, "editable seeded legal task").toBeTruthy();
  await page.route(`**/api/work-packages/${packageId}/tasks/${task.id}`, (route) =>
    route.request().method() === "PUT"
      ? route.fulfill({
          status: 400,
          json: {
            code: "LEGAL_MEMBER_INVALID",
            field: "assigneeUserId",
            message: "Người phụ trách phải thuộc đội ngũ chung của Dự án.",
          },
        })
      : route.continue(),
  );
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`/admin/work-packages/${packageId}`, { waitUntil: "networkidle" });
  await page.locator(`#task-${task.id}`).getByRole("button", { name: "Edit", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Edit task", exact: true });
  await dialog.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(
    page.getByText(
      "Legal assignees and approvers must be active members of this project's shared team. Select a person from the shared project team.",
      { exact: true },
    ),
  ).toBeVisible();
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Save changes", exact: true })).toBeEnabled();
});
