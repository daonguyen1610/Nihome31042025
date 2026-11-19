import type { Page } from "@playwright/test";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const LIMIT = "/api/site-settings/upload-limits/contracts";
const pdf = (name: string, megabytes: number) => ({
  name,
  mimeType: "application/pdf",
  buffer: Buffer.alloc(megabytes * 1024 * 1024),
});

async function setContractLimitInSettings(page: Page, value: string) {
  await page.goto("/admin/settings?section=contracts", { waitUntil: "networkidle" });
  const input = page.locator("#contract-upload-limit");
  await input.fill(value);
  await input
    .locator("xpath=ancestor::div[contains(@class,'space-y-3')][1]")
    .getByRole("button", { name: /Lưu|Save/i })
    .click();
}

/**
 * The contract file limit is an administrator setting. The Documents and
 * Appendix (VO) tabs read it and refuse a larger file before sending it, and
 * a limit above 20 MB lets a larger contract scan through.
 */
test("contract Documents and Appendix tabs follow the file limit set in Settings", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(90_000);
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const original = (await (await api.get(LIMIT, { headers: adminHeaders })).json())
    .maxFileSizeMb as number;
  const customerId = await createOwnCustomer(api, headers, "NICON – Chủ đầu tư kho lạnh Đồng Nai");
  let projectId: number | undefined;
  let contractId: number | undefined;

  try {
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Kho lạnh Đồng Nai" },
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
        value: 8_400_000_000,
        status: "Draft",
      },
    });
    expect(contract.status(), await contract.text()).toBe(201);
    contractId = (await contract.json()).id;

    // The administrator lowers the contract limit in Settings.
    await loginInBrowserAs(page, TEST_USERS.admin);
    await setContractLimitInSettings(page, "1");
    await expect
      .poll(async () => (await (await api.get(LIMIT, { headers })).json()).maxFileSizeMb)
      .toBe(1);

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    let uploads = 0;
    page.on("request", (request) => {
      const path = new URL(request.url()).pathname;
      if (request.method() === "POST" && /\/attachments$|\/appendices\/files$/.test(path))
        uploads++;
    });
    await page.goto(`/admin/contracts/${contractId}`);

    // Documents: the hint states the limit and an over-limit scan is refused.
    await page.getByRole("tab", { name: "Tài liệu", exact: true }).click();
    const panel = page.getByRole("tabpanel");
    await expect(panel.getByText(/tối đa 1 MB/)).toBeVisible();
    await panel.getByTestId("contract-attachment-files").setInputFiles(pdf("hop-dong-ky.pdf", 2));
    await expect(
      panel
        .getByRole("alert")
        .filter({ hasText: /“hop-dong-ky\.pdf” nặng 2 MB, vượt giới hạn 1 MB/ }),
    ).toBeVisible();
    await expect(panel.getByRole("button", { name: "Tải lên tài liệu" })).toBeDisabled();

    // Appendix (VO): the same limit applies to its attachments.
    await page.getByRole("tab", { name: "Phụ lục (VO)" }).click();
    await page.getByRole("button", { name: "Tạo VO mới" }).first().click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByText(/tối đa 1 MB/)).toBeVisible();
    await dialog.getByTestId("contract-appendix-files").setInputFiles(pdf("vo-01-bo-sung.pdf", 2));
    await expect(
      dialog
        .getByRole("alert")
        .filter({ hasText: /“vo-01-bo-sung\.pdf” nặng 2 MB, vượt giới hạn 1 MB/ }),
    ).toBeVisible();
    expect(uploads, "refused files must not be sent").toBe(0);

    // A higher limit lets a 25 MB scan through; the picker used to stop at 20 MB.
    const raised = await api.put(LIMIT, { headers: adminHeaders, data: { maxFileSizeMb: 30 } });
    expect(raised.ok(), await raised.text()).toBe(true);
    await page.goto(`/admin/contracts/${contractId}`);
    await page.getByRole("tab", { name: "Tài liệu", exact: true }).click();
    await expect(page.getByRole("tabpanel").getByText(/tối đa 30 MB/)).toBeVisible();
    await page
      .getByRole("tabpanel")
      .getByTestId("contract-attachment-files")
      .setInputFiles(pdf("hop-dong-ban-scan-day-du.pdf", 25));
    await expect(
      page.getByRole("tabpanel").getByText("hop-dong-ban-scan-day-du.pdf"),
    ).toBeVisible();
    await expect(page.getByRole("tabpanel").getByRole("alert")).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Tải lên tài liệu" })).toBeEnabled();
  } finally {
    const restored = await api.put(LIMIT, {
      headers: adminHeaders,
      data: { maxFileSizeMb: original },
    });
    expect(restored.ok(), await restored.text()).toBe(true);
    if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
