import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const pdf = (name: string, megabytes: number) => ({
  name,
  mimeType: "application/pdf",
  buffer: Buffer.alloc(Math.round(megabytes * 1024 * 1024)),
});

/**
 * A large contract file shows its progress in the upload tray. Leaving the page
 * mid-upload asks first, a cancel stops the upload without creating a document,
 * and a file sent in full is confirmed.
 */
test("the upload tray shows progress, cancels without saving and confirms a finished upload", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON – Chủ đầu tư nhà xưởng Long An");
  let projectId: number | undefined;
  let contractId: number | undefined;
  const attachmentIds = async () => {
    const response = await api.get(`/api/contracts/${contractId}/attachments`, { headers });
    expect(response.ok(), await response.text()).toBe(true);
    return ((await response.json()) as { id: number }[]).map((attachment) => attachment.id);
  };

  try {
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Nhà xưởng Long An giai đoạn 1" },
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
        value: 12_500_000_000,
        status: "Draft",
      },
    });
    expect(contract.status(), await contract.text()).toBe(201);
    contractId = (await contract.json()).id;

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`/admin/contracts/${contractId}`);
    await page.getByRole("tab", { name: "Tài liệu", exact: true }).click();
    const tray = page.getByTestId("upload-tray");
    const files = page.getByTestId("contract-attachment-files");
    const upload = page.getByRole("button", { name: "Tải lên tài liệu" });

    // A slow connection: 5 MB at 256 KB/s takes about 20 seconds.
    const network = await page.context().newCDPSession(page);
    await network.send("Network.enable");
    await network.send("Network.emulateNetworkConditions", {
      offline: false,
      latency: 0,
      downloadThroughput: -1,
      uploadThroughput: 256 * 1024,
    });
    const largeScan = "NICON-hop-dong-thi-cong-ban-scan.pdf";
    await files.setInputFiles(pdf(largeScan, 5));
    await upload.click();
    await expect(tray.getByText(largeScan)).toBeVisible();
    await expect(tray.getByText(/ \/ 5 MB · [1-9]\d?%$/)).toBeVisible({ timeout: 15_000 });

    // Closing the tab while the file uploads asks for confirmation; staying keeps it going.
    const leaving = page.waitForEvent("dialog");
    await page.close({ runBeforeUnload: true });
    const prompt = await leaving;
    expect(prompt.type()).toBe("beforeunload");
    await prompt.dismiss();
    await expect(tray.getByText(/ \/ 5 MB · \d+%$/)).toBeVisible();

    await tray.getByRole("button", { name: `Huỷ tải lên ${largeScan}` }).click();
    await expect(tray.getByText("Đã huỷ tải lên")).toBeVisible();
    await expect(tray.getByRole("button", { name: `Huỷ tải lên ${largeScan}` })).toHaveCount(0);
    // The server never received the whole file, so no document exists, now or later.
    await network.send("Network.emulateNetworkConditions", {
      offline: false,
      latency: 0,
      downloadThroughput: -1,
      uploadThroughput: -1,
    });
    await page.waitForTimeout(2_000);
    expect(await attachmentIds()).toEqual([]);
    await expect(page.getByText("Chưa có tài liệu").first()).toBeVisible();

    // An upload started from the Appendix (VO) dialog can be cancelled too: the
    // tray stays above the dialog, and the dialog keeps what was typed.
    await network.send("Network.emulateNetworkConditions", {
      offline: false,
      latency: 0,
      downloadThroughput: -1,
      uploadThroughput: 256 * 1024,
    });
    await page.getByRole("tab", { name: "Phụ lục (VO)" }).click();
    await page.getByRole("button", { name: "Tạo VO mới" }).first().click();
    const dialog = page.getByRole("dialog");
    const voTitle = "VO-01 Bổ sung mái che khu xuất hàng";
    await dialog.getByLabel("Tiêu đề").fill(voTitle);
    await dialog
      .getByLabel("Lý do")
      .fill("Chủ đầu tư bổ sung mái che theo biên bản họp ngày 12/11");
    await dialog.locator("input[inputmode]").first().fill("45000000");
    const voFile = "VO-01-bo-sung-mai-che.pdf";
    await dialog.getByTestId("contract-appendix-files").setInputFiles(pdf(voFile, 5));
    await dialog.getByRole("button", { name: "Lưu" }).click();
    const voRow = tray.getByRole("listitem").filter({ hasText: voFile });
    await expect(voRow.getByText(/ \/ 5 MB · [1-9]\d?%$/)).toBeVisible({ timeout: 15_000 });
    await voRow.getByRole("button", { name: `Huỷ tải lên ${voFile}` }).click();
    await expect(voRow.getByText("Đã huỷ tải lên")).toBeVisible();
    await expect(dialog.getByLabel("Tiêu đề")).toHaveValue(voTitle);
    await network.send("Network.emulateNetworkConditions", {
      offline: false,
      latency: 0,
      downloadThroughput: -1,
      uploadThroughput: -1,
    });
    const afterCancel = await api.get(`/api/contracts/${contractId}`, { headers });
    expect((await afterCancel.json()).appendixCount).toBe(0);
    // The tray leaves the dialog's own actions reachable.
    await dialog.getByRole("button", { name: "Huỷ", exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await page.getByRole("tab", { name: "Tài liệu", exact: true }).click();

    // A file sent in full is confirmed in the tray and listed on the contract.
    const signedScan = "NICON-hop-dong-da-ky.pdf";
    await files.setInputFiles(pdf(signedScan, 0.3));
    await upload.click();
    await expect(tray.getByText(signedScan)).toBeVisible();
    await expect(tray.getByText(/^Đã tải lên · 0,3 MB$/)).toBeVisible();
    await expect(tray.getByText("Đã xử lý xong các tệp")).toBeVisible();
    expect(await attachmentIds()).toHaveLength(1);

    // The tray folds to its header when it is in the way, and opens again.
    await tray.getByRole("button", { name: "Thu gọn danh sách tải lên" }).click();
    await expect(tray.getByRole("listitem")).toHaveCount(0);
    await tray.getByRole("button", { name: "Mở danh sách tải lên" }).click();
    await expect(tray.getByRole("listitem")).toHaveCount(3);

    await tray.getByRole("button", { name: "Xoá danh sách" }).click();
    await expect(tray).toHaveCount(0);
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
