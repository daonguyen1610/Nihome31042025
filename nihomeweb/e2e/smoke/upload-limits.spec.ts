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
  // Wait for the save to land: navigating away at once cancels the request.
  const saved = page.waitForResponse(
    (response) =>
      response.request().method() === "PUT" && new URL(response.url()).pathname.startsWith(LIMITS),
  );
  await input
    .locator("xpath=ancestor::div[contains(@class,'space-y-3')][1]")
    .getByRole("button", { name: /Lưu|Save/i })
    .click();
  expect((await saved).status()).toBe(200);
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

// Contract files follow the contract limit in Settings too. Sales cannot read
// Settings, yet the picker must refuse a file the server would reject, instead
// of a fixed 20 MB.
test("contract documents and appendices follow the contract limit set in Settings", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const settingsPath = `${LIMITS}/contracts`;
  const original = (await (await api.get(settingsPath, { headers: adminHeaders })).json())
    .maxFileSizeMb as number;
  const contracts = await (await api.get("/api/contracts?pageSize=1", { headers })).json();
  expect(contracts.items.length, "a seeded contract is needed").toBeGreaterThan(0);
  const contractId = contracts.items[0].id as number;

  try {
    await loginInBrowserAs(page, TEST_USERS.admin);
    await setLimitInSettings(page, "contracts", "contract-upload-limit", "1");

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    let uploads = 0;
    page.on("request", (request) => {
      if (request.method() === "POST" && /\/api\/contracts\/\d+\//.test(request.url())) uploads++;
    });
    await page.goto(`/admin/contracts/${contractId}?tab=documents`, { waitUntil: "networkidle" });
    const input = page.getByTestId("contract-attachment-files");
    const dropZone = input.locator("xpath=..");
    await expect(dropZone).toContainText("Tối đa 20 tệp, 1 MB mỗi tệp");
    await input.setInputFiles(TWO_MB_PDF("hop-dong-ky.pdf"));
    await expect(page.getByText("hop-dong-ky.pdf vượt quá 1 MB.").first()).toBeVisible();
    // A file within the limit is still accepted into the list.
    await input.setInputFiles({
      name: "bien-ban.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.alloc(200 * 1024),
    });
    await expect(page.getByText("bien-ban.pdf").first()).toBeVisible();

    // An appendix (VO) file follows the same limit.
    await page.goto(`/admin/contracts/${contractId}?tab=appendices`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Tạo VO mới" }).click();
    const appendixInput = page.getByRole("dialog").getByTestId("contract-appendix-files");
    await expect(appendixInput.locator("xpath=..")).toContainText("Tối đa 20 tệp, 1 MB mỗi tệp");
    await appendixInput.setInputFiles(TWO_MB_PDF("phu-luc-vo.pdf"));
    await expect(page.getByText("phu-luc-vo.pdf vượt quá 1 MB.").first()).toBeVisible();
    expect(uploads).toBe(0);
  } finally {
    await api.put(settingsPath, { headers: adminHeaders, data: { maxFileSizeMb: original } });
  }
});

// Quote documents follow the quote limit in Settings instead of a fixed 20 MB.
test("quote documents follow the quote limit set in Settings", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const settingsPath = `${LIMITS}/quotes`;
  const original = (await (await api.get(settingsPath, { headers: adminHeaders })).json())
    .maxFileSizeMb as number;
  const quotes = await (await api.get("/api/quotes?pageSize=1", { headers })).json();
  expect(quotes.items.length, "a seeded quote is needed").toBeGreaterThan(0);
  const quoteId = quotes.items[0].id as number;

  try {
    await loginInBrowserAs(page, TEST_USERS.admin);
    await setLimitInSettings(page, "quote-files", "quote-upload-limit", "1");

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    let uploads = 0;
    page.on("request", (request) => {
      if (request.method() === "POST" && /\/api\/quotes\/\d+\/documents$/.test(request.url()))
        uploads++;
    });
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu/ }).click();
    const input = page.getByTestId("quote-document-file");
    await expect(input.locator("xpath=..")).toContainText("Tối đa 20 tệp, 1 MB mỗi tệp");
    await input.setInputFiles(TWO_MB_PDF("bao-gia-gui-khach.pdf"));
    await expect(page.getByText("bao-gia-gui-khach.pdf vượt quá 1 MB.").first()).toBeVisible();
    await input.setInputFiles({
      name: "bang-ky-thuat.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.alloc(200 * 1024),
    });
    await expect(page.getByText("bang-ky-thuat.pdf").first()).toBeVisible();
    expect(uploads).toBe(0);
  } finally {
    await api.put(settingsPath, { headers: adminHeaders, data: { maxFileSizeMb: original } });
  }
});

// RFQ evidence follows the RFQ limit in Settings instead of a fixed 20 MB.
test("RFQ evidence follows the RFQ limit set in Settings", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const settingsPath = `${LIMITS}/rfqs`;
  const original = (await (await api.get(settingsPath, { headers: adminHeaders })).json())
    .maxFileSizeMb as number;
  const projects = await (
    await api.get("/api/operational-projects?pageSize=100", { headers: adminHeaders })
  ).json();
  const project = projects.items.find((item: { code: string }) => item.code === "PJ-SAMPLE-RFQ");
  expect(project, "seeded procurement project required").toBeTruthy();
  const base = `/api/operational-projects/${project.id}/procurement/rfqs`;
  const rfq = (
    await (await api.get(`${base}?status=Draft&pageSize=100`, { headers: adminHeaders })).json()
  ).items[0];
  expect(rfq, "seeded Draft RFQ required").toBeTruthy();

  try {
    await loginInBrowserAs(page, TEST_USERS.admin);
    await setLimitInSettings(page, "rfq-files", "rfq-upload-limit", "1");

    let uploads = 0;
    page.on("request", (request) => {
      if (request.method() === "POST" && request.url().endsWith(`${base}/documents/upload`))
        uploads++;
    });
    await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}&rfqId=${rfq.id}`, {
      waitUntil: "networkidle",
    });
    const input = page.getByTestId("rfq-files");
    await expect(input.locator("xpath=..")).toContainText("Tối đa 20 tệp, 1 MB mỗi tệp");
    await input.setInputFiles(TWO_MB_PDF("bao-gia-ncc-thep.pdf"));
    await expect(page.getByText("bao-gia-ncc-thep.pdf vượt quá 1 MB.").first()).toBeVisible();
    expect(uploads).toBe(0);
  } finally {
    await api.put(settingsPath, { headers: adminHeaders, data: { maxFileSizeMb: original } });
  }
});

// Customer documents follow the customer limit in Settings instead of a fixed 20 MB.
test("customer documents follow the customer limit set in Settings", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const settingsPath = `${LIMITS}/customers`;
  const original = (await (await api.get(settingsPath, { headers: adminHeaders })).json())
    .maxFileSizeMb as number;
  const customers = await (await api.get("/api/customers?pageSize=1", { headers })).json();
  expect(customers.items.length, "a seeded customer is needed").toBeGreaterThan(0);
  const customerId = customers.items[0].id as number;

  try {
    await loginInBrowserAs(page, TEST_USERS.admin);
    await setLimitInSettings(page, "customer-files", "customer-upload-limit", "1");

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    let uploads = 0;
    page.on("request", (request) => {
      if (request.method() === "POST" && /\/api\/customers\/\d+\/documents$/.test(request.url()))
        uploads++;
    });
    await page.goto(`/admin/customers?open=${customerId}`, { waitUntil: "networkidle" });
    const dialog = page.getByRole("dialog");
    await dialog.getByRole("tab", { name: /Tài liệu/ }).click();
    const input = dialog.locator("#customer-document-file");
    await expect(input.locator("xpath=..")).toContainText("Tối đa 20 tệp, 1 MB mỗi tệp");
    await input.setInputFiles(TWO_MB_PDF("giay-phep-kinh-doanh.pdf"));
    await expect(page.getByText("giay-phep-kinh-doanh.pdf vượt quá 1 MB.").first()).toBeVisible();
    await input.setInputFiles({
      name: "bien-ban-gap.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.alloc(200 * 1024),
    });
    await expect(dialog.getByText("bien-ban-gap.pdf").first()).toBeVisible();
    expect(uploads).toBe(0);
  } finally {
    await api.put(settingsPath, { headers: adminHeaders, data: { maxFileSizeMb: original } });
  }
});
