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
  const maxLengthItemCode = "C".repeat(60);
  let customerId = 0;
  let opportunityId = 0;
  let boqQuoteId = 0;
  let unitCostQuoteId = 0;
  let catalogId = 0;

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
      data: { code: catalogCode, name: `Rate catalog ${suffix}`, currency: "VND", isActive: true },
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
      "VL-E2E,Vật liệu kiểm thử,kg,2,100000,0",
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
    await expect(page.getByText(/chỉ áp dụng cho báo giá Suất đầu tư/i)).toBeVisible();
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
      expect.objectContaining({ itemCode: "BOQ-01", quantity: 25, unitPrice: 1_450_000, sortOrder: 3 }),
      expect.objectContaining({ itemCode: "BOQ-02", quantity: 1234.56, unitPrice: 85000.5, sortOrder: 4 }),
      expect.objectContaining({ itemCode: maxLengthItemCode, unit: "u".repeat(30), sortOrder: 5 }),
    ]));

    await page.goto(`${baseURL}/admin/quotes?create=1&opportunityId=${opportunityId}`, { waitUntil: "networkidle" });
  await page.getByTestId("quote-method").click();
  await page.getByRole("option", { name: /Bảng khối lượng/i }).click();
  await page.getByTestId("quote-create-boq-paste").click();
  await page.getByTestId("boq-paste-input").fill("BOQ-NEW\tDòng tạo mới\tm2\t2\t500.000");
  await page.getByTestId("boq-paste-confirm").click();
  await expect(page.getByTestId("quote-create-boq-name-0")).toHaveValue("Dòng tạo mới");
    await page.getByTestId("quote-discount").fill("7");
    await page.getByTestId("quote-vat").fill("9");
    page.once("dialog", async (dialog) => dialog.dismiss());
    await page.getByTestId("quote-method").click();
    await page.getByRole("option", { name: /Suất đầu tư/i }).click();
    await expect(page.getByTestId("quote-method")).toContainText(/Bảng khối lượng/i);
    await expect(page.getByTestId("quote-create-boq-name-0")).toHaveValue("Dòng tạo mới");
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
    await expect(page.getByTestId("quote-applied-rate")).toHaveValue("200.000");
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
      catalogUnitPricePerSqm: 200_000,
      unitPricePerSqm: 200_000,
      subtotal: 20_000_000,
      rateSource: "Catalog",
    }));
  } finally {
    if (unitCostQuoteId) await api.delete(`/api/quotes/${unitCostQuoteId}`, { headers });
    if (boqQuoteId) await api.delete(`/api/quotes/${boqQuoteId}`, { headers });
    if (opportunityId) await api.delete(`/api/opportunities/${opportunityId}`, { headers });
    if (customerId) await api.delete(`/api/customers/${customerId}`, { headers });
    if (catalogId) {
      await api.put(`/api/material-rate-catalogs/${catalogId}`, {
        headers,
        data: { code: catalogCode, name: `Rate catalog ${suffix}`, currency: "VND", isActive: false },
      });
    }
  }
});
