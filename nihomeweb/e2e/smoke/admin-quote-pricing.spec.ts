import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("creates a basic draft without choosing an output method", async ({
  api,
  loginAs,
  loginInBrowserAs,
  page,
}) => {
  test.setTimeout(120_000);
  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}`, "Idempotency-Key": randomUUID() };
  const suffix = randomUUID().slice(0, 8);
  const leadResponse = await api.post("/api/leads", {
    headers,
    data: {
      name: `Bà Nguyễn Thị Hương ${suffix}`,
      phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
      companyName: `Công ty Xây dựng NICON ${suffix}`,
      sourceCode: "marketing",
    },
  });
  expect(leadResponse.status(), await leadResponse.text()).toBe(201);
  const leadId = (await leadResponse.json()).id as number;
  let customerId = 0;
  let quoteId = 0;

  try {
    const convertedResponse = await api.post(`/api/leads/${leadId}/convert`, {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      data: { address: "Quận 7, TP.HCM", representativeName: "Nguyễn Thị Hương" },
    });
    expect(convertedResponse.status(), await convertedResponse.text()).toBe(200);
    const converted = await convertedResponse.json();
    customerId = converted.convertedCustomerId as number;
    const opportunityId = converted.convertedOpportunityId as number;

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.setViewportSize({ width: 320, height: 568 });
    await page.goto(`/admin/quotes?create=1&opportunityId=${opportunityId}`);
    const dialog = page.getByTestId("quote-create-dialog");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByTestId("quote-method")).toHaveCount(0);
    await expect(dialog.getByTestId("quote-create-boq-bulk-entry")).toHaveCount(0);
    for (const width of [320, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      expect(await dialog.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(
        true,
      );
    }

    const created = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/api/quotes" &&
        response.request().method() === "POST",
    );
    await page.getByTestId("quote-create-save").click();
    const response = await created;
    expect(response.status(), await response.text()).toBe(201);
    const quote = await response.json();
    quoteId = quote.id as number;
    expect(response.request().postDataJSON()).not.toHaveProperty("method");
    expect(quote).toMatchObject({ status: "Draft", grandTotal: 0, items: [] });
    await expect(page).toHaveURL(new RegExp(`/admin/quotes/${quoteId}$`));
    await expect(
      page.getByText(/Chưa chọn phương thức giá đầu ra|Output pricing method not selected/i),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: /Yêu cầu giá đầu vào|Request input pricing/i }),
    ).toBeVisible();
    await expect(page.getByTestId("quote-edit")).toHaveCount(0);
  } finally {
    if (quoteId) await hardDeleteBusinessRoot(api, headers, `/api/quotes/${quoteId}`);
    const unconvert = await api.post(`/api/leads/${leadId}/unconvert`, {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
    });
    expect(unconvert.status(), await unconvert.text()).toBe(200);
    await hardDeleteBusinessRoot(api, headers, `/api/leads/${leadId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
