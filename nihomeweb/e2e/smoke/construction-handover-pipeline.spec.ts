import { randomUUID } from "node:crypto";
import { type Page } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";

// APIs provision only customer, project and team foundations. All construction
// records, evidence uploads, approvals and handover transitions use the browser.
test("PM and Design deliver completed site works and a verified dossier to the client", async ({
  page, api, loginAs, loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(240_000);
  const suffix = randomUUID().slice(0, 8);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "Construction pipeline");
  async function user(role: string, phoneNumber: string) {
    const response = await api.get(`/api/users?role=${role}&take=100`, { headers });
    expect(response.ok()).toBeTruthy();
    const match = (await response.json()).items.find((item: { phoneNumber: string }) => item.phoneNumber === phoneNumber);
    expect(match).toBeTruthy();
    return match as { id: number; fullName: string };
  }
  const pm = await user("PM", TEST_USERS.pm.phoneNumber);
  const design = await user("DESIGN", TEST_USERS.design.phoneNumber);
  const projectName = `Factory fire-water ${suffix}`;
  const operational = await api.post("/api/operational-projects", {
    headers, data: { name: projectName, customerId, projectManagerUserId: pm.id },
  });
  expect(operational.status(), await operational.text()).toBe(201);
  const project = await api.post("/api/design-projects", {
    headers,
    data: { name: projectName, customerId, operationalProjectId: (await operational.json()).id,
      projectManagerUserId: pm.id, designLeadUserId: design.id },
  });
  expect(project.status(), await project.text()).toBe(201);
  const projectId = (await project.json()).id as number;
  const categoryResponse = await api.get("/api/asbuilt-categories", { headers });
  expect(categoryResponse.ok()).toBeTruthy();
  const categories = (await categoryResponse.json() as Array<{ code: string; nameEn: string; isRequired: boolean; isActive: boolean }>)
    .filter(category => category.isRequired && category.isActive);
  expect(categories.length).toBeGreaterThan(0);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  async function openProjectPage(route: string) {
    await page.goto(`/admin/construction/${route}`, { waitUntil: "networkidle" });
    await page.getByRole("combobox").first().click();
    await page.getByRole("option", { name: new RegExp(projectName) }).click();
  }
  async function mutate(path: string, method: string, action: () => Promise<unknown>, status = 200) {
    const waiter = page.waitForResponse(response => new URL(response.url()).pathname === path && response.request().method() === method);
    await action();
    const response = await waiter;
    expect(response.status(), await response.text()).toBe(status);
    return response.json();
  }
  async function confirmAction(prefix: string, action: string, path: string) {
    await page.getByTestId(`${prefix}-${action}`).click();
    return mutate(path, "POST", () => page.getByTestId(`${prefix}-action-confirm`).click());
  }

  const taskName = `Fire-water pipework ${suffix}`;
  let taskId = 0;
  await test.step("PM records site execution and completes the work package", async () => {
    await loginInBrowserAs(page, TEST_USERS.pm);
    await openProjectPage("tasks");
    await page.getByTestId("construction-new").click();
    await page.getByTestId("construction-new-name").fill(taskName);
    taskId = (await mutate("/api/construction-tasks", "POST", () => page.getByTestId("construction-new-save").click(), 201)).id;
    await page.getByTestId(`construction-row-${taskId}`).click();
    const dates = page.getByRole("dialog").locator('input[type="date"]');
    await dates.nth(2).fill(new Date().toISOString().slice(0, 10));
    await dates.nth(3).fill(new Date().toISOString().slice(0, 10));
    await page.getByTestId("construction-progress-slider").fill("100");
    const completed = await mutate(`/api/construction-tasks/${taskId}`, "PUT", () => page.getByTestId("construction-detail-save").click());
    expect(completed.status).toBe("Completed");

    await openProjectPage("diary");
    await page.getByTestId("diary-new").click();
    await page.getByTestId("diary-new-work").fill(`Installed fire-water pipework for ${taskName}; flange seal requires retest.`);
    const diary = await mutate("/api/site-diaries", "POST", () => page.getByTestId("diary-new-save").click(), 201);
    await page.getByTestId(`diary-row-${diary.id}`).getByTestId(`diary-view-${diary.id}`).click();
    await confirmAction("diary", "submit", `/api/site-diaries/${diary.id}/submit`);
    await confirmAction("diary", "confirm", `/api/site-diaries/${diary.id}/confirm`);
    await expect(page.getByTestId("diary-reopen")).toBeVisible();
  });

  let handoverId = 0;
  await test.step("PM opens a handover that remains blocked while quality evidence is missing", async () => {
    await page.goto("/admin/construction/handover", { waitUntil: "networkidle" });
    await page.getByTestId("handover-new").click();
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox").nth(0).click();
    await page.getByRole("option", { name: projectName, exact: true }).click();
    await dialog.getByRole("combobox").nth(1).click();
    await page.getByRole("option", { name: pm.fullName, exact: true }).click();
    await page.getByTestId("handover-form-title").fill(`Client fire-water handover ${suffix}`);
    await dialog.locator('input[type="date"]').fill("2035-09-15");
    await dialog.getByRole("checkbox").first().check();
    await dialog.getByRole("button", { name: "Add item", exact: true }).click();
    await dialog.getByPlaceholder("Checklist item name", { exact: true }).fill("Client commissioning walk completed");
    await dialog.getByRole("checkbox", { name: "Item completed", exact: true }).check();
    await dialog.getByRole("button", { name: "Add signatory", exact: true }).click();
    await dialog.getByPlaceholder("Person or organization name", { exact: true }).fill("Client facilities representative");
    const handover = await mutate("/api/handover-records", "POST", () => page.getByTestId("handover-form-save").click(), 201);
    handoverId = handover.id;
    expect(handover.readiness.isReady).toBe(false);
    await page.getByTestId(`handover-row-view-${handoverId}`).click();
    await expect(page.getByTestId("handover-detail").getByRole("button", { name: "Mark ready", exact: true })).toBeDisabled();
  });

  await test.step("PM approves linked partial acceptance and verifies repair of the site defect", async () => {
    await openProjectPage("acceptance");
    await page.getByTestId("acceptance-new").click();
    await page.getByTestId("acceptance-form-title").fill(`Fire-water pressure test ${suffix}`);
    await page.getByTestId("acceptance-form-task").getByRole("combobox").click();
    await page.getByRole("option", { name: new RegExp(taskName) }).click();
    const accepted = await mutate("/api/acceptance-records", "POST", () => page.getByTestId("acceptance-form-save").click(), 201);
    await page.getByTestId(`acceptance-row-view-${accepted.id}`).click();
    await expect(page.getByText(taskName, { exact: true })).toBeVisible();
    await confirmAction("acceptance", "submit", `/api/acceptance-records/${accepted.id}/status`);
    await confirmAction("acceptance", "approve", `/api/acceptance-records/${accepted.id}/approve`);

    await openProjectPage("punchlist");
    await page.getByTestId("punch-new").click();
    await page.getByTestId("punch-new-title").fill(`Replace flange seal ${suffix}`);
    const punch = await mutate("/api/punch-items", "POST", () => page.getByTestId("punch-new-save").click(), 201);
    await page.getByTestId(`punch-row-${punch.id}`).getByTestId(`punch-view-${punch.id}`).click();
    await mutate(`/api/punch-items/${punch.id}/status`, "POST", () => page.getByTestId("punch-start").click());
    await mutate(`/api/punch-items/${punch.id}/status`, "POST", () => page.getByTestId("punch-fix").click());
    await page.getByTestId("punch-verify").click();
    await page.getByTestId("punch-verify-root-cause").click();
    await page.getByRole("option", { name: "Construction", exact: true }).click();
    await mutate(`/api/punch-items/${punch.id}/verify`, "POST", () => page.getByTestId("punch-action-confirm").click());
    await expect(page.getByTestId("punch-reopen")).toBeVisible();
  });

  const documentIds: number[] = [];
  await test.step("Design uploads the required dossier and submits it to PM", async () => {
    await loginInBrowserAs(page, TEST_USERS.design);
    await openProjectPage("asbuilt");
    for (const category of categories) {
      await page.getByTestId("asbuilt-new").click();
      await page.getByTestId("asbuilt-form-title").fill(`Fire-water ${category.code} ${suffix}`);
      await page.getByTestId("asbuilt-form-category").click();
      await page.getByRole("option", { name: category.nameEn, exact: true }).click();
      await page.getByTestId("asbuilt-document-upload").setInputFiles({
        name: `${category.code}.pdf`, mimeType: "application/pdf", buffer: Buffer.from(`%PDF-1.4 Fire-water ${category.code} evidence`),
      });
      await expect(page.getByTestId("asbuilt-form-file-url")).toHaveValue(/\/files\/business-documents\/as-built\//);
      const document = await mutate("/api/as-built-documents", "POST", () => page.getByTestId("asbuilt-form-save").click(), 201);
      documentIds.push(document.id);
      await page.getByTestId(`asbuilt-row-view-${document.id}`).click();
      await confirmAction("asbuilt", "submit", `/api/as-built-documents/${document.id}/status`);
      await expect(page.getByTestId("asbuilt-approve")).toHaveCount(0);
      await closeSheet(page);
    }
  });

  await test.step("PM approves the complete dossier and hands over to the client", async () => {
    await loginInBrowserAs(page, TEST_USERS.pm);
    await openProjectPage("asbuilt");
    for (const id of documentIds) {
      await page.getByTestId(`asbuilt-row-view-${id}`).click();
      await confirmAction("asbuilt", "approve", `/api/as-built-documents/${id}/approve`);
      await closeSheet(page);
    }
    await page.goto("/admin/construction/handover", { waitUntil: "networkidle" });
    await page.getByTestId("handover-search").fill(`Client fire-water handover ${suffix}`);
    await page.getByTestId(`handover-row-view-${handoverId}`).click();
    const detail = page.getByTestId("handover-detail");
    await expect(detail.getByRole("button", { name: "Mark ready", exact: true })).toBeEnabled();
    await detail.getByRole("button", { name: "Mark ready", exact: true }).click();
    await mutate(`/api/handover-records/${handoverId}/status`, "POST", () => page.getByRole("dialog").last().getByRole("button", { name: "Confirm", exact: true }).click());
    await detail.getByRole("button", { name: "Complete handover", exact: true }).click();
    const completed = await mutate(`/api/handover-records/${handoverId}/complete`, "POST", () => page.getByRole("dialog").last().getByRole("button", { name: "Confirm", exact: true }).click());
    expect(completed).toMatchObject({ status: "HandedOver", designProjectId: projectId, readiness: { isReady: true, unresolvedPunchItems: 0, approvedAcceptanceRecords: 1 } });
    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("handover-search").fill(`Client fire-water handover ${suffix}`);
    const row = page.getByTestId(`handover-row-${handoverId}`);
    await expect(row).toContainText("Handed over");
    await page.screenshot({ path: testInfo.outputPath("completed-handover-desktop.png"), fullPage: true });
    for (const width of [390, 768]) {
      await page.setViewportSize({ width, height: 900 });
      const closeNavigation = page.getByRole("button", { name: "Close sidebar", exact: true });
      await expect(closeNavigation.first()).not.toBeInViewport();
      const responsiveRecord = width < 768 ? page.getByTestId(`handover-card-${handoverId}`) : row;
      await expect(responsiveRecord).toBeVisible();
      await expect(responsiveRecord).toContainText("Handed over");
      await page.screenshot({ path: testInfo.outputPath(`completed-handover-${width}.png`), fullPage: true });
    }
  });
  expect(errors).toEqual([]);
});

async function closeSheet(page: Page) {
  await page.getByRole("dialog").getByRole("button", { name: "Close", exact: true }).click();
}
