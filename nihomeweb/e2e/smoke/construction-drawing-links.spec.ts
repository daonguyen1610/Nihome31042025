import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { execSql } from "../fixtures/db";

// The released packet is an isolated SQL fixture. Design approval/release rules
// are exercised by their integration tests; this spec proves live browser wiring.
for (const width of [390, 768]) {
  test(`construction drawing links persist and render at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    test.slow();
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const customerId = await createOwnCustomer(api, headers, "IFC-WBS");
    const projectId = await createDesignProject(api, {
      headers,
      customerId,
      name: `NICON móng nhà xưởng ${randomUUID().slice(0, 8)}`,
    });
    const task = await api.post("/api/construction-tasks", {
      headers,
      data: {
        designProjectId: projectId,
        name: "Thi công cốt thép móng F1",
        wbs: "1.1.1",
        plannedStart: "2026-11-01",
        plannedEnd: "2026-11-10",
      },
    });
    expect(task.ok(), await task.text()).toBeTruthy();
    const taskId = (await task.json()).id as number;
    const code = `KC-F1-${randomUUID().slice(0, 8)}`;
    execSql(`
      DECLARE @drawing int, @release int;
      INSERT shop_drawings (DesignProjectId,DisciplineCode,ConstructionItem,DrawingCode,Title,Status,CreatedAt,UpdatedAt)
      VALUES (${projectId},'structure',N'Móng nhà xưởng','${code}',N'Chi tiết cốt thép móng F1','Released',SYSUTCDATETIME(),SYSUTCDATETIME());
      SET @drawing=SCOPE_IDENTITY();
      INSERT ifc_releases (DesignProjectId,ReleaseNumber,Title,Status,ReleaseDate,CreatedAt,UpdatedAt)
      VALUES (${projectId},'IFC-${code}',N'Phát hành móng','Released',SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME());
      SET @release=SCOPE_IDENTITY();
      INSERT ifc_release_items (IfcReleaseId,ShopDrawingId) VALUES (@release,@drawing);
    `);
    try {
      const errors: string[] = [];
      page.on("pageerror", (error) => errors.push(error.message));
      await page.setViewportSize({ width, height: 1024 });
      await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.goto(`/admin/construction/tasks?taskId=${taskId}`, { waitUntil: "networkidle" });
      const section = page.getByTestId("construction-task-drawings");
      await expect(section).toBeVisible();
      await expect(section.getByText("Chưa liên kết bản vẽ nào.")).toBeVisible();
      const checkbox = section.getByRole("checkbox");
      await checkbox.check();
      await section.getByTestId("construction-drawings-save").click();
      await expect(section.getByText("Đã lưu liên kết bản vẽ.")).toBeVisible();
      await page.reload({ waitUntil: "networkidle" });
      await expect(section.locator("li").filter({ hasText: code })).toBeVisible();
      await expect(section.getByRole("checkbox")).toBeChecked();
      const box = await section.boundingBox();
      expect(box!.width).toBeLessThanOrEqual(width);
      await section.screenshot({
        path: `test-results/construction-drawings-${width}.png`,
      });
      await section.getByRole("checkbox").uncheck();
      await section.getByTestId("construction-drawings-save").click();
      await expect(section.getByText("Chưa liên kết bản vẽ nào.")).toBeVisible();
      expect(errors).toEqual([]);
    } finally {
      execSql(`
        DELETE FROM construction_task_drawings WHERE ConstructionTaskId=${taskId};
        DELETE FROM ifc_release_items WHERE ShopDrawingId IN (SELECT Id FROM shop_drawings WHERE DesignProjectId=${projectId} AND DrawingCode='${code}');
        DELETE FROM ifc_releases WHERE DesignProjectId=${projectId} AND ReleaseNumber='IFC-${code}';
        DELETE FROM shop_drawings WHERE DesignProjectId=${projectId} AND DrawingCode='${code}';
      `);
    }
  });
}
