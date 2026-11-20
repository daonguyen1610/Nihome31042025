import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { approveProjectBoq } from "../fixtures/projectBoq";

// NC-04: sales builds a BOQ quote in the browser from the project's approved BOQ,
// including quantities and prices with decimals, and the saved total matches it.
test("a BOQ quote is created in the browser from the approved project BOQ", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(180_000);
  const sales = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const suffix = Math.random().toString(36).slice(2, 10);
  const pageErrors: string[] = [];
  const serverErrors: string[] = [];
  page.on("pageerror", (error) => pageErrors.push(error.message));
  page.on("response", (response) => {
    if (response.status() >= 500) serverErrors.push(`${response.status()} ${response.url()}`);
  });
  let customerId = 0;
  let opportunityId = 0;
  let projectId = 0;
  let quoteId = 0;

  try {
    const lead = await api.post("/api/leads", {
      headers: sales,
      data: {
        name: `Nhà kho lạnh Đức Hoà ${suffix}`,
        phone: `0914${Math.floor(100000 + Math.random() * 899999)}`,
        sourceCode: "marketing",
      },
    });
    expect(lead.status(), await lead.text()).toBe(201);
    const conversion = await api.post(`/api/leads/${(await lead.json()).id}/convert`, {
      headers: sales,
      data: { address: "KCN Đức Hoà 1, Long An", representativeName: "Phạm Minh Đức" },
    });
    expect(conversion.status(), await conversion.text()).toBe(200);
    const converted = await conversion.json();
    customerId = converted.convertedCustomerId;
    opportunityId = converted.convertedOpportunityId as number;
    projectId = (
      await (await api.get(`/api/opportunities/${opportunityId}`, { headers: sales })).json()
    ).operationalProjectId;
    for (const targetStage of ["Qualification", "Proposal"]) {
      const moved = await api.patch(`/api/opportunities/${opportunityId}/stage`, {
        headers: sales,
        data: { targetStage },
      });
      expect(moved.status(), await moved.text()).toBe(200);
    }
    const boq = await approveProjectBoq(api, admin, projectId, [
      { name: "Bê tông sàn kho M300", unit: "m3", quantity: 12.5, unitPrice: 2_850_000 },
      { name: "Panel cách nhiệt PU 100mm", unit: "m2", quantity: 3.75, unitPrice: 1_234_567 },
    ]);

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`/admin/quotes?create=1&opportunityId=${opportunityId}`, {
      waitUntil: "networkidle",
    });
    await page.getByTestId("quote-create-save").click();
    await page.waitForURL(/\/admin\/quotes\/\d+/);
    quoteId = Number(page.url().match(/\/admin\/quotes\/(\d+)/)![1]);

    await page.getByTestId("quote-edit").click();
    await page.getByTestId("quote-output-method").click();
    await page.getByRole("option", { name: "Bảng khối lượng (BOQ)" }).click();
    await page.getByTestId("quote-project-boq-apply").click();
    const saved = page.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        new URL(response.url()).pathname === `/api/quotes/${quoteId}`,
    );
    await page.getByTestId("quote-save").click();
    const response = await saved;
    expect(response.status(), await response.text()).toBe(200);
    await expect(page.getByText("Đã cập nhật báo giá").first()).toBeVisible();

    const quote = await (await api.get(`/api/quotes/${quoteId}`, { headers: sales })).json();
    expect(quote.method).toBe("Boq");
    expect(quote.projectBoqRevisionId).toBe(boq.id);
    expect(quote.items).toHaveLength(2);
    expect(Number(quote.subtotal)).toBeCloseTo(boq.total, 0);
    expect(pageErrors).toEqual([]);
    expect(serverErrors).toEqual([]);
  } finally {
    if (quoteId) await hardDeleteBusinessRoot(api, admin, `/api/quotes/${quoteId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, admin, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, admin, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, admin, `/api/customers/${customerId}`);
  }
});
