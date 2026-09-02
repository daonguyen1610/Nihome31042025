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

    await page.getByTestId("material-rates-retire").click();
    const retireResponse = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/retire`
      && response.request().method() === "POST",
    );
    await page.getByTestId("material-rates-decision-confirm").click();
    expect((await retireResponse).status()).toBe(200);
    await expect(page.getByTestId("material-rates-terminal-hint")).toBeVisible();
    await expect(page.getByTestId("material-rates-workflow-step-4")).toHaveAttribute("data-state", "complete");
    await expect(page.getByTestId("material-rates-open-quotes")).toHaveCount(0);

    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.getByTestId("material-rates-page")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
  });
});
