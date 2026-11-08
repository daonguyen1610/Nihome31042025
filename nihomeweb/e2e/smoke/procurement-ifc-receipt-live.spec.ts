import { createHash } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { execSql } from "../fixtures/db";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const fixtureSql = (sql: string) => execSql(`SET QUOTED_IDENTIFIER ON; ${sql}`);

// Isolated released fixture: the client-review/Drive issuance gate is covered
// by integration tests. No API interception or altered production seed records.
test("addressed Procurement user views and acknowledges released IFC", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.slow();
  const browserErrors: string[] = [];
  page.on("pageerror", (error) => browserErrors.push(error.message));
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(
    api,
    headers,
    "NICON – Bàn giao IFC kết cấu Long Thành",
  );
  let designId: number | undefined;
  let rootId: number | undefined;
  let releaseId: number | undefined;
  let drawingId: number | undefined;
  try {
    designId = await createDesignProject(api, {
      headers,
      customerId,
      name: `NICON – IFC kết cấu ${Date.now()}`,
    });
    rootId = (await (await api.get(`/api/design-projects/${designId}`, { headers })).json())
      .operationalProjectId;
    const users = await (await api.get("/api/users?take=200", { headers })).json();
    const receiver = users.items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.procurement.phoneNumber,
    );
    expect(receiver).toBeTruthy();
    const member = await api.post(`/api/operational-projects/${rootId}/team/members`, {
      headers,
      data: {
        userId: receiver.id,
        position: "Tiếp nhận hồ sơ Cung ứng",
        startedAt: new Date().toISOString(),
        roles: [{ roleCode: "Observer", scope: "Project" }],
      },
    });
    expect(member.status(), await member.text()).toBe(201);
    fixtureSql(`UPDATE design_projects SET CurrentStage=N'ShopDrawing' WHERE Id=${designId};`);
    const createdDrawing = await api.post("/api/shop-drawings", {
      headers,
      data: {
        designProjectId: designId,
        disciplineCode: "structure",
        constructionItem: "Kết cấu nhà xưởng",
        title: "Mặt bằng móng đã phát hành",
      },
    });
    expect(createdDrawing.ok(), await createdDrawing.text()).toBe(true);
    const drawing = await createdDrawing.json();
    drawingId = drawing.id;
    const bytes = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5Wl1sAAAAASUVORK5CYII=",
      "base64",
    );
    const uploaded = await api.post(`/api/shop-drawings/${drawing.id}/upload`, {
      headers,
      multipart: {
        file: { name: "NICON-mat-bang-mong.png", mimeType: "image/png", buffer: bytes },
      },
    });
    expect(uploaded.ok(), await uploaded.text()).toBe(true);
    fixtureSql(`UPDATE shop_drawings SET Status=N'Approved' WHERE Id=${drawing.id};`);
    const created = await api.post("/api/ifc-releases", {
      headers,
      data: { designProjectId: designId, title: "IFC – Kết cấu nhà xưởng Long Thành" },
    });
    expect(created.status(), await created.text()).toBe(201);
    releaseId = (await created.json()).id;
    const bundled = await api.post(`/api/ifc-releases/${releaseId}/items`, {
      headers,
      data: { shopDrawingIds: [drawing.id] },
    });
    expect(bundled.ok(), await bundled.text()).toBe(true);
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/design-projects/${designId}?tab=ifc`);
    await page.getByTestId(`ifc-row-${releaseId}`).click();
    await page.getByRole("combobox", { name: "Người nhận IFC Cung ứng" }).click();
    await page.getByRole("option", { name: receiver.fullName, exact: true }).click();
    await page.getByTestId("ifc-recipient-type").click();
    await page.getByRole("option", { name: /^Khác$|^Other$/i }).click();
    const recipientResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/ifc-releases/${releaseId}/recipients` &&
        response.request().method() === "POST",
    );
    await page.getByTestId("ifc-recipient-add").click();
    const added = await recipientResponse;
    expect(added.ok(), await added.text()).toBe(true);
    const recipientId = (await added.json()).recipients.find(
      (row: { recipientUserId?: number }) => row.recipientUserId === receiver.id,
    ).id;
    await loginInBrowserAs(page, TEST_USERS.procurement);
    await page.goto(`/admin/procurement-control?projectId=${rootId}&tab=ifc`);
    await expect(page.getByTestId("procurement-ifc-inbox")).toContainText(
      "Chưa có hồ sơ IFC đã phát hành",
    );
    const hash = createHash("sha256").update(bytes).digest("hex");
    fixtureSql(
      `UPDATE ifc_release_items SET ReleasedFileSha256='${hash}' WHERE IfcReleaseId=${releaseId}; UPDATE shop_drawings SET Status=N'Released' WHERE Id=${drawing.id}; UPDATE ifc_releases SET Status=N'Released', ReleaseDate=SYSUTCDATETIME() WHERE Id=${releaseId};`,
    );
    await page.reload();
    const packet = page.getByTestId(`procurement-ifc-${recipientId}`);
    await expect(packet).toContainText("IFC – Kết cấu nhà xưởng Long Thành");
    const contentResponse = page.waitForResponse(
      (response) =>
        response.url().includes(`/ifc/${recipientId}/items/`) &&
        response.url().endsWith("/content"),
    );
    await packet.getByRole("button", { name: /Xem trước|Preview/ }).click();
    expect((await contentResponse).status()).toBe(200);
    await expect(page.getByRole("dialog").locator("img")).toBeVisible();
    await page.keyboard.press("Escape");
    const receiptResponse = page.waitForResponse((response) =>
      response.url().endsWith(`/ifc/${recipientId}/acknowledge`),
    );
    await packet.getByRole("button", { name: "Xác nhận đã nhận IFC", exact: true }).click();
    const received = await receiptResponse;
    expect(received.ok(), await received.text()).toBe(true);
    const timestamp = (await received.json()).acknowledgedAt;
    await page.reload();
    await expect(packet).toContainText("Đã nhận");
    await expect(
      packet.getByRole("button", { name: "Xác nhận đã nhận IFC", exact: true }),
    ).toHaveCount(0);
    // Change the fixture checksum, then prove the live UI explains the block.
    fixtureSql(
      `UPDATE ifc_release_items SET ReleasedFileSha256=REPLICATE('0',64) WHERE IfcReleaseId=${releaseId};`,
    );
    for (const scenario of [
      { lang: "vi", width: 390, text: "Liên hệ Thiết kế" },
      { lang: "en", width: 768, text: "Ask Design" },
      { lang: "zh", width: 1440, text: "联系设计" },
      { lang: "ja", width: 390, text: "設計部門" },
    ]) {
      await page.evaluate(
        (language) => localStorage.setItem("nicon_lang", language),
        scenario.lang,
      );
      await page.setViewportSize({ width: scenario.width, height: 900 });
      await page.reload();
      await expect(packet).toBeVisible();
      await packet.getByRole("button").first().click();
      await expect(page.getByTestId("procurement-ifc-inbox")).toContainText(scenario.text);
      await page.keyboard.press("Escape");
      await expect(page.getByRole("dialog")).not.toBeVisible();
      await expect(
        page.getByTestId("procurement-ifc-inbox").getByText(scenario.text, { exact: false }),
      ).toBeVisible();
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1),
      ).toBe(true);
      await page.screenshot({ path: testInfo.outputPath(`ifc-receipt-${scenario.lang}.png`) });
    }
    const receipt = await api.get(`/api/operational-projects/${rootId}/procurement/ifc`, {
      headers: { Authorization: `Bearer ${await loginAs(TEST_USERS.procurement)}` },
    });
    expect((await receipt.json()).items[0].acknowledgedAt).toBe(timestamp);
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.evaluate(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`/admin/design-projects/${designId}?tab=ifc`);
    await page.getByTestId(`ifc-row-${releaseId}`).click();
    await expect(page.getByTestId(`ifc-recipient-${recipientId}`)).toContainText(receiver.fullName);
    await expect(page.getByTestId(`ifc-recipient-${recipientId}`)).toContainText("Đã xác nhận");
    const source = await (await api.get(`/api/ifc-releases/${releaseId}`, { headers })).json();
    const sourceReceipt = source.recipients.find((row: { id: number }) => row.id === recipientId);
    expect(sourceReceipt.acknowledgedByUserId).toBe(receiver.id);
    expect(sourceReceipt.acknowledgedAt).toBe(timestamp);
    await expect(page.getByTestId(`ifc-recipient-ack-${recipientId}`)).toHaveCount(0);
    expect(browserErrors).toEqual([]);
  } finally {
    // Unfreeze only this test's fixture; normal aggregate cleanup stages files.
    if (releaseId)
      fixtureSql(
        `DELETE FROM ifc_release_recipients WHERE IfcReleaseId=${releaseId}; DELETE FROM ifc_release_items WHERE IfcReleaseId=${releaseId}; DELETE FROM ifc_releases WHERE Id=${releaseId};`,
      );
    if (designId) {
      fixtureSql(`UPDATE shop_drawings SET Status=N'Rejected' WHERE DesignProjectId=${designId};`);
      // The isolated released fixture bypasses Drive. Retire only its host-only
      // replica; the source delete API removes the physical file.
      if (rootId && drawingId)
        fixtureSql(
          `UPDATE project_documents SET DesiredOperation=N'None', SyncStatus=N'Deleted', DeletedAt=SYSUTCDATETIME(), NextSyncAttemptAt=NULL WHERE OperationalProjectId=${rootId} AND SourceEntityType=N'ShopDrawing' AND SourceRecordId=${drawingId} AND DriveFileId IS NULL AND DeletedAt IS NULL;`,
        );
      if (drawingId) await hardDeleteBusinessRoot(api, headers, `/api/shop-drawings/${drawingId}`);
      await hardDeleteBusinessRoot(api, headers, `/api/design-projects/${designId}`);
    }
    if (rootId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${rootId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
