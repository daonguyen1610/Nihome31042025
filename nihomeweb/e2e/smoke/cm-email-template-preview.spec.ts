import { expect, test, TEST_USERS } from "../fixtures/auth";
import { openEmailTemplate } from "../fixtures/emailTemplates";

// The CM plan email preview re-requested itself after every render (an unstable
// permission check in its effect) and never showed.
test("the CM plan approval email shows its preview, requested once", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  let previews = 0;
  page.on("request", (request) => {
    if (request.url().includes("/cm-schedule/email-template/preview")) previews++;
  });
  await page.goto("/admin/email-templates");
  await openEmailTemplate(page, "Email duyệt kế hoạch CM");

  const preview = page.frameLocator('iframe[title="Email preview"]:visible');
  await expect(preview.locator("body")).toContainText("KẾ HOẠCH CM");
  await page.waitForTimeout(1500);
  expect(previews, "the preview is not re-requested in a loop").toBe(1);
});
