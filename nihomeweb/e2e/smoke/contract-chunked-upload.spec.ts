import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const MB = 1024 * 1024;

/**
 * A large contract file goes up in chunks. When the connection drops part-way,
 * the tray says so and the upload continues from where the server stopped
 * once the connection is back, so the whole file arrives once.
 */
test("a large contract file continues after the connection drops and arrives whole", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(180_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON – Chủ đầu tư nhà máy Bắc Ninh");
  let projectId: number | undefined;
  let contractId: number | undefined;

  try {
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Nhà máy linh kiện Bắc Ninh" },
    });
    expect(project.status(), await project.text()).toBe(201);
    projectId = (await project.json()).id;
    const contract = await api.post("/api/contracts", {
      headers,
      data: {
        customerId,
        operationalProjectId: projectId,
        direction: "Upstream",
        type: "Construction",
        value: 48_000_000_000,
        status: "Draft",
      },
    });
    expect(contract.status(), await contract.text()).toBe(201);
    contractId = (await contract.json()).id;

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`/admin/contracts/${contractId}`);
    await page.getByRole("tab", { name: "Tài liệu", exact: true }).click();
    const tray = page.getByTestId("upload-tray");

    // 20 MB goes up as chunks of 8 MB at 1 MB/s.
    const network = await page.context().newCDPSession(page);
    await network.send("Network.enable");
    const connect = (online: boolean) =>
      network.send("Network.emulateNetworkConditions", {
        offline: !online,
        latency: 0,
        downloadThroughput: -1,
        uploadThroughput: online ? MB : -1,
      });
    await connect(true);
    const fileName = "NICON-hop-dong-DB-nha-may-Bac-Ninh-ban-scan.pdf";
    const content = Buffer.alloc(20 * MB, 7);
    content.write("%PDF-1.4\n", 0);
    await page
      .getByTestId("contract-attachment-files")
      .setInputFiles({ name: fileName, mimeType: "application/pdf", buffer: content });
    await page.getByRole("button", { name: "Tải lên tài liệu" }).click();

    // Drop the connection inside the second chunk.
    const percentSent = async () => {
      const text = (await tray.getByText(/ \/ 20 MB · \d+%$/).textContent()) ?? "";
      return Number(/(\d+)%$/.exec(text)?.[1] ?? 0);
    };
    await expect.poll(percentSent, { timeout: 30_000 }).toBeGreaterThanOrEqual(45);
    await connect(false);
    await expect(tray.getByText(/^Mất kết nối ở .* · tự tải tiếp khi có mạng$/)).toBeVisible({
      timeout: 15_000,
    });
    await page.waitForTimeout(2_000);
    await connect(true);

    await expect(tray.getByText(/^Đã tải lên · 20 MB$/)).toBeVisible({ timeout: 60_000 });
    const attachments = await api.get(`/api/contracts/${contractId}/attachments`, { headers });
    const listed = (await attachments.json()) as { id: number; fileSize: number }[];
    expect(listed).toHaveLength(1);
    expect(listed[0].fileSize).toBe(content.length);
    const stored = await api.get(
      `/api/contracts/${contractId}/attachments/${listed[0].id}/content`,
      { headers },
    );
    expect(Buffer.compare(await stored.body(), content), "the stored file is byte-identical").toBe(
      0,
    );
  } finally {
    if (contractId && projectId) {
      // The E2E stack has no Google Drive connection: mark the uploaded scan's
      // Drive copy deleted so hard-delete can clean up the contract.
      execSql(
        `SET QUOTED_IDENTIFIER ON; UPDATE project_documents SET SyncStatus=N'Deleted', DesiredOperation=N'None', DeletedAt=SYSUTCDATETIME(), NextSyncAttemptAt=NULL WHERE OperationalProjectId=${projectId} AND SourceEntityType=N'ContractAttachment' AND SourceType=N'ExistingManagedFile' AND DriveFileId IS NULL AND DeletedAt IS NULL;`,
      );
    }
    if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
