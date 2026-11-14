import { confirmAction } from "../fixtures/confirmation";
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
      return (
        url.pathname === "/api/material-rate-catalogs" &&
        url.searchParams.get("search") === catalogName &&
        response.request().method() === "GET"
      );
    });
    await page
      .getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i)
      .fill(catalogName);
    await searchResponse;
    await page.getByRole("button", { name: new RegExp(catalogName) }).click();

    await expect(page.getByTestId("material-rates-manual-entry")).toContainText(
      /Add and edit material rows/i,
    );
    await page.getByTestId("material-rates-line-add").click();
    await page.getByTestId("material-rates-line-code").fill("VL-MANUAL");
    await page.getByTestId("material-rates-line-name").fill("Manually entered cement");
    await page.getByTestId("material-rates-line-unit").fill("kg");
    await page.getByTestId("material-rates-line-norm").fill("2.5");
    await page.getByTestId("material-rates-line-save").click();
    await expect(page.getByTestId("material-rates-line-error")).toContainText(
      /Unit rate is required/i,
    );
    await page.getByTestId("material-rates-line-price").fill("-1");
    await page.getByTestId("material-rates-line-waste").fill("5");
    await page.getByTestId("material-rates-line-save").click();
    await expect(page.getByTestId("material-rates-line-error")).toContainText(
      /Unit rate cannot be negative/i,
    );
    await page.getByTestId("material-rates-line-price").fill("100");
    await page.getByTestId("material-rates-line-waste").fill("-1");
    await page.getByTestId("material-rates-line-save").click();
    await expect(page.getByTestId("material-rates-line-error")).toContainText(
      /Waste must be between 0 and 100/i,
    );
    await page.getByTestId("material-rates-line-waste").fill("5");
    const createLinePromise = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/investment-lines` &&
        response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-line-save").click();
    const createLineResponse = await createLinePromise;
    expect(createLineResponse.status(), await createLineResponse.text()).toBe(201);
    const createdLineRevision = await createLineResponse.json();
    const manualLine = createdLineRevision.lines.find(
      (line: { materialCode: string }) => line.materialCode === "VL-MANUAL",
    );
    expect(manualLine).toEqual(expect.objectContaining({ amountPerSqm: "262.5", sortOrder: 1 }));

    await page.getByTestId(`material-rates-line-edit-${manualLine.id}`).click();
    await page.getByTestId("material-rates-line-name").fill("Updated manually entered cement");
    await page.getByTestId("material-rates-line-norm").fill("3");
    await page.getByTestId("material-rates-line-price").fill("200");
    await page.getByTestId("material-rates-line-waste").fill("10");
    const updateLinePromise = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/investment-lines/${manualLine.id}` &&
        response.request().method() === "PUT",
    );
    await page.getByTestId("material-rates-line-save").click();
    const updateLineResponse = await updateLinePromise;
    expect(updateLineResponse.status(), await updateLineResponse.text()).toBe(200);
    expect(await updateLineResponse.json()).toEqual(
      expect.objectContaining({ totalRatePerSqm: "660" }),
    );

    await page.reload({ waitUntil: "networkidle" });
    const reloadSearchResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return (
        url.pathname === "/api/material-rate-catalogs" &&
        url.searchParams.get("search") === catalogName &&
        response.request().method() === "GET"
      );
    });
    await page
      .getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i)
      .fill(catalogName);
    await reloadSearchResponse;
    await page.getByRole("button", { name: new RegExp(catalogName) }).click();
    await expect(
      page.locator("tbody").getByText("Updated manually entered cement", { exact: true }),
    ).toBeVisible();
    await page.getByTestId(`material-rates-line-delete-${manualLine.id}`).click();
    await expect(page.getByRole("dialog")).toContainText(/total rate\/m² is recalculated/i);
    const deleteLinePromise = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/investment-lines/${manualLine.id}` &&
        response.request().method() === "DELETE",
    );
    await page.getByTestId("material-rates-line-delete-confirm").click();
    const deleteLineResponse = await deleteLinePromise;
    expect(deleteLineResponse.status(), await deleteLineResponse.text()).toBe(200);
    expect((await deleteLineResponse.json()).lines).toHaveLength(0);

    await page.getByTestId("material-rates-import-file").setInputFiles({
      name: download.suggestedFilename(),
      mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      buffer: await readFile(downloadedFormPath!),
    });
    await page.getByTestId("material-rates-import-review").click();
    const emptyFormResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import` &&
        response.request().method() === "POST",
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
    const invalidImportResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import` &&
        response.request().method() === "POST",
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
    await expect(page.getByTestId("material-rates-selected-file")).toContainText(
      "customer-rate-data.csv",
    );
    await page.getByTestId("material-rates-import-review").click();
    await expect(page.getByRole("dialog")).toContainText(
      /thay thế toàn bộ|replaces every existing line/i,
    );

    const importResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import` &&
        response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-import-confirm").click();
    expect((await importResponse).status()).toBe(200);
    await expect(
      page.getByText(/Đã nhập thành công 2 dòng|Successfully imported 2 rows/i),
    ).toBeVisible();
    await expect(page.getByTestId("material-rates-approve")).toBeEnabled();

    await page.getByTestId("material-rates-approve").click();
    const approveResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/approve` &&
        response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-decision-confirm").click();
    expect((await approveResponse).status()).toBe(200);
    await expect(page.getByTestId("material-rates-open-quotes")).toBeVisible();

    await page.getByTestId("material-rates-edit-catalog").click();
    await page.getByTestId("material-rates-catalog-active").click();
    const deactivateResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}` &&
        response.request().method() === "PUT",
    );
    await page.getByTestId("material-rates-catalog-save").click();
    expect((await deactivateResponse).status()).toBe(200);
    await page.getByTestId("material-rates-include-inactive").click();
    await expect(page.getByTestId("material-rates-edit-catalog")).toBeVisible();
    await expect(page.getByTestId("material-rates-open-quotes")).toHaveCount(0);
    await expect(
      page.getByText(/catalog is inactive|danh mục đang ngừng hoạt động/i),
    ).toBeVisible();

    await page.getByTestId("material-rates-edit-catalog").click();
    await page.getByTestId("material-rates-catalog-active").click();
    const reactivateResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}` &&
        response.request().method() === "PUT",
    );
    await page.getByTestId("material-rates-catalog-save").click();
    expect((await reactivateResponse).status()).toBe(200);
    await expect(page.getByTestId("material-rates-open-quotes")).toBeVisible();

    await expect(page.getByTestId("material-rates-retire")).toHaveCount(0);

    const bulkSearchResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return (
        url.pathname === "/api/material-rate-catalogs" &&
        url.searchParams.get("search") === `E2E Bulk ${suffix}` &&
        response.request().method() === "GET"
      );
    });
    await page
      .getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i)
      .fill(`E2E Bulk ${suffix}`);
    await bulkSearchResponse;
    await page.getByTestId(`material-rates-select-${bulkCatalogIds[0]}`).click();
    await page.getByTestId(`material-rates-select-${bulkCatalogIds[1]}`).click();
    const bulkDeleteResponses = Promise.all(
      bulkCatalogIds.map((id) =>
        page.waitForResponse(
          (response) =>
            new URL(response.url()).pathname === `/api/material-rate-catalogs/${id}` &&
            response.request().method() === "DELETE",
        ),
      ),
    );
    await page.getByRole("button", { name: /Delete selected|Xóa mục đã chọn/i }).click();
    await confirmAction(page);
    for (const response of await bulkDeleteResponses) expect(response.status()).toBe(204);
    for (const name of bulkCatalogNames)
      await expect(page.getByText(name, { exact: true })).toHaveCount(0);

    const mainSearchResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return (
        url.pathname === "/api/material-rate-catalogs" &&
        url.searchParams.get("search") === catalogName &&
        response.request().method() === "GET"
      );
    });
    await page
      .getByPlaceholder(/Tìm mã hoặc tên danh mục|Search catalog code or name/i)
      .fill(catalogName);
    await mainSearchResponse;
    await page.getByRole("button", { name: new RegExp(catalogName) }).click();
    await page.getByTestId("material-rates-delete-catalog").click();
    await expect(page.getByRole("dialog")).toContainText(/cannot be undone|không thể hoàn tác/i);
    const deleteResponse = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}` &&
        response.request().method() === "DELETE",
    );
    await page.getByTestId("material-rates-delete-confirm").click();
    expect((await deleteResponse).status()).toBe(204);
    await expect(page.getByText(catalogName, { exact: true })).toHaveCount(0);
    await expect(page.locator("body")).not.toContainText("materialRates.");

    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.getByTestId("material-rates-page")).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBeTruthy();
  });
});
