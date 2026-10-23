import { test, expect, TEST_USERS } from "../fixtures/auth";

for (const width of [390, 768]) {
  test(`completed package can reopen and hard delete at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const writeHeaders = () => ({ ...headers, "Idempotency-Key": crypto.randomUUID() });
    const options = await (await api.get("/api/work-packages/options", { headers })).json();
    const department = options.departments.find(
      (item: { code: string }) => item.code === "PROCUREMENT",
    );
    const lead = options.users.find(
      (item: { roleGroupId: number; canLead: boolean }) =>
        item.roleGroupId === department.id && item.canLead,
    );
    expect(lead).toBeTruthy();
    const assignee = options.users.find(
      (item: { roleGroupId: number; userId: number }) =>
        item.roleGroupId === department.id && item.userId !== lead.userId,
    );
    expect(assignee).toBeTruthy();
    const projects = await (
      await api.get("/api/operational-projects?pageSize=1", { headers })
    ).json();
    const projectId = projects.items[0].id;
    let packageId: number | undefined;
    let removed = false;
    try {
      const created = await api.post("/api/work-packages", {
        headers: writeHeaders(),
        data: {
          kind: "Project",
          purpose: "General",
          name: `NICON kiểm tra bàn giao vật tư ${width} ${Date.now()}`,
          operationalProjectId: projectId,
          departmentRoleGroupId: department.id,
          leadUserId: lead.userId,
        },
      });
      expect(created.status(), await created.text()).toBe(201);
      let work = await created.json();
      packageId = work.id;
      const taskResponse = await api.post(`/api/work-packages/${packageId}/tasks`, {
        headers: writeHeaders(),
        data: {
          wbsCode: "SUP-REOPEN-01",
          title: "Kiểm tra giao nhận vật tư công trường",
          assigneeUserId: assignee.userId,
          approverUserId: lead.userId,
          plannedStart: "2026-10-23T00:00:00Z",
          plannedEnd: "2026-10-24T00:00:00Z",
        },
      });
      expect(taskResponse.status(), await taskResponse.text()).toBe(201);
      const task = await taskResponse.json();
      const update = async (status: string) => {
        work = await (await api.get(`/api/work-packages/${packageId}`, { headers })).json();
        const response = await api.put(`/api/work-packages/${packageId}`, {
          headers: writeHeaders(),
          data: {
            name: work.name,
            status,
            rowVersion: work.rowVersion,
          },
        });
        expect(response.status(), await response.text()).toBe(200);
        work = await response.json();
      };
      await update("Active");
      const completed = await api.put(`/api/work-packages/${packageId}/tasks/${task.id}/progress`, {
        headers: writeHeaders(),
        data: {
          status: "Completed",
          progressPercent: 100,
          rowVersion: task.rowVersion,
        },
      });
      expect(completed.status(), await completed.text()).toBe(200);
      const completedTask = await completed.json();
      const verified = await api.post(`/api/work-packages/${packageId}/tasks/${task.id}/verify`, {
        headers: writeHeaders(),
        data: { confirm: true, rowVersion: completedTask.rowVersion },
      });
      expect(verified.status(), await verified.text()).toBe(200);
      await update("Completed");
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.setViewportSize({ width, height: 900 });
      await page.goto(`/admin/work-packages/${packageId}`);
      const reopen = page.getByRole("button", {
        name: /Mở lại: Đang thực hiện|Reopen: In progress/i,
      });
      await expect(reopen).toBeVisible();
      await reopen.click();
      await expect(reopen).toHaveCount(0);
      const addTask = page.getByRole("button", { name: /Thêm đầu việc|Add task/i });
      await expect(addTask).toBeVisible();
      await addTask.click();
      await expect(page.getByRole("dialog")).toBeVisible();
      await page
        .getByRole("dialog")
        .getByRole("button", { name: /Hủy|Huỷ|Cancel/i })
        .click();
      await page.getByRole("button", { name: /^(Xóa|Xoá|Delete)$/i }).click();
      const dialog = page.getByRole("alertdialog");
      await expect(dialog).toBeVisible();
      const confirm = dialog.getByRole("button", {
        name: /Xóa vĩnh viễn|Xoá vĩnh viễn|Permanently delete/i,
      });
      await expect(confirm).toBeDisabled();
      await dialog.locator("#deletion-confirmation").fill(work.code);
      await expect(confirm).toBeEnabled();
      await expect
        .poll(async () => {
          const box = await dialog.boundingBox();
          return box !== null && box.x >= 0 && box.x + box.width <= width;
        })
        .toBe(true);
      const overflow = await page.locator("html").evaluate((el) => el.scrollWidth > el.clientWidth);
      expect(overflow).toBe(false);
      await page.screenshot({ path: testInfo.outputPath(`work-package-delete-${width}.png`) });
      const deletion = page.waitForResponse(
        (res) =>
          new URL(res.url()).pathname === `/api/work-packages/${packageId}` &&
          res.request().method() === "DELETE",
      );
      await confirm.click();
      expect((await deletion).status()).toBe(204);
      removed = true;
      await expect(page).toHaveURL(/\/admin\/work-packages$/);
      expect((await api.get(`/api/work-packages/${packageId}`, { headers })).status()).toBe(404);
    } finally {
      // Only the package created by this scenario is eligible for cleanup.
      if (packageId && !removed) {
        const impact = await api.get(`/api/work-packages/${packageId}/deletion-impact`, {
          headers,
        });
        if (impact.status() === 200) {
          const plan = await impact.json();
          const current = await (
            await api.get(`/api/work-packages/${packageId}`, { headers })
          ).json();
          const cleanup = await api.delete(`/api/work-packages/${packageId}`, {
            headers: writeHeaders(),
            data: {
              confirmation: plan.requiredConfirmation,
              planToken: plan.planToken,
              rowVersion: current.rowVersion,
            },
          });
          expect(cleanup.status()).toBe(204);
        }
      }
    }
  });
}
