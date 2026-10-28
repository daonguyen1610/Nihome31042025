import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("an approved quote opens its existing Finance review package", async ({
  api,
  loginAs,
  loginInBrowserAs,
  page,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const quotesResponse = await api.get("/api/quotes?search=QT-SAMPLE-007", { headers });
  expect(quotesResponse.status()).toBe(200);
  const quotes = (await quotesResponse.json()).items as {
    id: number;
    code: string;
    status: string;
    operationalProjectId: number | null;
    opportunityId: number | null;
  }[];
  const quote = quotes.find((item) => item.code === "QT-SAMPLE-007");
  expect(quote, "the approved sample quote is seeded").toBeTruthy();
  expect(quote!.status).toBe("Approved");
  expect(quote!.operationalProjectId).toBeTruthy();

  const optionsResponse = await api.get("/api/work-packages/options", { headers });
  expect(optionsResponse.status()).toBe(200);
  const options = await optionsResponse.json();
  const finance = options.departments.find((item: { code: string }) => item.code === "FINANCE");
  const lead = options.users.find(
    (item: { roleGroupId: number; canLead: boolean }) =>
      item.roleGroupId === finance?.id && item.canLead,
  );
  expect(lead, "an active Finance lead is seeded").toBeTruthy();

  const created = await api.post("/api/work-packages", {
    headers: { ...headers, "Idempotency-Key": randomUUID() },
    data: {
      kind: "Project",
      purpose: "FinanceCommercialReview",
      name: `Rà soát thương mại ${quote!.code} ${Date.now()}`,
      operationalProjectId: quote!.operationalProjectId,
      sourceOpportunityId: quote!.opportunityId,
      sourceQuoteId: quote!.id,
      departmentRoleGroupId: finance.id,
      leadUserId: lead.userId,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const packageId = (await created.json()).id as number;

  try {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/quotes/${quote!.id}`);
    await expect(page.getByRole("heading", { name: new RegExp(quote!.code) })).toBeVisible();
    const reviewLink = page.getByRole("link", { name: "Xem hồ sơ Tài chính rà soát" });
    for (const width of [375, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(reviewLink).toBeVisible();
      await expect(reviewLink).toHaveAttribute("href", `/admin/work-packages/${packageId}`);
      const bounds = await reviewLink.boundingBox();
      expect(bounds, `review link fits at ${width}px`).toBeTruthy();
      expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
    }
    await reviewLink.click();
    await expect(page).toHaveURL(new RegExp(`/admin/work-packages/${packageId}$`));
  } finally {
    await hardDeleteBusinessRoot(api, headers, `/api/work-packages/${packageId}`);
  }
});
