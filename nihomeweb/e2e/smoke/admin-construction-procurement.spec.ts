import { test, expect, type Page } from "@playwright/test";
import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";

// Browser-only contract fixtures. Real authorization, persistence and transaction
// assertions live in ConstructionProcurementControllerTests.
async function prepare(
  page: Page,
  baseURL: string,
  manage = true,
  language: "en" | "vi" | "zh" | "ja" = "en",
) {
  const permissions = [
    "reports.projects.view",
    "operations.projects.view",
    "proc.resource-requests.view",
    "crm.contracts.view",
  ];
  if (manage) permissions.push("proc.resource-requests.manage");
  const translations: Record<string, string> = {};
  const directory = resolve("../nihomebackend/Data/Seeds/i18n");
  for (const filename of readdirSync(directory).filter((name) => name.endsWith(".json"))) {
    const entries = JSON.parse(readFileSync(resolve(directory, filename), "utf8"));
    if (Array.isArray(entries))
      for (const entry of entries) translations[entry.key] = entry[language];
  }
  const user = {
    userId: 77,
    phoneNumber: "0911000006",
    fullName: "NICON Procurement",
    role: "PROCUREMENT",
    roleId: null,
    isActive: true,
    accessToken: "browser-fixture-access",
    refreshToken: "browser-fixture-refresh",
  };
  await page.context().addCookies(
    ["nicon_access_token", "nicon_refresh_token"].map((name) => ({
      name,
      value: name.endsWith("access_token") ? user.accessToken : user.refreshToken,
      url: baseURL,
    })),
  );
  await page.addInitScript((value) => localStorage.setItem("nicon_lang", value), language);
  let contractId: number | null = null;
  let suppliedAt: string | null = null;
  const history: object[] = [];
  const row = () => ({
    id: 1,
    operationalProjectId: 501,
    code: "RR-ANPHU-CRANE",
    type: "Equipment",
    wbsCode: "STRUCTURE-02",
    quantity: 2,
    unit: "set",
    reason: "Tower cranes for the An Phu structural package",
    needByDate: "2026-11-01T00:00:00Z",
    contractId,
    contractNumber: contractId ? "HD-DOWNSTREAM-01" : null,
    contractValid: contractId != null,
    suppliedAt,
    suppliedByUserId: suppliedAt ? 77 : null,
    supplyDocumentId: suppliedAt ? 901 : null,
    rowVersion: "AAAAAAAAAAE=",
    history,
  });
  const draftAttempts: { key: string | undefined; body: Record<string, unknown> }[] = [];
  await page.route(/\/api\//, async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace("/api/v1/", "/api/");
    let json: unknown = [];
    if (path === "/api/auth/refresh") json = user;
    else if (path === "/api/users/me") json = { ...user, id: user.userId };
    else if (path === "/api/users/me/permissions")
      json = { role: user.role, roleId: null, permissions };
    else if (path.startsWith("/api/translations/")) json = { languageCode: "en", translations };
    else if (path === "/api/operational-projects")
      json = {
        items: [
          {
            id: 501,
            code: "PJ-ANPHU",
            name: "NICON An Phu structural works",
            status: "Active",
            customerId: 1,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 200,
      };
    else if (path === "/api/operational-projects/501/team") json = { members: [], assignments: [] };
    else if (path === "/api/contracts") json = { items: [], total: 0 };
    else if (path === "/api/contracts/classification-options")
      json = {
        directions: ["Downstream"],
        types: ["Supply", "Subcontract"],
        allowedTypes: { Downstream: ["Supply", "Subcontract"] },
        vendors: [
          {
            id: 700,
            vendorCode: "NCC-CRANE",
            companyName: "Structural crane partner",
            vendorType: "SubContractor",
          },
        ],
      };
    else if (path.endsWith("/construction-resources/contracts")) {
      if (request.method() === "POST") {
        draftAttempts.push({
          key: request.headers()["idempotency-key"],
          body: request.postDataJSON(),
        });
        if (draftAttempts.length === 1) {
          await route.abort("failed");
          return;
        }
        json = { id: 702, contractNumber: "HD-DRAFT-02", status: "Draft", direction: "Downstream" };
      } else json = [{ id: 701, contractNumber: "HD-DOWNSTREAM-01", status: "Signed" }];
    } else if (path.endsWith("/construction-resources/documents"))
      json = [{ id: 901, originalFileName: "Tower-crane-site-supply.pdf" }];
    else if (path.endsWith("/construction-resources/1/contract")) {
      expect(request.postDataJSON().contractId).toBe(701);
      contractId = 701;
      history.push({
        id: 1,
        contractId,
        supplyDocumentId: null,
        actorUserId: 77,
        createdAt: "2026-10-18T08:00:00Z",
      });
      json = row();
    } else if (path.endsWith("/construction-resources/1/supply")) {
      expect(request.postDataJSON().documentId).toBe(901);
      suppliedAt = "2026-10-18T09:00:00Z";
      history.push({
        id: 2,
        contractId,
        supplyDocumentId: 901,
        actorUserId: 77,
        createdAt: suppliedAt,
      });
      json = row();
    } else if (path.endsWith("/construction-resources")) json = [row()];
    else if (path.endsWith("/unread-count")) json = { count: 0 };
    await route.fulfill({ json });
  });
  await page.goto(`${baseURL}/admin/procurement-control?projectId=501&tab=resources`);
  await expect(page.getByTestId("construction-procurement-1")).toBeVisible();
  return { draftAttempts };
}

for (const width of [390, 768, 1440]) {
  test(`procurement links and records supply at ${width}px`, async ({
    page,
    baseURL,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 1000 });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await prepare(page, baseURL!);
    const card = page.getByTestId("construction-procurement-1");
    await expect(card.getByRole("button", { name: "Confirm supply", exact: true })).toBeDisabled();
    await card.locator("#resource-contract-1").click();
    await page.getByRole("option", { name: "HD-DOWNSTREAM-01" }).click();
    await card.getByRole("button", { name: "Save contract link" }).click();
    await expect(card.getByText("Contract: HD-DOWNSTREAM-01", { exact: true })).toBeVisible();
    await card.locator("#resource-document-1").click();
    await page.getByRole("option", { name: "Tower-crane-site-supply.pdf" }).click();
    await card.getByRole("button", { name: "Confirm supply", exact: true }).click();
    await expect(card.getByText("Supply recorded", { exact: true })).toBeVisible();
    await expect(card.locator("#resource-contract-1")).toHaveCount(0);
    await card.locator("summary").click();
    await expect(card.locator("details li")).toHaveCount(2);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`procurement-supply-${width}.png`),
      fullPage: true,
    });
    expect(errors).toEqual([]);
  });
}

test("draft creation keeps the same retry key after a lost response", async ({ page, baseURL }) => {
  const { draftAttempts } = await prepare(page, baseURL!);
  await page.getByRole("button", { name: "Draft downstream contract", exact: true }).click();
  const form = page.getByTestId("construction-contract-draft");
  await form.locator("#supply-contract-vendor").click();
  await page.getByRole("option", { name: /Structural crane partner/ }).click();
  await form.locator("#supply-contract-type").click();
  await page.getByRole("option", { name: /Subcontract/ }).click();
  await form.locator("#supply-contract-value").fill("100000");
  await form.locator("#supply-contract-value").blur();
  await expect(form.locator("#supply-contract-value")).toHaveValue("100.000");
  await form.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(form.getByRole("alert")).toBeVisible();
  await form.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(form).toHaveCount(0);
  expect(draftAttempts).toHaveLength(2);
  expect(draftAttempts[0].key).toBeTruthy();
  expect(draftAttempts[1].key).toBe(draftAttempts[0].key);
  expect(draftAttempts[1].body.value).toBe(100000);
});

test("view-only procurement has no write controls", async ({ page, baseURL }) => {
  await prepare(page, baseURL!, false);
  await expect(
    page.getByRole("button", {
      name: /Save contract link|Confirm supply|Draft downstream contract/,
    }),
  ).toHaveCount(0);
});

for (const language of ["vi", "zh", "ja"] as const) {
  test(`procurement mobile labels and layout render in ${language}`, async ({ page, baseURL }) => {
    await page.setViewportSize({ width: 390, height: 900 });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await prepare(page, baseURL!, true, language);
    const title = {
      vi: "Máy và nhân công đã duyệt",
      zh: "已批准的设备和劳务",
      ja: "承認済み機材・労務",
    }[language];
    await expect(page.getByRole("tab", { name: title, exact: true })).toBeVisible();
    await expect(page.getByTestId("construction-procurement-1")).not.toContainText(
      "construction.handoff.",
    );
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    expect(errors).toEqual([]);
  });
}

test("CM source drill-down hydrates a project outside the first project page", async ({
  page,
  baseURL,
}) => {
  await prepare(page, baseURL!);
  await page.route(/\/api\/(?:v1\/)?users\/me\/permissions$/, (route) =>
    route.fulfill({
      json: {
        role: "PROCUREMENT",
        roleId: null,
        permissions: ["operations.projects.view", "construction.qa-qc.view"],
      },
    }),
  );
  await page.route(/\/api\/(?:v1\/)?operational-projects(?:\?|$)/, (route) =>
    route.fulfill({
      json: {
        items: [{ id: 5, code: "PJ-FIRST-PAGE", name: "Another assigned project" }],
        total: 201,
        page: 1,
        pageSize: 200,
      },
    }),
  );
  let hydrated = false;
  await page.route(/\/api\/(?:v1\/)?operational-projects\/501$/, (route) => {
    hydrated = true;
    return route.fulfill({
      json: {
        id: 501,
        code: "PJ-ANPHU",
        name: "NICON An Phu structural works",
        status: "Active",
        customerId: 1,
      },
    });
  });
  await page.goto(`${baseURL}/admin/construction/bch/qa-qc?projectId=501`);
  await expect(page.getByTestId("qa-qc-page-project")).toContainText("PJ-ANPHU");
  expect(hydrated).toBe(true);
});

test("CM portfolio separates source directions and identifies unavailable budget", async ({
  page,
  baseURL,
}, testInfo) => {
  await prepare(page, baseURL!);
  const unavailable = { availability: "Unavailable", reasonCode: "SOURCE_NOT_CONFIGURED" };
  const available = (code: string) => ({
    availability: "Available",
    reasonCode: null,
    count: 1,
    sources: [
      {
        id: code.endsWith("A") ? 801 : 802,
        code,
        status: "CmApproved",
        route: `/admin/construction/bch/qs?projectId=501#qs-${code.endsWith("A") ? 801 : 802}`,
      },
    ],
  });
  await page.route(/\/api\/(?:v1\/)?reports\/projects(?:\?|$)/, (route) =>
    route.fulfill({
      json: {
        generatedAtUtc: "2026-10-18T08:00:00Z",
        asOfUtc: "2026-10-18T08:00:00Z",
        projects: [
          {
            project: {
              operationalProjectId: 501,
              code: "PJ-ANPHU",
              name: "NICON An Phu structural works",
              customerId: 1,
              customerName: "An Phu Company",
              status: "Active",
              drillDown: { route: "/admin/operational-projects/501" },
            },
            design: unavailable,
            construction: unavailable,
            acceptance: unavailable,
            permits: unavailable,
            contractualFinance: unavailable,
            unavailableMetrics: [],
            cmControl: {
              generatedAtUtc: "2026-10-18T08:00:00Z",
              periodPolicy: "CurrentState",
              upstream: available("QS-OWNER-A"),
              downstream: available("QS-CREW-B"),
              qaQc: { availability: "Available", count: 0, sources: [] },
              safety: {
                availability: "Unavailable",
                reasonCode: "SOURCE_PERMISSION_REQUIRED",
                count: null,
                sources: [],
              },
              budget: unavailable,
            },
          },
        ],
      },
    }),
  );
  await page.goto(`${baseURL}/admin/reports/projects`);
  await page.getByTestId("project-report-trigger-501").click();
  const panel = page.getByTestId("cm-portfolio");
  await expect(panel).toContainText("Current state, not a reporting-period snapshot");
  await expect(panel.getByRole("link", { name: "QS-OWNER-A" })).toHaveAttribute(
    "href",
    "/admin/construction/bch/qs?projectId=501#qs-801",
  );
  await expect(panel.getByRole("link", { name: "QS-CREW-B" })).toHaveAttribute(
    "href",
    "/admin/construction/bch/qs?projectId=501#qs-802",
  );
  await expect(panel).toContainText("Source-record permission required");
  await expect(panel).toContainText(
    "Verified data sources and agreed calculations are not yet available",
  );
  await panel.scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath("cm-portfolio.png"), fullPage: true });
});
