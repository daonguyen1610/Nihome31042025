import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";

// Authentication is the only API setup. Lead creation, conversion, sales-stage
// handoffs and the terminal lost decision are all made through real screens.
test("sales follows a new lead through conversion, negotiation and a reasoned lost decision", async ({ page, loginInBrowserAs }, testInfo) => {
  const name = `Factory investor ${randomUUID().slice(0, 8)}`;
  const phone = `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`;
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.salesManager);
  await page.goto("/admin/leads");
  await page.getByRole("button", { name: "New lead", exact: true }).click();
  const create = page.getByRole("dialog", { name: "New lead", exact: true });
  // The existing form lacks label associations; scope its native inputs to
  // this dialog. Do not replace the business action with an API fixture.
  await create.locator("input").nth(0).fill(name);
  await create.locator("input").nth(2).fill(phone);
  await create.getByRole("combobox").nth(0).click();
  await page.getByRole("option", { name: /Marketing/i }).click();
  await create.getByRole("button", { name: "Save", exact: true }).click();
  await expect(create).not.toBeVisible();
  await page.getByRole("cell", { name, exact: true }).click();
  await page.getByRole("button", { name: "Convert to customer", exact: true }).click();
  const convert = page.getByRole("dialog", { name: "Convert Lead", exact: true });
  await convert.getByRole("button", { name: "Convert to customer", exact: true }).click();
  await expect(convert).not.toBeVisible();
  const opportunityLink = page.getByRole("link", { name: "View opportunity", exact: true });
  await expect(opportunityLink).toBeVisible();
  const opportunityUrl = await opportunityLink.getAttribute("href");
  await opportunityLink.click();
  const detail = page.getByRole("dialog");
  const stages = ["Survey", "Quotation / Tender", "Negotiation"] as const;
  for (const [index, stage] of stages.entries()) {
    const stageButton = detail.getByRole("button", { name: stage, exact: true });
    await stageButton.click();
    await expect(stageButton).toHaveCount(0);
    await expect(detail.getByText(stage, { exact: true })).toBeVisible();
    if (index < stages.length - 1)
      await expect(detail.getByRole("button", { name: stages[index + 1], exact: true })).toBeEnabled();
  }
  if (!await detail.isVisible()) {
    await page.goto(opportunityUrl!);
    await expect(detail).toBeVisible();
  }
  await detail.getByRole("button", { name: "Lost", exact: true }).click();
  const decision = page.getByRole("dialog", { name: "Move to Lost", exact: true });
  await expect(decision.getByRole("button", { name: "Save changes", exact: true })).toBeDisabled();
  await decision.getByRole("combobox").click();
  await page.getByRole("option", { name: /Price|Giá/i }).click();
  await decision.getByRole("textbox").fill("Investor selected another contractor after negotiation");
  await decision.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(decision).not.toBeVisible();
  await page.goto(opportunityUrl!);
  await expect(page.getByRole("dialog")).toContainText("Investor selected another contractor after negotiation");
  await expect(page.getByRole("dialog").getByRole("button", { name: "Negotiation", exact: true })).toHaveCount(0);
  await page.reload();
  await expect(page.getByRole("dialog")).toContainText("Investor selected another contractor after negotiation");
  await page.screenshot({ path: testInfo.outputPath("crm-lost-outcome.png"), fullPage: true });
  expect(errors).toEqual([]);
});
