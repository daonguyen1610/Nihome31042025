import { expect, test, TEST_USERS } from "../fixtures/auth";
import type { Page } from "@playwright/test";

const projectId = 170001;
const alertId = 170101;
const permissions = ["proc.material-alerts.view", "proc.material-alerts.manage"];

const openAlert = {
  id: alertId,
  operationalProjectId: projectId,
  operationalProjectCode: "PJ-0170",
  operationalProjectName: "Material Control Project",
  customerId: 170201,
  customerName: "NICON Customer",
  projectBoqLineId: 170301,
  code: "MA-0001",
  type: "Shortage",
  status: "Open",
  severity: "Critical",
  itemCode: "MAT-STEEL",
  description: "Reinforcement steel D16",
  unit: "kg",
  boqAllowance: 1000,
  requiredQuantity: 600,
  receivedQuantity: 200,
  issuedQuantity: 100,
  onHandQuantity: 100,
  varianceQuantity: 400,
  sourceEntityType: "MaterialRequest",
  sourceEntityId: 170401,
  assignedToUserId: 1,
  assignedToUserName: "Super Admin",
  detectedAt: "2026-09-08T08:00:00Z",
  acknowledgedAt: null,
  acknowledgedByUserId: null,
  acknowledgedByName: null,
  acknowledgementNote: null,
  resolvedAt: null,
  lastEvaluatedAt: "2026-09-08T08:00:00Z",
  createdAt: "2026-09-08T08:00:00Z",
  updatedAt: "2026-09-08T08:00:00Z",
  rowVersion: "alert-row-1",
  contracts: [{ id: 170501, contractNumber: "PO-0170", direction: "Downstream", type: "Supply", vendorId: 170601, vendorName: "Steel Supplier", status: "InProgress" }],
  events: [{ id: 1, type: "Detected", fromStatus: null, toStatus: "Open", reason: null, changedByUserId: 1, changedByName: "Super Admin", changedAt: "2026-09-08T08:00:00Z" }],
};

const mockProjectContext = async (page: Page) => {
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ role: "PM", roleId: 170, permissions }),
  }));
  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ total: 1, page: 1, pageSize: 100, items: [{ id: projectId, code: "PJ-0170", name: "Material Control Project" }] }),
  }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ operationalProjectId: projectId, members: [{ id: 1, userId: 1, userName: "Super Admin", position: "PM", isActive: true, roles: [{ roleCode: "PM", endedAt: null }], rowVersion: "team-row" }], assignments: [] }),
  }));
  await page.route(/\/api\/(?:v1\/)?kpi\/eligible-users$/, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ boqRevisions: [], materialRequests: [], contractLines: [], receipts: [], issues: [], vendorRatings: [] }) }));
};

test("material alert queue filters, reconciles, exports, and stays responsive", async ({ page, loginInBrowserAs, baseURL }) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  const listQueries: URL[] = [];
  let evaluateCalls = 0;
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
  await mockProjectContext(page);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-alerts(?:\\?.*)?$`), route => {
    listQueries.push(new URL(route.request().url()));
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 1, page: 1, pageSize: 20, statusCounts: { Open: 1, Acknowledged: 1, Resolved: 2 }, items: [openAlert] }) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-alerts/evaluate$`), route => {
    evaluateCalls += 1;
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([openAlert]) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${appUrl}/admin/procurement-control?projectId=${projectId}&tab=alerts`, { waitUntil: "networkidle" });
  await expect(page.getByRole("tab", { selected: true })).toContainText("Material alerts (2)");
  await expect(page.getByRole("heading", { name: "Material alerts" })).toBeVisible();
  const mobileAlert = page.getByRole("article").filter({ hasText: "MAT-STEEL" });
  await expect(mobileAlert.getByRole("heading", { name: /MAT-STEEL/ })).toBeVisible();
  await expect(mobileAlert.getByText("400 kg", { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

  await page.getByRole("textbox", { name: "Search alerts" }).fill("steel");
  await page.getByRole("combobox", { name: "Alert type" }).click();
  await page.getByRole("option", { name: "Material shortage" }).click();
  await page.getByRole("combobox", { name: "Severity" }).click();
  await page.getByRole("option", { name: "Critical" }).click();
  await expect.poll(() => Object.fromEntries(listQueries.at(-1)?.searchParams ?? [])).toMatchObject({ search: "steel", type: "Shortage", status: "Open", severity: "Critical" });

  await page.getByRole("button", { name: "Recheck source data" }).click();
  await expect.poll(() => evaluateCalls).toBe(1);
  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export CSV" }).click();
  expect((await downloadPromise).suggestedFilename()).toContain(`material-alerts-project-${projectId}`);

  await page.setViewportSize({ width: 768, height: 1024 });
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("table")).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("table")).toBeVisible();
  await expect(page.getByRole("article")).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  expect(errors).toEqual([]);
});

test("material alert detail preserves context and acknowledges with concurrency", async ({ page, loginInBrowserAs, baseURL }) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  let alert = { ...openAlert };
  let acknowledgementPayload: Record<string, unknown> | null = null;
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
  await mockProjectContext(page);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-alerts/${alertId}$`), route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(alert) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/procurement/material-alerts/${alertId}/acknowledge$`), route => {
    acknowledgementPayload = route.request().postDataJSON() as Record<string, unknown>;
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    alert = {
      ...alert,
      status: "Acknowledged",
      acknowledgedAt: "2026-09-08T09:00:00Z",
      acknowledgedByUserId: 1,
      acknowledgedByName: "Super Admin",
      acknowledgementNote: String(acknowledgementPayload.note),
      rowVersion: "alert-row-2",
      events: [...alert.events, { id: 2, type: "Acknowledged", fromStatus: "Open", toStatus: "Acknowledged", reason: String(acknowledgementPayload.note), changedByUserId: 1, changedByName: "Super Admin", changedAt: "2026-09-08T09:00:00Z" }],
    };
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(alert) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${appUrl}/admin/procurement-control/projects/${projectId}/material-alerts/${alertId}`, { waitUntil: "networkidle" });
  await expect(page.getByRole("navigation", { name: "Material alert context" })).toContainText("NICON Customer");
  await expect(page.getByRole("heading", { level: 1, name: /MA-0001.*MAT-STEEL/ })).toBeVisible();
  await expect(page.getByText("400 kg requires action")).toBeVisible();
  await expect(page.getByRole("link", { name: "Open source record" })).toHaveAttribute("href", `/admin/procurement-control/projects/${projectId}/material-requests/${openAlert.sourceEntityId}`);
  await expect(page.getByText("PO-0170")).toBeVisible();
  await expect(page.getByText("Downstream - Partner")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);

  await page.setViewportSize({ width: 390, height: 600 });
  await page.getByRole("button", { name: "Acknowledge" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("button", { name: "Confirm acknowledgement" })).toBeVisible();
  const note = dialog.getByLabel("Response note");
  await note.fill("No");
  await dialog.getByRole("button", { name: "Confirm acknowledgement" }).click();
  await expect(dialog.getByRole("alert")).toContainText("3 to 2,000");
  await expect(note).toHaveValue("No");
  await note.fill("Procurement owner confirmed an additional delivery for tomorrow.");
  await dialog.getByRole("button", { name: "Confirm acknowledgement" }).click();
  await expect(dialog).toBeHidden();
  expect(acknowledgementPayload).toMatchObject({ note: "Procurement owner confirmed an additional delivery for tomorrow.", rowVersion: "alert-row-1" });
  await expect(page.getByText("Acknowledged", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Acknowledge" })).toHaveCount(0);
  await expect(page.getByText("Procurement owner confirmed an additional delivery for tomorrow.").first()).toBeVisible();
  await expect(page.getByRole("link", { name: "Back" })).toHaveAttribute("href", `/admin/procurement-control?projectId=${projectId}&tab=alerts`);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("region", { name: "Response ownership" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Alert history" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  expect(errors).toEqual([]);
});