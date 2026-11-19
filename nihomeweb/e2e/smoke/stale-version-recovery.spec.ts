import { expect, test, TEST_USERS } from "../fixtures/auth";

// After a redeploy an open tab asks for bundle files that no longer exist; the
// page used to spin until the user reloaded by hand.
test("a page whose bundle file vanished after a deploy reloads itself to the new version", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin", { waitUntil: "networkidle" });

  // The email templates page's module is gone once, as after a deploy.
  let removed = 0;
  await page.route(/EmailTemplateConfig[^/]*\.(tsx|js)(\?|$)/, async (route) => {
    if (removed++ === 0) await route.fulfill({ status: 404, body: "Not found" });
    else await route.continue();
  });
  // Navigate inside the open app, as a user clicking the menu would.
  await page.evaluate(() => {
    window.history.pushState({}, "", "/admin/email-templates");
    window.dispatchEvent(new PopStateEvent("popstate"));
  });

  await expect(page.getByRole("heading", { name: "Mẫu email", level: 1 })).toBeVisible({
    timeout: 20_000,
  });
  expect(removed, "the missing module was requested again after reloading").toBeGreaterThan(1);
  // The reload came from the stale-bundle recovery, which records when it ran.
  expect(
    await page.evaluate(() => sessionStorage.getItem("nicon_new_version_reload_at")),
  ).not.toBeNull();
  await expect(page).toHaveURL(/\/admin\/email-templates$/);
});

test("a session check that fails offers a retry instead of spinning forever", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  let failed = 0;
  await page.route("**/api/users/me", async (route) => {
    // The first check fails on the network; the retry goes through.
    if (failed < 1) {
      failed++;
      await route.abort("connectionfailed");
    } else await route.continue();
  });

  await page.goto("/admin/email-templates");
  const retry = page.getByRole("button", { name: "Thử lại" });
  await expect(retry).toBeVisible({ timeout: 20_000 });
  await retry.click();
  await expect(page.getByRole("heading", { name: "Mẫu email", level: 1 })).toBeVisible();
});
