import { randomUUID } from "node:crypto";
import type { APIRequestContext } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

/**
 * Demo path Lead → Opportunity → Quote. Conversion opens the operational
 * project, the opportunity offers "Create quote" while it is still open, and a
 * basic quote draft leads to the Procurement input-price handoff.
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
  const { id } = (await lead.json()) as { id: number };
  const converted = await api.post(`/api/leads/${id}/convert`, {
    headers: { ...headers, "Idempotency-Key": randomUUID() },
    data: { address: "KCN Tân Phú Thạnh, Hậu Giang", representativeName: "Trần Văn Hậu" },
  });
  expect(converted.status(), "convert lead").toBe(200);
  return (await converted.json()) as ConvertedLead;
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
  test("converted lead has a project and its opportunity offers a quote while open", async ({
    api,
    loginAs,
    loginInBrowserAs,
    page,
  }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const lead = await convertLead(api, token);
    try {
      const opportunity = (await (
        await api.get(`/api/opportunities/${lead.convertedOpportunityId}`, {
          headers: { Authorization: `Bearer ${token}` },
        })
      ).json()) as { stage: string; operationalProjectId: number | null };
      expect(opportunity.stage).toBe("Prospecting");
      expect(opportunity.operationalProjectId).not.toBeNull();

      await loginInBrowserAs(page, TEST_USERS.salesManager);
      await page.goto(`/admin/opportunities/${lead.convertedOpportunityId}`);
      await expect(page.getByTestId("opportunity-create-quote")).toBeVisible();
      await page.getByTestId("opportunity-quotes-tab").click();
      await expect(page.getByRole("tabpanel")).toContainText(/báo giá|quote/i);
      await page.getByTestId("opportunity-create-quote").click();
      await expect(page).toHaveURL(
        new RegExp(`/admin/quotes\\?create=1&opportunityId=${lead.convertedOpportunityId}`),
      );
    } finally {
      await cleanup(api, token, lead);
    }
  });

  test("creates a basic draft before requesting Procurement input pricing", async ({
    api,
    loginAs,
    loginInBrowserAs,
    page,
  }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const lead = await convertLead(api, token);
    let quoteId = 0;
    try {
      await loginInBrowserAs(page, TEST_USERS.salesManager);
      await page.setViewportSize({ width: 390, height: 844 });
      await page.goto(`/admin/quotes?create=1&opportunityId=${lead.convertedOpportunityId}`);
      const dialog = page.getByTestId("quote-create-dialog");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByTestId("quote-method")).toHaveCount(0);
      await expect(dialog.getByTestId("quote-area")).toHaveCount(0);
      await expect(dialog.getByTestId("quote-rate-catalog")).toHaveCount(0);
      expect(await dialog.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(
        true,
      );

      const createResponse = page.waitForResponse(
        (response) =>
          new URL(response.url()).pathname === "/api/quotes" &&
          response.request().method() === "POST",
      );
      await page.getByTestId("quote-create-save").click();
      const response = await createResponse;
      expect(response.status(), await response.text()).toBe(201);
      expect(response.request().postDataJSON()).toMatchObject({
        draftOnly: true,
        opportunityId: lead.convertedOpportunityId,
      });
      expect(response.request().postDataJSON()).not.toHaveProperty("method");
      const quote = await response.json();
      quoteId = quote.id as number;
      expect(quote.grandTotal).toBe(0);
      await expect(page).toHaveURL(new RegExp(`/admin/quotes/${quoteId}$`));
      await expect(page.getByText(/bước 1:|step 1:/i)).toBeVisible();
      await expect(page.getByTestId("quote-edit")).toHaveCount(0);
      await expect(page.getByRole("button", { name: /gửi duyệt|submit/i })).toHaveCount(0);
      await expect(
        page.getByRole("link", { name: /yêu cầu giá đầu vào|request input pricing/i }),
      ).toHaveAttribute(
        "href",
        new RegExp(`projectId=\\d+&opportunityId=${lead.convertedOpportunityId}`),
      );
    } finally {
      if (quoteId)
        await hardDeleteBusinessRoot(
          api,
          { Authorization: `Bearer ${token}` },
          `/api/quotes/${quoteId}`,
        );
      await cleanup(api, token, lead);
    }
  });

  test("catalog screen ignores a return target outside the quote screens", async ({
    loginInBrowserAs,
    page,
  }) => {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(
      "/admin/material-rates/investment?returnTo=%2F%2Fevil.example%2Fadmin%2Fquotes",
    );
    await expect(page.getByTestId("material-rates-page")).toBeVisible();
    await expect(page.getByTestId("material-rates-quote-return")).toHaveCount(0);
  });
});
