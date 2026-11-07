import { expect, test, TEST_USERS } from "../fixtures/auth";

const limits = {
  vi: "Tối đa 20 tệp, 20 MB mỗi tệp",
  en: "Up to 20 files, 20 MB each",
  zh: "最多 20 个文件，每个 20 MB",
  ja: "最大 20 ファイル、各 20 MB",
};

for (const [language, limitHint] of Object.entries(limits)) {
  test(`quote picker retains file limits and named controls in ${language}`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const response = await api.get("/api/quotes?pageSize=1", { headers });
    expect(response.ok()).toBeTruthy();
    const quotes = await response.json();
    expect(quotes.items.length, "seeded quote required for upload UI").toBeGreaterThan(0);
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript((lang) => localStorage.setItem("nicon_lang", lang), language);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/admin/quotes/${quotes.items[0].id}`);
    await page.locator('[role="tab"][aria-controls$="documents"]').click();
    const input = page.getByTestId("quote-document-file");
    const dropzone = page.locator("label").filter({ has: input });
    await expect(dropzone).toContainText(limitHint);
    await expect(input).toHaveAccessibleName(/.+/);
    await input.setInputFiles([
      { name: "Nicon-site-survey.pdf", mimeType: "application/pdf", buffer: Buffer.from("survey") },
      { name: "Nicon-drawing.pdf", mimeType: "application/pdf", buffer: Buffer.from("drawing") },
    ]);
    const picker = dropzone.locator("..");
    const remove = picker.getByRole("button", { name: /Nicon-site-survey\.pdf/ });
    await expect(remove).toBeVisible();
    await remove.click();
    await expect(picker.getByText("Nicon-site-survey.pdf", { exact: true })).toHaveCount(0);
    await expect(picker.getByText("Nicon-drawing.pdf", { exact: true })).toBeVisible();
    await page.setViewportSize({ width: 768, height: 1024 });
    await expect(dropzone).toContainText(limitHint);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
      true,
    );
  });
}

test("project upload is available only after selecting files", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/operational-projects?pageSize=1", { headers });
  expect(response.ok()).toBeTruthy();
  const projects = await response.json();
  expect(projects.items.length, "seeded operational project required").toBeGreaterThan(0);
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`/admin/operational-projects/${projects.items[0].id}`);
  const trigger = page.getByTestId("project-documents-trigger");
  if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click();
  const upload = page.getByTestId("project-documents-upload");
  const button = upload.getByRole("button", { name: /Tải lên|Upload|上传|アップロード/ });
  await expect(button).toBeDisabled();
  await page.getByTestId("project-document-file").setInputFiles({
    name: "Nicon-site-survey.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("survey"),
  });
  await expect(button).toBeEnabled();
  await upload.getByRole("button", { name: /Nicon-site-survey\.pdf/ }).click();
  await expect(button).toBeDisabled();
});

test("RFQ upload retains rejected files and blocks duplicate drops while busy", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const projectsResponse = await api.get("/api/operational-projects?pageSize=100", { headers });
  expect(projectsResponse.ok()).toBeTruthy();
  const project = (await projectsResponse.json()).items.find(
    (item: { code: string }) => item.code === "PJ-SAMPLE-RFQ",
  );
  expect(project, "seeded procurement project required").toBeTruthy();
  const base = `/api/operational-projects/${project.id}/procurement/rfqs`;
  const rfqsResponse = await api.get(`${base}?status=Draft&pageSize=100`, { headers });
  expect(rfqsResponse.ok()).toBeTruthy();
  const rfq = (await rfqsResponse.json()).items[0];
  expect(rfq, "seeded Draft RFQ required").toBeTruthy();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}&rfqId=${rfq.id}`);
  const input = page.getByTestId("rfq-files");
  await expect(input).toBeAttached();
  let releaseUpload = () => {};
  const forwardUpload = new Promise<void>((resolve) => {
    releaseUpload = resolve;
  });
  let uploadRequests = 0;
  // Delay only the outgoing request; validation still uses the live server response.
  await page.route(`**${base}/documents/upload`, async (route) => {
    uploadRequests++;
    if (uploadRequests === 1) await forwardUpload;
    await route.continue();
  });
  const fileName = "Nicon-invalid-upload.exe";
  const secondFileName = "Nicon-second-invalid-upload.exe";
  const rejectedResponse = page.waitForResponse((response) =>
    response.url().endsWith(`${base}/documents/upload`),
  );
  try {
    await input.setInputFiles(
      [fileName, secondFileName].map((name) => ({
        name,
        mimeType: "application/octet-stream",
        buffer: Buffer.from("invalid attachment"),
      })),
    );
    await expect(input).toBeDisabled();
    await expect.poll(() => uploadRequests).toBe(1);
    await input.locator("..").evaluate((label) => {
      const dataTransfer = new DataTransfer();
      dataTransfer.items.add(new File(["duplicate"], "duplicate-upload.exe"));
      label.dispatchEvent(new DragEvent("drop", { bubbles: true, dataTransfer }));
    });
    expect(uploadRequests).toBe(1);
  } finally {
    releaseUpload();
  }
  expect((await rejectedResponse).status()).toBe(400);
  await expect(input).toBeEnabled();
  expect(uploadRequests).toBe(2);
  const picker = input.locator("../..");
  await expect(picker.getByText(fileName, { exact: true })).toBeVisible();
  const retriedResponse = page.waitForResponse((response) =>
    response.url().endsWith(`${base}/documents/upload`),
  );
  await picker.locator("..").getByRole("button", { name: "Retry", exact: true }).click();
  expect((await retriedResponse).status()).toBe(400);
  await expect(input).toBeEnabled();
  expect(uploadRequests).toBe(4);
  await expect(picker.getByText(fileName, { exact: true })).toBeVisible();
  await picker.getByRole("button", { name: new RegExp(fileName) }).click();
  await expect(picker.getByText(fileName, { exact: true })).toHaveCount(0);
  await expect(picker.getByText(secondFileName, { exact: true })).toBeVisible();
  await expect(input).toBeEnabled();
  expect(uploadRequests).toBe(4);
  await picker.getByRole("button", { name: `Remove file: ${secondFileName}`, exact: true }).click();
  await expect(picker.getByText(secondFileName, { exact: true })).toHaveCount(0);
  expect(uploadRequests).toBe(4);
});
