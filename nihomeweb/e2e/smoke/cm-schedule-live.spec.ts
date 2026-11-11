import { randomInt, randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { execSql } from "../fixtures/db";
import type { APIResponse } from "@playwright/test";

test("CM baseline approval, BCH offline retry and independent recovery work on Compose", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(180_000);
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const json = async (response: APIResponse) => {
    expect(response.ok(), await response.text()).toBe(true);
    return response.json();
  };
  const post = (path: string, data: unknown, headers = admin) =>
    api.post(path, { headers: { ...headers, "Idempotency-Key": randomUUID() }, data });
  // A non-deliverable, test-owned PM address prevents live approval email from
  // reaching a real person. No seeded account email/configuration is changed.
  const pmUser = {
    phoneNumber: `09${randomInt(10_000_000, 99_999_999)}`,
    password: "NiconTest@123",
    role: "PM",
  };
  const pm = await json(
    await post("/api/users", {
      ...pmUser,
      fullName: "PM kiểm thử tiến độ CM",
      email: `cm-${randomUUID()}@example.test`,
    }),
  );
  const customer = await createOwnCustomer(
    api,
    admin,
    "NICON – Kiểm thử thi công móng nhà máy Long An",
  );
  let projectId: number | undefined;
  let contractId: number | undefined;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  try {
    const users = await json(await api.get("/api/users?take=200", { headers: admin }));
    const site = users.items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.siteCommander.phoneNumber,
    );
    expect(site).toBeTruthy();
    const today = new Date();
    const date = (days: number) =>
      new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate() + days))
        .toISOString()
        .slice(0, 10);
    const project = await json(
      await post("/api/operational-projects", {
        customerId: customer,
        name: "NICON – Đối chiếu tiến độ móng Long An",
        projectManagerUserId: pm.id,
      }),
    );
    projectId = project.id;
    const design = await json(
      await post("/api/design-projects", {
        customerId: customer,
        operationalProjectId: projectId,
        name: "Hồ sơ kết cấu móng Long An",
        designManagerUserId: pm.id,
        designLeadUserId: pm.id,
        startDate: date(-20),
        deadline: date(20),
      }),
    );
    await json(
      await post(`/api/operational-projects/${projectId}/team/members`, {
        userId: site.id,
        position: "Chỉ huy trưởng",
        startedAt: date(-20) + "T00:00:00Z",
        roles: [{ roleCode: "SiteEngineer", scope: "Module", scopeValue: "Construction" }],
      }),
    );
    const contract = await json(
      await post("/api/contracts", {
        customerId: customer,
        operationalProjectId: projectId,
        direction: "Upstream",
        type: "DesignAndBuild",
        value: 500_000_000,
        status: "Draft",
      }),
    );
    contractId = contract.id;
    const node = randomUUID();
    // Established upstream/CRM prerequisites. Their approval and scan gates
    // have dedicated tests; this spec exercises the complete new CM journey.
    execSql(`UPDATE contracts SET Status=N'Signed', SignedDate=SYSUTCDATETIME() WHERE Id=${contractId};
      INSERT INTO project_schedule_nodes (Id,OperationalProjectId,CreatedAt) VALUES ('${node}',${projectId},SYSUTCDATETIME());
      INSERT INTO project_schedule_revisions (OperationalProjectId,Version,Status,CreatedAt,CreatedByUserId,PublishedAt) VALUES (${projectId},1,N'Published',SYSUTCDATETIME(),${pm.id},SYSUTCDATETIME());
      DECLARE @revision int=SCOPE_IDENTITY();
      INSERT INTO project_schedule_items (RevisionId,NodeId,WbsCode,Title,PlannedStart,PlannedEnd,SortOrder) VALUES (@revision,'${node}',N'1.1',N'Thi công móng Long An','${date(-10)}','${date(-1)}',0);`);
    await json(
      await post("/api/construction-tasks", {
        designProjectId: design.id,
        projectScheduleNodeId: node,
        wbs: "1.1",
        name: "Thi công móng Long An",
        plannedStart: date(-10),
        plannedEnd: date(-1),
        ownerUserId: site.id,
      }),
    );
    const url = `/admin/construction/progress?projectId=${projectId}`;
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/");
    await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(url);
    await page.getByRole("button", { name: "Create CM revision", exact: true }).click();
    await page.locator("#cm-change-reason").fill("Triển khai kế hoạch thi công móng theo CRM");
    await page
      .getByLabel("Record the scope, quantity, or complexity basis; a BOQ is not required.")
      .fill("Tỷ trọng theo phạm vi công tác móng");
    await page.getByLabel("Scope share (%) 1.1").fill("100");
    await page.getByRole("combobox", { name: "Site Manager for alerts", exact: true }).click();
    await page.getByRole("option", { name: site.fullName, exact: true }).click();
    await page
      .getByRole("dialog")
      .getByRole("button", { name: "Save changes", exact: true })
      .click();
    await page.getByRole("button", { name: "Submit to PM", exact: true }).click();
    const preview = page.getByRole("dialog");
    await expect(preview).toContainText(pm.email);
    await expect(preview.locator("iframe")).toHaveCount(1);
    await preview.getByRole("button", { name: "Submit to PM", exact: true }).click();
    await expect(preview).not.toBeVisible();
    await loginInBrowserAs(page, pmUser);
    await page.goto(url);
    await page.getByRole("button", { name: "PM review", exact: true }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Approve", exact: true }).click();
    await expect(page.getByRole("dialog")).not.toBeVisible();
    const siteToken = await loginAs(TEST_USERS.siteCommander);
    const siteWorkspace = await json(
      await api.get(`/api/operational-projects/${projectId}/construction/schedule`, {
        headers: { Authorization: `Bearer ${siteToken}` },
      }),
    );
    const publishedBaseline = siteWorkspace.baselines.find(
      (baseline: { status: string }) => baseline.status === "Published",
    );
    expect(publishedBaseline).toBeTruthy();
    const weightedLeafIds = publishedBaseline.items
      .filter((item: { isLeaf: boolean; isMilestone: boolean }) => item.isLeaf && !item.isMilestone)
      .map((item: { taskId: number }) => item.taskId);
    for (const taskId of weightedLeafIds) {
      const actualInput = siteWorkspace.actualInputs.find(
        (input: { taskId: number }) => input.taskId === taskId,
      );
      expect(actualInput, `actual input for weighted task ${taskId}`).toBeTruthy();
      expect(actualInput.progressReportedAt).toBeNull();
    }
    await loginInBrowserAs(page, TEST_USERS.siteCommander);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(url);
    // The collapsed "view values" table repeats this text; assert the line under
    // the task, which stays visible in the task list.
    await expect(page.locator("p", { hasText: "Not yet reported by BCH" }).first()).toBeVisible();
    // The working calendar lives in the collapsed plan-details disclosure so the
    // mobile task list stays in view; open it before asserting the frozen calendar.
    await page.getByText("Plan details and working calendar", { exact: true }).click();
    await expect(
      page.getByText("Working days: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday", {
        exact: true,
      }),
    ).toBeVisible();
    await expect(page.getByRole("button", { name: "Create CM revision", exact: true })).toHaveCount(
      0,
    );
    await page.getByRole("button", { name: "Update site actuals", exact: true }).click();
    await expect(page.locator("#cm-actual-progress")).toHaveValue("0");
    await page.locator("#cm-actual-progress").fill("20");
    await page.context().setOffline(true);
    await page
      .getByRole("dialog")
      .getByRole("button", { name: "Save changes", exact: true })
      .click();
    await expect(page.getByRole("dialog").getByRole("alert")).toBeVisible();
    await expect(page.locator("#cm-actual-progress")).toHaveValue("20");
    await page.context().setOffline(false);
    await page
      .getByRole("dialog")
      .getByRole("button", { name: "Save changes", exact: true })
      .click();
    await expect(page.getByRole("dialog")).not.toBeVisible();
    await page.getByRole("button", { name: "Explain and plan recovery", exact: true }).click();
    await page.locator("#cm-explanation").fill("Thiếu thiết bị ép cọc");
    await page.locator("#cm-recovery-plan").fill("Điều phối máy ép từ công trường lân cận");
    await page.locator("#cm-recovery-date").fill(date(5));
    await page
      .getByRole("dialog")
      .getByRole("button", { name: "Save changes", exact: true })
      .click();
    await expect(page.getByRole("dialog")).not.toBeVisible();
    await page.screenshot({ path: testInfo.outputPath("bch-progress-mobile.png"), fullPage: true });
    await loginInBrowserAs(page, pmUser);
    await page.setViewportSize({ width: 768, height: 1000 });
    await page.goto(url);
    await page.getByRole("button", { name: "CM confirms review", exact: true }).click();
    await expect(page.getByRole("button", { name: "CM confirms review", exact: true })).toHaveCount(
      0,
    );
    for (const language of ["vi", "en", "zh", "ja"]) {
      await page.evaluate((lang) => localStorage.setItem("nicon_lang", lang), language);
      await page.goto(url);
      await expect(page.getByTestId("cm-schedule-workspace")).toBeVisible();
      await expect(page.getByTestId("cm-schedule-workspace")).not.toContainText(/cmSchedule\./);
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
      await page.screenshot({
        path: testInfo.outputPath(`cm-progress-${language}.png`),
        fullPage: true,
      });
    }
    const siteHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.siteCommander)}` };
    const workspace = await json(
      await api.get(`/api/operational-projects/${projectId}/construction/schedule`, {
        headers: siteHeaders,
      }),
    );
    expect(workspace.snapshots).toHaveLength(1);
    expect(workspace.snapshots[0].actualPercent).toBe(20);
    expect(workspace.snapshots[0].reviewedAt).toBeTruthy();
    expect(errors).toEqual([]);
  } finally {
    await page.context().setOffline(false);
    if (contractId) await hardDeleteBusinessRoot(api, admin, `/api/contracts/${contractId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, admin, `/api/customers/${customer}`);
    await api.delete(`/api/users/${pm.id}`, { headers: admin });
  }
});
