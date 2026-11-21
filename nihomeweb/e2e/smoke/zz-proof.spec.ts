// TEMPORARY: evidence screenshots (not committed).
import { expect, test, TEST_USERS } from "../fixtures/auth";
const OUT = "/Users/datran/workspace/Nihome31042025/.playwright-mcp/proof";
for (const w of [1440, 390]) {
  test(`menu ${w}`, async ({ page, loginInBrowserAs }) => {
    await page.setViewportSize({ width: w, height: 900 });
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/customers", { waitUntil: "networkidle" });
    if (w < 768) await page.getByRole("button", { name: /menu|Mở menu/i }).first().click().catch(() => {});
    await page.waitForTimeout(800);
    const crm = page.getByRole("button", { name: "CRM" }).first();
    if ((await crm.getAttribute("aria-expanded")) !== "true") await crm.click();
    await page.waitForTimeout(500);
    const labels = await page.locator("nav a").allInnerTexts();
    console.log(w, JSON.stringify(labels.map((l) => l.trim()).filter(Boolean).slice(0, 16)));
    await page.screenshot({ path: `${OUT}/R31-menu-${w}.png` });
  });
}
