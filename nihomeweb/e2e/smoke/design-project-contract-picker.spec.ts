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

// NICON feedback row 33: choosing the contract fills the dossier's start and due dates
// from it, while dates already typed stay as they are.
test("choosing a contract fills the empty dates from it", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const { items } = (await (await api.get("/api/contracts?pageSize=200", { headers })).json()) as {
    items: {
      contractNumber: string;
      direction: string;
      designProjectId?: number | null;
      startDate?: string | null;
      endDate?: string | null;
    }[];
  };
  const contract = items.find(
    (item) =>
      item.direction === "Upstream" && !item.designProjectId && item.startDate && item.endDate,
  );
  expect(contract, "a customer contract with dates is needed").toBeTruthy();

  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin/design-projects", { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "Tạo hồ sơ thiết kế" }).first().click();
  const dialog = page.getByRole("dialog", { name: "Tạo hồ sơ thiết kế" });
  const dates = dialog.locator('input[type="date"]');
  // The due date is typed first: it must be kept.
  await dates.nth(1).fill("2030-12-31");
  await dialog
    .locator("label", { hasText: "Hợp đồng" })
    .locator("xpath=..")
    .getByRole("combobox")
    .click();
  await page
    .getByPlaceholder("Tìm theo số hợp đồng, khách hàng hoặc dự án…")
    .fill(contract!.contractNumber);
  await page
    .getByRole("option", { name: new RegExp(contract!.contractNumber) })
    .first()
    .click();

  await expect(dates.nth(0)).toHaveValue(contract!.startDate!.slice(0, 10));
  await expect(dates.nth(1)).toHaveValue("2030-12-31");
  await expect(dialog).toContainText(`lấy từ hợp đồng ${contract!.contractNumber}`);
});
