import { test, expect, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

for (const width of [390, 768]) {
  test(`offline project upload exposes retry and delete at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const configuration = await api.get("/api/site-settings/google-drive/configuration", {
      headers,
    });
    expect(configuration.status()).toBe(200);
    // This test intentionally exercises offline storage without writing to a live Drive.
    test.skip((await configuration.json()).enabled, "Requires the offline Compose configuration");
    const projects = await (
      await api.get("/api/operational-projects?pageSize=1", { headers })
    ).json();
    expect(projects.items.length).toBeGreaterThan(0);
    const projectId = projects.items[0].id;
    const fileName = `NICON-offline-${Date.now()}-${width}.pdf`;
    let documentId: number | undefined;
    try {
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.setViewportSize({ width, height: 900 });
      await page.goto(`/admin/operational-projects/${projectId}`);
      const section = page.getByTestId("project-documents-section");
      await section.locator("#project-document-file").setInputFiles({
        name: fileName,
        mimeType: "application/pdf",
        buffer: Buffer.from("%PDF-1.4\nNICON offline document evidence\n%%EOF"),
      });
      await section.locator("#project-document-category").click();
      await page.getByRole("option", { name: /Thiết kế cơ sở|Basic design/i }).click();
      const uploaded = page.waitForResponse(
        (res) =>
          res.request().method() === "POST" &&
          new URL(res.url()).pathname === `/api/operational-projects/${projectId}/documents`,
      );
      await section.getByRole("button", { name: /^(Tải lên|Upload)$/i }).click();
      const response = await uploaded;
      expect(response.status()).toBe(201);
      const document = await response.json();
      documentId = document.id;
      expect(document.syncStatus).toBe("Pending");
      expect(Number.isSafeInteger(documentId)).toBe(true);
      execSql(
        `SET QUOTED_IDENTIFIER ON; UPDATE project_documents SET SyncStatus=N'Failed', SyncAttemptCount=3, SyncError=N'E2E offline retry exhausted' WHERE Id=${documentId}`,
      );
      await page.reload();
      const actions = page
        .getByTestId(`project-document-actions-${documentId}`)
        .filter({ visible: true });
      const retry = actions
        .getByRole("button", { name: new RegExp(fileName.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")) })
        .filter({ hasText: /Gửi lại|Send to Drive/i });
      await expect(retry).toBeVisible();
      const retried = page.waitForResponse(
        (res) =>
          res.url().endsWith(`/documents/${documentId}/retry`) && res.request().method() === "POST",
      );
      await retry.click();
      expect((await retried).status()).toBe(200);
      await page.screenshot({
        path: testInfo.outputPath(`offline-document-${width}.png`),
        fullPage: true,
      });
      const removed = page.waitForResponse(
        (res) =>
          res.url().endsWith(`/documents/${documentId}`) && res.request().method() === "DELETE",
      );
      page.once("dialog", (dialog) => dialog.accept());
      await actions.getByRole("button", { name: /Xóa|Xoá|Delete/i }).click();
      expect((await removed).status()).toBe(204);
      await expect(actions).toHaveCount(0);
      const dimensions = await page
        .locator("html")
        .evaluate((element) => ({ width: element.clientWidth, scroll: element.scrollWidth }));
      expect(dimensions.scroll).toBeLessThanOrEqual(dimensions.width);
    } finally {
      if (documentId) {
        const cleanup = await api.delete(
          `/api/operational-projects/${projectId}/documents/${documentId}`,
          { headers },
        );
        expect([204, 404]).toContain(cleanup.status());
      }
    }
  });
}

test("SQL-backed project deletion preview handles Drive folder categories", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.skip(
    process.env.E2E_SQL_DATABASE !== "NiconDriveLifecycleE2E",
    "Uses an isolated disposable database binding",
  );
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const projects = await (
    await api.get("/api/operational-projects?pageSize=1", { headers })
  ).json();
  const projectId = projects.items[0].id;
  expect(Number.isSafeInteger(projectId)).toBe(true);
  const folderId = `e2e-preview-${Date.now()}`;
  execSql(
    `INSERT INTO project_drive_folders (OperationalProjectId,Category,DriveFolderId,CreatedAt,UpdatedAt) VALUES (${projectId},N'Unclassified','${folderId}',SYSUTCDATETIME(),SYSUTCDATETIME())`,
  );
  try {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/operational-projects/${projectId}`);
    const preview = page.waitForResponse((res) =>
      res.url().endsWith(`/operational-projects/${projectId}/deletion-impact`),
    );
    await page
      .getByRole("button", { name: /^(Xóa|Xoá|Delete)$/i })
      .first()
      .click();
    const response = await preview;
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body.canDelete).toBe(false);
    expect(
      body.items.some(
        (item: { key: string; action: string }) =>
          item.key === "operations.driveFolders" && item.action === "Block",
      ),
    ).toBe(true);
    const dialog = page.getByRole("dialog").or(page.getByRole("alertdialog"));
    await expect(dialog).toBeVisible();
    await expect(dialog).not.toContainText("LINQ");
    await expect(dialog).not.toContainText("could not be translated");
    await page.screenshot({
      path: testInfo.outputPath("project-delete-preview.png"),
      fullPage: true,
    });
  } finally {
    execSql(
      `DELETE FROM project_drive_folders WHERE OperationalProjectId=${projectId} AND DriveFolderId='${folderId}'`,
    );
  }
});
