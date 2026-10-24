import { expect, test, TEST_USERS } from "../fixtures/auth";

const contractTab = "Mẫu email gia hạn và kết thúc hợp đồng";
const approvalTab = "Duyệt nhóm công việc";

test("email editors share responsive previews and preserve drafts", async ({
  page,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => {
    if (window === window.top) localStorage.setItem("nicon_lang", "vi");
  });
  await page.goto("/admin/email-templates");

  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    for (const editor of [
      { tab: "Thông báo ứng tuyển", id: "email", token: "{{candidateName}}", desc: "Tên ứng viên" },
      { tab: "OTP xác thực", id: "email", token: "{{otpCode}}", desc: "Mã OTP" },
      { tab: "Báo giá", id: "email", token: "{{quoteCode}}", desc: "Mã báo giá" },
      { tab: contractTab, id: "renewal", token: "{{contractNumber}}", desc: "Số hợp đồng" },
      { tab: contractTab, id: "end", token: "{{endDate}}", desc: "Ngày kết thúc (dd/MM/yyyy)" },
      {
        tab: approvalTab,
        id: "approval-email",
        token: "{{workPackageCode}}",
        desc: "Mã nhóm công việc",
      },
    ]) {
      await page.getByRole("button", { name: editor.tab, exact: true }).click();
      if (editor.tab === contractTab) {
        await page
          .getByRole("button", {
            name: editor.id === "renewal" ? "Nhắc gia hạn" : "Thông báo kết thúc",
            exact: true,
          })
          .click();
      }
      const subject = page.locator(`#${editor.id}-subject`);
      const body = page.locator(`#${editor.id}-body`);
      await expect(body).toBeVisible();
      const originalSubject = await subject.inputValue();
      const originalBody = await body.inputValue();
      const iframe = page.locator("iframe").filter({ visible: true });
      await expect(iframe.contentFrame().locator("body")).not.toBeEmpty();
      const originalPreviewText = await iframe.contentFrame().locator("body").innerText();
      if (editor.tab === contractTab || editor.tab === approvalTab) {
        const table = iframe.contentFrame().locator('table[role="presentation"]');
        await expect(table).toHaveCSS("max-width", "600px");
        await expect(table.locator("tr > td").first()).toHaveCSS(
          "background-color",
          "rgb(229, 57, 74)",
        );
        await expect(table.locator("tr > td").first()).toContainText("NICON");
        expect(
          await iframe
            .contentFrame()
            .locator("body")
            .evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
        ).toBe(true);
      }
      await expect(
        page.locator("header").getByRole("button", { name: "Lưu thay đổi", exact: true }),
      ).toBeVisible();
      await expect(
        page.locator("header").getByRole("button", { name: "Khôi phục", exact: true }),
      ).toBeVisible();

      await subject.fill("NICON draft ");
      await page.getByTitle(editor.desc, { exact: true }).filter({ visible: true }).click();
      await expect(subject).toHaveValue(`NICON draft ${editor.token}`);
      await body.fill("<p>NICON preview </p>");
      await body.press("Home");
      await page.getByTitle(editor.desc, { exact: true }).filter({ visible: true }).click();
      await expect(body).toHaveValue(`${editor.token}<p>NICON preview </p>`);
      await expect(iframe).toHaveAttribute("sandbox", "");
      await expect(iframe.contentFrame().locator("body")).toContainText(`NICON preview`);
      await expect(iframe.contentFrame().locator("body")).toContainText(editor.token);
      await page.getByRole("button", { name: "Toàn màn hình", exact: true }).click();
      await expect(page.getByRole("button", { name: "Thu nhỏ", exact: true })).toHaveAttribute(
        "aria-expanded",
        "true",
      );
      await page.keyboard.press("Escape");
      await expect(page.getByRole("button", { name: "Toàn màn hình", exact: true })).toBeVisible();

      await page
        .getByRole("button", {
          name: editor.tab === approvalTab ? contractTab : approvalTab,
          exact: true,
        })
        .click();
      await page.getByRole("button", { name: editor.tab, exact: true }).click();
      await expect(subject).toHaveValue(`NICON draft ${editor.token}`);
      await subject.fill(originalSubject);
      await body.fill(originalBody);
      await expect(iframe.contentFrame().locator("body")).toHaveText(originalPreviewText, {
        useInnerText: true,
      });
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        width,
      );
      if (editor.tab === contractTab || editor.tab === approvalTab) {
        await iframe.scrollIntoViewIfNeeded();
        await iframe.screenshot({ path: testInfo.outputPath(`${editor.id}-${width}-preview.png`) });
        await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
        await page.screenshot({
          path: testInfo.outputPath(`${editor.id}-${width}.png`),
          fullPage: true,
        });
      }
    }
  }
  expect(errors).toEqual([]);
});

test("header actions save the selected template and restore saved content", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const contractPath = "/api/site-settings/contract-reminder-templates";
  const approvalPath = "/api/site-settings/work-package-approval-template";
  const contractResponse = await api.get(contractPath, { headers });
  const approvalResponse = await api.get(approvalPath, { headers });
  expect(contractResponse.ok()).toBe(true);
  expect(approvalResponse.ok()).toBe(true);
  const contract = await contractResponse.json();
  const approval = await approvalResponse.json();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => {
    if (window === window.top) localStorage.setItem("nicon_lang", "vi");
  });
  await page.goto("/admin/email-templates");
  try {
    for (const editor of [
      {
        tab: contractTab,
        id: "renewal",
        path: contractPath,
        key: "renewalSubject",
        subject: "NICON renewal {{contractNumber}}",
      },
      {
        tab: contractTab,
        id: "end",
        path: contractPath,
        key: "endSubject",
        subject: "NICON end {{contractNumber}}",
      },
      {
        tab: approvalTab,
        id: "approval-email",
        path: approvalPath,
        key: "subject",
        subject: "NICON approval {{workPackageCode}}",
      },
    ]) {
      await page.getByRole("button", { name: editor.tab, exact: true }).click();
      if (editor.tab === contractTab)
        await page
          .getByRole("button", {
            name: editor.id === "renewal" ? "Nhắc gia hạn" : "Thông báo kết thúc",
            exact: true,
          })
          .click();
      const subject = page.locator(`#${editor.id}-subject`);
      await subject.fill(editor.subject);
      const saved = page.waitForResponse(
        (response) => response.url().endsWith(editor.path) && response.request().method() === "PUT",
      );
      await page.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
      expect((await saved).ok()).toBe(true);
      await expect(page.getByRole("status").filter({ hasText: "Đã lưu" })).toBeVisible();
      expect((await (await api.get(editor.path, { headers })).json())[editor.key]).toBe(
        editor.subject,
      );
      await subject.fill("Unsaved draft");
      await page.getByRole("button", { name: "Khôi phục", exact: true }).click();
      await expect(subject).toHaveValue(editor.subject);
      const body = page.locator(`#${editor.id}-body`);
      const originalBody = await body.inputValue();
      await body.fill("<p>Missing required variables</p>");
      await page.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
      await expect(page.getByRole("alert")).toBeVisible();
      await body.fill(originalBody);
      await page.getByRole("button", { name: "Khôi phục", exact: true }).click();
      await expect(body).toHaveValue(originalBody);
    }
  } finally {
    expect((await api.put(contractPath, { headers, data: contract })).ok()).toBe(true);
    expect((await api.put(approvalPath, { headers, data: approval })).ok()).toBe(true);
  }
});
