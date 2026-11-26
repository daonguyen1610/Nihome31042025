import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";

// Everything after login happens on real screens: a salesperson records a
// walk-in customer, logs a call and schedules the call-back reminder.
test("sales creates a customer and schedules a call-back from the care history", async ({
  page,
  loginInBrowserAs,
}) => {
  const name = `Anh Quang nhà phố ${randomUUID().slice(0, 8)}`;
  const phone = `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.sale);

  await page.goto("/admin/customers");
  await page.getByRole("button", { name: /New customer|Add customer|Create customer/i }).click();
  const create = page.getByRole("dialog", { name: "New customer" });
  await create.getByRole("combobox", { name: /Source/ }).click();
  await page.getByRole("option").first().click();
  await create.getByRole("textbox", { name: /Customer name/ }).fill(name);
  await create.getByRole("textbox", { name: "Phone" }).fill(phone);
  await create.getByRole("button", { name: "Save", exact: true }).click();
  await expect(create).not.toBeVisible();

  const content = page.locator("main");
  await content
    .getByRole("searchbox")
    .or(content.getByPlaceholder(/Search/i))
    .first()
    .fill(name);
  await page.getByRole("cell", { name, exact: true }).click();
  const detail = page.getByRole("dialog");
  await expect(detail.getByRole("heading", { name: new RegExp(name) })).toBeVisible();

  // A follow-up date before today is not offered; today is the earliest choice.
  const followUp = detail.getByLabel(/Follow-up date/);
  const today = await followUp.getAttribute("min");
  expect(today).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  await detail
    .getByLabel(/Activity content/)
    .fill("Called about a 5x20m townhouse; call back with the design quote");
  await followUp.fill(today!);
  await detail.getByRole("button", { name: /Add/ }).last().click();

  await expect(
    detail.getByText("Called about a 5x20m townhouse; call back with the design quote"),
  ).toBeVisible();
  await expect(detail.getByText(/^Follow up on /)).toBeVisible();
  await expect(followUp).toHaveValue("");
  expect(errors).toEqual([]);
});
