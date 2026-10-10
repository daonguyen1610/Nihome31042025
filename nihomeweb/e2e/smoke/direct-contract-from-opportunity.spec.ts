import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("agreed opportunity value creates an upstream contract without a quote", async ({ api, loginAs, loginInBrowserAs, page }) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const suffix = Math.random().toString(36).slice(2, 10);
  let customerId = 0;
  let opportunityId = 0;
  let projectId = 0;
  let contractId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Khách hàng đã chốt giá ${suffix}`,
        sourceCode: "referral",
        primaryContact: { fullName: "Trần Thị Lan", phone: `0913${Math.floor(100000 + Math.random() * 899999)}`, isPrimary: true },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = (await customerResponse.json()).id as number;
    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: { name: `Thiết kế văn phòng chị Lan ${suffix}`, customerId, estimatedValue: 500_000_000, winProbability: 80 },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = (await opportunityResponse.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`/admin/opportunities/${opportunityId}`, { waitUntil: "networkidle" });
    await page.getByTestId("opportunity-create-contract").click();
    await expect(page).toHaveURL(new RegExp(`opportunityId=${opportunityId}`));
    await expect(page).not.toHaveURL(/fromQuote=/);
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByText(/Không bắt buộc khi khách hàng đã thống nhất giá trị/)).toBeVisible();
    await dialog.getByTestId("contract-quick-project-open").click();
    await dialog.getByTestId("contract-quick-project-name").fill(`Dự án thiết kế văn phòng chị Lan ${suffix}`);
    const projectResponsePromise = page.waitForResponse((response) =>
      response.url().includes("/api/operational-projects") && response.request().method() === "POST");
    await dialog.getByTestId("contract-quick-project-create").click();
    const projectResponse = await projectResponsePromise;
    expect(projectResponse.status(), await projectResponse.text()).toBe(201);
    projectId = (await projectResponse.json()).id as number;
    await dialog.locator("#c-value").fill("500000000");
    const contractResponsePromise = page.waitForResponse((response) =>
      /\/api\/contracts(?:\?.*)?$/.test(response.url()) && response.request().method() === "POST");
    await dialog.getByRole("button", { name: "Lưu" }).click();
    const contractResponse = await contractResponsePromise;
    expect(contractResponse.status(), await contractResponse.text()).toBe(201);
    const contract = await contractResponse.json() as { id: number; direction: string; opportunityId: number | null; quoteId: number | null; value: number };
    contractId = contract.id;
    expect(contract.direction).toBe("Upstream");
    expect(contract.opportunityId).toBe(opportunityId);
    expect(contract.quoteId).toBeNull();
    expect(contract.value).toBe(500_000_000);
  } finally {
    if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
    if (projectId) await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    if (opportunityId) await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
