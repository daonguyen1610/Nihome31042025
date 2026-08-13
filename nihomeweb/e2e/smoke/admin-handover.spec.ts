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
    const documentPath = `/files/handover/e2e-${uid()}.pdf`;
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
    const row = page.locator('[data-testid^="handover-row-"]').filter({ hasText: title });
    await expect(row).toBeVisible();
    await expect(row.locator('[data-testid^="handover-row-view-"]')).toBeVisible();
    await expect(row.locator('[data-testid^="handover-row-edit-"]')).toBeVisible();
    await expect(row.locator('[data-testid^="handover-row-delete-"]')).toBeVisible();

    await row.locator('[data-testid^="handover-row-view-"]').click();
    const detail = page.getByTestId("handover-detail");
    await expect(detail.getByText(title, { exact: true })).toBeVisible();
    await page.getByTestId("handover-document-preview-0").click();
    await expect(page.getByTestId("handover-document-preview-0-frame")).toHaveAttribute(
      "src",
      new RegExp(documentPath.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$"),
    );
    await page.getByTestId("handover-document-preview-0-close").click();
    await expect(page.getByTestId("handover-document-preview-0-dialog")).toHaveCount(0);
    await page.getByTestId("handover-detail-edit").click();
    const updatedTitle = `${title} updated`;
    await page.getByTestId("handover-form-title").fill(updatedTitle);
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/handover-records\/\d+$/.test(r.url()) && r.request().method() === "PUT" && r.status() === 200,
      ),
      page.getByTestId("handover-form-save").click(),
    ]);

    const updatedRow = page.locator('[data-testid^="handover-row-"]').filter({ hasText: updatedTitle });
    await expect(updatedRow).toBeVisible();
    await expect(page.locator('[data-radix-collection-item][data-state="open"]')).toHaveCount(0, {
      timeout: 10_000,
    });
    await page.getByTestId("handover-detail-delete").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/handover-records\/\d+$/.test(r.url()) && r.request().method() === "DELETE" && r.status() === 204,
      ),
      page.getByTestId("handover-delete-confirm").click(),
    ]);
    await expect(updatedRow).toHaveCount(0);
  });
});