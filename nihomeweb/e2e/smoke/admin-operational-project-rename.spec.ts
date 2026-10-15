/// <reference types="node" />
import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 456120;

test("authorized manager changes the project code and retries Drive rename", async ({
  page,
  loginInBrowserAs,
}) => {
  const jsErrors: string[] = [];
  page.on("pageerror", (error) => jsErrors.push(error.message));
  page.on("console", (message) => {
    const text = message.text();
    const isBenignDevSocketError =
      /WebSocket connection to .* failed/i.test(text) || /\[vite\]/i.test(text);
    if (message.type() === "error" && !isBenignDevSocketError) jsErrors.push(text);
  });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let detail = {
    id: projectId,
    code: "NICON-OLD-001",
    name: "Trụ sở điều hành NICON",
    customerId: 1,
    customerName: "NICON",
    projectManagerUserId: 1,
    projectManagerName: "Ban Tổng Giám đốc",
    status: "Active",
    startDate: "2026-10-01T00:00:00Z",
    endDate: null,
    opportunityCount: 0,
    quoteCount: 0,
    contractCount: 0,
    updatedAt: "2026-10-15T00:00:00Z",
    note: null,
    designProjectId: null,
    designProjectCode: null,
    rowVersion: "AAAAAAAAAAA=",
    latestCodeChange: null as null | Record<string, unknown>,
    createdAt: "2026-10-01T00:00:00Z",
    opportunities: [],
    quotes: [],
    contracts: [],
  };
  let renamePayload: Record<string, unknown> | null = null;
  let renameIdempotencyKey = "";
  let retryIdempotencyKey = "";

  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/code$`),
    async (route) => {
      if (route.request().method() !== "PATCH") return route.fallback();
      renamePayload = route.request().postDataJSON() as Record<string, unknown>;
      renameIdempotencyKey = route.request().headers()["idempotency-key"] ?? "";
      detail = {
        ...detail,
        code: "NICON-NEW-001",
        rowVersion: "AAAAAAAAAAE=",
        latestCodeChange: {
          id: 91,
          oldCode: "NICON-OLD-001",
          newCode: "NICON-NEW-001",
          reason: "Chuẩn hóa mã theo quyết định NICON",
          status: "Failed",
          attemptCount: 1,
          error: "Drive temporarily unavailable",
          changedAt: "2026-10-15T08:00:00Z",
          renamedAt: null,
        },
      };
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(detail),
      });
    },
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/code/drive-retry$`),
    async (route) => {
      retryIdempotencyKey = route.request().headers()["idempotency-key"] ?? "";
      detail = {
        ...detail,
        latestCodeChange: { ...detail.latestCodeChange!, status: "Pending", error: null },
      };
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(detail),
      });
    },
  );
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }),
  );
  await page.route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), (route) =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team/history$`),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }),
  );
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/team$`), (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        operationalProjectId: projectId,
        canManage: false,
        roleDefinitions: [],
        moduleOptions: [],
        disciplineOptions: [],
        members: [],
        assignments: [],
      }),
    }),
  );

  await page.goto(`/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });
  expect(jsErrors, "project detail must mount without browser errors").toEqual([]);
  await page.getByRole("button", { name: /Đổi mã dự án|Change project code/i }).click();
  const dialog = page.getByRole("dialog", {
    name: /Đổi mã dự án vận hành|Change operational project code/i,
  });
  await dialog.getByRole("textbox", { name: /Mã Dự án|Project code/i }).fill("nicon-new-001");
  await dialog
    .getByRole("textbox", { name: /Lý do đổi mã|Reason for change/i })
    .fill("Chuẩn hóa mã theo quyết định NICON");
  await dialog.getByRole("button", { name: /Lưu|Save/i }).click();

  await expect(page.getByText("NICON-NEW-001")).toBeVisible();
  await expect(
    page
      .locator("span")
      .filter({ hasText: /Đồng bộ thất bại|Drive folder rename status: Failed/i }),
  ).toBeVisible();
  expect(renamePayload).toMatchObject({
    newCode: "NICON-NEW-001",
    reason: "Chuẩn hóa mã theo quyết định NICON",
    rowVersion: "AAAAAAAAAAA=",
  });
  expect(renameIdempotencyKey).not.toBe("");

  await page.getByRole("button", { name: /Thử lại|Retry/i }).click();
  await expect(page.getByText(/Đang chờ đồng bộ|Pending/i)).toBeVisible();
  expect(retryIdempotencyKey).not.toBe("");
});
