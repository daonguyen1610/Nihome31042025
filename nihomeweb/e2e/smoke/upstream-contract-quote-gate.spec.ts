import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("customer contract form directs Sales to an approved quote", async ({ loginInBrowserAs, page }) => {
  await loginInBrowserAs(page, TEST_USERS.salesManager);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));

  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    await page.goto("/admin/finance/contracts", { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Thêm hợp đồng" }).first().click();

    const dialog = page.getByRole("dialog");
    await expect(dialog.getByText(/Báo giá nguồn \*/)).toBeVisible();
    await expect(dialog.getByText(/Hợp đồng đầu ra cần Báo giá đã duyệt/)).toBeVisible();
    await expect(dialog.getByRole("link", { name: "Mở Báo giá" })).toHaveAttribute("href", "/admin/quotes");
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
    await page.keyboard.press("Escape");
  }
});

test("Opportunity offers a contract only after its customer quote is approved", async ({ api, loginAs, loginInBrowserAs, page }) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const suffix = Math.random().toString(36).slice(2, 10);
  let customerId = 0;
  let opportunityId = 0;
  let quoteId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Customer quote gate ${suffix}`,
        sourceCode: "marketing",
        primaryContact: { fullName: "Nguyễn Minh Châu", phone: `0912${Math.floor(100000 + Math.random() * 899999)}`, isPrimary: true },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = (await customerResponse.json()).id as number;

    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: { name: `Cải tạo văn phòng Minh Châu ${suffix}`, customerId, estimatedValue: 750_000_000, winProbability: 45 },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = (await opportunityResponse.json()).id as number;
    for (const targetStage of ["Qualification", "Proposal", "Negotiation"]) {
      const response = await api.patch(`/api/opportunities/${opportunityId}/stage`, { headers, data: { targetStage } });
      expect(response.status(), await response.text()).toBe(200);
    }

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`/admin/opportunities/${opportunityId}`, { waitUntil: "networkidle" });
    await expect(page.getByTestId("opportunity-create-contract")).toHaveCount(0);

    const quoteResponse = await api.post("/api/quotes", {
      headers,
      data: {
        opportunityId,
        method: "Boq",
        packageDescription: "Cải tạo văn phòng và hệ thống MEP Minh Châu",
        items: [{ itemCode: "CT-01", name: "Thi công hoàn thiện văn phòng", unit: "m2", quantity: 500, unitPrice: 1_500_000 }],
        vatPercent: 8,
      },
    });
    expect(quoteResponse.status(), await quoteResponse.text()).toBe(201);
    let quote = await quoteResponse.json() as { id: number; rowVersion: string };
    quoteId = quote.id;
    const submitted = await api.post(`/api/quotes/${quoteId}/submit`, { headers, data: { rowVersion: quote.rowVersion } });
    expect(submitted.status(), await submitted.text()).toBe(200);
    quote = await submitted.json() as { id: number; rowVersion: string };
    const approved = await api.post(`/api/quotes/${quoteId}/approve`, { headers, data: { rowVersion: quote.rowVersion } });
    expect(approved.status(), await approved.text()).toBe(200);

    await page.reload({ waitUntil: "networkidle" });
    await page.getByTestId("opportunity-create-contract").click();
    await expect(page).toHaveURL(new RegExp(`fromQuote=${quoteId}`));
    await expect(page.getByRole("dialog").getByText(`#${quoteId}`)).toBeVisible();
  } finally {
    if (quoteId) await hardDeleteBusinessRoot(api, headers, `/api/quotes/${quoteId}`);
    if (opportunityId) await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
