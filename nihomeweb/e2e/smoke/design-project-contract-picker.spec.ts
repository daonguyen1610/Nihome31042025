import { expect, test, TEST_USERS } from "../fixtures/auth";

// NICON feedback: the contract picker of a new design project cut long contract names
// off and listed supplier purchase orders, which never carry a design. The phone-width
// layout is presentation only and is verified manually (AGENTS.md).
test("the design project contract picker lists only customer contracts and names their customer", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  // Add a supplier purchase order to the real list: the picker must leave it out.
  await page.route(/\/api\/contracts\?/, async (route) => {
    const response = await route.fetch();
    const body = await response.json();
    const [first] = body.items as Record<string, unknown>[];
    if (first)
      body.items.push({
        ...first,
        id: 9_900_001,
        contractNumber: "PO-RFQ-9999-0001",
        direction: "Downstream",
        designProjectId: null,
      });
    await route.fulfill({ response, json: body });
  });

  await page.goto("/admin/design-projects", { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "Tạo hồ sơ thiết kế" }).first().click();
  const dialog = page.getByRole("dialog", { name: "Tạo hồ sơ thiết kế" });
  await dialog
    .locator("label", { hasText: "Hợp đồng" })
    .locator("xpath=..")
    .getByRole("combobox")
    .click();
  await expect(page.getByPlaceholder("Tìm theo số hợp đồng, khách hàng hoặc dự án…")).toBeVisible();

  const options = page.getByRole("option");
  await expect(options.filter({ hasText: "PO-RFQ-9999-0001" })).toHaveCount(0);
  const texts = await options.allInnerTexts();
  expect(texts.length, "a seeded customer contract is needed").toBeGreaterThan(1);
  // Each contract names its customer on a second line instead of cutting it off.
  expect(texts.some((text) => text.includes("\n"))).toBe(true);
});
