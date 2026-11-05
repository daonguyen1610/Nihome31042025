import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import type { APIRequestContext, APIResponse } from "@playwright/test";

const uid = () => Math.random().toString(36).slice(2, 8);

const expectJson = async <T>(response: APIResponse, action: string): Promise<T> => {
  const body = await response.text();
  expect(response.ok(), `${action} (${response.status()}): ${body}`).toBeTruthy();
  return (body ? JSON.parse(body) : {}) as T;
};

interface SeededUser {
  id: number;
  phoneNumber: string;
  fullName: string;
}

interface ScheduleRevision {
  id: number;
  version: number;
  status: string;
  approverUserId?: number | null;
  rowVersion: string;
  items: Array<{
    nodeId: string;
    wbsCode: string;
    title: string;
    plannedStart: string;
    plannedEnd: string;
  }>;
}

interface ScheduleResponse {
  operationalProjectId: number;
  hasSignedUpstreamContract: boolean;
  publishedRevisionId?: number | null;
  approverOptions: Array<{ userId: number; name: string }>;
  revisions: ScheduleRevision[];
}

const scheduleUrl = (projectId: number) => `/api/operational-projects/${projectId}/master-schedule`;

const login = async (
  api: APIRequestContext,
  user: { phoneNumber: string; password: string },
): Promise<string> => {
  const response = await api.post("/api/auth/login", {
    data: { phoneNumber: user.phoneNumber, password: user.password },
  });
  return (await expectJson<{ accessToken: string }>(response, "login")).accessToken;
};

const loadUsers = async (api: APIRequestContext, headers: Record<string, string>) => {
  const users = await expectJson<{ items: SeededUser[] }>(
    await api.get("/api/users?take=200", { headers }),
    "load users",
  );
  const byPhone = (phone: string) => {
    const found = users.items.find((user) => user.phoneNumber === phone);
    expect(found, `seeded user ${phone}`).toBeTruthy();
    return found!;
  };
  return {
    salesManager: byPhone(TEST_USERS.salesManager.phoneNumber),
    pm: byPhone(TEST_USERS.pm.phoneNumber),
    designLead: byPhone(TEST_USERS.designLead.phoneNumber),
    architect: byPhone(TEST_USERS.architect.phoneNumber),
  };
};

const createOperationalProject = async (
  api: APIRequestContext,
  headers: Record<string, string>,
  customerId: number,
  name: string,
  projectManagerUserId: number,
): Promise<number> => {
  const project = await expectJson<{ id: number }>(
    await api.post("/api/operational-projects", {
      headers,
      data: {
        name,
        customerId,
        projectManagerUserId,
        startDate: "2026-06-01",
        endDate: "2026-12-31",
      },
    }),
    "create operational project",
  );
  return project.id;
};

const saveRevision = async (
  api: APIRequestContext,
  headers: Record<string, string>,
  projectId: number,
  changeReason: string,
  items: unknown[],
): Promise<ScheduleResponse> =>
  expectJson<ScheduleResponse>(
    await api.post(`${scheduleUrl(projectId)}/revisions`, {
      headers: { ...headers, "Idempotency-Key": `schedule-create-${uid()}` },
      data: { changeReason, items },
    }),
    "create schedule revision",
  );

const submitRevision = async (
  api: APIRequestContext,
  headers: Record<string, string>,
  projectId: number,
  revision: ScheduleRevision,
  approverUserId: number,
): Promise<ScheduleResponse> =>
  expectJson<ScheduleResponse>(
    await api.post(`${scheduleUrl(projectId)}/revisions/${revision.id}/submit`, {
      headers: { ...headers, "Idempotency-Key": `schedule-submit-${uid()}` },
      data: { approverUserId, rowVersion: revision.rowVersion },
    }),
    "submit schedule revision",
  );

const reviewRevision = async (
  api: APIRequestContext,
  headers: Record<string, string>,
  projectId: number,
  revision: ScheduleRevision,
  decision: "Approve" | "Reject",
  comment?: string,
): Promise<ScheduleResponse> =>
  expectJson<ScheduleResponse>(
    await api.post(`${scheduleUrl(projectId)}/revisions/${revision.id}/review`, {
      headers: { ...headers, "Idempotency-Key": `schedule-review-${uid()}` },
      data: { decision, comment, rowVersion: revision.rowVersion },
    }),
    `review schedule revision ${decision}`,
  );

test.describe("Master schedule — MoM one-way planning contract", () => {
  test("administrator can inspect schedule email delivery at desktop and mobile widths", async ({
    loginInBrowserAs,
    page,
  }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/email-templates?tab=projectSchedule#schedule-delivery");
    const delivery = page.locator("#schedule-delivery");
    await expect(delivery).toBeVisible();
    await expect(delivery).toContainText("Email tiến độ cần kiểm tra");
    await expect(delivery.getByRole("button", { name: "Làm mới" })).toBeVisible();
    for (const width of [768, 390]) {
      await page.setViewportSize({ width, height: 844 });
      await expect(delivery).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        width,
      );
    }
  });

  test("CRM approves the master schedule, departments stay inside its frame, and a client change notifies them", async ({
    api,
    loginInBrowserAs,
    page,
  }, testInfo) => {
    test.slow();
    const adminToken = await login(api, TEST_USERS.superAdmin);
    const adminHeaders = { Authorization: `Bearer ${adminToken}` };
    const salesManagerToken = await login(api, TEST_USERS.salesManager);
    const salesManagerHeaders = { Authorization: `Bearer ${salesManagerToken}` };
    const pmToken = await login(api, TEST_USERS.pm);
    const pmHeaders = { Authorization: `Bearer ${pmToken}` };
    const architectToken = await login(api, TEST_USERS.architect);
    const users = await loadUsers(api, adminHeaders);
    const suffix = uid();
    const customerId = await createOwnCustomer(api, adminHeaders, "master-schedule");

    const projectId = await createOperationalProject(
      api,
      adminHeaders,
      customerId,
      `E2E master schedule ${suffix}`,
      users.pm.id,
    );

    // Pre-contract: eligible approvers are CRM Sales Managers, never the project PM.
    // Earlier runs may leave extra Sales Manager accounts in the shared database.
    const initial = await expectJson<ScheduleResponse>(
      await api.get(scheduleUrl(projectId), { headers: adminHeaders }),
      "load empty master schedule",
    );
    expect(initial.hasSignedUpstreamContract).toBe(false);
    const initialApproverIds = initial.approverOptions.map((option) => option.userId);
    expect(initialApproverIds).toContain(users.salesManager.id);
    expect(initialApproverIds).not.toContain(users.pm.id);

    const parentNode = crypto.randomUUID();
    const childNode = crypto.randomUUID();
    const createResponse = await saveRevision(
      api,
      adminHeaders,
      projectId,
      "Khách chốt tiến độ tổng thể",
      [
        {
          nodeId: parentNode,
          wbsCode: "1",
          title: "Phần móng và kết cấu",
          plannedStart: "2026-06-01",
          plannedEnd: "2026-06-30",
          sortOrder: 0,
        },
        {
          nodeId: childNode,
          parentNodeId: parentNode,
          wbsCode: "1.1",
          title: "Ép cọc",
          plannedStart: "2026-06-01",
          plannedEnd: "2026-06-10",
          sortOrder: 1,
        },
      ],
    );
    const revision1 = createResponse.revisions[0];
    expect(revision1.status).toBe("Draft");

    // A child WBS outside its parent's frame is rejected at the source.
    const invalidParent = await api.put(`${scheduleUrl(projectId)}/revisions/${revision1.id}`, {
      headers: { ...adminHeaders, "Idempotency-Key": `schedule-invalid-${suffix}` },
      data: {
        changeReason: "Sai khung",
        items: [
          {
            nodeId: parentNode,
            wbsCode: "1",
            title: "Phần móng và kết cấu",
            plannedStart: "2026-06-01",
            plannedEnd: "2026-06-30",
            sortOrder: 0,
          },
          {
            nodeId: childNode,
            parentNodeId: parentNode,
            wbsCode: "1.1",
            title: "Ép cọc",
            plannedStart: "2026-06-11",
            plannedEnd: "2026-07-01",
            sortOrder: 1,
          },
        ],
      },
    });
    expect(invalidParent.status()).toBe(400);
    expect(await invalidParent.text()).toContain("khung của mốc cha");

    const submitted = await submitRevision(
      api,
      adminHeaders,
      projectId,
      revision1,
      users.salesManager.id,
    );
    const pending = submitted.revisions.find((revision) => revision.id === revision1.id)!;
    expect(pending.status).toBe("PendingApproval");
    expect(pending.approverUserId).toBe(users.salesManager.id);

    // Only the assigned approver may decide — not the PM, not an administrator.
    const pmReview = await api.post(`${scheduleUrl(projectId)}/revisions/${revision1.id}/review`, {
      headers: { ...pmHeaders, "Idempotency-Key": `schedule-wrong-${suffix}` },
      data: { decision: "Approve", rowVersion: pending.rowVersion },
    });
    expect(pmReview.status()).toBe(403);
    expect(await pmReview.text()).toContain("Chỉ người duyệt được chỉ định");

    const adminReview = await api.post(
      `${scheduleUrl(projectId)}/revisions/${revision1.id}/review`,
      {
        headers: { ...adminHeaders, "Idempotency-Key": `schedule-admin-wrong-${suffix}` },
        data: { decision: "Approve", rowVersion: pending.rowVersion },
      },
    );
    expect(adminReview.status()).toBe(403);

    // The CRM department head may hand the review to another eligible watcher
    // while the revision is pending.
    const secondReviewerPhone = `09${Date.now().toString().slice(-8)}`;
    const secondReviewer = await expectJson<{ id: number }>(
      await api.post("/api/users", {
        headers: { ...adminHeaders, "Idempotency-Key": `schedule-reviewer-${suffix}` },
        data: {
          phoneNumber: secondReviewerPhone,
          fullName: `Trưởng phòng Kinh doanh ${suffix}`,
          email: `schedule-reviewer-${suffix}@nihome.test`,
          password: "Demo@123!",
          role: "SALES_MANAGER",
        },
      }),
      "create second CRM manager",
    );
    const secondReviewerToken = await login(api, {
      phoneNumber: secondReviewerPhone,
      password: "Demo@123!",
    });
    const reassigned = await submitRevision(
      api,
      adminHeaders,
      projectId,
      pending,
      secondReviewer.id,
    );
    const pendingReassigned = reassigned.revisions.find(
      (revision) => revision.id === revision1.id,
    )!;
    expect(pendingReassigned.status).toBe("PendingApproval");
    expect(pendingReassigned.approverUserId).toBe(secondReviewer.id);

    const previousApproverReview = await api.post(
      `${scheduleUrl(projectId)}/revisions/${revision1.id}/review`,
      {
        headers: { ...salesManagerHeaders, "Idempotency-Key": `schedule-previous-${suffix}` },
        data: { decision: "Approve", rowVersion: pendingReassigned.rowVersion },
      },
    );
    expect(previousApproverReview.status()).toBe(403);

    const published = await reviewRevision(
      api,
      { Authorization: `Bearer ${secondReviewerToken}` },
      projectId,
      pendingReassigned,
      "Approve",
      "Tiến độ tổng thể phù hợp hợp đồng.",
    );
    expect(published.revisions.find((revision) => revision.id === revision1.id)!.status).toBe(
      "Published",
    );
    expect(published.publishedRevisionId).toBe(revision1.id);

    // Design and construction work must now reference a published node and
    // remain inside its dates.
    const designProject = await expectJson<{ id: number }>(
      await api.post("/api/design-projects", {
        headers: adminHeaders,
        data: {
          name: `E2E master schedule design ${suffix}`,
          customerId,
          operationalProjectId: projectId,
          projectManagerUserId: users.designLead.id,
          designLeadUserId: users.designLead.id,
          startDate: "2026-06-01",
          deadline: "2026-06-30",
        },
      }),
      "create design project",
    );

    let team = await expectJson<{ members: Array<{ id: number; userId: number }> }>(
      await api.get(
        `/api/operational-projects/${projectId}/team?designProjectId=${designProject.id}`,
        { headers: adminHeaders },
      ),
      "load project team",
    );
    let architectMember = team.members.find((member) => member.userId === users.architect.id);
    if (!architectMember) {
      await expectJson(
        await api.post(`/api/operational-projects/${projectId}/team/members`, {
          headers: { ...adminHeaders, "Idempotency-Key": `schedule-architect-${suffix}` },
          data: {
            designProjectId: designProject.id,
            userId: users.architect.id,
            position: "Kiến trúc sư triển khai hồ sơ",
            startedAt: "2026-06-01T00:00:00Z",
            roles: [{ roleCode: "Architect", scope: "Discipline", scopeValue: "architecture" }],
          },
        }),
        "add architect to the project team",
      );
      team = await expectJson(
        await api.get(
          `/api/operational-projects/${projectId}/team?designProjectId=${designProject.id}`,
          { headers: adminHeaders },
        ),
        "reload project team",
      );
      architectMember = team.members.find((member) => member.userId === users.architect.id)!;
    }
    const leadMember = team.members.find((member) => member.userId === users.designLead.id);
    expect(leadMember, "Design Lead project member").toBeTruthy();

    const schedule = await expectJson<{ phases: Array<{ id: number; code: string }> }>(
      await api.post(`/api/operational-projects/${projectId}/design-schedule/initialize`, {
        headers: { ...adminHeaders, "Idempotency-Key": `schedule-phases-${suffix}` },
        data: {
          designProjectId: designProject.id,
          phases: [
            { code: "Concept", weight: 34 },
            { code: "BasicDesign", weight: 33 },
            { code: "ShopDrawing", weight: 33 },
          ],
        },
      }),
      "initialize design schedule",
    );
    const phaseId = schedule.phases[0].id;
    const designTask = (overrides: Record<string, unknown>) =>
      api.post(`/api/operational-projects/${projectId}/design-schedule/phases/${phaseId}/tasks`, {
        headers: { ...adminHeaders, "Idempotency-Key": `schedule-design-${uid()}` },
        data: {
          code: `MS-${uid()}`,
          name: "Triển khai hồ sơ theo tiến độ tổng thể",
          departmentCode: "design",
          assigneeMemberId: architectMember!.id,
          approverMemberId: leadMember!.id,
          isMilestone: false,
          status: "NotStarted",
          progressPercent: 0,
          weight: 1,
          isCritical: true,
          predecessorTaskIds: [],
          ccMemberIds: [],
          ...overrides,
        },
      });

    const missingNode = await designTask({
      plannedStart: "2026-06-02",
      plannedEnd: "2026-06-05",
    });
    expect(missingNode.status()).toBe(400);
    expect(await missingNode.text()).toContain("Cần liên kết đầu việc");

    const insideFrame = await designTask({
      projectScheduleNodeId: childNode,
      plannedStart: "2026-06-02",
      plannedEnd: "2026-06-05",
    });
    expect(insideFrame.ok(), await insideFrame.text()).toBeTruthy();

    const outsideFrame = await designTask({
      projectScheduleNodeId: childNode,
      plannedStart: "2026-06-08",
      plannedEnd: "2026-06-15",
    });
    expect(outsideFrame.status()).toBe(400);
    expect(await outsideFrame.text()).toContain("nằm trong khung WBS 1.1");

    const constructionInside = await api.post("/api/construction-tasks", {
      headers: adminHeaders,
      data: {
        designProjectId: designProject.id,
        name: "Thi công ép cọc theo WBS 1.1",
        plannedStart: "2026-06-02",
        plannedEnd: "2026-06-06",
        ownerUserId: users.pm.id,
        projectScheduleNodeId: childNode,
      },
    });
    expect(constructionInside.ok(), await constructionInside.text()).toBeTruthy();

    const constructionOutside = await api.post("/api/construction-tasks", {
      headers: adminHeaders,
      data: {
        designProjectId: designProject.id,
        name: "Thi công vượt khung WBS 1.1",
        plannedStart: "2026-06-06",
        plannedEnd: "2026-06-14",
        ownerUserId: users.pm.id,
        projectScheduleNodeId: childNode,
      },
    });
    expect(constructionOutside.status()).toBe(400);
    expect(await constructionOutside.text()).toContain(
      "Ngày thi công phải nằm trong khung WBS 1.1",
    );

    // The client changes the requirement: a new revision updates the original
    // schedule and the linked departments are notified with the delta.
    const change = await saveRevision(
      api,
      adminHeaders,
      projectId,
      "Khách dời mốc ép cọc thêm 2 ngày",
      [
        {
          nodeId: parentNode,
          wbsCode: "1",
          title: "Phần móng và kết cấu",
          plannedStart: "2026-06-01",
          plannedEnd: "2026-06-30",
          sortOrder: 0,
        },
        {
          nodeId: childNode,
          parentNodeId: parentNode,
          wbsCode: "1.1",
          title: "Ép cọc",
          plannedStart: "2026-06-01",
          plannedEnd: "2026-06-12",
          sortOrder: 1,
        },
      ],
    );
    const revision2 = change.revisions.find((revision) => revision.version === 2)!;
    expect(revision2.status).toBe("Draft");
    expect(revision2.changeReason).toContain("Khách dời mốc");

    const submitted2 = await submitRevision(
      api,
      adminHeaders,
      projectId,
      revision2,
      users.salesManager.id,
    );
    const pending2 = submitted2.revisions.find((revision) => revision.id === revision2.id)!;
    expect(pending2.status).toBe("PendingApproval");

    const published2 = await reviewRevision(
      api,
      salesManagerHeaders,
      projectId,
      pending2,
      "Approve",
    );
    expect(published2.revisions.find((revision) => revision.id === revision2.id)!.status).toBe(
      "Published",
    );
    expect(published2.publishedRevisionId).toBe(revision2.id);
    expect(published2.revisions.find((revision) => revision.id === revision1.id)!.status).toBe(
      "Published",
    );

    // The updated frame constrains new work immediately.
    const staleFrame = await designTask({
      projectScheduleNodeId: childNode,
      plannedStart: "2026-06-08",
      plannedEnd: "2026-06-14",
    });
    expect(staleFrame.status()).toBe(400);
    expect(await staleFrame.text()).toContain("2026-06-12");

    const notifications = await expectJson<Array<{ templateCode?: string; body?: string }>>(
      await api.get("/api/notifications?take=50", {
        headers: { Authorization: `Bearer ${architectToken}` },
      }),
      "load design assignee notifications",
    );
    const scheduleNotifications = notifications.filter(
      (notification) => notification.templateCode === "project.schedule.published",
    );
    expect(scheduleNotifications.length).toBeGreaterThan(0);
    expect(scheduleNotifications[0].body).toContain("2026-06-12");
    expect(scheduleNotifications[0].body).toContain("2026-06-10");
    expect(scheduleNotifications[0].body).toContain("→");

    // The rendered admin screen exposes the published revision and its WBS rows.
    await loginInBrowserAs(page, TEST_USERS.designLead);
    await page.goto(`/admin/operational-projects/${projectId}?tab=schedule`);
    const masterSchedule = page.locator("#project-master-schedule");
    await expect(masterSchedule).toBeVisible();
    await expect(masterSchedule).toContainText("Tiến độ tổng thể");
    await expect(masterSchedule).toContainText("Ép cọc");
    await expect(masterSchedule).toContainText("Đã phát hành");
    await testInfo.attach("master-schedule", {
      body: await page.locator("#project-master-schedule").innerText(),
      contentType: "text/plain",
    });
  });

  test("after the upstream contract is signed only the project PM can approve", async ({ api }) => {
    test.slow();
    const adminToken = await login(api, TEST_USERS.superAdmin);
    const adminHeaders = { Authorization: `Bearer ${adminToken}` };
    const users = await loadUsers(api, adminHeaders);
    const suffix = uid();
    const customerId = await createOwnCustomer(api, adminHeaders, "master-schedule-contract");
    const projectId = await createOperationalProject(
      api,
      adminHeaders,
      customerId,
      `E2E master schedule contract ${suffix}`,
      users.pm.id,
    );

    const contract = await expectJson<{ id: number; rowVersion: string }>(
      await api.post("/api/contracts", {
        headers: { ...adminHeaders, "Idempotency-Key": `schedule-contract-${suffix}` },
        data: {
          customerId,
          direction: "Upstream",
          type: "Construction",
          operationalProjectId: projectId,
          status: "Draft",
          value: 50_000_000_000,
          scopeOfWork: "Thi công xây dựng công trình theo tiến độ tổng thể.",
          startDate: "2026-11-06T00:00:00Z",
          endDate: "2027-12-31T00:00:00Z",
        },
      }),
      "create upstream contract",
    );
    const signed = await expectJson<{ id: number }>(
      await api.post(`/api/contracts/${contract.id}/transition`, {
        headers: { ...adminHeaders, "Idempotency-Key": `schedule-sign-${suffix}` },
        data: { newStatus: "Signed", rowVersion: contract.rowVersion },
      }),
      "sign upstream contract",
    );
    expect(signed.id).toBe(contract.id);

    const schedule = await saveRevision(api, adminHeaders, projectId, "Khách chốt tiến độ", [
      {
        nodeId: crypto.randomUUID(),
        wbsCode: "1",
        title: "Khởi công",
        plannedStart: "2026-06-01",
        plannedEnd: "2026-06-30",
        sortOrder: 0,
      },
    ]);
    const revision = schedule.revisions[0];
    const options = await expectJson<ScheduleResponse>(
      await api.get(scheduleUrl(projectId), { headers: adminHeaders }),
      "load approver options after contract",
    );
    expect(options.hasSignedUpstreamContract).toBe(true);
    expect(options.approverOptions.map((option) => option.userId)).toEqual([users.pm.id]);

    const crmAttempt = await api.post(`${scheduleUrl(projectId)}/revisions/${revision.id}/submit`, {
      headers: { ...adminHeaders, "Idempotency-Key": `schedule-crm-${suffix}` },
      data: { approverUserId: users.salesManager.id, rowVersion: revision.rowVersion },
    });
    expect(crmAttempt.status()).toBe(400);
    expect(await crmAttempt.text()).toContain("PM dự án");

    const submitted = await submitRevision(api, adminHeaders, projectId, revision, users.pm.id);
    expect(submitted.revisions.find((item) => item.id === revision.id)!.status).toBe(
      "PendingApproval",
    );
  });
});
