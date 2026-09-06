import { expect, test, TEST_USERS } from "../fixtures/auth";

const vendorId = 165001;
const vendor = {
  id: vendorId,
  vendorCode: "NCC-ALPHA",
  companyName: "Alpha Industrial Supply",
  vendorType: "Supplier",
  taxCode: "0312345678",
  phone: "0901234567",
  email: "contact@alpha.example",
  address: "Ho Chi Minh City",
  contactPerson: "Nguyen Van An",
  licenseNo: "LIC-2026-001",
  tradeCategory: "Electrical",
  capabilityFileUrl: null,
  driveFolder: null,
  isActive: true,
  createdByUserId: 1,
  createdByName: "System Administrator",
  createdAt: "2026-09-01T08:00:00Z",
  updatedByUserId: 1,
  updatedByName: "Procurement Manager",
  updatedAt: "2026-09-06T08:00:00Z",
  rowVersion: "AAAAAAAAB9M=",
  contracts: [{
    id: 701,
    contractNumber: "HD-2026-0701",
    status: "InProgress",
    operationalProjectId: 301,
    operationalProjectCode: "PJ-2026-0301",
    operationalProjectName: "Alpha Factory",
  }],
  ratings: [{
    id: 801,
    contractId: 701,
    contractNumber: "HD-2026-0701",
    operationalProjectId: 301,
    operationalProjectCode: "PJ-2026-0301",
    status: "Approved",
    overallScore: 8.5,
    approvedAt: "2026-09-05T08:00:00Z",
  }],
  history: [{
    id: 901,
    action: "vendor.update",
    message: "Vendor profile updated.",
    actorUserId: 1,
    actorName: "Procurement Manager",
    createdAt: "2026-09-06T08:00:00Z",
  }],
};

test("vendor detail shows traceability and blocks unsafe deletion on mobile", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  await page.setViewportSize({ width: 390, height: 844 });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));

  await page.route(new RegExp(`/api/(?:v1/)?vendors/${vendorId}$`), route => {
    if (route.request().method() === "GET") {
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(vendor) });
    }
    return route.continue();
  });
  await page.route(new RegExp(`/api/(?:v1/)?vendors/${vendorId}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "Vendor",
        resourceId: vendorId,
        resourceLabel: "NCC-ALPHA · Alpha Industrial Supply",
        requiredConfirmation: "NCC-ALPHA",
        planToken: "a".repeat(64),
        canDelete: false,
        totalAffected: 2,
        items: [{
          key: "vendor.contracts",
          action: "Block",
          count: 1,
          examples: ["701"],
          resolutionLinks: [{ label: "HD-2026-0701", url: "/admin/contracts/701" }],
          resolutionUrl: "/admin/contracts",
        }],
      }),
    }));

  await page.goto(`${appUrl}/admin/vendors/${vendorId}`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: vendor.companyName })).toBeVisible();
  await expect(page.getByText("HD-2026-0701", { exact: true })).toBeVisible();
  await expect(page.getByText("PJ-2026-0301 · Alpha Factory", { exact: true })).toBeVisible();
  await expect(page.getByText("Procurement Manager", { exact: true }).first()).toBeVisible();

  await page.getByRole("button", { name: "Delete" }).click();
  await expect(page.getByText("Contracts using this vendor", { exact: true })).toBeVisible();
  await expect(page.getByText("Deletion is not available yet. Resolve every blocking item and reopen this dialog.", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: /Delete permanently/i })).toBeDisabled();

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  expect(overflow).toBe(false);
});