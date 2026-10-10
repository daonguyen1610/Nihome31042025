import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("new quote picker searches all eligible opportunities and shows identifying details", async ({ api, loginAs, loginInBrowserAs, page }) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const suffix = Math.random().toString(36).slice(2, 10);
  const marker = `Picker ${suffix}`;
  let customerId = 0;
  let opportunityId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Khách hàng ${marker}`,
        sourceCode: "marketing",
        primaryContact: { fullName: "Nguyễn Minh An", phone: `0913${Math.floor(100000 + Math.random() * 899999)}`, isPrimary: true },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = (await customerResponse.json()).id as number;
    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: { name: `Cải tạo văn phòng ${marker}`, customerId, estimatedValue: 2_500_000_000, winProbability: 45 },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = (await opportunityResponse.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/admin/quotes", { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Báo giá mới", exact: true }).click();

    const dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox").first().click();
    const searchRequest = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return url.pathname.endsWith("/api/opportunities") &&
        url.searchParams.get("quoteEligibleOnly") === "true" &&
        url.searchParams.get("search") === marker;
    });
    await page.getByPlaceholder("Tìm theo #ID, tên cơ hội, khách hàng, người phụ trách...").fill(marker);
    expect((await searchRequest).status()).toBe(200);
    const option = page.getByRole("option").filter({ hasText: marker });
    await expect(option).toContainText(`#${opportunityId} · Cải tạo văn phòng ${marker}`);
    await expect(option).toContainText("Khách hàng");
    await expect(option).toContainText("2.500.000.000 ₫");
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  } finally {
    if (opportunityId) await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
