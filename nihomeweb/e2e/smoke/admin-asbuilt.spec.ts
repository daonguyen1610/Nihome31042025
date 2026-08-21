import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";

/**
 * NIH-145 M4 As-built dossier end-to-end. Real path through the
 * running docker stack. Verifies:
 *
 *   1. SUPER_ADMIN provisions a DesignProject.
 *   2. The /admin/construction/asbuilt page renders + accepts the
 *      New document dialog.
 *   3. Workflow toolbar walks Draft → Submitted → Approved via the
 *      dedicated /approve endpoint.
 *   4. Completeness roll-up reflects Approved category coverage.
 *   5. SALE is blocked from the API.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-145 — As-built dossier (real-user flow)", () => {
  test("SUPER_ADMIN drafts, submits, approves and completeness updates", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    const customerId = await createOwnCustomer(api, authHeader, "AB");

    const projSuffix = uid();
    const projectId = await createDesignProject(api, {
      headers: authHeader,
      name: `E2E-AB ${projSuffix}`,
      customerId,
    });

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/construction/asbuilt`, { waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await expect(page.getByTestId("asbuilt-page")).toBeVisible();

    const projectFilter = page.locator('button[role="combobox"]').first();
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().includes("/api/as-built-documents") &&
          r.url().includes(`designProjectId=${projectId}`),
      ),
      (async () => {
        await projectFilter.click();
        await page
          .getByRole("option", { name: new RegExp(`E2E-AB ${projSuffix}`) })
          .click();
      })(),
    ]);

    // Create a Drawing document
    await page.getByTestId("asbuilt-new").click();
    await page.waitForSelector('[data-testid="asbuilt-form-title"]');
    const titleText = `E2E as-built ${uid()}`;
    const documentPath = "/process-assets/files/501e798356d44d8792986b936ac2d100.pdf";
    const documentResponse = await api.get(documentPath);
    expect(documentResponse.ok()).toBeTruthy();
    expect(documentResponse.headers()["content-type"]).toContain("application/pdf");
    expect((await documentResponse.body()).subarray(0, 5).toString()).toBe("%PDF-");
    await page.getByTestId("asbuilt-form-title").fill(titleText);
    await page.getByTestId("asbuilt-form-file-url").fill(documentPath);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/as-built-documents") &&
          r.request().method() === "POST" &&
          r.status() === 201,
      ),
      page.getByTestId("asbuilt-form-save").click({ force: true }),
    ]);
    await page.waitForResponse(
      (r) => r.url().includes("/api/as-built-documents?") && r.request().method() === "GET",
    );

    const row = page.locator('[data-testid^="asbuilt-row-"]').filter({ hasText: titleText });
    await expect(row).toBeVisible();
    await expect(row.locator('[data-testid^="asbuilt-row-view-"]')).toBeVisible();
    await expect(row.locator('[data-testid^="asbuilt-row-edit-"]')).toBeVisible();
    await expect(row.locator('[data-testid^="asbuilt-row-delete-"]')).toBeVisible();

    // API validation feedback keeps the user's input in the open form.
    await page.getByTestId("asbuilt-new").click();
    await page.getByTestId("asbuilt-form-title").fill(titleText);
    await Promise.all([
      page.waitForResponse(
        (r) =>
          r.url().endsWith("/api/as-built-documents") &&
          r.request().method() === "POST" &&
          r.status() === 400,
      ),
      page.getByTestId("asbuilt-form-save").click(),
    ]);
    await expect(page.getByTestId("asbuilt-form-title")).toHaveValue(titleText);
    await expect(page.getByText(/đã tồn tại|already exists/i)).toBeVisible();
    await page.keyboard.press("Escape");

    // Edit the draft and verify the list reflects the saved value.
    await row.locator('[data-testid^="asbuilt-row-view-"]').click();
    await page.getByTestId("asbuilt-detail-file-preview").click();
    await expect(page.getByTestId("asbuilt-detail-file-preview-frame")).toHaveAttribute(
      "src",
      new RegExp(documentPath.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$"),
    );
    await page.getByTestId("asbuilt-detail-file-preview-close").click();
    await expect(page.getByTestId("asbuilt-detail-file-preview-dialog")).toHaveCount(0);
    await page.getByTestId("asbuilt-edit").click();
    const updatedTitle = `${titleText} updated`;
    await page.getByTestId("asbuilt-form-title").fill(updatedTitle);
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/as-built-documents\/\d+$/.test(r.url()) && r.request().method() === "PUT" && r.status() === 200,
      ),
      page.getByTestId("asbuilt-form-save").click(),
    ]);
    const updatedRow = page.locator('[data-testid^="asbuilt-row-"]').filter({ hasText: updatedTitle });
    await expect(updatedRow).toBeVisible();
    await page.getByRole("dialog").getByRole("button", { name: "Close" }).click();

    await page.getByTestId("asbuilt-sort").click();
    await page.getByRole("option", { name: /Recently updated|Mới cập nhật/i }).click();
    await expect(page.getByTestId("asbuilt-sort")).toContainText(/Recently updated|Mới cập nhật/i);

    const downloadPromise = page.waitForEvent("download");
    await page.getByTestId("asbuilt-export").click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/^as-built-documents-\d{4}-\d{2}-\d{2}\.csv$/);

    await updatedRow.click();

    // Draft → Submitted
    await page.getByTestId("asbuilt-submit").click();
    const submitConfirm = page.getByRole("alertdialog").getByTestId("asbuilt-action-confirm");
    await expect(submitConfirm).toBeVisible();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/as-built-documents\/\d+\/status$/.test(r.url()) && r.status() === 200,
      ),
      submitConfirm.click(),
    ]);
    await expect(page.getByTestId("asbuilt-approve")).toBeVisible();

    // Submitted → Approved via /approve
    await page.getByTestId("asbuilt-approve").click();
    const approveConfirm = page.getByRole("alertdialog").getByTestId("asbuilt-action-confirm");
    await expect(approveConfirm).toBeVisible();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/as-built-documents\/\d+\/approve$/.test(r.url()) && r.status() === 200,
      ),
      approveConfirm.click(),
    ]);

    await expect
      .poll(
        async () => {
          const list = await api.get(
            `/api/as-built-documents?designProjectId=${projectId}&pageSize=50`,
            { headers: authHeader },
          );
          if (!list.ok()) return { status: "err", completedRequiredCategories: 0 };
          const body = await list.json();
          const match = (body.items as Array<{ title: string; status: string }>).find((i) => i.title === updatedTitle);
          return {
            status: match?.status ?? "missing",
            completedRequiredCategories: body.completedRequiredCategories,
          };
        },
        { timeout: 5_000 },
      )
      .toMatchObject({ status: "Approved", completedRequiredCategories: 1 });

    await page.getByTestId("asbuilt-delete").click();
    await Promise.all([
      page.waitForResponse(
        (r) => /\/api\/as-built-documents\/\d+$/.test(r.url()) && r.request().method() === "DELETE" && r.status() === 204,
      ),
      page.getByTestId("asbuilt-delete-confirm").click(),
    ]);
  });

  test("SALE role is blocked from as-built endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/as-built-documents", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
