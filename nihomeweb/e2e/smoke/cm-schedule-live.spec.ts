import { randomInt, randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { uploadSignedContractScan } from "../fixtures/contracts";
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
  let contractAttachmentId: number | undefined;
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
        startDate: date(1),
        endDate: date(181),
        scopeOfWork: "Thi công móng nhà máy theo tiến độ đã thống nhất.",
      }),
    );
    contractId = contract.id;
    contractAttachmentId = await uploadSignedContractScan(api, admin, contractId);
    await json(
      await post(`/api/contracts/${contractId}/transition`, {
        newStatus: "Signed",
        rowVersion: contract.rowVersion,
      }),
    );

    const node = randomUUID();
    const scheduleUrl = `/api/operational-projects/${projectId}/master-schedule`;
    const schedule = await json(
      await post(`${scheduleUrl}/revisions`, {
        changeReason: "Lập tiến độ tổng thể cho dự án nhà máy Long An.",
        items: [
          {
            nodeId: node,
            wbsCode: "1.1",
            title: "Thi công móng Long An",
            plannedStart: date(1),
            plannedEnd: date(8),
            sortOrder: 0,
          },
        ],
      }),
    );
    const revision = schedule.revisions[0];
    const submitPreview = await json(
      await api.get(
        `${scheduleUrl}/revisions/${revision.id}/email-preview?action=Submit&recipientUserId=${pm.id}`,
        { headers: admin },
      ),
    );
    const submitted = await json(
      await post(`${scheduleUrl}/revisions/${revision.id}/submit`, {
        approverUserId: pm.id,
        rowVersion: revision.rowVersion,
        previewToken: submitPreview.previewToken,
      }),
    );
    const pendingRevision = submitted.revisions.find(
      (item: { id: number }) => item.id === revision.id,
    );
    const pmToken = await loginAs(pmUser);
    const pmHeaders = { Authorization: `Bearer ${pmToken}` };
    const publishPreview = await json(
      await api.get(`${scheduleUrl}/revisions/${revision.id}/email-preview?action=Publish`, {
        headers: pmHeaders,
      }),
    );
    const published = await json(
      await post(
        `${scheduleUrl}/revisions/${revision.id}/review`,
        {
          decision: "Approve",
          rowVersion: pendingRevision.rowVersion,
          previewToken: publishPreview.previewToken,
        },
        pmHeaders,
      ),
    );
    expect(published.revisions.find((item: { id: number }) => item.id === revision.id).status).toBe(
      "Published",
    );
    await json(
      await post("/api/construction-tasks", {
        designProjectId: design.id,
        projectScheduleNodeId: node,
        wbs: "1.1",
        name: "Thi công móng Long An",
        plannedStart: date(1),
        plannedEnd: date(8),
        ownerUserId: site.id,
      }),
    );

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/");
    await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(`/admin/construction/cm/schedule?projectId=${projectId}&tab=plan`);
    await page.getByText("Startup readiness", { exact: true }).click();
    const hseRequired = page.locator("#hsePlanRequired");
    await expect(hseRequired).toBeVisible();
    const savePreparation = page.getByTestId("cm-preparation-save");
    const evaluateReadiness = page.getByTestId("cm-preparation-evaluate");
    await expect(page.getByTestId("cm-preparation-missing-references")).toBeVisible();
    await expect(savePreparation).toBeDisabled();
    for (const [field, value] of [
      ["wbsBaselineReference", "WBS-2026-01"],
      ["scheduleBaselineReference", "SCHED-2026-01"],
      ["sCurveBaselineReference", "SCURVE-2026-01"],
      ["bchOrganizationReference", "BCH-ORG-2026-01"],
      ["resourcePlanReference", "RESOURCE-2026-01"],
      ["budgetBaselineReference", "BUDGET-2026-01"],
    ]) {
      await page.locator(`#${field}`).fill(value);
    }
    await hseRequired.click();
    await expect(page.getByTestId("cm-preparation-unsaved")).toContainText("unsaved changes");
    await expect(evaluateReadiness).toBeDisabled();
    await expect(page.getByTestId("cm-preparation-start")).toBeDisabled();
    const firstSave = page.waitForResponse(
      (response) =>
        response.url().includes(`/operational-projects/${projectId}/construction/preparation`) &&
        response.request().method() === "PUT",
    );
    await savePreparation.click();
    expect((await firstSave).status()).toBe(200);
    await expect(evaluateReadiness).toBeEnabled();
    await hseRequired.click();
    const secondSave = page.waitForResponse(
      (response) =>
        response.url().includes(`/operational-projects/${projectId}/construction/preparation`) &&
        response.request().method() === "PUT",
    );
    await savePreparation.click();
    expect((await secondSave).status()).toBe(200);
    const readinessResponse = page.waitForResponse((response) =>
      response.url().includes(`/operational-projects/${projectId}/construction/readiness/evaluate`),
    );
    await evaluateReadiness.click();
    const readiness = await (await readinessResponse).json();
    expect(readiness.missingConditions).not.toContain(
      "approved project ISO conditional-requirements profile",
    );
    expect(readiness.missingConditions).not.toContain("HSE plan evidence");
    await expect(page.getByTestId("cm-preparation-start")).toBeDisabled();

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
    await page.goto(url);
    await expect(page.locator("p", { hasText: "Not yet reported by BCH" }).first()).toBeVisible();
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
    await page.screenshot({
      path: testInfo.outputPath("bch-progress-desktop.png"),
      fullPage: true,
    });
    await loginInBrowserAs(page, pmUser);
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
    if (contractId && projectId && contractAttachmentId) {
      // The isolated E2E stack has no Google Drive connection. Mark this test
      // attachment replica deleted so hard-delete can clean up the root.
      execSql(
        `SET QUOTED_IDENTIFIER ON; UPDATE project_documents SET SyncStatus=N'Deleted', DesiredOperation=N'None', DeletedAt=SYSUTCDATETIME(), NextSyncAttemptAt=NULL WHERE OperationalProjectId=${projectId} AND SourceEntityType=N'ContractAttachment' AND SourceRecordId=${contractAttachmentId} AND SourceType=N'ExistingManagedFile' AND DriveFileId IS NULL AND DeletedAt IS NULL;`,
      );
    }
    if (contractId) await hardDeleteBusinessRoot(api, admin, `/api/contracts/${contractId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, admin, `/api/customers/${customer}`);
    await api.delete(`/api/users/${pm.id}`, { headers: admin });
  }
});
