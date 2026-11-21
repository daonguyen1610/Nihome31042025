import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

// NICON feedback row 35: a project entered with the wrong customer is moved to the right one
// from its own page, and the page shows the new customer straight away.
test("a project entered with the wrong customer is moved to the right one", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const suffix = randomUUID().slice(0, 6);
  const cleanup: string[] = [];
  const createCustomer = async (name: string) => {
    const response = await api.post("/api/customers", {
      headers: admin,
      data: {
        type: "Company",
        name,
        sourceCode: "referral",
        primaryContact: {
          fullName: "Chị Lan",
          phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
          isPrimary: true,
        },
      },
    });
    expect(response.status(), await response.text()).toBe(201);
    const id = (await response.json()).id as number;
    cleanup.push(`/api/customers/${id}`);
    return id;
  };

  try {
    const wrong = await createCustomer(`Công ty Thực phẩm Đức Hòa nhập nhầm ${suffix}`);
    const rightName = `Công ty TNHH Thực phẩm Đức Hoà ${suffix}`;
    await createCustomer(rightName);
    const created = await api.post("/api/operational-projects", {
      headers: admin,
      data: {
        name: `Kho lạnh Đức Hoà ${suffix}`,
        customerId: wrong,
        siteAddress: "Đức Hoà, Long An",
        constructionKind: "NewBuild",
        businessSectorCode: "food-beverage",
        siteAreaM2: 3200,
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    const projectId = (await created.json()).id as number;
    cleanup.unshift(`/api/operational-projects/${projectId}`);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto(`/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Sửa", exact: true }).first().click();
    const edit = page.getByRole("dialog", { name: "Chỉnh sửa dự án" });
    await edit.getByRole("combobox", { name: "Khách hàng" }).click();
    await page.keyboard.type(suffix);
    await page.getByRole("option", { name: rightName }).click();
    await edit.getByRole("button", { name: "Lưu thay đổi" }).click();
    await expect(edit).toBeHidden();

    // No reload: the page already names the right customer.
    await expect(page.getByText(rightName, { exact: true })).toBeVisible();
    await expect(page.getByText(/nhập nhầm/)).toHaveCount(0);
  } finally {
    // A background job may touch the project right after the save; retry once.
    for (const path of cleanup)
      await hardDeleteBusinessRoot(api, admin, path).catch(() =>
        hardDeleteBusinessRoot(api, admin, path),
      );
  }
});
