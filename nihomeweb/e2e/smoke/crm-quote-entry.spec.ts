import { randomUUID } from "node:crypto";
import type { APIRequestContext } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

/**
 * Demo path Lead → Opportunity → Quote. Conversion opens the operational
 * project, the opportunity offers "Create quote" while it is still open, and a
 * quote form with no rate catalog sends the user to set one up and back.
 */

interface ConvertedLead {
  id: number;
  convertedCustomerId: number;
  convertedOpportunityId: number;
}

async function convertLead(api: APIRequestContext, token: string): Promise<ConvertedLead> {
  const headers = { Authorization: `Bearer ${token}` };
  const suffix = randomUUID().slice(0, 8);
  const lead = await api.post("/api/leads", {
    headers: { ...headers, "Idempotency-Key": randomUUID() },
    data: {
      name: `[E2E-QUOTE-ENTRY] Ông Trần Văn Hậu ${suffix}`,
      phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
      companyName: `[E2E-QUOTE-ENTRY] Công ty Kho lạnh Hậu Giang ${suffix}`,
      sourceCode: "marketing",
    },
  });
  expect(lead.status(), "create lead").toBe(201);
  const { id } = await lead.json() as { id: number };
  const converted = await api.post(`/api/leads/${id}/convert`, {
    headers: { ...headers, "Idempotency-Key": randomUUID() },
    data: { address: "KCN Tân Phú Thạnh, Hậu Giang", representativeName: "Trần Văn Hậu" },
  });
  expect(converted.status(), "convert lead").toBe(200);
  return await converted.json() as ConvertedLead;
}

async function cleanup(api: APIRequestContext, token: string, lead: ConvertedLead) {
  const headers = { Authorization: `Bearer ${token}` };
  // Unconvert removes the untouched opportunity and its auto-created project.
  await api.post(`/api/leads/${lead.id}/unconvert`, {
    headers: { ...headers, "Idempotency-Key": randomUUID() },
  });
  await hardDeleteBusinessRoot(api, headers, `/api/leads/${lead.id}`);
  await hardDeleteBusinessRoot(api, headers, `/api/customers/${lead.convertedCustomerId}`);
}

test.describe("CRM quote entry", () => {
  test("converted lead has a project and its opportunity offers a quote while open", async ({ api, loginAs, loginInBrowserAs, page }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const lead = await convertLead(api, token);
    try {
      const opportunity = await (await api.get(`/api/opportunities/${lead.convertedOpportunityId}`, {
        headers: { Authorization: `Bearer ${token}` },
      })).json() as { stage: string; operationalProjectId: number | null };
      expect(opportunity.stage).toBe("Prospecting");
      expect(opportunity.operationalProjectId).not.toBeNull();

      await loginInBrowserAs(page, TEST_USERS.salesManager);
      await page.goto(`/admin/opportunities/${lead.convertedOpportunityId}`);
      await expect(page.getByTestId("opportunity-create-quote")).toBeVisible();
      await page.getByTestId("opportunity-quotes-tab").click();
      await expect(page.getByRole("tabpanel")).toContainText(/báo giá|quote/i);
      await page.getByTestId("opportunity-create-quote").click();
      await expect(page).toHaveURL(new RegExp(`/admin/quotes\\?create=1&opportunityId=${lead.convertedOpportunityId}`));
    } finally {
      await cleanup(api, token, lead);
    }
  });

  test("quote form without a rate catalog links to catalog setup and back", async ({ loginInBrowserAs, page }) => {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    // Simulate a fresh installation: no active catalog of either type.
    await page.route(/\/api\/material-rate-catalogs\?/, (route) =>
      route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

    await page.goto("/admin/quotes");
    await page.getByRole("button", { name: /báo giá mới|new quote/i }).click();
    const emptyState = page.getByTestId("quote-catalog-empty-InvestmentRate");
    await expect(emptyState).toBeVisible();
    await page.getByTestId("quote-catalog-setup-InvestmentRate").click();

    await expect(page).toHaveURL(/\/admin\/material-rates\/investment\?returnTo=/);
    await expect(page.getByRole("dialog")).toBeVisible();
    await page.keyboard.press("Escape");
    const back = page.getByTestId("material-rates-quote-return").getByRole("link");
    await expect(back).toHaveAttribute("href", "/admin/quotes?create=1");

    await page.unroute(/\/api\/material-rate-catalogs\?/);
    await back.click();
    await expect(page).toHaveURL(/\/admin\/quotes\?create=1$/);
  });

  test("catalog screen ignores a return target outside the quote screens", async ({ loginInBrowserAs, page }) => {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/material-rates/investment?returnTo=%2F%2Fevil.example%2Fadmin%2Fquotes");
    await expect(page.getByTestId("material-rates-page")).toBeVisible();
    await expect(page.getByTestId("material-rates-quote-return")).toHaveCount(0);
  });
});
