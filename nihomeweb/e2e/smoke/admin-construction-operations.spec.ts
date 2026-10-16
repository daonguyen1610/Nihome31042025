import { expect, test, TEST_USERS } from "../fixtures/auth";

test("BCH resource workspace renders the assigned project on mobile", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  const projectId = 190001;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error" && !message.text().includes("WebSocket"))
      errors.push(message.text());
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });

  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        total: 1,
        page: 1,
        pageSize: 200,
        items: [{ id: projectId, code: "NICON-M4-01", name: "Nhà xưởng NICON Bình Dương" }],
      }),
    }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/construction/resource-requests$`),
    (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            id: 190101,
            code: "RR-190001-0001",
            type: "Equipment",
            status: "Draft",
            wbsCode: "WBS-STR-01",
            needByDate: "2026-10-30T00:00:00Z",
            quantity: 2,
            unit: "set",
            reason: "Tower cranes for structural works",
            projectBoqLineId: null,
            createdByUserId: 1,
            cmDecisionReason: null,
            handoffEntityType: null,
            handoffEntityId: null,
            handoffStatus: "NotReady",
            rowVersion: "AAAAAAAAAAE=",
          },
        ]),
      }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/construction/resource-references$`),
    (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          boqLines: [
            {
              id: 1101,
              itemCode: "MAT-STEEL-01",
              description: "Reinforcement steel",
              unit: "kg",
              approvedQuantity: 1000,
              remainingQuantity: 1000,
            },
          ],
          siteUsers: [{ userId: 501, userName: "Nguyen Van BCH" }],
          procurementUsers: [{ userId: 502, userName: "Tran Thi Procurement" }],
        }),
      }),
  );

  await page.goto(`${appUrl}/admin/construction/bch/resource-requests`, {
    waitUntil: "networkidle",
  });

  await expect(page.getByTestId("bch-resource-page")).toBeVisible();
  await expect(page.getByTestId("resource-row-190101")).toContainText("WBS-STR-01");
  await expect(page.getByTestId("bch-resource-page-project")).toContainText("NICON-M4-01");
  await page.getByTestId("resource-new").click();
  await page.getByTestId("resource-boq-line").click();
  await expect(page.getByRole("option", { name: /MAT-STEEL-01/ })).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByTestId("resource-site-user").click();
  await expect(page.getByRole("option", { name: "Nguyen Van BCH" })).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByTestId("resource-procurement-user").click();
  await expect(page.getByRole("option", { name: "Tran Thi Procurement" })).toBeVisible();
  expect(errors).toEqual([]);
});

test("BCH QA/QC form sends a type for each evidence document on mobile", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const appUrl = baseURL ?? "http://localhost:5043";
  const projectId = 190002;
  const errors: string[] = [];
  let submittedEvidence: unknown;
  page.on("pageerror", (error) => errors.push(error.message));
  await page.setViewportSize({ width: 390, height: 844 });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  if (page.url() === "about:blank") await page.goto(appUrl, { waitUntil: "domcontentloaded" });

  await page.route(/\/api\/(?:v1\/)?operational-projects\?.*/, (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        total: 1,
        page: 1,
        pageSize: 200,
        items: [{ id: projectId, code: "NICON-QA-01", name: "Nhà xưởng NICON Bình Dương" }],
      }),
    }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/construction/qa-qc/templates$`),
    (route) =>
      route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            id: 1001,
            code: "ISO-QA-01",
            version: 1,
            name: "Material quality dossier",
            nameVi: "Hồ sơ chất lượng vật liệu",
            nameZh: "材料质量档案",
            nameJa: "材料品質記録",
            schemaJson: "{}",
            isPublished: true,
            rowVersion: "AAAAAAAAAAE=",
          },
        ]),
      }),
  );
  await page.route(
    new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/construction/qa-qc/dossiers$`),
    async (route) => {
      if (route.request().method() === "POST") {
        submittedEvidence = route.request().postDataJSON().evidence;
        await route.fulfill({ status: 201, contentType: "application/json", body: "{}" });
      } else {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify([
            {
              id: 4001,
              code: "QA-190002-0001",
              title: "Kiểm tra bê tông móng",
              status: "Draft",
              wbsCode: "WBS-FOUNDATION-01",
              templateId: 1001,
              templateVersion: 1,
              ifcReleaseId: 2001,
              evidenceDocumentIds: [3001],
              evidence: [{ projectDocumentId: 3001, evidenceType: "LaboratoryResult" }],
              rowVersion: "AAAAAAAAAAE=",
            },
          ]),
        });
      }
    },
  );

  await page.goto(`${appUrl}/admin/construction/bch/qa-qc`, { waitUntil: "networkidle" });
  await expect(page.getByTestId("qa-qc-page")).toBeVisible();
  await expect(page.getByTestId("qa-qc-evidence-4001")).toContainText("3001");
  await expect(page.getByTestId("qa-qc-evidence-4001")).toContainText(
    /Kết quả thí nghiệm|Laboratory result/,
  );
  await page.getByTestId("qa-qc-new").click();
  await page.getByTestId("qa-qc-template").click();
  await page.getByRole("option", { name: /Hồ sơ chất lượng vật liệu/ }).click();
  await page.locator("#qa-wbs").fill("WBS-FOUNDATION-01");
  await page.locator("#qa-ifc").fill("2001");
  await page.locator("#qa-title").fill("Kiểm tra bê tông móng");
  await page.getByTestId("qa-add-evidence").click();
  await page.getByTestId("qa-evidence-document-0").fill("3001");
  await page.getByTestId("qa-evidence-type-0").click();
  await page.getByRole("option", { name: /Kết quả thí nghiệm|Laboratory result/ }).click();
  await page.getByTestId("qa-qc-save").click();

  await expect
    .poll(() => submittedEvidence)
    .toEqual([{ projectDocumentId: 3001, evidenceType: "LaboratoryResult" }]);
  expect(errors).toEqual([]);
});
