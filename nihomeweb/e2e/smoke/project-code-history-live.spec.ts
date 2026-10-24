import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("project code changes appear immediately in change history and survive reload", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(90_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON project code audit");
  let projectId: number | undefined;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  try {
    const originalCode = `NICON-HIST-${Date.now()}`;
    const created = await api.post("/api/operational-projects", {
      headers,
      data: { code: originalCode, name: "NICON trụ sở điều hành - lịch sử mã", customerId },
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    projectId = (await created.json()).id as number;
    await page.addInitScript(() => {
      if (!localStorage.getItem("nicon_lang")) localStorage.setItem("nicon_lang", "en");
    });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/operational-projects/${projectId}`);
    await expect(page.getByRole("heading", { name: "Change history", exact: true })).toBeVisible();
    const entries = page.locator('[data-testid^="project-history-ProjectCode-"]');
    await expect(entries).toHaveCount(0);
    let oldCode = originalCode;
    for (const suffix of ["R01", "R02"]) {
      const newCode = `${originalCode}-${suffix}`;
      const reason = `Chuẩn hóa mã hồ sơ NICON ${suffix}`;
      await page.getByRole("button", { name: "Change project code", exact: true }).click();
      await page.locator("#rename-project-code").fill(newCode);
      await page.locator("#rename-project-reason").fill(reason);
      const [response] = await Promise.all([
        page.waitForResponse(
          (item) =>
            item.url().endsWith(`/api/operational-projects/${projectId}/code`) &&
            item.request().method() === "PATCH",
        ),
        page
          .getByRole("dialog")
          .getByRole("button", { name: /^Save( changes)?$/ })
          .click(),
      ]);
      expect(response.ok()).toBeTruthy();
      await expect(entries).toHaveCount(suffix === "R01" ? 1 : 2);
      await expect(entries.first()).toContainText(`${oldCode} → ${newCode}`);
      await expect(entries.first()).toContainText(reason);
      await expect(entries.first().locator("strong")).not.toBeEmpty();
      await expect(entries.first()).toContainText("Code changed");
      oldCode = newCode;
    }
    await page.reload();
    await expect(entries).toHaveCount(2);
    await expect(entries.first()).toContainText(`${originalCode}-R01 → ${originalCode}-R02`);
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(entries.first()).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        width,
      );
    }
    for (const [lang, label] of [
      ["vi", "Đã đổi mã"],
      ["zh", "编号已更改"],
      ["ja", "コード変更"],
    ]) {
      await page.evaluate((value) => localStorage.setItem("nicon_lang", value), lang);
      await page.reload();
      await expect(entries).toHaveCount(2);
      await expect(entries.first()).toContainText(label);
    }
    expect(errors).toEqual([]);
  } finally {
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
