import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";

const uid = () => Math.random().toString(36).slice(2, 8);

const expectOk = async (response: { ok: () => boolean; status: () => number; text: () => Promise<string> }, action: string) => {
  const body = await response.text();
  expect(response.ok(), `${action} (${response.status()}): ${body}`).toBeTruthy();
  return body ? JSON.parse(body) as Record<string, unknown> : {};
};

test.describe("Detail Design Schedule — assigned Design Lead flow", () => {
  test("assigned Design Lead initializes, creates, updates, and filters schedule tasks", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    const adminToken = await loginAs(TEST_USERS.superAdmin);
    const adminHeaders = { Authorization: `Bearer ${adminToken}` };
    const usersResponse = await api.get(`/api/users?role=DESIGN_LEAD&search=${TEST_USERS.designLead.phoneNumber}&take=10`, {
      headers: adminHeaders,
    });
    const users = await expectOk(usersResponse, "load Design Lead") as { items?: Array<{ id: number; phoneNumber: string }> };
    const designLead = users.items?.find((user) => user.phoneNumber === TEST_USERS.designLead.phoneNumber);
    expect(designLead?.id).toBeGreaterThan(0);

    const customerId = await createOwnCustomer(api, adminHeaders, "schedule");
    const suffix = uid();
    const projectName = `E2E schedule ${suffix}`;
    const operationalProject = await expectOk(await api.post("/api/operational-projects", {
      headers: adminHeaders,
      data: {
        name: `${projectName} operational`,
        customerId,
        projectManagerUserId: designLead!.id,
        startDate: "2026-04-01",
        endDate: "2026-06-30",
      },
    }), "create operational project") as { id: number };
    const designProject = await expectOk(await api.post("/api/design-projects", {
      headers: adminHeaders,
      data: {
        name: projectName,
        customerId,
        operationalProjectId: operationalProject.id,
        designLeadUserId: designLead!.id,
        startDate: "2026-04-01",
        deadline: "2026-06-30",
      },
    }), "create design project") as { id: number };

    const designLeadToken = await loginAs(TEST_USERS.designLead);
    const designLeadHeaders = {
      Authorization: `Bearer ${designLeadToken}`,
      "Idempotency-Key": `e2e-schedule-init-${suffix}`,
    };
    const initialized = await api.post(`/api/operational-projects/${operationalProject.id}/design-schedule/initialize`, {
      headers: designLeadHeaders,
      data: {
        phases: [
          { code: "Concept", weight: 34 },
          { code: "BasicDesign", weight: 33 },
          { code: "ShopDrawing", weight: 33 },
        ],
      },
    });
    expect(initialized.ok(), `Design Lead initialize (${initialized.status()}): ${await initialized.text()}`).toBeTruthy();

    await loginInBrowserAs(page, TEST_USERS.designLead);
    await page.goto(`${baseURL}/admin/design-projects/${designProject.id}?tab=schedule`, { waitUntil: "networkidle" });
    await page.evaluate(() => document.querySelector('[aria-label*="Notifications ("]')?.remove());

    const schedule = page.getByTestId("design-schedule-tab");
    await expect(schedule).toBeVisible();
    const createTaskButton = page.getByRole("button", { name: /Thêm công việc hoặc mốc|Add task or milestone/i }).first();
    await expect(createTaskButton).toBeVisible();

    await createTaskButton.click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await dialog.locator("#schedule-task-code").fill(`ARC-${suffix}`);
    await dialog.locator("#schedule-task-name").fill(`Architecture coordination ${suffix}`);

    const selectTriggers = dialog.locator('button[role="combobox"]');
    await selectTriggers.nth(2).click();
    await page.getByRole("option", { name: /Kiến trúc|Architecture/i }).click();
    await selectTriggers.nth(3).click();
    await page.getByRole("option").first().click();
    await dialog.locator("#task-planned-start").fill("2026-04-01");
    await dialog.locator("#task-planned-end").fill("2026-04-15");
    await dialog.getByRole("button", { name: /Lưu|Save/i }).click();

    await expect(dialog).toBeHidden();
    const tasksSection = schedule.getByRole("heading", { name: /Danh sách công việc và mốc|Tasks and milestones/i })
      .locator("xpath=ancestor::section[1]");
    const taskRow = tasksSection.locator("article").filter({ hasText: `Architecture coordination ${suffix}` });
    await expect(taskRow).toBeVisible();
    await taskRow.getByRole("button", { name: /Sửa công việc|Edit task/i }).click();

    await expect(dialog).toBeVisible();
    await dialog.locator("#task-actual-start").fill("2026-04-02");
    await dialog.locator("#task-progress").fill("25");
    await dialog.locator('button[role="combobox"]').nth(4).click();
    await page.getByRole("option", { name: /Đang thực hiện|In progress/i }).click();
    await dialog.getByRole("button", { name: /Lưu|Save/i }).click();

    await expect(dialog).toBeHidden();
    await expect(taskRow).toContainText("25% · 1%");
    await expect(taskRow).toContainText(/Đang thực hiện|In progress/i);

    const filters = schedule.locator("section").filter({ hasText: /Bộ lọc|Filters/i });
    await filters.locator('button[role="combobox"]').nth(3).click();
    await page.getByRole("option", { name: /Hoàn thành|Completed/i }).click();
    await expect(schedule.getByText(/Không có công việc nào khớp|No tasks match/i)).toBeVisible();
    await filters.getByRole("button", { name: /Xóa bộ lọc|Clear filters/i }).click();
    await expect(taskRow).toBeVisible();

    await taskRow.getByRole("button", { name: /Sửa công việc|Edit task/i }).click();
    await expect(dialog).toBeVisible();
    const currentScheduleResponse = await api.get(
      `/api/operational-projects/${operationalProject.id}/design-schedule?pageSize=100`,
      { headers: { Authorization: `Bearer ${designLeadToken}` } },
    );
    const currentSchedule = await expectOk(currentScheduleResponse, "load current schedule") as {
      tasks: { items: Array<{
        id: number;
        code: string;
        name: string;
        departmentCode: string;
        assigneeMemberId: number;
        isMilestone: boolean;
        plannedStart: string;
        plannedEnd: string;
        actualStart?: string | null;
        actualEnd?: string | null;
        status: string;
        progressPercent: number;
        weight: number;
        predecessorTaskIds: number[];
        rowVersion: string;
      }> };
    };
    const currentTask = currentSchedule.tasks.items.find((item) => item.code === `ARC-${suffix}`);
    expect(currentTask?.id).toBeGreaterThan(0);
    const competingName = `Competing update ${suffix}`;
    const competingUpdate = await api.put(
      `/api/operational-projects/${operationalProject.id}/design-schedule/tasks/${currentTask!.id}`,
      {
        data: {
          code: currentTask!.code,
          name: competingName,
          departmentCode: currentTask!.departmentCode,
          assigneeMemberId: currentTask!.assigneeMemberId,
          isMilestone: currentTask!.isMilestone,
          plannedStart: currentTask!.plannedStart.slice(0, 10),
          plannedEnd: currentTask!.plannedEnd.slice(0, 10),
          actualStart: currentTask!.actualStart?.slice(0, 10) ?? null,
          actualEnd: currentTask!.actualEnd?.slice(0, 10) ?? null,
          status: currentTask!.status,
          progressPercent: currentTask!.progressPercent,
          weight: currentTask!.weight,
          predecessorTaskIds: currentTask!.predecessorTaskIds,
          rowVersion: currentTask!.rowVersion,
        },
        headers: {
          Authorization: `Bearer ${designLeadToken}`,
          "Idempotency-Key": `e2e-schedule-competing-${suffix}`,
          "If-Match": `"${currentTask!.rowVersion}"`,
        },
      },
    );
    expect(competingUpdate.ok(), `competing update (${competingUpdate.status()}): ${await competingUpdate.text()}`).toBeTruthy();
    await dialog.locator("#schedule-task-name").fill(`Stale edit ${suffix}`);
    await dialog.getByRole("button", { name: /Lưu|Save/i }).click();
    await expect(dialog.getByRole("alert")).toContainText(/người khác cập nhật|Someone else updated/i);
    await dialog.getByRole("button", { name: /Tải lại dữ liệu mới nhất|Reload latest data/i }).click();
    await expect(dialog.locator("#schedule-task-name")).toHaveValue(competingName);
    await expect(dialog.getByRole("alert")).toContainText(
      /Đã tải (?:toàn bộ dữ liệu|phiên bản) mới nhất|latest server values are loaded/i,
    );
    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();

    await page.setViewportSize({ width: 390, height: 844 });
    await expect(schedule).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

    await page.setViewportSize({ width: 768, height: 1024 });
    await createTaskButton.focus();
    await page.keyboard.press("Enter");
    await expect(dialog).toBeVisible();
    await dialog.locator("#schedule-task-code").focus();
    await page.keyboard.type(`KEY-${suffix}`);
    await expect(dialog.locator("#schedule-task-code")).toHaveValue(`KEY-${suffix}`);
    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto(`${baseURL}/admin/design-projects/${designProject.id}?tab=schedule`, { waitUntil: "networkidle" });
    await page.evaluate(() => document.querySelector('[aria-label*="Notifications ("]')?.remove());
    const readOnlySchedule = page.getByTestId("design-schedule-tab");
    await expect(readOnlySchedule).toBeVisible();
    await expect(readOnlySchedule.getByText(/Chỉ xem|Read only/i).first()).toBeVisible();
    await expect(readOnlySchedule.getByRole("button", { name: /Thêm công việc hoặc mốc|Add task or milestone/i })).toHaveCount(0);
    await expect(readOnlySchedule.getByRole("button", { name: /Chỉnh sửa giai đoạn|Edit phase/i })).toHaveCount(0);
    await expect(readOnlySchedule.getByRole("button", { name: /Chỉnh sửa công việc|Edit task/i })).toHaveCount(0);

    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto(`${baseURL}/admin/design-projects/${designProject.id}?tab=schedule`, { waitUntil: "networkidle" });
    await page.evaluate(() => document.querySelector('[aria-label*="Notifications ("]')?.remove());
    await expect(page.getByRole("heading", { name: "403" })).toBeVisible();
    await expect(page.getByText(/Truy cập bị từ chối|Access denied/i)).toBeVisible();
    await expect(readOnlySchedule).toHaveCount(0);

    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto(`${baseURL}/admin/design-projects/${designProject.id}?tab=schedule`, { waitUntil: "networkidle" });
    await page.evaluate(() => document.querySelector('[aria-label*="Notifications ("]')?.remove());
    await expect(readOnlySchedule).toBeVisible();

    const languageToggle = page.getByRole("button", { name: "Change language" });
    await languageToggle.focus();
    await page.keyboard.press("Enter");
    await page.getByRole("menuitem", { name: "中文" }).focus();
    await page.keyboard.press("Enter");
    await expect(readOnlySchedule.getByRole("heading", { name: "项目任务与截止日期" })).toBeVisible();
    await expect(readOnlySchedule).not.toContainText("designProjects.schedule.");

    await languageToggle.focus();
    await page.keyboard.press("Enter");
    await page.getByRole("menuitem", { name: "日本語" }).focus();
    await page.keyboard.press("Enter");
    await expect(readOnlySchedule.getByRole("heading", { name: "プロジェクトのタスクと期限" })).toBeVisible();
    await expect(readOnlySchedule).not.toContainText("designProjects.schedule.");
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  });
});
