import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 456100;
const project = {
  id: projectId,
  code: "PJ-DELETE-001",
  name: "Deletion dialog project",
  customerId: 1,
  customerName: "NICON",
  projectManagerUserId: 1,
  projectManagerName: "Project Manager",
  status: "Planning",
  startDate: null,
  endDate: null,
  opportunityCount: 1,
  quoteCount: 0,
  contractCount: 0,
  updatedAt: "2026-09-03T00:00:00Z",
  note: null,
  designProjectId: null,
  designProjectCode: null,
  rowVersion: "AAAAAAAAB9Q=",
  createdAt: "2026-09-01T00:00:00Z",
  opportunities: [],
  quotes: [],
  contracts: [],
};

test("hard-delete dialog discloses impact and requires exact typed confirmation", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let deleteBody: Record<string, unknown> | null = null;

  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleteBody = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(project) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "OperationalProject",
        resourceId: projectId,
        resourceLabel: `${project.code} · ${project.name}`,
        requiredConfirmation: project.code,
        planToken: "a".repeat(64),
        canDelete: true,
        totalAffected: 2,
        items: [{
          key: "operations.opportunities",
          action: "Unlink",
          count: 1,
          examples: ["Opportunity A"],
        }],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`/admin/operational-projects/${projectId}`);
  await page.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();

  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText("Opportunity A");
  await expect(dialog).toContainText("1");
  const confirmButton = dialog.getByRole("button", { name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i });
  await expect(confirmButton).toBeDisabled();
  await dialog.getByRole("textbox").fill("WRONG");
  await expect(confirmButton).toBeDisabled();
  await dialog.getByRole("textbox").fill(project.code);
  await expect(confirmButton).toBeEnabled();
  await confirmButton.click();

  await expect.poll(() => deleteBody).not.toBeNull();
  expect(deleteBody).toMatchObject({
    planToken: "a".repeat(64),
    confirmation: project.code,
    rowVersion: project.rowVersion,
  });
  await expect(page).toHaveURL(/\/admin\/operational-projects$/);
});

test("hard-delete dialog shows blockers and disables deletion", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let deleteRequested = false;
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), async route => {
    if (route.request().method() === "DELETE") deleteRequested = true;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(project) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "OperationalProject",
        resourceId: projectId,
        resourceLabel: `${project.code} · ${project.name}`,
        requiredConfirmation: project.code,
        planToken: "b".repeat(64),
        canDelete: false,
        totalAffected: 2,
        items: [{
          key: "operations.pendingDocuments",
          action: "Block",
          count: 1,
          examples: ["pending.pdf"],
        }],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`/admin/operational-projects/${projectId}`);
  await page.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();

  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toContainText("pending.pdf");
  await expect(dialog.getByRole("textbox")).toHaveCount(0);
  await expect(dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  })).toBeDisabled();
  expect(deleteRequested).toBe(false);
});

test("Design Project deletion removes the row after confirmation", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const designProject = {
    id: 456101,
    projectCode: "DP-DELETE-001",
    name: "Design deletion project",
    customerId: 1,
    customerName: "NICON",
    operationalProjectId: null,
    contractId: null,
    contractNumber: null,
    projectManagerUserId: null,
    projectManagerName: null,
    designLeadUserId: null,
    designLeadName: null,
    startDate: null,
    deadline: null,
    currentStage: "Concept",
    status: "Active",
    updatedAt: "2026-09-03T00:00:00Z",
  };
  let deleted = false;
  await page.route(new RegExp("/api/(?:v1/)?design-projects(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: deleted ? 0 : 1, page: 1, pageSize: 20,
        items: deleted ? [] : [designProject] }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?design-projects/${designProject.id}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "DesignProject",
        resourceId: designProject.id,
        resourceLabel: `${designProject.projectCode} · ${designProject.name}`,
        requiredConfirmation: designProject.projectCode,
        planToken: "c".repeat(64),
        canDelete: true,
        totalAffected: 1,
        items: [],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?design-projects/${designProject.id}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleted = true;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.continue();
  });

  await page.goto("/admin/design-projects");
  const table = page.getByRole("table");
  await expect(table.getByText(designProject.name)).toBeVisible();
  const row = table.getByText(designProject.name).locator("xpath=ancestor::tr");
  await row.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();
  const dialog = page.getByRole("alertdialog");
  await dialog.getByRole("textbox").fill(designProject.projectCode);
  await dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  }).click();

  await expect(table.getByText(designProject.name)).toHaveCount(0);
  expect(deleted).toBe(true);
});