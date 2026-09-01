/// <reference types="node" />
import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 456000;

const detail = {
  id: projectId,
  code: "PJ-2026-0456",
  name: "NIH-456 Drive project",
  customerId: 1,
  customerName: "NICON",
  projectManagerUserId: 1,
  projectManagerName: "Project Manager",
  status: "Active",
  startDate: "2026-08-01T00:00:00Z",
  endDate: null,
  opportunityCount: 0,
  quoteCount: 0,
  contractCount: 0,
  updatedAt: "2026-08-31T00:00:00Z",
  note: null,
  designProjectId: null,
  designProjectCode: null,
  rowVersion: "AAAAAAAAB9Q=",
  createdAt: "2026-08-01T00:00:00Z",
  opportunities: [],
  quotes: [],
  contracts: [],
};

const categories = [
  ["CrmPreDesign", "01_CRM_PreDesign"],
  ["DesignConcept", "02_Thiet_ke/01_So_bo_Concept"],
  ["DesignBasic", "02_Thiet_ke/02_Co_so"],
  ["DesignShopDrawing", "02_Thiet_ke/03_Chi_tiet_ShopDrawing"],
  ["LegalPermits", "03_Xin_phep_Phap_ly"],
  ["ConstructionAcceptance", "04_Thi_cong_Nghiem_thu"],
  ["Procurement", "05_Cung_ung_Vat_tu"],
  ["FinanceContracts", "06_Tai_chinh_Hop_dong"],
  ["Survey", "01_Khao_sat"],
].map(([value, folderPath]) => ({
  value,
  folderPath,
  translationKey: `operationalProjects.documents.category.${value}`,
}));

const documentResponse = (overrides: Record<string, unknown>) => ({
  id: 1,
  operationalProjectId: projectId,
  category: "DesignBasic",
  sourceModule: "General",
  sourceType: "ManualUpload",
  sourceEntityType: null,
  sourceSlot: null,
  sourceRecordId: null,
  customerId: null,
  contractId: null,
  originalFileName: "manual-plan.pdf",
  contentType: "application/pdf",
  size: 1024,
  sha256: "a".repeat(64),
  origin: "Nicon",
  generation: 1,
  desiredOperation: "Upsert",
  syncStatus: "Pending",
  syncAttemptCount: 0,
  maxSyncAttempts: 3,
  syncError: null,
  nextSyncAttemptAt: "2026-08-31T10:00:00Z",
  driveWebViewLink: null,
  driveModifiedAt: null,
  isDownloadable: true,
  unsupportedReason: null,
  conflictState: "None",
  conflictWithDocumentId: null,
  createdAt: "2026-08-31T10:00:00Z",
  updatedAt: "2026-08-31T10:00:00Z",
  ...overrides,
});

test("project manager operates the responsive Drive document catalog", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  test.slow();
  await page.setViewportSize({ width: 390, height: 844 });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);

  let documents = [
    documentResponse({
      id: 10,
      originalFileName: "contract-source.pdf",
      category: "FinanceContracts",
      sourceModule: "Crm",
      sourceType: "ExistingManagedFile",
      sourceEntityType: "ContractAttachment",
      sourceRecordId: 100,
      syncStatus: "Synced",
      desiredOperation: "None",
      nextSyncAttemptAt: null,
    }),
    documentResponse({
      id: 11,
      originalFileName: "drive-unclassified.pdf",
      category: "Unclassified",
      sourceType: "GoogleDriveImport",
      origin: "GoogleDrive",
      syncStatus: "Synced",
      desiredOperation: "None",
      nextSyncAttemptAt: null,
      driveWebViewLink: "https://drive.google.com/file/d/drive-unclassified/view",
    }),
    documentResponse({
      id: 12,
      originalFileName: "drive-conflict.pdf",
      category: "DesignBasic",
      sourceType: "GoogleDriveImport",
      origin: "GoogleDrive",
      syncStatus: "Conflict",
      desiredOperation: "None",
      nextSyncAttemptAt: null,
      conflictState: "PendingConfirmation",
    }),
    documentResponse({
      id: 14,
      originalFileName: "delete-without-refresh.pdf",
      syncStatus: "Synced",
      desiredOperation: "None",
      nextSyncAttemptAt: null,
    }),
  ];
  const uploadIdempotencyKeys: string[] = [];
  let deleteRequested = false;

  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(new RegExp("/api/(?:v1/)?operational-projects/document-categories$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(categories) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents$`), async route => {
    if (route.request().method() === "POST") {
      uploadIdempotencyKeys.push(route.request().headers()["idempotency-key"] ?? "");
      if (uploadIdempotencyKeys.length === 1) {
        await route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({ message: "Temporary upload failure" }),
        });
        return;
      }
      documents.push(documentResponse({ id: 13, originalFileName: "new-plan.pdf" }));
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(documents.at(-1)) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(documents) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents/11/classify$`), async route => {
    documents = documents.map(document => document.id === 11
      ? { ...document, category: "DesignConcept", syncStatus: "Pending", desiredOperation: "Upsert" }
      : document);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(documents.find(document => document.id === 11)) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents/12/resolve-conflict$`), async route => {
    documents = documents.map(document => document.id === 12
      ? { ...document, conflictState: "None", syncStatus: "Synced" }
      : document);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(documents.find(document => document.id === 12)) });
  });
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/documents/14$`), async route => {
    deleteRequested = true;
    await route.fulfill({ status: 204 });
  });

  await page.goto(`${baseURL}/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });
  const section = page.getByTestId("project-documents-section");
  await expect(section).toBeVisible();
  const sourceOwnedCard = section.locator("article").filter({ hasText: "contract-source.pdf" });
  await expect(sourceOwnedCard).toBeVisible();
  await expect(sourceOwnedCard.getByRole("button", { name: /Xoá|Delete|删除|削除/i })).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

  const unclassifiedCard = section.locator("article").filter({ hasText: "drive-unclassified.pdf" });
  await unclassifiedCard.getByRole("combobox").click();
  await page.getByRole("option", { name: /Thiết kế sơ bộ|Concept design|概念设计|コンセプト設計/i }).click();
  await unclassifiedCard.getByRole("button", { name: /Phân loại|Classify|分类|分類/i }).click();
  await expect(unclassifiedCard.getByText(/Thiết kế sơ bộ|Concept design|概念设计|コンセプト設計/i)).toBeVisible();

  page.once("dialog", dialog => dialog.accept());
  const conflictCard = section.locator("article").filter({ hasText: "drive-conflict.pdf" });
  await conflictCard.getByRole("button", { name: /Giữ cả hai|Keep both|两者都保留|両方を保持/i }).click();
  await expect(conflictCard.getByRole("button", { name: /Giữ cả hai|Keep both|两者都保留|両方を保持/i })).toHaveCount(0);

  page.once("dialog", dialog => dialog.accept());
  const deletedCard = section.locator("article").filter({ hasText: "delete-without-refresh.pdf" });
  await deletedCard.getByRole("button", { name: /^(Xoá|Delete|删除).*|.*削除$/i }).click();
  await expect(deletedCard).toHaveCount(0);
  expect(deleteRequested).toBe(true);
  expect(documents.some(document => document.id === 14)).toBe(true);

  await page.locator("#project-document-file").setInputFiles({
    name: "new-plan.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.4 NIH-456"),
  });
  await page.locator("#project-document-category").click();
  await page.getByRole("option", { name: /Thiết kế cơ sở|Basic design|基础设计|基本設計/i }).click();
  const uploadButton = section.getByRole("button", { name: /^(Tải lên|Upload|上传|アップロード)$/i });
  await uploadButton.click();
  await expect.poll(() => uploadIdempotencyKeys.length).toBe(1);
  await uploadButton.click();
  await expect(section.locator("article").filter({ hasText: "new-plan.pdf" })).toBeVisible();
  expect(uploadIdempotencyKeys).toHaveLength(2);
  expect(uploadIdempotencyKeys[0]).not.toBe("");
  expect(uploadIdempotencyKeys[1]).toBe(uploadIdempotencyKeys[0]);
  expect(documents.filter(document => document.id === 13)).toHaveLength(1);
});
