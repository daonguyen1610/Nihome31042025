import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

// NICON feedback row 27: a project records its address, new build or renovation, business
// sector and area. The sector starts as the customer's; all four are required on the form.
test("a project is created with its site details", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const suffix = randomUUID().slice(0, 6);
  const customerName = `Công ty CP Bánh kẹo Hữu Nghị ${suffix}`;
  const cleanup: string[] = [];

  try {
    const customer = await api.post("/api/customers", {
      headers: admin,
      data: {
        type: "Company",
        name: customerName,
        sourceCode: "referral",
        businessSectorCode: "food-beverage",
        primaryContact: {
          fullName: "Chị Lan",
          phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
          isPrimary: true,
        },
      },
    });
    expect(customer.status(), await customer.text()).toBe(201);
    cleanup.push(`/api/customers/${(await customer.json()).id}`);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/operational-projects?create=1", { waitUntil: "networkidle" });
    const dialog = page.getByRole("dialog", { name: "Tạo dự án" });
    await dialog.getByLabel("Tên dự án").fill(`Nhà máy bánh kẹo Hữu Nghị ${suffix}`);
    await dialog.getByRole("combobox", { name: "Khách hàng" }).click();
    await page.keyboard.type(suffix);
    await page.getByRole("option", { name: new RegExp(customerName) }).click();

    // The sector comes from the customer; the other details are still required.
    await expect(dialog.getByRole("combobox", { name: "Ngành nghề kinh doanh" })).toContainText(
      "Thực phẩm & Đồ uống",
    );
    await dialog.getByRole("button", { name: /^Lưu/ }).click();
    await expect(dialog).toContainText("Nhập địa chỉ dự án.");

    await dialog.getByLabel(/Địa chỉ dự án/).fill("Lô C5, KCN Tân Uyên, Bình Dương");
    await dialog.getByRole("combobox", { name: /Loại hình/ }).click();
    await page.getByRole("option", { name: "Cải tạo" }).click();
    await dialog.getByLabel(/Diện tích/).fill("12500");
    const saved = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/api/operational-projects" &&
        response.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: /^Lưu/ }).click();
    const created = await saved;
    expect(created.status(), await created.text()).toBe(201);
    cleanup.unshift(`/api/operational-projects/${(await created.json()).id}`);

    const site = page.getByTestId("project-site");
    await expect(site).toContainText("Lô C5, KCN Tân Uyên, Bình Dương");
    await expect(site).toContainText("Cải tạo");
    await expect(site).toContainText("Thực phẩm & Đồ uống");
    await expect(site).toContainText("12.500 m²");
    await expect(site).not.toContainText("Còn thiếu");
  } finally {
    for (const path of cleanup)
      await hardDeleteBusinessRoot(api, admin, path).catch(() =>
        hardDeleteBusinessRoot(api, admin, path),
      );
  }
});
