import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import {
  approveDesignStage,
  createDesignProject,
  createOwnCustomer,
  isGoogleDriveConnected,
} from "../fixtures/designProjects";

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
