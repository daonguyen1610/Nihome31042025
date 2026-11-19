import type { Page } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const LIMITS = "/api/site-settings/upload-limits";
const TWO_MB_PDF = (name: string) => ({
  name,
  mimeType: "application/pdf",
  buffer: Buffer.alloc(2 * 1024 * 1024),
});

async function setLimitInSettings(page: Page, section: string, inputId: string, value: string) {
  await page.goto(`/admin/settings?section=${section}`, { waitUntil: "networkidle" });
  const input = page.locator(`#${inputId}`);
  await input.fill(value);
  await input
    .locator("xpath=ancestor::div[contains(@class,'space-y-3')][1]")
    .getByRole("button", { name: /Lưu|Save/i })
    .click();
}

/**
 * Tender and capability document limits are administrator settings
 * (AGENTS.md: no hard-coded limits). A change in Settings reaches the upload
 * controls, which refuse a wrong file before sending it.
 */
test("tender and capability upload limits set in Settings stop wrong files before upload", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const original = {
    tenders: (await (await api.get(`${LIMITS}/tenders`, { headers: adminHeaders })).json())
      .maxFileSizeMb as number,
    capability: (await (await api.get(`${LIMITS}/capability`, { headers: adminHeaders })).json())
      .maxFileSizeMb as number,
  };
  const unique = Date.now().toString();
  let customerId = 0;
  let projectId = 0;
  let tenderId = 0;

  try {
    const customer = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Chủ đầu tư nhà xưởng ${unique}`,
        sourceCode: "marketing",
        primaryContact: { fullName: "Anh Hưng", phone: `07${unique.slice(-8)}`, isPrimary: true },
      },
    });
    expect(customer.status(), await customer.text()).toBe(201);
    customerId = (await customer.json()).id as number;
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { name: `Nhà xưởng Bình Dương ${unique}`, customerId },
    });
    expect(project.status(), await project.text()).toBe(201);
    projectId = (await project.json()).id as number;
    const tender = await api.post("/api/tenders", {
      headers,
      data: {
        name: `Gói thầu thi công nhà xưởng ${unique}`,
        customerId,
        operationalProjectId: projectId,
        invitationReceivedAt: new Date(Date.now() - 24 * 60 * 60 * 1_000).toISOString(),
        invitationSource: "Cổng đấu thầu quốc gia",
        submissionDeadline: new Date(Date.now() + 14 * 24 * 60 * 60 * 1_000).toISOString(),
      },
    });
    expect(tender.status(), await tender.text()).toBe(201);
    tenderId = (await tender.json()).id as number;

    // The administrator lowers both limits in Settings.
    await loginInBrowserAs(page, TEST_USERS.admin);
    await setLimitInSettings(page, "tender-files", "tender-upload-limit", "1");
    await setLimitInSettings(page, "capability-files", "capability-upload-limit", "1");
    await expect
      .poll(
        async () => (await (await api.get(`${LIMITS}/tenders`, { headers })).json()).maxFileSizeMb,
      )
      .toBe(1);
    await expect
      .poll(
        async () =>
          (await (await api.get(`${LIMITS}/capability`, { headers })).json()).maxFileSizeMb,
      )
      .toBe(1);

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    let uploads = 0;
    page.on("request", (request) => {
      const path = new URL(request.url()).pathname;
      if (
        request.method() === "POST" &&
        (path.endsWith("/invitation-documents") ||
          path.endsWith("/upload") ||
          path === "/api/capability-documents/upload")
      )
        uploads++;
    });

    // Tender invitation documents.
    await page.goto(`/admin/tenders/${tenderId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Thông tin|Information/i }).click();
    const invitationInput = page.locator('input[type="file"][multiple]').first();
    await invitationInput.setInputFiles([
      TWO_MB_PDF("ho-so-moi-thau.pdf"),
      {
        name: "ban-ve-hien-trang.dwg",
        mimeType: "application/octet-stream",
        buffer: Buffer.from("x"),
      },
    ]);
    await expect(
      page.getByText(/“ho-so-moi-thau\.pdf” nặng 2 MB, vượt giới hạn 1 MB/).first(),
    ).toBeVisible();
    await expect(page.getByText(/“ban-ve-hien-trang\.dwg” chưa được nhận/).first()).toBeVisible();

    // A tender checklist item's upload follows the same limit.
    await page.getByRole("tab", { name: /Checklist hồ sơ|Bid checklist/i }).click();
    const chooser = page.waitForEvent("filechooser");
    await page.locator('[data-testid^="tender-checklist-upload-"]:visible').first().click();
    await (await chooser).setFiles(TWO_MB_PDF("du-toan-du-thau.pdf"));
    await expect(
      page.getByText(/“du-toan-du-thau\.pdf” nặng 2 MB, vượt giới hạn 1 MB/).first(),
    ).toBeVisible();

    // Capability documents: the drop hint states the configured limit.
    await page.goto("/admin/capability-documents", { waitUntil: "networkidle" });
    await expect(page.getByText(/tối đa 1 MB/).first()).toBeVisible();
    // A tag is chosen first, as the page requires.
    await page
      .getByTestId("capability-select-file")
      .locator("xpath=preceding-sibling::button[@role='combobox']")
      .click();
    await page.getByRole("option").first().click();
    await page
      .locator('input[type="file"]')
      .first()
      .setInputFiles(TWO_MB_PDF("chung-chi-iso-9001.pdf"));
    await expect(
      page.getByText(/“chung-chi-iso-9001\.pdf” nặng 2 MB, vượt giới hạn 1 MB/).first(),
    ).toBeVisible();
    expect(uploads, "refused files must not be sent").toBe(0);
  } finally {
    for (const [kind, value] of Object.entries(original)) {
      const restored = await api.put(`${LIMITS}/${kind}`, {
        headers: adminHeaders,
        data: { maxFileSizeMb: value },
      });
      expect(restored.ok(), await restored.text()).toBeTruthy();
    }
    if (tenderId) await hardDeleteBusinessRoot(api, headers, `/api/tenders/${tenderId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
