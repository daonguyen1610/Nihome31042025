import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 456100;
const project = {
  id: projectId,
  code: "PJ-DELETE-001",
  name: "Deletion dialog project",
  customerId: 1,
  customerName: "NICON",
  projectManagerUserId: 1,
  projectManagerName: "Project Manager",
  status: "Planning",
  startDate: null,
  endDate: null,
  opportunityCount: 1,
  quoteCount: 0,
  contractCount: 0,
  updatedAt: "2026-09-03T00:00:00Z",
  note: null,
  designProjectId: null,
  designProjectCode: null,
  rowVersion: "AAAAAAAAB9Q=",
  createdAt: "2026-09-01T00:00:00Z",
  opportunities: [],
  quotes: [],
  contracts: [],
};

test("hard-delete dialog discloses impact and requires exact typed confirmation", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let deleteBody: Record<string, unknown> | null = null;

  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleteBody = route.request().postDataJSON() as Record<string, unknown>;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(project) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "OperationalProject",
        resourceId: projectId,
        resourceLabel: `${project.code} · ${project.name}`,
        requiredConfirmation: project.code,
        planToken: "a".repeat(64),
        canDelete: true,
        totalAffected: 2,
        items: [{
          key: "operations.opportunities",
          action: "Unlink",
          count: 1,
          examples: ["Opportunity A"],
        }],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`/admin/operational-projects/${projectId}`);
  await page.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();

  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText("Opportunity A");
  await expect(dialog).toContainText("1");
  const confirmButton = dialog.getByRole("button", { name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i });
  await expect(confirmButton).toBeDisabled();
  await dialog.getByRole("textbox").fill("WRONG");
  await expect(confirmButton).toBeDisabled();
  await dialog.getByRole("textbox").fill(project.code);
  await expect(confirmButton).toBeEnabled();
  await confirmButton.click();

  await expect.poll(() => deleteBody).not.toBeNull();
  expect(deleteBody).toMatchObject({
    planToken: "a".repeat(64),
    confirmation: project.code,
    rowVersion: project.rowVersion,
  });
  await expect(page).toHaveURL(/\/admin\/operational-projects$/);
});

test("hard-delete dialog shows blockers and disables deletion", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let deleteRequested = false;
  await page.context().route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), async route => {
    if (route.request().method() === "DELETE") deleteRequested = true;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(project) });
  });
  await page.context().route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "OperationalProject",
        resourceId: projectId,
        resourceLabel: `${project.code} · ${project.name}`,
        requiredConfirmation: project.code,
        planToken: "b".repeat(64),
        canDelete: false,
        totalAffected: 2,
        items: [{
          key: "operations.pendingDocuments",
          action: "Block",
          count: 1,
          examples: ["pending.pdf"],
          resolutionLinks: [{
            label: "pending.pdf",
            url: `/admin/operational-projects/${projectId}#project-documents`,
          }],
        }],
      }),
    }));
  await page.context().route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.context().route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.context().route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`/admin/operational-projects/${projectId}`);
  await page.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();

  const dialog = page.getByRole("alertdialog");
  await expect(dialog).toContainText("pending.pdf");
  const cleanupLink = dialog.getByRole("link", { name: "pending.pdf" });
  await expect(cleanupLink).toHaveAttribute(
    "href",
    `/admin/operational-projects/${projectId}#project-documents`,
  );
  await expect(cleanupLink).toHaveAttribute("target", "_blank");
  const [cleanupPage] = await Promise.all([
    page.waitForEvent("popup"),
    cleanupLink.click(),
  ]);
  await cleanupPage.waitForLoadState("domcontentloaded");
  await expect(cleanupPage).toHaveURL(
    new RegExp(`/admin/operational-projects/${projectId}#project-documents$`),
  );
  await expect(cleanupPage.getByTestId("project-documents-trigger")).toBeVisible();
  await cleanupPage.close();
  await expect(dialog.getByRole("textbox")).toHaveCount(0);
  await expect(dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  })).toBeDisabled();
  expect(deleteRequested).toBe(false);
});

test("hard-delete dialog polls pending work and completes only after retry succeeds", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const operationId = "8df971fe-7064-4a3f-89bd-0c15d72ea108";
  let statusRequests = 0;
  let retryRequests = 0;

  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), async route => {
    if (route.request().method() === "DELETE") {
      await route.fulfill({
        status: 202,
        contentType: "application/json",
        body: JSON.stringify({
          operationId,
          status: "Processing",
          isComplete: false,
          requiresManualAction: false,
          errorCode: null,
          errorMessage: null,
        }),
      });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(project) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "OperationalProject",
        resourceId: projectId,
        resourceLabel: `${project.code} · ${project.name}`,
        requiredConfirmation: project.code,
        planToken: "d".repeat(64),
        canDelete: true,
        totalAffected: 1,
        items: [],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?hard-delete-operations/${operationId}/retry$`), async route => {
    retryRequests += 1;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        operationId,
        status: "Completed",
        isComplete: true,
        requiresManualAction: false,
        errorCode: null,
        errorMessage: null,
      }),
    });
  });
  await page.route(new RegExp(`/api/(?:v1/)?hard-delete-operations/${operationId}$`), async route => {
    statusRequests += 1;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        operationId,
        status: "Failed",
        isComplete: false,
        requiresManualAction: false,
        errorCode: "hard_delete_processing_failed",
        errorMessage: "internal detail must not be displayed",
      }),
    });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`/admin/operational-projects/${projectId}`);
  await page.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();
  const dialog = page.getByRole("alertdialog");
  await dialog.getByRole("textbox").fill(project.code);
  await dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  }).click();

  await expect(dialog).toContainText(operationId);
  await expect.poll(() => statusRequests).toBeGreaterThan(0);
  await expect(page).toHaveURL(new RegExp(`/admin/operational-projects/${projectId}$`));
  await expect(dialog).not.toContainText("internal detail must not be displayed");
  await dialog.getByRole("button", {
    name: /Retry deletion|Thử tiếp tục xoá|重试删除|削除を再試行|deletionImpact\.operation\.retry/i,
  }).click();

  await expect.poll(() => retryRequests).toBe(1);
  await expect(page).toHaveURL(/\/admin\/operational-projects$/);
});

test("Design Project deletion removes the row after confirmation", async ({ page, loginInBrowserAs }) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const designProject = {
    id: 456101,
    projectCode: "DP-DELETE-001",
    name: "Design deletion project",
    customerId: 1,
    customerName: "NICON",
    operationalProjectId: null,
    contractId: null,
    contractNumber: null,
    projectManagerUserId: null,
    projectManagerName: null,
    designLeadUserId: null,
    designLeadName: null,
    startDate: null,
    deadline: null,
    currentStage: "Concept",
    status: "Active",
    updatedAt: "2026-09-03T00:00:00Z",
  };
  let deleted = false;
  await page.route(new RegExp("/api/(?:v1/)?design-projects(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: deleted ? 0 : 1, page: 1, pageSize: 20,
        items: deleted ? [] : [designProject] }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?design-projects/${designProject.id}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "DesignProject",
        resourceId: designProject.id,
        resourceLabel: `${designProject.projectCode} · ${designProject.name}`,
        requiredConfirmation: designProject.projectCode,
        planToken: "c".repeat(64),
        canDelete: true,
        totalAffected: 1,
        items: [],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?design-projects/${designProject.id}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleted = true;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.continue();
  });

  await page.goto("/admin/design-projects");
  const table = page.getByRole("table");
  await expect(table.getByText(designProject.name)).toBeVisible();
  const row = table.getByText(designProject.name).locator("xpath=ancestor::tr");
  await row.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();
  const dialog = page.getByRole("alertdialog");
  await dialog.getByRole("textbox").fill(designProject.projectCode);
  await dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  }).click();

  await expect(table.getByText(designProject.name)).toHaveCount(0);
  expect(deleted).toBe(true);
});

test("Lead deletion uses preview confirmation and removes the row only after completion", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const lead = {
    id: 456102,
    name: "Durable deletion lead",
    companyName: "NICON",
    phone: "0900000000",
    email: null,
    sourceCode: "marketing",
    segmentCode: "unclassified",
    status: "New",
    ownerUserId: 1,
    ownerName: "Admin",
    note: null,
    convertedAt: null,
    convertedCustomerId: null,
    convertedOpportunityId: null,
    createdAt: "2026-09-03T00:00:00Z",
    updatedAt: "2026-09-03T00:00:00Z",
    rowVersion: "AAAAAAAAB9Q=",
    activities: [],
  };
  let deleted = false;
  let deleteBody: Record<string, unknown> | null = null;
  await page.route(new RegExp("/api/(?:v1/)?leads(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: deleted ? 0 : 1, page: 1, pageSize: 20, items: deleted ? [] : [lead] }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?leads/${lead.id}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "Lead",
        resourceId: lead.id,
        resourceLabel: lead.name,
        requiredConfirmation: `LEAD-${lead.id}`,
        planToken: "e".repeat(64),
        canDelete: true,
        totalAffected: 1,
        items: [],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?leads/${lead.id}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleteBody = route.request().postDataJSON() as Record<string, unknown>;
      deleted = true;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(lead) });
  });
  await page.route(new RegExp("/api/(?:v1/)?master-data/(customer_source|lead_segment)$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto("/admin/leads");
  const table = page.getByRole("table");
  await expect(table.getByText(lead.name)).toBeVisible();
  const row = table.getByText(lead.name).locator("xpath=ancestor::tr");
  await row.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();
  const dialog = page.getByRole("alertdialog");
  await dialog.getByRole("textbox").fill(`LEAD-${lead.id}`);
  await dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  }).click();

  await expect(table.getByText(lead.name)).toHaveCount(0);
  expect(deleteBody).toMatchObject({
    planToken: "e".repeat(64),
    confirmation: `LEAD-${lead.id}`,
    rowVersion: lead.rowVersion,
  });
});

test("Tender deletion uses typed preview confirmation without bulk selection", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const tender = {
    id: 456103,
    code: "TD-DELETE-001",
    name: "Durable deletion tender",
    customerId: 1,
    customerName: "NICON",
    openingDate: null,
    submissionDeadline: "2026-10-01T00:00:00Z",
    preparerUserId: 1,
    preparerName: "Admin",
    status: "Preparing",
    checklistCompletionPercent: 0,
    isDeadlineImminent: false,
    updatedAt: "2026-09-03T00:00:00Z",
  };
  let deleted = false;
  let deleteBody: Record<string, unknown> | null = null;
  await page.route(new RegExp("/api/(?:v1/)?tenders(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: deleted ? 0 : 1, page: 1, pageSize: 20, items: deleted ? [] : [tender] }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?tenders/${tender.id}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "Tender",
        resourceId: tender.id,
        resourceLabel: tender.name,
        requiredConfirmation: tender.code,
        planToken: "f".repeat(64),
        canDelete: true,
        totalAffected: 2,
        items: [{
          key: "tender.checklistItems",
          action: "Delete",
          count: 1,
          examples: ["123"],
        }],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?tenders/${tender.id}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleteBody = route.request().postDataJSON() as Record<string, unknown>;
      deleted = true;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.continue();
  });
  await page.route(new RegExp("/api/(?:v1/)?customers(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, page: 1, pageSize: 200, items: [] }),
    }));
  await page.route(new RegExp("/api/(?:v1/)?users(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, items: [] }),
    }));

  await page.goto("/admin/tenders");
  const table = page.getByRole("table");
  await expect(table.getByText(tender.name)).toBeVisible();
  await expect(table.getByRole("checkbox")).toHaveCount(0);
  const row = table.getByText(tender.name).locator("xpath=ancestor::tr");
  await row.getByRole("button", { name: /Delete|Xoá|删除|削除/i }).click();
  const dialog = page.getByRole("alertdialog");
  const confirmButton = dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  });
  await expect(confirmButton).toBeDisabled();
  await dialog.getByRole("textbox").fill(tender.code);
  await confirmButton.click();

  await expect(table.getByText(tender.name)).toHaveCount(0);
  expect(deleteBody).toMatchObject({
    planToken: "f".repeat(64),
    confirmation: tender.code,
  });
});

test("Customer deletion discloses downstream blockers without bulk selection", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const customer = {
    id: 456105,
    type: "Company",
    name: "Blocked deletion customer",
    sourceCode: "REFERRAL",
    relationshipStatus: "Signed",
    ownerUserId: 1,
    ownerName: "Admin",
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-03T00:00:00Z",
    rowVersion: "AAAAAAAAB9Q=",
    contacts: [],
    activities: [],
  };
  let deleteRequested = false;
  await page.route(new RegExp("/api/(?:v1/)?customers(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 1, page: 1, pageSize: 20, items: [customer] }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?customers/${customer.id}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "Customer",
        resourceId: customer.id,
        resourceLabel: customer.name,
        requiredConfirmation: `CUSTOMER-${customer.id}`,
        planToken: "8".repeat(64),
        canDelete: false,
        totalAffected: 6,
        items: [
          {
            key: "customer.operationalProjects",
            action: "Block",
            count: 4,
            examples: ["456106", "456107", "456108"],
            resolutionUrl: `/admin/operational-projects?customerId=${customer.id}`,
            resolutionLinks: [
              { label: "OP-001 · First project", url: "/admin/operational-projects/456106" },
              { label: "OP-002 · Second project", url: "/admin/operational-projects/456107" },
              { label: "OP-003 · Third project", url: "/admin/operational-projects/456108" },
            ],
          },
          {
            key: "customer.contracts",
            action: "Block",
            count: 1,
            examples: ["456109"],
            resolutionUrl: `/admin/contracts?customerId=${customer.id}`,
            resolutionLinks: [{ label: "HD-CUSTOMER-001", url: "/admin/contracts/456109" }],
          },
          {
            key: "customer.fileBlockers",
            action: "Block",
            count: 1,
            examples: ["unsafe.pdf"],
            resolutionUrl: "/admin/customers",
            resolutionLinks: [
              { label: "Unsafe file", url: "/admin/customers/456105" },
              { label: "External unsafe file", url: "https://example.invalid/unsafe.pdf" },
            ],
          },
        ],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?customers/${customer.id}$`), async route => {
    if (route.request().method() === "DELETE") deleteRequested = true;
    await route.fulfill({ status: 204 });
  });

  await page.goto("/admin/customers");
  const table = page.getByRole("table");
  await expect(table.getByText(customer.name)).toBeVisible();
  await expect(table.getByRole("checkbox")).toHaveCount(0);
  const row = table.getByText(customer.name).locator("xpath=ancestor::tr");
  await row.getByRole("button", { name: /Delete|Xóa|Xoá|删除|削除/i }).click();

  const dialog = page.getByRole("alertdialog");
  const detailLink = dialog.getByRole("link", {
    name: "OP-001 · First project",
  });
  await expect(detailLink).toHaveAttribute("href", "/admin/operational-projects/456106");
  await expect(detailLink).toHaveAttribute("target", "_blank");
  await expect(dialog.getByRole("link", {
    name: /View all blocking records|Xem tất cả bản ghi đang chặn|查看所有阻止记录|すべてのブロック中レコードを表示/i,
  })).toHaveAttribute("href", `/admin/operational-projects?customerId=${customer.id}`);
  await expect(dialog.getByRole("link", { name: "HD-CUSTOMER-001" })).toHaveAttribute(
    "href",
    "/admin/contracts/456109",
  );
  await expect(dialog.locator(`a[href="/admin/contracts?customerId=${customer.id}"]`)).toHaveCount(0);
  await expect(dialog.getByRole("link", { name: "Unsafe file", exact: true })).toHaveAttribute(
    "href",
    `/admin/customers/${customer.id}`,
  );
  await expect(dialog.getByRole("link", { name: "Unsafe file", exact: true })).toHaveAttribute("target", "_blank");
  await expect(dialog.getByRole("link", { name: "External unsafe file" })).toHaveCount(0);
  await expect(dialog.locator('a[href="/admin/customers"]')).toHaveCount(0);
  await expect(dialog.getByRole("textbox")).toHaveCount(0);
  await expect(dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  })).toBeDisabled();
  expect(deleteRequested).toBe(false);
});

test("Quote deletion uses shared exact confirmation without bulk selection", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const quote = {
    id: 456104,
    code: "QT-DELETE-001",
    opportunityId: 1,
    opportunityName: "Durable deletion opportunity",
    customerName: "NICON",
    operationalProjectId: null,
    ownerUserId: 1,
    ownerName: "Admin",
    version: 1,
    method: "UnitCost",
    grandTotal: 1000000,
    status: "CustomerApproved",
    validUntil: "2026-10-01T00:00:00Z",
    isExpiringSoon: false,
    updatedAt: "2026-09-03T00:00:00Z",
    rowVersion: "AAAAAAAAB9Q=",
  };
  let deleted = false;
  let deleteBody: Record<string, unknown> | null = null;
  await page.route(new RegExp("/api/(?:v1/)?quotes(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: deleted ? 0 : 1, page: 1, pageSize: 20, items: deleted ? [] : [quote] }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?quotes/${quote.id}/deletion-impact$`), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        resourceType: "Quote",
        resourceId: quote.id,
        resourceLabel: quote.code,
        requiredConfirmation: quote.code,
        planToken: "9".repeat(64),
        canDelete: true,
        totalAffected: 2,
        items: [{
          key: "quote.winningOpportunities",
          action: "Unlink",
          count: 1,
          examples: ["1"],
        }],
      }),
    }));
  await page.route(new RegExp(`/api/(?:v1/)?quotes/${quote.id}$`), async route => {
    if (route.request().method() === "DELETE") {
      deleteBody = route.request().postDataJSON() as Record<string, unknown>;
      deleted = true;
      await route.fulfill({ status: 204 });
      return;
    }
    await route.continue();
  });
  await page.route(new RegExp("/api/(?:v1/)?opportunities(?:\\?.*)?$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ total: 0, page: 1, pageSize: 100, items: [] }),
    }));

  await page.goto("/admin/quotes");
  const table = page.getByRole("table");
  await expect(table.getByText(quote.code)).toBeVisible();
  await expect(table.getByRole("checkbox")).toHaveCount(0);
  const row = table.getByText(quote.code).locator("xpath=ancestor::tr");
  await row.getByRole("button", { name: /Delete|Xóa|Xoá|删除|削除/i }).click();
  const dialog = page.getByRole("alertdialog");
  const confirmButton = dialog.getByRole("button", {
    name: /Delete permanently|Xoá vĩnh viễn|永久删除|完全に削除/i,
  });
  await dialog.getByRole("textbox").fill(`${quote.code} `);
  await expect(confirmButton).toBeDisabled();
  await dialog.getByRole("textbox").fill(quote.code);
  await confirmButton.click();

  await expect(table.getByText(quote.code)).toHaveCount(0);
  expect(deleteBody).toMatchObject({
    planToken: "9".repeat(64),
    confirmation: quote.code,
    rowVersion: quote.rowVersion,
  });
});