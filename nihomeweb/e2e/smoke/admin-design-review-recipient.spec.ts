import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";
import {
  approveDesignStage,
  createDesignProject,
  createOwnCustomer,
  isGoogleDriveConnected,
} from "../fixtures/designProjects";

test("design review UI explains task approval and dossier responsibilities", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const managerHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.designManager)}` };
  const projects = await api.get("/api/design-projects?search=DP-SAMPLE-003&pageSize=10", {
    headers: managerHeaders,
  });
  expect(projects.ok(), await projects.text()).toBeTruthy();
  const project = (await projects.json()).items.find(
    (item: { projectCode: string }) => item.projectCode === "DP-SAMPLE-003",
  ) as { id: number } | undefined;
  expect(project).toBeTruthy();

  const forbiddenResponses: string[] = [];
  page.on("response", (response) => {
    if (response.status() === 403) forbiddenResponses.push(response.url());
  });
  await loginInBrowserAs(page, TEST_USERS.designManager);
  await page.goto(`/admin/design-projects/${project!.id}?tab=reviews`);
  await expect(page.getByTestId("design-review-role-guide")).toContainText(
    /Người duyệt duy nhất|sole approver/i,
  );
  await expect(page.getByTestId("design-review-role-guide")).toContainText(
    /Chủ nhiệm thiết kế|Design Manager/i,
  );
  await expect(page.getByTestId("create-design-review")).toBeVisible();

  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(page.getByTestId("design-review-role-guide")).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
        ),
      )
      .toBe(true);
  }
  expect(forbiddenResponses).toEqual([]);

  await loginInBrowserAs(page, TEST_USERS.designLead);
  await page.goto(`/admin/design-projects/${project!.id}?tab=reviews`);
  await expect(page.getByTestId("design-review-role-guide")).toBeVisible();
  await expect(page.getByTestId("create-design-review")).toHaveCount(0);
});

test("newly uploaded design evidence explains Drive sync and becomes selectable after refresh", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const managerHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.designManager)}` };
  const projects = await api.get("/api/design-projects?search=DP-SAMPLE-003&pageSize=10", {
    headers: managerHeaders,
  });
  expect(projects.ok(), await projects.text()).toBeTruthy();
  const project = (await projects.json()).items.find(
    (item: { projectCode: string }) => item.projectCode === "DP-SAMPLE-003",
  ) as { id: number; operationalProjectId: number } | undefined;
  expect(project).toBeTruthy();

  const detailResponse = await api.get(`/api/design-projects/${project!.id}`, {
    headers: managerHeaders,
  });
  expect(detailResponse.ok(), await detailResponse.text()).toBeTruthy();
  const detail = (await detailResponse.json()) as { operationalProjectId: number };
  const basicDocumentsResponse = await api.get(
    `/api/basic-design-docs?designProjectId=${project!.id}&pageSize=1`,
    { headers: managerHeaders },
  );
  expect(basicDocumentsResponse.ok(), await basicDocumentsResponse.text()).toBeTruthy();
  const source = ((await basicDocumentsResponse.json()) as { items: Array<{ id: number }> })
    .items[0];
  expect(source).toBeTruthy();

  const unique = Date.now().toString(36);
  const fileName = `basic-design-awaiting-sync-${unique}.pdf`;
  const inserted = execSql(`SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
    INSERT INTO project_documents
      (OperationalProjectId, Category, SourceModule, SourceType, SourceEntityType, SourceRecordId,
       LocalPath, OriginalFileName, ContentType, Size, Sha256, Origin, Generation, DesiredOperation,
       SyncStatus, SyncAttemptCount, NextSyncAttemptAt, IsDownloadable, ConflictState, CreatedAt, UpdatedAt)
    OUTPUT INSERTED.Id
    VALUES (${detail.operationalProjectId}, 'DesignBasic', 'Design', 'ManualUpload',
      'BasicDesignDoc', ${source.id}, '/files/project-documents/${detail.operationalProjectId}/${fileName}',
      '${fileName}', 'application/pdf', 20,
      'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'Nicon', 1, 'Upsert',
      'Pending', 0, '2099-01-01', 1, 'None', SYSUTCDATETIME(), SYSUTCDATETIME());`);
  const documentId = Number(inserted.trim());

  try {
    await loginInBrowserAs(page, TEST_USERS.designManager);
    await page.goto(`/admin/design-projects/${project!.id}?tab=reviews`);
    await page.getByTestId("create-design-review").click();
    const dialog = page.getByRole("dialog");
    await dialog.getByLabel(/Giai đoạn|Stage/i).click();
    await page.getByRole("option", { name: /Thiết kế cơ sở|Basic design/i }).click();

    const documentRow = dialog.getByTestId(`review-document-${documentId}`);
    await expect(documentRow).toContainText(fileName);
    await expect(documentRow.getByRole("checkbox")).toBeDisabled();
    await expect(documentRow).toContainText(/đang chờ đồng bộ|waiting for Drive sync/i);
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(documentRow).toBeVisible();
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
    }

    execSql(`SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
      UPDATE project_documents SET SyncStatus='Synced', DesiredOperation='None',
        NextSyncAttemptAt=NULL, UpdatedAt=SYSUTCDATETIME() WHERE Id=${documentId};`);
    await dialog
      .getByRole("button", { name: /Tải lại trạng thái tài liệu|Refresh document status/i })
      .click();
    await expect(documentRow.getByRole("checkbox")).toBeEnabled();
  } finally {
    execSql(`SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
      DELETE FROM project_documents WHERE Id=${documentId};`);
  }
});

test("design notification opens recipient evidence and comments without project-wide access", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(240_000);
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  test.skip(
    !(await isGoogleDriveConnected(api, adminHeaders)),
    "Requires a connected Google Drive because approved design evidence must be durably synced.",
  );
  const customerId = await createOwnCustomer(api, adminHeaders, "NICON Concept recipient");
  let operationalProjectId: number | undefined;
  try {
    const projectId = await createDesignProject(api, {
      headers: adminHeaders,
      customerId,
      name: `NICON Concept circulation ${Date.now()}`,
    });
    const projectResponse = await api.get(`/api/design-projects/${projectId}`, {
      headers: adminHeaders,
    });
    expect(projectResponse.ok()).toBeTruthy();
    operationalProjectId = (await projectResponse.json()).operationalProjectId;
    const concept = await api.post("/api/concept-options", {
      headers: adminHeaders,
      data: { designProjectId: projectId, name: "Phương án nhà xưởng NICON R00" },
    });
    expect(concept.ok(), await concept.text()).toBeTruthy();
    const conceptId = (await concept.json()).id as number;
    await approveDesignStage(api, {
      headers: adminHeaders,
      projectId,
      stage: "Concept",
      category: "DesignConcept",
      sourceEntityType: "ConceptOption",
      sourceRecordIds: [conceptId],
      stopAfterInternal: true,
    });
    const reviewsResponse = await api.get(`/api/design-projects/${projectId}/reviews`, {
      headers: adminHeaders,
    });
    expect(reviewsResponse.ok()).toBeTruthy();
    const review = (await reviewsResponse.json())[0] as {
      id: number;
      title: string;
      documents: Array<{ originalFileName: string; sha256: string }>;
    };
    const saleHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.sale)}` };
    expect(
      (await api.get(`/api/design-projects/${projectId}`, { headers: saleHeaders })).status(),
    ).toBe(403);
    const notifications = await api.get("/api/notifications?take=100", { headers: saleHeaders });
    expect(notifications.ok()).toBeTruthy();
    const notification = (await notifications.json()).find(
      (item: { refEntityType: string; refEntityId: number }) =>
        item.refEntityType === "DesignReview" && item.refEntityId === review.id,
    );
    expect(notification?.linkUrl).toBe(`/admin/design-reviews/${projectId}?reviewId=${review.id}`);

    const pageErrors: string[] = [];
    const requests: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    page.on("request", (request) =>
      requests.push(new URL(request.url()).pathname.replace(/^\/api\/v1\//, "/api/")),
    );
    await loginInBrowserAs(page, TEST_USERS.sale);
    await page.goto(notification.linkUrl);
    const card = page.locator(`#design-review-${review.id}`);
    await expect(card.getByRole("heading", { name: review.title, exact: true })).toBeVisible();
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(card).toBeVisible();
      await expect
        .poll(() =>
          page.evaluate(
            () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
          ),
        )
        .toBe(true);
    }
    expect(requests).not.toContain(`/api/design-projects/${projectId}`);
    expect(requests).not.toContain(`/api/operational-projects/${operationalProjectId}/documents`);
    expect(requests).not.toContain(
      `/api/design-projects/${projectId}/reviews/recipient-candidates`,
    );
    const comment =
      "Kinh doanh xác nhận lối tiếp cận xe tải; đề nghị giữ phương án cổng phía Đông.";
    await card.locator(`#review-${review.id}-note`).fill(comment);
    const commentResponse = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/reviews/${review.id}/comments`) &&
        response.request().method() === "POST",
    );
    await card.getByRole("button", { name: /Bình luận|Comment|评论|コメント/i }).click();
    expect((await commentResponse).ok()).toBeTruthy();
    await expect(card.getByText(comment, { exact: true })).toBeVisible();

    const file = review.documents[0];
    const downloadEvent = page.waitForEvent("download");
    await card.getByRole("button", { name: file.originalFileName, exact: true }).click();
    const download = await downloadEvent;
    expect(download.suggestedFilename()).toBe(file.originalFileName);
    const downloadPath = await download.path();
    expect(downloadPath).toBeTruthy();
    expect(
      createHash("sha256")
        .update(await readFile(downloadPath!))
        .digest("hex"),
    ).toBe(file.sha256);

    await page.goto(`/admin/design-projects/${projectId}?tab=reviews`);
    await expect(card.getByRole("heading", { name: review.title, exact: true })).toBeVisible();
    await expect(card.getByText(comment, { exact: true })).toBeVisible();
    await loginInBrowserAs(page, TEST_USERS.accountant);
    await page.goto(notification.linkUrl);
    await expect(page.getByTestId("design-reviews-tab")).toBeVisible();
    await expect(card).toHaveCount(0);
    expect(pageErrors).toEqual([]);
  } finally {
    if (operationalProjectId) {
      // Frozen Concept evidence is deliberately retained by the public API.
      // Preserve its isolated fixture and never mask a workflow failure with
      // an attempted cleanup that bypasses the evidence-retention contract.
      test.info().annotations.push({
        type: "retained-review-evidence",
        description: `Operational project ${operationalProjectId}, customer ${customerId}: frozen Concept review evidence retained for inspection.`,
      });
    }
  }
});
