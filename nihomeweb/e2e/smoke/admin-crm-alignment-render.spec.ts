import { expect } from "@playwright/test";
import { test, TEST_USERS } from "../fixtures/auth";

const json = (body: unknown) => ({ status: 200, contentType: "application/json", body: JSON.stringify(body) });

test("tender detail renders estimate revisions, totals, and blocked submission gates", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route("**/api/tenders/77/estimates", (route) => route.fulfill(json([{
    id: 8,
    tenderId: 77,
    versionNumber: 1,
    status: "Draft",
    currency: "VND",
    vatPercent: 10,
    costSubtotal: 800000,
    bidSubtotal: 1000000,
    vatAmount: 100000,
    grandBidTotal: 1100000,
    sourceFileName: "estimate.csv",
    sourceSha256: "hash",
    importedByUserId: 1,
    importedAt: "2026-09-01T00:00:00Z",
    note: null,
    lines: [{ id: 1, itemCode: "HM-001", description: "Wall", unit: "m2", quantity: 10, unitCost: 80000, bidUnitPrice: 100000, costAmount: 800000, bidAmount: 1000000, note: null, sortOrder: 0 }],
  }])));
  await page.route("**/api/tenders/77/timeline?*", (route) => route.fulfill(json([])));
  await page.route("**/api/tenders/77", (route) => route.fulfill(json({
    id: 77,
    code: "TD-077",
    name: "Rendered tender",
    customerId: 1,
    customerName: "Customer A",
    openingDate: "2026-09-10T00:00:00Z",
    submissionDeadline: "2026-10-10T00:00:00Z",
    preparerUserId: 1,
    preparerName: "Manager",
    infoSource: null,
    status: "Preparing",
    note: null,
    wonOpportunityId: null,
    lostReasonCode: null,
    closedAt: null,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
    checklistItems: [{ id: 1, title: "Document", status: "Preparing", sortOrder: 0 }],
    checklistCompletionPercent: 0,
    isDeadlineImminent: false,
  })));

  await page.goto("/admin/tenders/77");
  await page.getByTestId("tender-estimate-tab").click();
  await expect(page.getByTestId("tender-estimates")).toBeVisible();
  await expect(page.getByTestId("tender-lifecycle").getByRole("button").first()).toBeDisabled();
  await expect(page.getByText("HM-001")).toBeVisible();
});

test("migrated survey renders project-routed mutation controls", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route("**/api/surveys/81/timeline?*", (route) => route.fulfill(json([])));
  await page.route("**/api/surveys/81", (route) => route.fulfill(json({
    id: 81,
    code: "SV-081",
    location: "Legacy site",
    surveyDate: "2026-09-01T00:00:00Z",
    operationalProjectId: 9,
    operationalProjectName: "Migrated operational project",
    linkedOpportunityId: 71,
    linkedOpportunityName: "Legacy opportunity",
    driveSyncStatus: "NotSynced",
    media: [],
    checklistResults: [],
    siteConditions: [],
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
  })));

  await page.goto("/admin/surveys/81");
  await page.getByTestId("survey-conditions-tab").click();
  const panel = page.getByTestId("survey-conditions");
  await expect(panel).toBeVisible();
  await expect(panel.getByRole("tab")).toHaveCount(2);
  await expect(panel.getByRole("button").nth(1)).toBeEnabled();

  await page.getByTestId("survey-media-tab").click();
  await expect(page.getByTestId("survey-media-choose-file")).toBeVisible();
});
