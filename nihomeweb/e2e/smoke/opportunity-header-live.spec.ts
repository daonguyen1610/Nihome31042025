import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("opportunity header labels its currency and stays readable on smaller screens", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON opportunity header");
  let opportunityId: number | undefined;
  const name = "Thiết kế và thi công nhà văn phòng cùng nhà ăn cho nhà máy NICON";
  try {
    const response = await api.post("/api/opportunities", {
      headers,
      data: { name, customerId, estimatedValue: 10_220_235_598, winProbability: 80 },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    opportunityId = (await response.json()).id;
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/opportunities/${opportunityId}`);
    const dialog = page.getByTestId("opportunity-detail-dialog");
    const heading = dialog.getByRole("heading", { level: 2 });
    await expect(dialog.getByText("10.220.235.598 ₫", { exact: true })).toBeVisible();
    await expect(heading).toHaveText(name);
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(heading).toBeVisible();
      await expect(dialog.getByText("10.220.235.598 ₫", { exact: true })).toBeVisible();
      await expect
        .poll(() => dialog.evaluate((element) => element.scrollWidth <= element.clientWidth))
        .toBe(true);
      await expect
        .poll(async () => {
          const bounds = await dialog.boundingBox();
          return !!bounds && bounds.x >= 0 && bounds.x + bounds.width <= width;
        })
        .toBe(true);
      await dialog.screenshot({ path: testInfo.outputPath(`opportunity-header-${width}.png`) });
    }
  } finally {
    if (opportunityId)
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
