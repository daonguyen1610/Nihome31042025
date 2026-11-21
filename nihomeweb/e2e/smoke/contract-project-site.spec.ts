import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

// NICON feedback row 25: choosing the customer and the project fills the contract with the
// project's address, type of works, sector and area, and the permit contract types read as
// NICON names them ("Xin phép").
test("a new contract shows its project's site details", async ({
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
    const customerId = (await customer.json()).id as number;
    cleanup.push(`/api/customers/${customerId}`);
    const project = await api.post("/api/operational-projects", {
      headers: admin,
      data: {
        name: `Nhà máy bánh kẹo ${suffix}`,
        customerId,
        siteAddress: "Lô C5, KCN Tân Uyên, Bình Dương",
        constructionKind: "Renovation",
        siteAreaM2: 12500,
      },
    });
    expect(project.status(), await project.text()).toBe(201);
    const { id: projectId, code: projectCode } = (await project.json()) as {
      id: number;
      code: string;
    };
    cleanup.unshift(`/api/operational-projects/${projectId}`);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/contracts", { waitUntil: "networkidle" });
    await page.locator("header").getByRole("button", { name: "Thêm hợp đồng" }).click();
    await page.locator("#c-customer-form").click();
    await page.keyboard.type(suffix);
    await page.getByRole("option", { name: new RegExp(customerName) }).click();
    await page.locator("#c-project-form").click();
    await page.getByRole("option", { name: new RegExp(projectCode) }).click();

    const site = page.getByTestId("contract-project-site");
    await expect(site).toContainText("Lô C5, KCN Tân Uyên, Bình Dương");
    await expect(site).toContainText("Cải tạo");
    await expect(site).toContainText("Thực phẩm & Đồ uống");
    await expect(site).toContainText("12.500 m²");

    await page.locator("#c-type-form").click();
    await expect(
      page.getByRole("option", { name: "Thiết kế - Xin phép - Thi công" }),
    ).toBeVisible();
    await expect(page.getByRole("option", { name: "Xin phép", exact: true })).toBeVisible();
  } finally {
    for (const resource of cleanup)
      await hardDeleteBusinessRoot(api, admin, resource).catch(() =>
        hardDeleteBusinessRoot(api, admin, resource),
      );
  }
});
