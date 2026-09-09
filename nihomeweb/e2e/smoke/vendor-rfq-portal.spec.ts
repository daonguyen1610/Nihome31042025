import { expect, test } from "@playwright/test";

test("vendor token portal submits a foreign-currency quotation", async ({ page, baseURL }) => {
  const token = "secure-vendor-token";
  let submitted: Record<string, unknown> | null = null;
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.route(/\/api\/(?:v1\/)?vendor-rfqs\/documents\/501\/download$/, route => {
    expect(route.request().headers()["x-rfq-portal-token"]).toBe(token);
    return route.fulfill({ status: 200, contentType: "application/pdf", headers: { "Content-Disposition": "attachment; filename*=UTF-8''cable-specification.pdf" }, body: "RFQ package" });
  });
  await page.route(/\/api\/(?:v1\/)?vendor-rfqs(?:\/bids)?$/, async route => {
    expect(route.request().headers()["x-rfq-portal-token"]).toBe(token);
    if (route.request().method() === "POST") {
      submitted = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
        code: "RFQ-PORTAL-001", title: "Factory cable package", dueAt: "2035-01-01T09:00:00Z",
        vendorName: "International Cable Supplier", canSubmit: true,
        lines: [{ id: 101, itemCode: "CABLE-01", description: "Power cable", unit: "m", quantity: 100 }],
        documents: [{ id: 501, name: "cable-specification.pdf" }],
      }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      code: "RFQ-PORTAL-001", title: "Factory cable package", dueAt: "2035-01-01T09:00:00Z",
      vendorName: "International Cable Supplier", canSubmit: true,
      lines: [{ id: 101, itemCode: "CABLE-01", description: "Power cable", unit: "m", quantity: 100 }],
      documents: [{ id: 501, name: "cable-specification.pdf" }],
    }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${baseURL ?? "http://localhost:5043"}/vendor/rfqs#token=${token}`);
  await expect(page.getByRole("heading", { name: "Factory cable package" })).toBeVisible();
  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: /cable-specification\.pdf/ }).click();
  expect((await downloadPromise).suggestedFilename()).toBe("cable-specification.pdf");
  await page.getByLabel("Unit price CABLE-01").fill("2.5");
  await page.getByLabel("Currency").fill("USD");
  await page.getByLabel("Exchange rate to VND").fill("25000");
  await page.getByLabel("Delivery lead time").fill("7");
  await page.getByLabel("Freight").fill("10");
  await page.getByLabel("Discount (%)").fill("5");
  await page.getByLabel("VAT (%)").fill("10");
  await expect(page.getByRole("region", { name: "Commercial total preview" })).toContainText("272.25 USD");
  await expect(page.getByRole("region", { name: "Commercial total preview" })).toContainText("6,806,250 VND");
  await page.getByLabel("Quote valid until").fill("2035-01-02T09:00");
  await page.getByLabel("Payment terms").fill("Net 30 after inspected delivery");
  await page.getByRole("button", { name: "Submit quotation" }).click();

  await expect(page.getByRole("heading", { name: "Quotation submitted" })).toBeVisible();
  await expect(page.getByText("272.25 USD", { exact: true })).toBeVisible();
  await expect(page.getByText("6,806,250 VND", { exact: true })).toBeVisible();
  expect(submitted).toMatchObject({ currency: "USD", exchangeRateToVnd: 25000, freightAmount: 10,
    discountPercent: 5, vatPercent: 10, lines: [{ rfqLineId: 101, unitPrice: 2.5 }] });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
});

test("vendor portal explains an invalid or expired invitation", async ({ page, baseURL }) => {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.route(/\/api\/(?:v1\/)?vendor-rfqs$/, route =>
    route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ message: "Not found" }) }));
  await page.goto(`${baseURL ?? "http://localhost:5043"}/vendor/rfqs#token=expired-token`);
  await expect(page.getByRole("heading", { name: "Quotation link unavailable" })).toBeVisible();
  await expect(page.getByText("Contact the Procurement owner if you need a new invitation.")).toBeVisible();
  await expect(page.getByText(/status code 404/i)).toHaveCount(0);
});