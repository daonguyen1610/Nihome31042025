import { test, expect, TEST_USERS } from "../fixtures/auth";

const SETTINGS_PATH = "/api/site-settings/design-uploads";

/**
 * The design file limit is an administrator setting (AGENTS.md: no hard-coded
 * limits). A change in Settings must reach the upload buttons of the design
 * tabs, which refuse a wrong file before it is sent.
 */
test("a design file limit set in Settings stops oversized and wrong files before upload", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const leadHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.designLead)}` };
  const original = (await (await api.get(SETTINGS_PATH, { headers: adminHeaders })).json())
    .maxFileSizeMb as number;

  // A seeded dossier in basic design, where documents still take files.
  const projects = await api.get("/api/design-projects?pageSize=50", { headers: leadHeaders });
  expect(projects.ok(), await projects.text()).toBeTruthy();
  let designProjectId = 0;
  const inBasicDesign = (
    (await projects.json()).items as { id: number; currentStage: string }[]
  ).filter((project) => project.currentStage === "BasicDesign");
  for (const project of inBasicDesign) {
    const docs = await api.get(`/api/basic-design-docs?designProjectId=${project.id}`, {
      headers: leadHeaders,
    });
    if (docs.ok() && (await docs.json()).total > 0) {
      designProjectId = project.id;
      break;
    }
  }
  expect(designProjectId, "a seeded dossier with basic design documents is required").not.toBe(0);

  try {
    // The administrator lowers the limit in Settings.
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/settings?section=design-files", { waitUntil: "networkidle" });
    const limit = page.locator("#design-upload-limit");
    await limit.fill("1");
    await limit
      .locator("xpath=ancestor::div[contains(@class,'space-y-3')][1]")
      .getByRole("button", { name: /Lưu|Save/i })
      .click();
    await expect
      .poll(
        async () =>
          (await (await api.get(SETTINGS_PATH, { headers: adminHeaders })).json()).maxFileSizeMb,
      )
      .toBe(1);

    // The design lead's upload button follows it without a reload of the app code.
    await loginInBrowserAs(page, TEST_USERS.designLead);
    let uploads = 0;
    page.on("request", (request) => {
      if (request.method() === "POST" && request.url().includes("/upload")) uploads++;
    });
    await page.goto(`/admin/design-projects/${designProjectId}?tab=basic`, {
      waitUntil: "networkidle",
    });
    const fileInput = page.locator('input[type="file"][accept*=".pdf"]').first();
    await expect(page.getByRole("button", { name: /Tải|Upload/i }).first()).toHaveAttribute(
      "title",
      /1 MB/,
    );

    await fileInput.setInputFiles({
      name: "mat-bang-tang-1.pdf",
      mimeType: "application/pdf",
      buffer: Buffer.alloc(2 * 1024 * 1024),
    });
    await expect(
      page.getByText(/“mat-bang-tang-1\.pdf” nặng 2 MB, vượt giới hạn 1 MB/),
    ).toBeVisible();

    await fileInput.setInputFiles({
      name: "ban-ve-kien-truc.dwg",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("dwg"),
    });
    await expect(page.getByText(/“ban-ve-kien-truc\.dwg” chưa được nhận/)).toBeVisible();
    expect(uploads, "refused files must not be sent").toBe(0);
  } finally {
    const restored = await api.put(SETTINGS_PATH, {
      headers: adminHeaders,
      data: { maxFileSizeMb: original },
    });
    expect(restored.ok(), await restored.text()).toBeTruthy();
  }
});
