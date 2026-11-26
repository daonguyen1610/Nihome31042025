import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

// NICON feedback rows 21-22: a customer records its business sector from NICON's list and
// what it does, and the customer list filters by sector.
test("sales records a customer's business sector and filters the list by it", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const name = `Công ty CP Bánh kẹo Hữu Nghị ${randomUUID().slice(0, 8)}`;
  const phone = `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`;
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await loginInBrowserAs(page, TEST_USERS.sale);
  let customerId = 0;

  try {
    await page.goto("/admin/customers", { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Thêm Khách hàng" }).click();
    const create = page.getByRole("dialog", { name: "Thêm Khách hàng" });
    await create.getByRole("combobox", { name: /Nguồn/ }).click();
    await page.getByRole("option").first().click();
    await create.getByRole("textbox", { name: /Tên khách hàng/ }).fill(name);
    await create.getByTestId("customer-business-sector").click();
    await page.getByPlaceholder("Tìm lĩnh vực…").fill("thực phẩm");
    await page.getByRole("option", { name: "Thực phẩm & Đồ uống" }).click();
    await create.getByRole("textbox", { name: "Chi tiết ngành nghề" }).fill("Sản xuất bánh kẹo");
    await create.getByRole("textbox", { name: "Điện thoại" }).fill(phone);
    const saved = page.waitForResponse(
      (response) =>
        response.url().endsWith("/api/customers") && response.request().method() === "POST",
    );
    await create.getByRole("button", { name: "Lưu", exact: true }).click();
    customerId = (await (await saved).json()).id;
    await expect(create).not.toBeVisible();

    // The sector filter keeps the customer; another sector leaves it out.
    await page.getByRole("button", { name: /^Bộ lọc( ?\d+)?$/ }).click();
    const sector = page.getByRole("combobox", { name: "Lĩnh vực kinh doanh" });
    await sector.click();
    await page.getByRole("option", { name: "Thực phẩm & Đồ uống" }).click();
    await expect(page.getByRole("cell", { name, exact: true })).toBeVisible();
    await sector.click();
    await page.getByRole("option", { name: "Dệt may" }).click();
    await expect(page.getByRole("cell", { name, exact: true })).toHaveCount(0);
  } finally {
    if (customerId) {
      const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
      await hardDeleteBusinessRoot(api, admin, `/api/customers/${customerId}`);
    }
  }
});
