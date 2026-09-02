import { expect, test, TEST_USERS } from "../fixtures/auth";
import { readFile } from "node:fs/promises";

const uid = () => Math.random().toString(36).slice(2, 10).toUpperCase();

test.describe("Material rate customer import workflow", () => {
  test("downloads guidance, confirms replacement, imports, and exposes Quote handoff", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const suffix = uid();
    const catalogName = `E2E Material Rates ${suffix}`;
    const catalogResponse = await api.post("/api/material-rate-catalogs", {
      headers,
      data: {
        code: `E2E-${suffix}`,
        name: catalogName,
        currency: "VND",
        isActive: true,
      },
    });
    expect(catalogResponse.ok(), await catalogResponse.text()).toBeTruthy();
    const catalogId = (await catalogResponse.json()).id as number;
    const bulkCatalogNames = [`E2E Bulk ${suffix} A`, `E2E Bulk ${suffix} B`];
    const bulkCatalogIds: number[] = [];
    for (const [index, name] of bulkCatalogNames.entries()) {
      const response = await api.post("/api/material-rate-catalogs", {
        headers,
        data: {
          code: `E2E-BULK-${suffix}-${index + 1}`,
          name,
          currency: "VND",
          isActive: true,
        },
      });
      expect(response.ok(), await response.text()).toBeTruthy();
      bulkCatalogIds.push((await response.json()).id as number);
    }

    const revisionResponse = await api.post(`/api/material-rate-catalogs/${catalogId}/revisions`, {
      headers,
      data: {
        effectiveFrom: "2035-01-01",
        effectiveTo: "2035-12-31",
        note: "Browser workflow verification",
      },
    });
    expect(revisionResponse.ok(), await revisionResponse.text()).toBeTruthy();
    const revisionId = (await revisionResponse.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(`${baseURL}/admin/material-rates`, { waitUntil: "networkidle" });

    await expect(page.getByTestId("material-rates-page")).toBeVisible();
    await expect(page.locator("body")).not.toContainText("materialRates.");

    const downloadPromise = page.waitForEvent("download");
    await page.getByTestId("material-rates-download-package").click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe("NICON-Material-Rate-Form.xlsx");
    const downloadedFormPath = await download.path();
    expect(downloadedFormPath).not.toBeNull();

    const searchResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return url.pathname === "/api/material-rate-catalogs"
        && url.searchParams.get("search") === catalogName
        && response.request().method() === "GET";
    });
    await page.getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i).fill(catalogName);
    await searchResponse;
    await page.getByRole("button", { name: new RegExp(catalogName) }).click();

    await page.getByTestId("material-rates-import-file").setInputFiles({
      name: download.suggestedFilename(),
      mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      buffer: await readFile(downloadedFormPath!),
    });
    await page.getByTestId("material-rates-import-review").click();
    const emptyFormResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`
      && response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-import-confirm").click();
    expect((await emptyFormResponse).status()).toBe(400);
    await expect(page.getByText(/must contain at least one data row/i)).toBeVisible();

    const invalidCsv = [
      "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent",
      "VL-BAD,Invalid decimal,kg,abc,100,0",
      "",
    ].join("\r\n");
    await page.getByTestId("material-rates-import-file").setInputFiles({
      name: "invalid-customer-form.csv",
      mimeType: "text/csv",
      buffer: Buffer.from(invalidCsv, "utf8"),
    });
    await page.getByTestId("material-rates-import-review").click();
    const invalidImportResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`
      && response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-import-confirm").click();
    expect((await invalidImportResponse).status()).toBe(400);
    await expect(page.getByText(/Norm\/m² must be a decimal using a period/i)).toBeVisible();
    await expect(page.locator("body")).not.toContainText("phải là số thập phân");

    const csv = [
      "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent",
      "VL-E2E-01,Gạch kiểm thử,viên,68,1450,4",
      "VL-E2E-02,Sơn kiểm thử,lít,0.35,95000,8",
      "",
    ].join("\r\n");
    await page.getByTestId("material-rates-import-file").setInputFiles({
      name: "customer-rate-data.csv",
      mimeType: "text/csv",
      buffer: Buffer.from(csv, "utf8"),
    });
    await expect(page.getByTestId("material-rates-selected-file")).toBeVisible();
    await expect(page.getByTestId("material-rates-selected-file")).toContainText("customer-rate-data.csv");
    await page.getByTestId("material-rates-import-review").click();
    await expect(page.getByRole("dialog")).toContainText(/thay thế toàn bộ|replaces every existing line/i);

    const importResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`
      && response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-import-confirm").click();
    expect((await importResponse).status()).toBe(200);
    await expect(page.getByText(/Đã nhập thành công 2 dòng|Successfully imported 2 rows/i)).toBeVisible();
    await expect(page.getByTestId("material-rates-approve")).toBeEnabled();

    await page.getByTestId("material-rates-approve").click();
    const approveResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/approve`
      && response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-decision-confirm").click();
    expect((await approveResponse).status()).toBe(200);
    await expect(page.getByTestId("material-rates-open-quotes")).toBeVisible();

    await page.getByTestId("material-rates-edit-catalog").click();
    await page.getByTestId("material-rates-catalog-active").click();
    const deactivateResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}`
      && response.request().method() === "PUT",
    );
    await page.getByTestId("material-rates-catalog-save").click();
    expect((await deactivateResponse).status()).toBe(200);
    await page.getByTestId("material-rates-include-inactive").click();
    await expect(page.getByTestId("material-rates-edit-catalog")).toBeVisible();
    await expect(page.getByTestId("material-rates-open-quotes")).toHaveCount(0);
    await expect(page.getByText(/catalog is inactive|danh mục đang ngừng hoạt động/i)).toBeVisible();

    await page.getByTestId("material-rates-edit-catalog").click();
    await page.getByTestId("material-rates-catalog-active").click();
    const reactivateResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}`
      && response.request().method() === "PUT",
    );
    await page.getByTestId("material-rates-catalog-save").click();
    expect((await reactivateResponse).status()).toBe(200);
    await expect(page.getByTestId("material-rates-open-quotes")).toBeVisible();

    await expect(page.getByTestId("material-rates-retire")).toHaveCount(0);

    const bulkSearchResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return url.pathname === "/api/material-rate-catalogs"
        && url.searchParams.get("search") === `E2E Bulk ${suffix}`
        && response.request().method() === "GET";
    });
    await page.getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i).fill(`E2E Bulk ${suffix}`);
    await bulkSearchResponse;
    await page.getByTestId(`material-rates-select-${bulkCatalogIds[0]}`).click();
    await page.getByTestId(`material-rates-select-${bulkCatalogIds[1]}`).click();
    page.once("dialog", (dialog) => dialog.accept());
    const bulkDeleteResponses = Promise.all(bulkCatalogIds.map((id) => page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${id}`
      && response.request().method() === "DELETE",
    )));
    await page.getByRole("button", { name: /Delete selected|Xóa mục đã chọn/i }).click();
    for (const response of await bulkDeleteResponses) expect(response.status()).toBe(204);
    for (const name of bulkCatalogNames) await expect(page.getByText(name, { exact: true })).toHaveCount(0);

    const mainSearchResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return url.pathname === "/api/material-rate-catalogs"
        && url.searchParams.get("search") === catalogName
        && response.request().method() === "GET";
    });
    await page.getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i).fill(catalogName);
    await mainSearchResponse;
    await page.getByRole("button", { name: new RegExp(catalogName) }).click();
    await page.getByTestId("material-rates-delete-catalog").click();
    await expect(page.getByRole("dialog")).toContainText(/cannot be undone|không thể hoàn tác/i);
    const deleteResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}`
      && response.request().method() === "DELETE",
    );
    await page.getByTestId("material-rates-delete-confirm").click();
    expect((await deleteResponse).status()).toBe(204);
    await expect(page.getByText(catalogName, { exact: true })).toHaveCount(0);
    await expect(page.locator("body")).not.toContainText("materialRates.");

    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.getByTestId("material-rates-page")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
  });

  test("completes the BOQ catalog lifecycle with exact quantities and totals", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.setTimeout(120_000);
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const suffix = uid();
    const catalogCode = `BOQ-E2E-${suffix}`;
    const catalogName = `BOQ Browser ${suffix}`;
    const bulkCatalogNames = [`BOQ Bulk ${suffix} A`, `BOQ Bulk ${suffix} B`];
    const bulkCatalogIds: number[] = [];
    let catalogId: number | null = null;
    let revisionId: number | null = null;
    let raceCatalogId: number | null = null;

    const importCsv = async (name: string, csv: string) => {
      await page.getByTestId("material-rates-import-file").setInputFiles({
        name,
        mimeType: "text/csv",
        buffer: Buffer.from(csv, "utf8"),
      });
      await page.getByTestId("material-rates-import-review").click();
      const responsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-import-confirm").click();
      return responsePromise;
    };

    try {
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
      await page.goto(`${baseURL}/admin/material-rates/boq`, { waitUntil: "networkidle" });

      await expect(page.getByRole("heading", { name: "BOQ rate catalogs" })).toBeVisible();
      await expect(page.locator("body")).not.toContainText("materialRates.");

      const noMatch = `NO-BOQ-${suffix}`;
      const emptySearchResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname === "/api/material-rate-catalogs"
          && url.searchParams.get("catalogType") === "Boq"
          && url.searchParams.get("search") === noMatch;
      });
      const searchInput = page.getByPlaceholder(/Search catalog code or name/i);
      await searchInput.fill(noMatch);
      await emptySearchResponse;
      const emptyDetail = page.getByTestId("material-rates-empty-detail");
      await expect(emptyDetail).toBeVisible();
      expect(await emptyDetail.evaluate((element) => {
        const style = getComputedStyle(element);
        return {
          alignItems: style.alignItems,
          display: style.display,
          height: element.getBoundingClientRect().height,
          justifyContent: style.justifyContent,
        };
      })).toEqual(expect.objectContaining({
        alignItems: "center",
        display: "flex",
        height: expect.any(Number),
        justifyContent: "center",
      }));
      expect((await emptyDetail.boundingBox())!.height).toBeGreaterThanOrEqual(240);

      await page.locator("header").getByRole("button", { name: /New catalog/i }).click();
      await page.getByTestId("material-rates-catalog-code").fill(catalogCode);
      await page.getByTestId("material-rates-catalog-name").fill(catalogName);
      await page.getByTestId("material-rates-catalog-description").fill("Browser-created BOQ rate catalog");
      const createCatalogResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === "/api/material-rate-catalogs"
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-catalog-save").click();
      const createCatalogResponse = await createCatalogResponsePromise;
      expect(createCatalogResponse.status(), await createCatalogResponse.text()).toBe(201);
      const createdCatalog = await createCatalogResponse.json();
      catalogId = createdCatalog.id as number;
      expect(createdCatalog).toEqual(expect.objectContaining({
        catalogType: "Boq",
        code: catalogCode,
        currency: "VND",
        name: catalogName,
      }));
      const createdCatalogSearchResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname === "/api/material-rate-catalogs"
          && url.searchParams.get("catalogType") === "Boq"
          && url.searchParams.get("search") === catalogName;
      });
      await searchInput.fill(catalogName);
      await createdCatalogSearchResponse;
      await page.getByRole("button", { name: new RegExp(catalogName) }).click();
      await expect(page.getByRole("heading", { name: catalogName })).toBeVisible();

      await page.getByTestId("material-rates-new-revision").click();
      await page.getByTestId("material-rates-effective-from").fill("2037-01-01");
      await page.getByTestId("material-rates-effective-to").fill("2037-12-31");
      await page.getByTestId("material-rates-revision-note").fill("BOQ browser lifecycle");
      const createRevisionResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions`
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-revision-save").click();
      const createRevisionResponse = await createRevisionResponsePromise;
      expect(createRevisionResponse.status(), await createRevisionResponse.text()).toBe(201);
      const createdRevision = await createRevisionResponse.json();
      revisionId = createdRevision.id as number;
      expect(createdRevision.catalogType).toBe("Boq");

      await expect(page.getByTestId("material-rates-manual-entry")).toContainText(/Add and edit BOQ items/i);
      await expect(page.getByText(/Add items manually or import the complete Excel form/i)).toBeVisible();
      await page.getByTestId("material-rates-line-add").click();
      await page.getByTestId("material-rates-line-code").fill("AMOUNT-OVERFLOW");
      await page.getByTestId("material-rates-line-name").fill("Frontend amount overflow validation");
      await page.getByTestId("material-rates-line-unit").fill("item");
      await page.getByTestId("material-rates-line-quantity").fill("2");
      await page.getByTestId("material-rates-line-price").fill("50000000000000");
      let overflowRequestCount = 0;
      const countOverflowRequest = (request: { method: () => string; url: () => string }) => {
        if (request.method() === "POST" && new URL(request.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`) {
          overflowRequestCount += 1;
        }
      };
      page.on("request", countOverflowRequest);
      await page.getByTestId("material-rates-line-save").click();
      await expect(page.getByTestId("material-rates-line-error")).toContainText(/Amount exceeds the storage limit/i);
      expect(overflowRequestCount).toBe(0);
      page.off("request", countOverflowRequest);

      await page.getByTestId("material-rates-line-code").fill("CT-TRAC-DAC");
      await page.getByTestId("material-rates-line-name").fill("Backfill and compact foundation trenches");
      await page.getByTestId("material-rates-line-unit").fill("m3");
      await page.getByTestId("material-rates-line-quantity").fill("18.75");
      await page.getByTestId("material-rates-line-price").fill("125000");
      const manualCreateResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-line-save").click();
      const manualCreateResponse = await manualCreateResponsePromise;
      expect(manualCreateResponse.status(), await manualCreateResponse.text()).toBe(201);
      expect(await manualCreateResponse.json()).toEqual(expect.objectContaining({ totalAmount: "2343750" }));
      await expect(page.locator("tbody").getByText("Backfill and compact foundation trenches", { exact: true })).toBeVisible();

      await page.getByTestId("material-rates-line-add").click();
      await page.getByTestId("material-rates-line-code").fill("MAX-PRICE-TRANSPORT");
      await page.getByTestId("material-rates-line-name").fill("Exact maximum unit price transport check");
      await page.getByTestId("material-rates-line-unit").fill("item");
      await page.getByTestId("material-rates-line-quantity").fill("0.0001");
      await page.getByTestId("material-rates-line-price").fill("99999999999999.99");
      const exactPriceResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-line-save").click();
      const exactPriceResponse = await exactPriceResponsePromise;
      expect(exactPriceResponse.status(), await exactPriceResponse.text()).toBe(201);
      expect(exactPriceResponse.request().postDataJSON()).toEqual(expect.objectContaining({
        quantity: "0.0001",
        unitPrice: "99999999999999.99",
      }));
      expect(await exactPriceResponse.text()).toContain('"unitRate":"99999999999999.99"');
      const exactPriceRevision = await exactPriceResponse.json();
      const exactPriceLineId = exactPriceRevision.lines.find(
        (line: { materialCode: string }) => line.materialCode === "MAX-PRICE-TRANSPORT",
      ).id as number;
      expect(exactPriceRevision.lines.find(
        (line: { materialCode: string; unitRate: string }) => line.materialCode === "MAX-PRICE-TRANSPORT",
      ).unitRate).toBe("99999999999999.99");

      await page.reload({ waitUntil: "networkidle" });
      await page.getByTestId(`material-rates-line-edit-${exactPriceLineId}`).click();
      await expect(page.getByTestId("material-rates-line-price")).toHaveValue("99999999999999.99");
      await page.getByTestId("material-rates-line-name").fill("Exact maximum unit price retained after reload");
      const exactPriceUpdatePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${exactPriceLineId}`
        && response.request().method() === "PUT",
      );
      await page.getByTestId("material-rates-line-save").click();
      const exactPriceUpdateResponse = await exactPriceUpdatePromise;
      expect(exactPriceUpdateResponse.status(), await exactPriceUpdateResponse.text()).toBe(200);
      expect(exactPriceUpdateResponse.request().postDataJSON()).toEqual(expect.objectContaining({
        unitPrice: "99999999999999.99",
      }));
      expect(await exactPriceUpdateResponse.text()).toContain('"unitRate":"99999999999999.99"');

      await page.getByTestId(`material-rates-line-delete-${exactPriceLineId}`).click();
      const exactPriceDeletePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${exactPriceLineId}`
        && response.request().method() === "DELETE",
      );
      await page.getByTestId("material-rates-line-delete-confirm").click();
      expect((await exactPriceDeletePromise).status()).toBe(200);

      const downloadPromise = page.waitForEvent("download");
      await page.getByTestId("material-rates-download-package").click();
      expect((await downloadPromise).suggestedFilename()).toBe("NICON-BOQ-Rate-Form.xlsx");

      const investmentRateCsv = [
        "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent",
        "VL-WRONG,Wrong catalog type,kg,1,100,0",
        "",
      ].join("\r\n");
      const wrongHeadersResponse = await importCsv("investment-rate-template.csv", investmentRateCsv);
      expect(wrongHeadersResponse.status()).toBe(400);
      await expect(page.getByText(/Invalid CSV headers.*ItemCode,ItemName,Unit,Quantity,UnitPrice/i)).toBeVisible();
      await expect(page.locator("tbody").getByText("Backfill and compact foundation trenches", { exact: true })).toBeVisible();

      const invalidPrecisionCsv = [
        "ItemCode,ItemName,Unit,Quantity,UnitPrice",
        "BOQ-BAD-QTY,Invalid quantity,m3,1.00001,100",
        "BOQ-BAD-PRICE,Invalid unit price,kg,1,100.001",
        "",
      ].join("\r\n");
      const invalidPrecisionResponse = await importCsv("invalid-boq-precision.csv", invalidPrecisionCsv);
      expect(invalidPrecisionResponse.status()).toBe(400);
      await expect(page.getByText(/Quantity may have at most 4 decimal places/i)).toBeVisible();
      await expect(page.getByText(/Unit rate may have at most 2 decimal places/i)).toBeVisible();
      const afterInvalidImportsResponse = await api.get(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}`, { headers });
      expect(afterInvalidImportsResponse.ok(), await afterInvalidImportsResponse.text()).toBeTruthy();
      expect(await afterInvalidImportsResponse.json()).toEqual(expect.objectContaining({
        totalAmount: expect.stringMatching(/^2343750(?:\.0+)?$/),
        lines: [expect.objectContaining({ materialCode: "CT-TRAC-DAC", sortOrder: 1 })],
      }));

      const concurrentCreates = await Promise.all([
        api.post(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`, {
          headers,
          data: { itemCode: "LOCK-A", itemName: "Concurrent excavation line A", unit: "m3", quantity: 1, unitPrice: 1000 },
        }),
        api.post(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`, {
          headers,
          data: { itemCode: "LOCK-B", itemName: "Concurrent excavation line B", unit: "m3", quantity: 1, unitPrice: 1000 },
        }),
      ]);
      expect(concurrentCreates.map((response) => response.status()).sort()).toEqual([201, 201]);
      const afterConcurrentCreatesResponse = await api.get(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}`, { headers });
      const afterConcurrentCreates = await afterConcurrentCreatesResponse.json();
      expect(afterConcurrentCreates.lines.map((line: { sortOrder: number }) => line.sortOrder)).toEqual([1, 2, 3]);

      const duplicateCreates = await Promise.all([
        api.post(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`, {
          headers,
          data: { itemCode: "LOCK-DUP", itemName: "Concurrent duplicate line", unit: "m3", quantity: 1, unitPrice: 1000 },
        }),
        api.post(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`, {
          headers,
          data: { itemCode: "lock-dup", itemName: "Concurrent duplicate line", unit: "m3", quantity: 1, unitPrice: 1000 },
        }),
      ]);
      expect(duplicateCreates.map((response) => response.status()).sort()).toEqual([201, 400]);
      const duplicateFailure = duplicateCreates.find((response) => response.status() === 400)!;
      expect(await duplicateFailure.json()).toEqual(expect.objectContaining({
        messageKey: "materialRates.line.validation.duplicateCode",
      }));

      const raceCatalogResponse = await api.post("/api/material-rate-catalogs", {
        headers,
        data: { catalogType: "Boq", code: `BOQ-RACE-${suffix}`, name: `BOQ lifecycle race ${suffix}`, currency: "VND", isActive: true },
      });
      expect(raceCatalogResponse.status(), await raceCatalogResponse.text()).toBe(201);
      raceCatalogId = (await raceCatalogResponse.json()).id as number;
      const raceRevisionResponse = await api.post(`/api/material-rate-catalogs/${raceCatalogId}/revisions`, {
        headers,
        data: { effectiveFrom: "2040-01-01", effectiveTo: "2040-12-31" },
      });
      const raceRevisionId = (await raceRevisionResponse.json()).id as number;
      expect((await api.post(`/api/material-rate-catalogs/${raceCatalogId}/revisions/${raceRevisionId}/lines`, {
        headers,
        data: { itemCode: "RACE-SEED", itemName: "Seed line for lifecycle race", unit: "item", quantity: "1", unitPrice: "1000" },
      })).status()).toBe(201);
      const [raceCreateResponse, raceApproveResponse] = await Promise.all([
        api.post(`/api/material-rate-catalogs/${raceCatalogId}/revisions/${raceRevisionId}/lines`, {
          headers,
          data: { itemCode: "RACE-LINE", itemName: "Create competing with approval", unit: "item", quantity: "1", unitPrice: "1000" },
        }),
        api.post(`/api/material-rate-catalogs/${raceCatalogId}/revisions/${raceRevisionId}/approve`, {
          headers,
          data: { note: "Concurrent lifecycle decision" },
        }),
      ]);
      expect(raceApproveResponse.status(), await raceApproveResponse.text()).toBe(200);
      expect([201, 400]).toContain(raceCreateResponse.status());
      const raceRevision = await (await api.get(
        `/api/material-rate-catalogs/${raceCatalogId}/revisions/${raceRevisionId}`,
        { headers },
      )).json();
      expect(raceRevision.status).toBe("Approved");
      if (raceCreateResponse.status() === 201) {
        expect(raceRevision.lines).toEqual([
          expect.objectContaining({ materialCode: "RACE-SEED", sortOrder: 1 }),
          expect.objectContaining({ materialCode: "RACE-LINE", sortOrder: 2 }),
        ]);
      } else {
        expect(await raceCreateResponse.json()).toEqual(expect.objectContaining({ messageKey: "materialRates.line.draftOnly" }));
        expect(raceRevision.lines).toEqual([expect.objectContaining({ materialCode: "RACE-SEED", sortOrder: 1 })]);
      }
      expect((await api.delete(`/api/material-rate-catalogs/${raceCatalogId}`, { headers })).status()).toBe(204);
      raceCatalogId = null;

      const validBoqCsv = [
        "ItemCode,ItemName,Unit,Quantity,UnitPrice",
        "CT-DAT-MONG,Machine excavation for foundation trenches,m3,25.5,85000",
        "BT-LOT-M100,Lean concrete blinding grade 100,m3,2.75,1100000",
        "BT-MONG-M300,Foundation concrete grade 300 with 1x2 aggregate,m3,12.3456,1500000.25",
        "",
      ].join("\r\n");
      const validImportResponse = await importCsv("valid-boq-rates.csv", validBoqCsv);
      expect(validImportResponse.status(), await validImportResponse.text()).toBe(200);
      await expect(page.getByText(/Successfully imported 3 rows/i)).toBeVisible();
      await expect(page.locator("tbody").getByText("Machine excavation for foundation trenches", { exact: true })).toBeVisible();
      await expect(page.locator("tbody").getByText("Foundation concrete grade 300 with 1x2 aggregate", { exact: true })).toBeVisible();
      await expect(page.locator("tbody").getByText("Backfill and compact foundation trenches", { exact: true })).toHaveCount(0);
      await expect(page.getByRole("columnheader", { name: "Quantity" })).toBeVisible();
      await expect(page.getByRole("columnheader", { name: "Amount" })).toBeVisible();
      await expect(page.getByRole("columnheader", { name: /Norm/i })).toHaveCount(0);
      await expect(page.getByText(/Review the total BOQ value/i)).toBeVisible();
      await expect(page.locator("body")).not.toContainText("UnitCost Quotes");

      const persistedRevisionResponse = await api.get(`/api/material-rate-catalogs/${catalogId}/revisions`, { headers });
      expect(persistedRevisionResponse.ok(), await persistedRevisionResponse.text()).toBeTruthy();
      const persistedRevision = (await persistedRevisionResponse.json()).find((revision: { id: number }) => revision.id === revisionId);
      expect(persistedRevision).toEqual(expect.objectContaining({
        catalogType: "Boq",
        totalAmount: "23710903.0864",
      }));
      expect(persistedRevision.lines).toEqual([
        expect.objectContaining({ amountPerSqm: expect.stringMatching(/^2167500(?:\.0+)?$/), materialCode: "CT-DAT-MONG", quantity: expect.stringMatching(/^25\.5(?:0+)?$/), sortOrder: 1, unitRate: expect.stringMatching(/^85000(?:\.0+)?$/) }),
        expect.objectContaining({ amountPerSqm: expect.stringMatching(/^3025000(?:\.0+)?$/), materialCode: "BT-LOT-M100", quantity: expect.stringMatching(/^2\.75(?:0+)?$/), sortOrder: 2, unitRate: expect.stringMatching(/^1100000(?:\.0+)?$/) }),
        expect.objectContaining({ amountPerSqm: "18518403.0864", materialCode: "BT-MONG-M300", quantity: expect.stringMatching(/^12\.3456(?:0+)?$/), sortOrder: 3, unitRate: expect.stringMatching(/^1500000\.25(?:0+)?$/) }),
      ]);
      const blindingLineId = persistedRevision.lines.find((line: { materialCode: string }) => line.materialCode === "BT-LOT-M100").id as number;
      const concreteLineId = persistedRevision.lines.find((line: { materialCode: string }) => line.materialCode === "BT-MONG-M300").id as number;

      await page.getByTestId(`material-rates-line-edit-${concreteLineId}`).click();
      await page.getByTestId("material-rates-line-code").fill("ct-dat-mong");
      const duplicateResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${concreteLineId}`
        && response.request().method() === "PUT",
      );
      await page.getByTestId("material-rates-line-save").click();
      const duplicateResponse = await duplicateResponsePromise;
      expect(duplicateResponse.status()).toBe(400);
      await expect(page.getByTestId("material-rates-line-error")).toContainText(/already exists in the BOQ revision/i);

      await page.getByTestId("material-rates-line-code").fill("BT-MONG-M300");
      await page.getByTestId("material-rates-line-name").fill("Foundation concrete grade 300, concrete pump placement");
      await page.getByTestId("material-rates-line-quantity").fill("12.5");
      await page.getByTestId("material-rates-line-price").fill("1520000");
      const updateResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${concreteLineId}`
        && response.request().method() === "PUT",
      );
      await page.getByTestId("material-rates-line-save").click();
      const updateResponse = await updateResponsePromise;
      expect(updateResponse.status(), await updateResponse.text()).toBe(200);
      expect(await updateResponse.json()).toEqual(expect.objectContaining({ totalAmount: "24192500" }));
      await expect(page.locator("tbody").getByText("Foundation concrete grade 300, concrete pump placement", { exact: true })).toBeVisible();

      await page.getByTestId("material-rates-line-add").click();
      await page.getByTestId("material-rates-line-code").fill("CT-THEP-MONG");
      await page.getByTestId("material-rates-line-name").fill("Cut, bend and install foundation reinforcement steel");
      await page.getByTestId("material-rates-line-unit").fill("kg");
      await page.getByTestId("material-rates-line-quantity").fill("850.25");
      await page.getByTestId("material-rates-line-price").fill("18750.50");
      const addSteelResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-line-save").click();
      const addSteelResponse = await addSteelResponsePromise;
      expect(addSteelResponse.status(), await addSteelResponse.text()).toBe(201);
      const afterSteel = await addSteelResponse.json();
      expect(afterSteel.totalAmount).toBe("40135112.625");
      expect(afterSteel.lines.at(-1)).toEqual(expect.objectContaining({
        amountPerSqm: "15942612.625",
        materialCode: "CT-THEP-MONG",
        sortOrder: 4,
      }));

      await page.getByTestId(`material-rates-line-delete-${blindingLineId}`).click();
      await expect(page.getByRole("dialog")).toContainText(/cannot be undone/i);
      const deleteLineResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${blindingLineId}`
        && response.request().method() === "DELETE",
      );
      await page.getByTestId("material-rates-line-delete-confirm").click();
      const deleteLineResponse = await deleteLineResponsePromise;
      expect(deleteLineResponse.status(), await deleteLineResponse.text()).toBe(200);
      expect(await deleteLineResponse.json()).toEqual(expect.objectContaining({ totalAmount: expect.stringMatching(/^37110112\.625(?:0+)?$/) }));
      await expect(page.locator("tbody").getByText("Lean concrete blinding grade 100", { exact: true })).toHaveCount(0);

      const finalDraftResponse = await api.get(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}`, { headers });
      expect(finalDraftResponse.ok(), await finalDraftResponse.text()).toBeTruthy();
      const finalDraft = await finalDraftResponse.json();
      expect(finalDraft).toEqual(expect.objectContaining({ totalAmount: expect.stringMatching(/^37110112\.625(?:0+)?$/) }));
      expect(finalDraft.lines).toEqual([
        expect.objectContaining({ materialCode: "CT-DAT-MONG", sortOrder: 1 }),
        expect.objectContaining({ amountPerSqm: expect.stringMatching(/^19000000(?:\.0+)?$/), materialCode: "BT-MONG-M300", quantity: expect.stringMatching(/^12\.5(?:0+)?$/), sortOrder: 2, unitRate: expect.stringMatching(/^1520000(?:\.0+)?$/) }),
        expect.objectContaining({ amountPerSqm: expect.stringMatching(/^15942612\.625(?:0+)?$/), materialCode: "CT-THEP-MONG", quantity: expect.stringMatching(/^850\.25(?:0+)?$/), sortOrder: 3, unitRate: expect.stringMatching(/^18750\.5(?:0+)?$/) }),
      ]);

      await page.setViewportSize({ width: 390, height: 844 });
      await expect(page.getByTestId("material-rates-line-add")).toBeVisible();
      const mobileSteelCard = page.getByTestId(`material-rates-line-card-${finalDraft.lines[2].id}`);
      await expect(mobileSteelCard).toBeVisible();
      await expect(mobileSteelCard.getByRole("button", { name: /Edit/i })).toBeVisible();
      await expect(mobileSteelCard.getByRole("button", { name: /Delete row/i })).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
      await page.setViewportSize({ width: 1280, height: 720 });

      await page.getByTestId("material-rates-approve").click();
      const approveResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/approve`
        && response.request().method() === "POST",
      );
      await page.getByTestId("material-rates-decision-confirm").click();
      expect((await approveResponsePromise).status()).toBe(200);
      await expect(page.getByText(/effective revision supplies items and rates to BOQ Quotes/i)).toBeVisible();
      await expect(page.getByText(/When creating a BOQ Quote/i)).toBeVisible();
      await expect(page.getByTestId("material-rates-open-quotes")).toHaveAttribute("href", "/admin/quotes");
      await expect(page.getByTestId("material-rates-retire")).toHaveCount(0);
      await expect(page.getByTestId("material-rates-line-add")).toHaveCount(0);
      await expect(page.getByTestId(`material-rates-line-edit-${concreteLineId}`)).toHaveCount(0);

      const effectiveResponse = await api.get(`/api/material-rate-catalogs/${catalogId}/effective?onDate=2037-06-30`, { headers });
      expect(effectiveResponse.ok(), await effectiveResponse.text()).toBeTruthy();
      expect(await effectiveResponse.json()).toEqual(expect.objectContaining({
        catalogType: "Boq",
        id: revisionId,
        status: "Approved",
        totalAmount: expect.stringMatching(/^37110112\.625(?:0+)?$/),
      }));

      await page.setViewportSize({ width: 390, height: 844 });
      await expect(page.locator("main ul.grid").getByText("Foundation concrete grade 300, concrete pump placement", { exact: true })).toBeVisible();
      await expect(page.locator("main ul.grid").getByText("Cut, bend and install foundation reinforcement steel", { exact: true })).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
      await page.setViewportSize({ width: 1280, height: 720 });

      for (const [index, name] of bulkCatalogNames.entries()) {
        const response = await api.post("/api/material-rate-catalogs", {
          headers,
          data: {
            catalogType: "Boq",
            code: `BOQ-BULK-${suffix}-${index + 1}`,
            name,
            currency: "VND",
            isActive: true,
          },
        });
        expect(response.ok(), await response.text()).toBeTruthy();
        bulkCatalogIds.push((await response.json()).id as number);
      }
      const bulkSearchResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname === "/api/material-rate-catalogs"
          && url.searchParams.get("catalogType") === "Boq"
          && url.searchParams.get("search") === `BOQ Bulk ${suffix}`;
      });
      await searchInput.fill(`BOQ Bulk ${suffix}`);
      await bulkSearchResponse;
      for (const id of bulkCatalogIds) await page.getByTestId(`material-rates-select-${id}`).click();
      const blockedCatalogId = bulkCatalogIds[1];
      const blockedDeleteUrl = `${baseURL}/api/material-rate-catalogs/${blockedCatalogId}`;
      await page.route(blockedDeleteUrl, async (route) => {
        await route.fulfill({
          status: 409,
          contentType: "application/json",
          body: JSON.stringify({
            message: "This catalog cannot be deleted because it is used by a Quote or Quote version history.",
            messageKey: "materialRates.catalog.deleteBlocked",
          }),
        });
      });
      page.once("dialog", (dialog) => dialog.accept());
      const bulkDeleteResponses = Promise.all(bulkCatalogIds.map((id) => page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${id}`
        && response.request().method() === "DELETE",
      )));
      await page.getByRole("button", { name: /Delete selected/i }).click();
      const [deletedResponse, blockedResponse] = await bulkDeleteResponses;
      expect(deletedResponse.status()).toBe(204);
      expect(blockedResponse.status()).toBe(409);
      await expect(page.getByText(bulkCatalogNames[0], { exact: true })).toHaveCount(0);
      await expect(page.getByRole("button", { name: new RegExp(bulkCatalogNames[1]) })).toBeVisible();
      await expect(page.getByTestId(`material-rates-select-${blockedCatalogId}`)).toBeChecked();
      await expect(page.getByText("1 selected", { exact: true })).toBeVisible();
      const bulkDeleteErrors = page.getByTestId("material-rates-bulk-delete-errors");
      await expect(bulkDeleteErrors).toContainText(bulkCatalogNames[1]);
      await expect(bulkDeleteErrors).toContainText(/used by a Quote or Quote version history/i);

      await page.unroute(blockedDeleteUrl);
      page.once("dialog", (dialog) => dialog.accept());
      const retryDeleteResponse = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${blockedCatalogId}`
        && response.request().method() === "DELETE",
      );
      await page.getByRole("button", { name: /Delete selected/i }).click();
      expect((await retryDeleteResponse).status()).toBe(204);
      await expect(page.getByText(bulkCatalogNames[1], { exact: true })).toHaveCount(0);
      await expect(page.getByTestId("material-rates-bulk-delete-errors")).toHaveCount(0);

      const mainSearchResponse = page.waitForResponse((response) => {
        const url = new URL(response.url());
        return url.pathname === "/api/material-rate-catalogs"
          && url.searchParams.get("search") === catalogName;
      });
      await searchInput.fill(catalogName);
      await mainSearchResponse;
      await page.getByRole("button", { name: new RegExp(catalogName) }).click();
      await page.getByTestId("material-rates-delete-catalog").click();
      await expect(page.getByRole("dialog")).toContainText(/cannot be undone/i);
      const deleteResponsePromise = page.waitForResponse((response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}`
        && response.request().method() === "DELETE",
      );
      await page.getByTestId("material-rates-delete-confirm").click();
      expect((await deleteResponsePromise).status()).toBe(204);
      catalogId = null;
      await expect(page.getByText(catalogName, { exact: true })).toHaveCount(0);
      await expect(page.locator("body")).not.toContainText("materialRates.");
    } finally {
      if (raceCatalogId) await api.delete(`/api/material-rate-catalogs/${raceCatalogId}`, { headers });
      if (catalogId) await api.delete(`/api/material-rate-catalogs/${catalogId}`, { headers });
      for (const id of bulkCatalogIds) await api.delete(`/api/material-rate-catalogs/${id}`, { headers });
    }
  });
});
