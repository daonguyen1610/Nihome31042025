import { expect, test, TEST_USERS } from "../fixtures/auth";

const uid = () => Math.random().toString(36).slice(2, 10).toUpperCase();

test("pastes Excel BOQ safely and applies an approved material rate to a Unit Cost quote", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
  baseURL,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  const suffix = uid();
  const catalogCode = `RATE-${suffix}`;
  const boqCatalogCode = `BOQ-RATE-${suffix}`;
  const maxLengthItemCode = "C".repeat(60);
  let customerId = 0;
  let opportunityId = 0;
  let boqQuoteId = 0;
  let manualBoqQuoteId = 0;
  let catalogBoqQuoteId = 0;
  let unitCostQuoteId = 0;
  let catalogId = 0;
  let boqCatalogId = 0;

  try {
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Quote pricing customer ${suffix}`,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "Quote Pricing Contact",
          phone: `07${Date.now().toString().slice(-8)}`,
          email: `quote-pricing-${suffix.toLowerCase()}@test.example`,
          isPrimary: true,
        },
      },
    });
    expect(customerResponse.status(), await customerResponse.text()).toBe(201);
    customerId = (await customerResponse.json()).id as number;

    const opportunityResponse = await api.post("/api/opportunities", {
      headers,
      data: {
        name: `Quote pricing opportunity ${suffix}`,
        customerId,
        estimatedValue: 500_000_000,
        winProbability: 50,
      },
    });
    expect(opportunityResponse.status(), await opportunityResponse.text()).toBe(201);
    opportunityId = (await opportunityResponse.json()).id as number;

    const catalogResponse = await api.post("/api/material-rate-catalogs", {
      headers,
      data: { catalogType: "InvestmentRate", code: catalogCode, name: `Rate catalog ${suffix}`, currency: "VND", isActive: true },
    });
    expect(catalogResponse.status(), await catalogResponse.text()).toBe(201);
    catalogId = (await catalogResponse.json()).id as number;

    const revisionResponse = await api.post(`/api/material-rate-catalogs/${catalogId}/revisions`, {
      headers,
      data: { effectiveFrom: "2036-01-01", effectiveTo: "2036-12-31" },
    });
    expect(revisionResponse.status(), await revisionResponse.text()).toBe(201);
    const revisionId = (await revisionResponse.json()).id as number;
    const csv = [
      "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent",
      "VL-E2E,Vật liệu kiểm thử,kg,2,100000.0025,0",
      "",
    ].join("\r\n");
    const importResponse = await api.post(
      `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`,
      {
        headers,
        multipart: {
          file: { name: "customer-rate-data.csv", mimeType: "text/csv", buffer: Buffer.from(csv) },
        },
      },
    );
    expect(importResponse.status(), await importResponse.text()).toBe(200);
    expect((await api.post(`/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/approve`, {
      headers,
      data: { note: "Browser pricing workflow" },
    })).status()).toBe(200);

    const boqCatalogResponse = await api.post("/api/material-rate-catalogs", {
      headers,
      data: { catalogType: "Boq", code: boqCatalogCode, name: `BOQ catalog ${suffix}`, currency: "VND", isActive: true },
    });
    expect(boqCatalogResponse.status(), await boqCatalogResponse.text()).toBe(201);
    boqCatalogId = (await boqCatalogResponse.json()).id as number;
    const boqRevisionResponse = await api.post(`/api/material-rate-catalogs/${boqCatalogId}/revisions`, {
      headers,
      data: { effectiveFrom: "2036-01-01", effectiveTo: "2036-12-31" },
    });
    expect(boqRevisionResponse.status(), await boqRevisionResponse.text()).toBe(201);
    const boqRevisionId = (await boqRevisionResponse.json()).id as number;
    const boqCatalogCsv = [
      "ItemCode,ItemName,Unit,Quantity,UnitPrice",
      "CAT-BOQ-01,Hạng mục từ danh mục,m2,3,250000",
      "CAT-BOQ-MAX,Kiểm tra đơn giá biên,item,0.0001,99999999999999.99",
      "",
    ].join("\r\n");
    const boqImportResponse = await api.post(
      `/api/material-rate-catalogs/${boqCatalogId}/revisions/${boqRevisionId}/import`,
      {
        headers,
        multipart: {
          file: { name: "boq-rate-data.csv", mimeType: "text/csv", buffer: Buffer.from(boqCatalogCsv) },
        },
      },
    );
    expect(boqImportResponse.status(), await boqImportResponse.text()).toBe(200);
    expect((await api.post(`/api/material-rate-catalogs/${boqCatalogId}/revisions/${boqRevisionId}/approve`, {
      headers,
      data: { note: "Browser BOQ catalog workflow" },
    })).status()).toBe(200);

    const boqResponse = await api.post("/api/quotes", {
      headers,
      data: {
        opportunityId,
        method: "Boq",
        items: [
          { itemCode: "OLD-1", name: "Existing row 1", unit: "m2", quantity: 1, unitPrice: 100, sortOrder: 1 },
          { itemCode: "OLD-2", name: "Existing row 2", unit: "m2", quantity: 1, unitPrice: 200, sortOrder: 2 },
          { itemCode: "OLD-3", name: "Existing row 3", unit: "m2", quantity: 1, unitPrice: 300, sortOrder: 3 },
        ],
        discountPercent: 0,
        vatPercent: 10,
      },
    });
    expect(boqResponse.status(), await boqResponse.text()).toBe(201);
    boqQuoteId = (await boqResponse.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto(`${baseURL}/admin/quotes/${boqQuoteId}`, { waitUntil: "networkidle" });
    await page.getByTestId("quote-edit").click();
    await expect(page.getByTestId("quote-boq-catalog-fields")).toBeVisible();
    await page.getByTestId("boq-remove-desktop-1").click();

    await page.getByTestId("boq-paste-open").click();
    const pasteDialog = page.getByTestId("boq-paste-dialog");
    await expect(pasteDialog).toContainText(/không cần cấp quyền đọc clipboard/i);
    await page.getByTestId("boq-paste-input").fill([
      "Mã\tHạng mục\tĐơn vị\tKhối lượng\tĐơn giá",
      "BOQ-01\tBê tông móng M300\tm3\t25\t1.450.000",
      "BOQ-02\tDòng lỗi\tm2\t0\t85.000",
      "BOQ-03\tKhối lượng mơ hồ\tm2\t1.234\t85.000",
      `BOQ-04\tĐơn vị quá dài\t${"a".repeat(31)}\t1\t85.000`,
      `${"C".repeat(61)}\tMã quá dài\tm2\t1\t85.000`,
      "BOQ-05\tNhóm khối lượng sai\tm2\t1234,567,890\t85.000",
      "BOQ-06\tNhóm đơn giá sai\tm2\t1\t1234,567.89",
      "BOQ-07\tNhóm đầu giá dấu phẩy sai\tm2\t1\t1234,567",
      "BOQ-08\tNhóm đầu giá dấu chấm sai\tm2\t1\t1234.567",
    ].join("\n"));
    await expect(page.getByTestId("boq-paste-errors")).toContainText("3, 4, 5, 6, 7, 8, 9, 10");
    await expect(page.getByTestId("boq-paste-confirm")).toBeDisabled();

    await page.getByTestId("boq-paste-input").fill([
      "Mã\tHạng mục\tĐơn vị\tKhối lượng\tĐơn giá",
      "BOQ-01\tBê tông móng M300\tm3\t25\t1.450.000",
      "BOQ-02\tSơn nội thất\tm2\t1.234,56\t85,000.50",
      `${maxLengthItemCode}\tMã và đơn vị đúng giới hạn\t${"u".repeat(30)}\t1\t1`,
    ].join("\n"));
    await expect(pasteDialog.getByText(/3 dòng hợp lệ/i)).toBeVisible();
    await page.getByTestId("boq-paste-confirm").click();

    const updateResponsePromise = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/quotes/${boqQuoteId}` && response.request().method() === "PUT",
    );
    await page.getByTestId("quote-save").click();
    const updateResponse = await updateResponsePromise;
    expect(updateResponse.status(), await updateResponse.text()).toBe(200);
    const updatedQuote = await updateResponse.json();
    expect(updatedQuote.items).toEqual(expect.arrayContaining([
      expect.objectContaining({ itemCode: "OLD-1", sortOrder: 1 }),
      expect.objectContaining({ itemCode: "OLD-3", sortOrder: 2 }),
      expect.objectContaining({ itemCode: "BOQ-01", quantity: "25", unitPrice: "1450000", sortOrder: 3 }),
      expect.objectContaining({ itemCode: "BOQ-02", quantity: "1234.56", unitPrice: "85000.5", sortOrder: 4 }),
      expect.objectContaining({ itemCode: maxLengthItemCode, unit: "u".repeat(30), sortOrder: 5 }),
    ]));

    await page.goto(`${baseURL}/admin/quotes?create=1&opportunityId=${opportunityId}`, { waitUntil: "networkidle" });
    await page.getByTestId("quote-method").click();
    await page.getByRole("option", { name: /Bảng khối lượng/i }).click();
    await page.getByTestId("quote-create-boq-paste").click();
    await page.getByTestId("boq-paste-input").fill("BOQ-MANUAL\tDòng BOQ thủ công\tm2\t1.0050\t1,00");
    await page.getByTestId("boq-paste-confirm").click();
    const manualCreateResponsePromise = page.waitForResponse((response) =>
      new URL(response.url()).pathname === "/api/quotes" && response.request().method() === "POST",
    );
    await page.getByTestId("quote-create-save").click();
    const manualCreateResponse = await manualCreateResponsePromise;
    expect(manualCreateResponse.status(), await manualCreateResponse.text()).toBe(201);
    const manualBoqQuote = await manualCreateResponse.json();
    manualBoqQuoteId = manualBoqQuote.id as number;
    expect(manualBoqQuote).toEqual(expect.objectContaining({
      materialRateCatalogId: null,
      pricingEffectiveDate: null,
      rateSource: "Override",
      subtotal: 1.01,
      grandTotal: 1.09,
    }));

    await page.goto(`${baseURL}/admin/quotes?create=1&opportunityId=${opportunityId}`, { waitUntil: "networkidle" });
    await page.getByTestId("quote-method").click();
    await page.getByRole("option", { name: /Bảng khối lượng/i }).click();
    await page.getByTestId("quote-boq-catalog-date").fill("2036-06-15");
    await page.getByTestId("quote-boq-catalog").click();
    await page.getByRole("option", { name: new RegExp(boqCatalogCode) }).click();
    await expect(page.getByTestId("quote-boq-catalog-apply")).toBeEnabled();
    await page.getByTestId("quote-boq-catalog-apply").click();
    await expect(page.getByTestId("quote-create-boq-name-0")).toHaveValue("Hạng mục từ danh mục");
    await page.getByTestId("quote-create-boq-name-0").fill("Hạng mục danh mục đã chỉnh sửa");
    await page.getByTestId("quote-create-boq-paste").click();
    await page.getByTestId("boq-paste-input").fill("BOQ-NEW\tDòng tạo mới\tm2\t2\t500.000");
    await page.getByTestId("boq-paste-confirm").click();
    await expect(page.getByTestId("quote-create-boq-name-1")).toHaveValue("Kiểm tra đơn giá biên");
    await expect(page.getByTestId("quote-create-boq-name-2")).toHaveValue("Dòng tạo mới");
    await page.getByTestId("quote-discount").fill("7");
    await page.getByTestId("quote-vat").fill("9");
    page.once("dialog", async (dialog) => dialog.dismiss());
    await page.getByTestId("quote-method").click();
    await page.getByRole("option", { name: /Suất đầu tư/i }).click();
    await expect(page.getByTestId("quote-method")).toContainText(/Bảng khối lượng/i);
    await expect(page.getByTestId("quote-create-boq-name-0")).toHaveValue("Hạng mục danh mục đã chỉnh sửa");
    page.once("dialog", async (dialog) => {
      expect(dialog.message()).toContain("thay thế toàn bộ dòng BOQ");
      await dialog.accept();
    });
    await page.getByTestId("quote-boq-catalog-apply").click();
    await expect(page.getByTestId("quote-create-boq-name-0")).toHaveValue("Hạng mục từ danh mục");
    await expect(page.getByTestId("quote-create-boq-name-1")).toHaveValue("Kiểm tra đơn giá biên");
    await expect(page.getByTestId("quote-create-boq-price-1")).toHaveValue("99999999999999.99");
    await expect(page.getByTestId("quote-create-boq-name-2")).toHaveCount(0);
    await page.getByTestId("quote-create-boq-name-0").fill("Hạng mục danh mục sau thay thế");
    await page.getByTestId("quote-create-boq-price-0").fill("");
    await page.getByTestId("quote-create-boq-price-0").pressSequentially("250000.25");
    const catalogBoqCreateResponsePromise = page.waitForResponse((response) =>
      new URL(response.url()).pathname === "/api/quotes" && response.request().method() === "POST",
    );
    await page.getByTestId("quote-create-save").click();
    const catalogBoqCreateResponse = await catalogBoqCreateResponsePromise;
    expect(catalogBoqCreateResponse.status(), await catalogBoqCreateResponse.text()).toBe(201);
    expect(catalogBoqCreateResponse.request().postDataJSON().items[1]).toEqual(expect.objectContaining({
      quantity: "0.0001",
      unitPrice: "99999999999999.99",
    }));
    const catalogBoqQuote = await catalogBoqCreateResponse.json();
    catalogBoqQuoteId = catalogBoqQuote.id as number;
    expect(catalogBoqQuote).toEqual(expect.objectContaining({
      materialRateCatalogId: boqCatalogId,
      materialRateRevisionId: boqRevisionId,
      pricingEffectiveDate: "2036-06-15",
      rateSource: "CatalogReference",
      rateOverrideReason: null,
      subtotal: 10_000_750_000.75,
    }));
    expect(catalogBoqQuote.items[0]).toEqual(expect.objectContaining({
      unitPrice: "250000.25",
      amount: "750000.75",
    }));
    expect(catalogBoqQuote.items[1]).toEqual(expect.objectContaining({
      quantity: "0.0001",
      unitPrice: "99999999999999.99",
      amount: "10000000000",
    }));
    await page.goto(`${baseURL}/admin/quotes/${catalogBoqQuoteId}`, { waitUntil: "networkidle" });
    await expect(page.getByText(boqCatalogCode)).toBeVisible();
    await expect(page.getByRole("cell", { name: "Hạng mục danh mục sau thay thế", exact: true })).toBeVisible();
    await page.getByTestId("quote-edit").click();
    const exactQuoteRow = page.locator("tbody tr").filter({ has: page.locator('input[value="Kiểm tra đơn giá biên"]') });
    await expect(exactQuoteRow.locator('input[type="number"]')).toHaveValue("99999999999999.99");
    await exactQuoteRow.locator("input").nth(1).fill("Kiểm tra đơn giá biên sau tải lại");
    const exactQuoteUpdatePromise = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/quotes/${catalogBoqQuoteId}` && response.request().method() === "PUT",
    );
    await page.getByTestId("quote-save").click();
    const exactQuoteUpdateResponse = await exactQuoteUpdatePromise;
    expect(exactQuoteUpdateResponse.status(), await exactQuoteUpdateResponse.text()).toBe(200);
    expect(exactQuoteUpdateResponse.request().postDataJSON().items[1]).toEqual(expect.objectContaining({
      unitPrice: "99999999999999.99",
    }));
    expect(await exactQuoteUpdateResponse.text()).toContain('"unitPrice":"99999999999999.99"');

    await page.goto(`${baseURL}/admin/quotes?create=1&opportunityId=${opportunityId}`, { waitUntil: "networkidle" });
    await page.getByTestId("quote-method").click();
    await page.getByRole("option", { name: /Bảng khối lượng/i }).click();
    await page.getByTestId("quote-create-boq-paste").click();
    await page.getByTestId("boq-paste-input").fill("BOQ-SWITCH\tDòng kiểm thử chuyển phương thức\tm2\t1\t100.000");
    await page.getByTestId("boq-paste-confirm").click();
    await page.getByTestId("quote-discount").fill("7");
    await page.getByTestId("quote-vat").fill("9");
    page.once("dialog", async (dialog) => {
      expect(dialog.message()).toContain("xóa các dòng BOQ");
      await dialog.accept();
    });
  await page.getByTestId("quote-switch-unit-cost").click();
    await expect(page.getByTestId("quote-discount")).toHaveValue("7");
    await expect(page.getByTestId("quote-vat")).toHaveValue("9");
    await expect(page.getByText(/Chọn danh mục đang hoạt động và ngày có phiên bản Đã duyệt/i)).toBeVisible();
    await page.getByTestId("quote-rate-date").fill("2036-06-15");
    const effectiveResponsePromise = page.waitForResponse((response) =>
      new URL(response.url()).pathname === `/api/material-rate-catalogs/${catalogId}/effective`
      && new URL(response.url()).searchParams.get("onDate") === "2036-06-15",
    );
    await page.getByTestId("quote-rate-catalog").click();
    await page.getByRole("option", { name: new RegExp(catalogCode) }).click();
    expect((await effectiveResponsePromise).status()).toBe(200);
    await expect(page.getByTestId("quote-applied-rate")).toHaveValue("200000.01");
    await page.getByTestId("quote-applied-rate").press(process.platform === "darwin" ? "Meta+A" : "Control+A");
    await page.getByTestId("quote-applied-rate").pressSequentially("200000.02");
    await expect(page.getByTestId("quote-applied-rate")).toHaveValue("200000.02");
    await page.getByTestId("quote-applied-rate").fill("200000.01");
    await page.getByTestId("quote-area").fill("100");

    const createResponsePromise = page.waitForResponse((response) =>
      new URL(response.url()).pathname === "/api/quotes" && response.request().method() === "POST",
    );
    await page.getByTestId("quote-create-save").click();
    const createResponse = await createResponsePromise;
    expect(createResponse.status(), await createResponse.text()).toBe(201);
    const unitCostQuote = await createResponse.json();
    unitCostQuoteId = unitCostQuote.id as number;
    expect(unitCostQuote).toEqual(expect.objectContaining({
      materialRateCatalogId: catalogId,
      catalogUnitPricePerSqm: 200_000.01,
      unitPricePerSqm: 200_000.01,
      subtotal: 20_000_001,
      rateSource: "Catalog",
    }));
  } finally {
    if (unitCostQuoteId) await api.delete(`/api/quotes/${unitCostQuoteId}`, { headers });
    if (catalogBoqQuoteId) await api.delete(`/api/quotes/${catalogBoqQuoteId}`, { headers });
    if (manualBoqQuoteId) await api.delete(`/api/quotes/${manualBoqQuoteId}`, { headers });
    if (boqQuoteId) await api.delete(`/api/quotes/${boqQuoteId}`, { headers });
    if (opportunityId) await api.delete(`/api/opportunities/${opportunityId}`, { headers });
    if (customerId) await api.delete(`/api/customers/${customerId}`, { headers });
    if (catalogId) {
      await api.put(`/api/material-rate-catalogs/${catalogId}`, {
        headers,
        data: { catalogType: "InvestmentRate", code: catalogCode, name: `Rate catalog ${suffix}`, currency: "VND", isActive: false },
      });
    }
    if (boqCatalogId) {
      await api.put(`/api/material-rate-catalogs/${boqCatalogId}`, {
        headers,
        data: { catalogType: "Boq", code: boqCatalogCode, name: `BOQ catalog ${suffix}`, currency: "VND", isActive: false },
      });
    }
  }
});
