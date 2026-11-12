import { expect, test, TEST_USERS } from "../fixtures/auth";

test("customer create controls use their visible labels", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.goto("/admin/customers");
  await page.getByRole("button", { name: /New customer|Add customer|Create customer/i }).click();
  const dialog = page.getByRole("dialog");
  const name = dialog.getByRole("textbox", { name: /Customer name|Name/i }).first();
  await expect(name).toBeVisible();
  const id = await name.getAttribute("id");
  expect(id).toBeTruthy();
  await dialog.locator(`label[for="${id}"]`).click();
  await expect(name).toBeFocused();
});

test("construction project filter uses its visible field label", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.goto("/admin/construction/tasks");
  const project = page.getByRole("combobox", { name: /^Operational project$/i }).first();
  await expect(project).toBeVisible();
  const id = await project.getAttribute("id");
  expect(id).toBeTruthy();
  await page.locator(`label[for="${id}"]`).click();
  await expect(project).toHaveAttribute("aria-expanded", "true");
  await page.keyboard.press("Escape");
  await expect(project).toBeFocused();
});

for (const route of [
  "/admin/customers",
  "/admin/leads",
  "/admin/opportunities",
  "/admin/contracts",
  "/admin/quotes",
  "/admin/design-projects",
  "/admin/construction/tasks",
  "/admin/construction/diary",
  "/admin/construction/punchlist",
  "/admin/permits",
  "/admin/work-packages",
]) {
  test(`visible field labels resolve on ${route}`, async ({ page, loginInBrowserAs }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(route);
    await expect(page.locator("main")).toBeVisible();
    await expect(page.locator("main h1").first()).toBeVisible();
    const invalid = await page.locator("main").evaluate((main) =>
      Array.from(main.querySelectorAll("label"))
        .filter((label) => {
          if (label.getClientRects().length === 0) return false;
          return !label.control && !label.querySelector("input, select, textarea, button");
        })
        .map((label) => label.textContent?.trim()),
    );
    expect(invalid, "every visible field label must resolve to its control").toEqual([]);
    const duplicateIds = await page.locator("main").evaluate((main) => {
      const ids = Array.from(
        main.querySelectorAll("input[id], textarea[id], button[role=combobox][id], select[id]"),
      )
        .map((control) => control.id)
        .filter(Boolean);
      return ids.filter((id, index) => ids.indexOf(id) !== index);
    });
    expect(duplicateIds).toEqual([]);
    const unnamed = await page.locator("main").evaluate((main) =>
      Array.from(
        main.querySelectorAll<
          HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement | HTMLButtonElement
        >("input, textarea, select, button[role=combobox]"),
      )
        .filter((control) => {
          if (
            !control.getClientRects().length ||
            (control instanceof HTMLInputElement &&
              ["hidden", "checkbox", "radio"].includes(control.type))
          )
            return false;
          return (
            !control.getAttribute("aria-label")?.trim() &&
            !control
              .getAttribute("aria-labelledby")
              ?.split(/\s+/)
              .some((id) => document.getElementById(id)?.textContent?.trim()) &&
            !control.getAttribute("title")?.trim() &&
            !Array.from(control.labels ?? []).some((label) => label.textContent?.trim())
          );
        })
        .map((control) => ({
          tag: control.tagName,
          testId: control.dataset.testid,
          id: control.id,
        })),
    );
    expect(unnamed, "visible text and selection controls need meaningful accessible names").toEqual(
      [],
    );
  });
}

for (const [language, title, application, otp] of [
  ["vi", "Mẫu email", "Thông báo ứng tuyển", "OTP xác thực"],
  ["en", "Email templates", "Application notifications", "OTP verification"],
  ["zh", "电子邮件模板", "应聘通知", "一次性密码验证"],
  ["ja", "メールテンプレート", "応募通知", "ワンタイムパスワード認証"],
]) {
  test(`email template navigation uses ${language} consistently`, async ({
    page,
    loginInBrowserAs,
  }) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript((lang) => localStorage.setItem("nicon_lang", lang), language);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/admin/email-templates");
    await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: application, exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: otp, exact: true })).toBeVisible();
  });
}
