import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject } from "../fixtures/designProjects";

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-144 — Project handover", () => {
  test("authorized user can create, view, edit and delete a handover", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    const customers = await api.get("/api/customers?pageSize=1", { headers: authHeader });
    const customerId = customers.ok() ? (await customers.json()).items?.[0]?.id ?? 0 : 0;
    expect(customerId).toBeGreaterThan(0);
    const projectName = `E2E-HO ${uid()}`;
    const projectId = await createDesignProject(api, { headers: authHeader, name: projectName, customerId });
    const users = await api.get("/api/users?take=1", { headers: authHeader });
    expect(users.ok()).toBeTruthy();
    const responsibleUserId = (await users.json()).items?.[0]?.id as number;
    expect(responsibleUserId).toBeGreaterThan(0);
    const title = `E2E handover ${uid()}`;
    const documentPath = "/images/activities/activity-ceremony.jpg";
    const documentResponse = await api.get(documentPath);
    expect(documentResponse.ok()).toBeTruthy();
    expect(documentResponse.headers()["content-type"]).toContain("image/jpeg");
    const imageBytes = await documentResponse.body();
    expect([...imageBytes.subarray(0, 3)]).toEqual([0xff, 0xd8, 0xff]);
    const created = await api.post("/api/handover-records", {
      headers: authHeader,
      data: {
        designProjectId: projectId,
        title,
        plannedHandoverDate: "2026-09-15",
        responsibleUserId,
        commissioningCompleted: false,
        checklistItems: [],
        documents: [documentPath],
        signatories: [],
      },
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    const handoverId = (await created.json()).id as number;
    expect(handoverId).toBeGreaterThan(0);

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/handover`, { waitUntil: "networkidle" });

    await expect(page.getByTestId("handover-page")).toBeVisible();
    await expect(page.locator("body")).not.toContainText("handover.title");
    await Promise.all([
      page.waitForResponse(
        (response) => {
          const url = new URL(response.url());
          return (
            url.pathname === "/api/handover-records" &&
            url.searchParams.get("search") === title &&
            response.request().method() === "GET"
          );
        },
      ),
      page.getByTestId("handover-search").fill(title),
    ]);
    const row = page.getByTestId(`handover-row-${handoverId}`);
    await expect(row).toBeVisible({ timeout: 10_000 });
    await expect(row).toContainText(title);
    await expect(row.locator('[data-testid^="handover-row-view-"]')).toBeVisible();
    await expect(row.locator('[data-testid^="handover-row-edit-"]')).toBeVisible();
    await expect(row.locator('[data-testid^="handover-row-delete-"]')).toBeVisible();

    await row.locator('[data-testid^="handover-row-view-"]').click();
    const detail = page.getByTestId("handover-detail");
    await expect(detail.getByText(title, { exact: true })).toBeVisible();
    await page.getByTestId("handover-document-preview-0").click();
    const previewImage = page.getByTestId("handover-document-preview-0-image");
    await expect(previewImage).toHaveAttribute(
      "src",
      new RegExp(documentPath.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$"),
    );
    await expect
      .poll(() => previewImage.evaluate((image: HTMLImageElement) => ({
        complete: image.complete,
        width: image.naturalWidth,
        height: image.naturalHeight,
      })))
      .toEqual({ complete: true, width: 1280, height: 960 });
    await page.getByTestId("handover-document-preview-0-close").click();
    await expect(page.getByTestId("handover-document-preview-0-dialog")).toHaveCount(0);
    await page.getByTestId("handover-detail-edit").click();
    const updatedTitle = `${title} updated`;
    await page.getByTestId("handover-form-title").fill(updatedTitle);
    // Set up response waiter before clicking to avoid race conditions in CI
    const updateResponsePromise = page.waitForResponse(
      (response) => new URL(response.url()).pathname === `/api/handover-records/${handoverId}`
        && response.request().method() === "PUT",
      { timeout: 15_000 },
    );
    const refreshedDetailPromise = page.waitForResponse(
      (response) => new URL(response.url()).pathname === `/api/handover-records/${handoverId}`
        && response.request().method() === "GET",
      { timeout: 15_000 },
    );
    await page.getByTestId("handover-form-save").click();
    const updateResponse = await updateResponsePromise;
    expect(updateResponse.status(), await updateResponse.text()).toBe(200);
    const refreshedDetail = await refreshedDetailPromise;
    expect(refreshedDetail.status(), await refreshedDetail.text()).toBe(200);

    const updatedRow = page.getByTestId(`handover-row-${handoverId}`);
    await expect(updatedRow).toContainText(updatedTitle, { timeout: 10_000 });
    await expect(detail.getByText(updatedTitle, { exact: true })).toBeVisible();
    await page.getByTestId("handover-detail-delete").click();
    const deleteResponsePromise = page.waitForResponse(
      (response) => new URL(response.url()).pathname === `/api/handover-records/${handoverId}`
        && response.request().method() === "DELETE",
      { timeout: 15_000 },
    );
    await page.getByTestId("handover-delete-confirm").click();
    const deleteResponse = await deleteResponsePromise;
    expect(deleteResponse.status(), await deleteResponse.text()).toBe(204);
    await expect(updatedRow).toHaveCount(0);
  });
});
