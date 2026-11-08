import type { Locator } from "@playwright/test";
import { expect, test, TEST_USERS } from "../fixtures/auth";

async function expectFullyVisible(tab: Locator) {
  await expect(async () => {
    const geometry = await tab.evaluate((element) => {
      const viewport = element.closest('[role="tablist"]')!.parentElement!;
      const active = element.getBoundingClientRect();
      const visible = viewport.getBoundingClientRect();
      return { left: active.left, right: active.right, start: visible.left, end: visible.right };
    });
    expect(geometry.left).toBeGreaterThanOrEqual(geometry.start - 1);
    expect(geometry.right).toBeLessThanOrEqual(geometry.end + 1);
  }).toPass({ timeout: 5000 });
}

for (const width of [390, 768, 1440]) {
  test(`Team deep link is fully visible at ${width}px`, async ({
    page,
    loginInBrowserAs,
  }, testInfo) => {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.setViewportSize({ width, height: 844 });
    await page.goto("/admin/design-projects/2?tab=team");
    const tab = page.getByRole("tab", { name: "Đội ngũ", exact: true });
    await expect(tab).toHaveAttribute("data-state", "active");
    await expectFullyVisible(tab);
    await page.screenshot({ path: testInfo.outputPath(`design-team-${width}px.png`) });
  });
}

test("deep-linked Team tab stays visible after responsive resizing", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.setViewportSize({ width: 1440, height: 844 });
  await page.goto("/admin/design-projects/2?tab=team");
  const tab = page.getByRole("tab", { name: "Đội ngũ", exact: true });
  await expect(tab).toHaveAttribute("data-state", "active");

  for (const width of [390, 768, 1440, 390]) {
    await page.setViewportSize({ width, height: 844 });
    await expectFullyVisible(tab);
    await expect(tab).toHaveAttribute("data-state", "active");
  }
  await tab.focus();
  await tab.press("End");
  await expect(page.getByRole("tab", { name: "Tài liệu", exact: true })).toHaveAttribute(
    "data-state",
    "active",
  );
  await page.goBack();
  await expect(tab).toHaveAttribute("data-state", "active");
  await expectFullyVisible(tab);

  // Browsing earlier tabs must not snap back to the selected tab when the
  // previous arrow disappears at the beginning of the scrollable strip.
  const previous = page.getByTestId("scrollable-tabs-previous");
  for (let step = 0; step < 4 && (await previous.isVisible()); step += 1) {
    const viewport = tab.locator("xpath=../..");
    const start = await viewport.evaluate((element) => element.scrollLeft);
    await previous.click();
    await expect
      .poll(() => viewport.evaluate((element) => element.scrollLeft))
      .toBeLessThanOrEqual(Math.max(0, start - 240) + 1);
  }
  await expectFullyVisible(page.getByRole("tab", { name: "Tổng quan", exact: true }));
  await expect(tab).toHaveAttribute("data-state", "active");
});
