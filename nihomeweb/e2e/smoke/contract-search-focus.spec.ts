import { expect } from "@playwright/test";
import { test, TEST_USERS } from "../fixtures/auth";

/**
 * The page used to swap itself for a loading state whenever a fetch was in
 * flight and the list happened to be empty. Typing past the last match therefore
 * unmounted the very input being typed into, so the box refused both new
 * characters and backspace. Only the first load replaces the page now.
 */
test("the contract search box survives typing past the last match", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/contracts");

  const search = page.locator("#c-search");
  await expect(search).toBeVisible({ timeout: 15_000 });

  // Type a string that certainly matches nothing, so the list empties mid-way.
  await search.click();
  await search.pressSequentially("zzzzzz", { delay: 60 });

  await expect(search).toHaveValue("zzzzzz", { timeout: 15_000 });
  await expect(search).toBeFocused();

  // Keep typing once the list is already empty — this is where it used to break.
  await search.pressSequentially("qq", { delay: 60 });
  await expect(search).toHaveValue("zzzzzzqq", { timeout: 15_000 });
  await expect(search).toBeFocused();

  // And backspace back out again.
  for (let i = 0; i < 8; i++) await search.press("Backspace");
  await expect(search).toHaveValue("", { timeout: 15_000 });
  await expect(search).toBeFocused();

  // The filter panel never went away, so the rows come back.
  await expect(page.locator("#c-search")).toBeVisible();
});
