import { test, expect, TEST_USERS } from "../fixtures/auth";
import JSZip from "jszip";
import type { MaterialRateCatalogResponse } from "../../src/services/adminApi";

test("category forms, download and sheet changes keep BOQ fields separate", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const response = await api.get("/api/material-rate-catalogs?catalogType=Boq", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const catalogs = (await response.json()) as MaterialRateCatalogResponse[];
  const apartment = catalogs.find((item) => item.code === "BOQ-APARTMENT-FIRE")!;
  const design = catalogs.find((item) => item.code === "BOQ-DESIGN-PERMIT")!;
  expect(apartment.boqConfiguration).toBeTruthy();
  expect(design.boqConfiguration).toBeTruthy();
  const selected = [apartment, design];
  let delayApartment = false;
  let releaseApartment: (() => void) | undefined;
  let saved: { rows: { sheetKey: string; values: Record<string, string> }[] } | undefined;
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(/\/api\/material-rate-catalogs(?:\?|$)/, (route) =>
    route.request().method() === "GET" ? route.fulfill({ json: selected }) : route.continue(),
  );
  await page.route(/\/api\/material-rate-catalogs\/\d+\/revisions(?:\?|$)/, async (route) => {
    const catalogId = Number(
      route
        .request()
        .url()
        .match(/catalogs\/(\d+)/)![1],
    );
    const catalog = selected.find((item) => item.id === catalogId)!;
    if (delayApartment && catalogId === apartment.id)
      await new Promise<void>((resolve) => {
        releaseApartment = resolve;
      });
    return route.fulfill({
      json: [
        {
          id: catalog.id + 100000,
          catalogId: catalog.id,
          catalogType: "Boq",
          catalogCode: catalog.code,
          catalogName: catalog.name,
          currency: "VND",
          version: 1,
          status: "Draft",
          effectiveFrom: "2044-01-01",
          updatedAt: "2044-01-01T00:00:00Z",
          createdAt: "2044-01-01T00:00:00Z",
          lines: [],
          boqRows: [],
          totalAmount: "0",
          totalRatePerSqm: "0",
        },
      ],
    });
  });
  await page.route(/\/configured-boq-rows$/, async (route) => {
    saved = route.request().postDataJSON();
    await route.fulfill({
      json: {
        id: apartment.id + 100000,
        catalogId: apartment.id,
        catalogType: "Boq",
        version: 1,
        status: "Draft",
        effectiveFrom: "2044-01-01",
        updatedAt: "2044-01-01T00:01:00Z",
        lines: [],
        boqRows: saved!.rows,
        totalAmount: "0",
        totalRatePerSqm: "0",
      },
    });
  });
  await page.goto("/admin/material-rates/boq");
  await page.getByRole("button", { name: new RegExp(apartment.code) }).click();
  const entry = page.getByTestId("configured-boq-rows");
  await expect(entry).toBeVisible();
  for (const width of [320, 390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    if (width < 1024)
      await expect
        .poll(async () => {
          const box = await page.locator("aside").boundingBox();
          return box ? box.x + box.width : 0;
        })
        .toBeLessThanOrEqual(1);
    const template = entry.getByTestId("boq-template-download");
    await template.scrollIntoViewIfNeeded();
    await expect(template).toBeInViewport();
    const box = await template.boundingBox();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(width + 1);
    await page.screenshot({ path: test.info().outputPath(`boq-import-${width}.png`) });
  }
  await entry.getByLabel("Category Excel file").setInputFiles({
    name: "filled-boq.xlsx",
    mimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    buffer: Buffer.from("ui-selection"),
  });
  await expect(entry.getByText("filled-boq.xlsx", { exact: true })).toBeVisible();
  await expect(entry.getByRole("button", { name: "Validate file", exact: true })).toBeEnabled();
  const downloadPromise = page.waitForEvent("download");
  await entry.getByTestId("boq-template-download").click();
  const download = await downloadPromise;
  const stream = await download.createReadStream();
  const chunks: Buffer[] = [];
  for await (const chunk of stream!) chunks.push(Buffer.from(chunk));
  const zip = await JSZip.loadAsync(Buffer.concat(chunks));
  const workbook = await zip.file("xl/workbook.xml")!.async("string");
  expect(workbook).toContain('name="fire"');
  expect(workbook).not.toContain('name="site"');
  await entry.getByRole("button", { name: "Add / edit sheet entry" }).click();
  const dialog = page.getByRole("dialog");
  await page.locator("#boq-entry-code").fill("PCCC-001");
  await page.locator("#boq-entry-description").fill("Fire alarm panel");
  await page.locator("#boq-entry-unit").fill("set");
  await page.locator("#boq-entry-quantity").fill("2");
  await page.locator("#boq-entry-unit_price").fill("100000");
  await page.locator("#boq-entry-brand_origin").fill("Nicon approved origin");
  await page.locator("#boq-entry-unit").focus();
  await expect(page.locator("#boq-entry-unit_price")).toHaveValue("100.000");
  await expect(page.locator("#boq-entry-land_area")).toHaveCount(0);
  for (const viewport of [
    { width: 320, height: 568 },
    { width: 390, height: 844 },
    { width: 768, height: 1024 },
    { width: 1440, height: 900 },
  ]) {
    await page.setViewportSize(viewport);
    await expect
      .poll(async () => {
        const box = await dialog.boundingBox();
        return (
          !!box &&
          box.x >= 0 &&
          box.x + box.width <= viewport.width + 1 &&
          box.y >= 0 &&
          box.y + box.height <= viewport.height + 1
        );
      })
      .toBeTruthy();
    await dialog.locator('button[type="submit"]').scrollIntoViewIfNeeded();
    await expect(dialog.locator('button[type="submit"]')).toBeInViewport();
    await page.screenshot({ path: test.info().outputPath(`configured-${viewport.width}.png`) });
  }
  await dialog.locator('button[type="submit"]').click();
  await expect(dialog).not.toBeVisible();
  expect(saved!.rows[0].sheetKey).toBe("fire");
  expect(saved!.rows[0].values.brand_origin).toBe("Nicon approved origin");
  expect(saved!.rows[0].values.unit_price).toBe("100000");
  await page.getByRole("button", { name: new RegExp(design.code) }).click();
  await entry.getByRole("button", { name: "Add / edit sheet entry" }).click();
  await expect(page.locator("#boq-entry-land_area")).toBeVisible();
  await expect(page.locator("#boq-entry-code")).toHaveValue("");
  await expect(page.locator("#boq-entry-brand_origin")).toHaveCount(0);
  await expect(page.locator("#boq-entry-unit_price")).toHaveCount(0);
  await page.keyboard.press("Escape");
  delayApartment = true;
  await page.getByRole("button", { name: new RegExp(apartment.code) }).click();
  await expect.poll(() => !!releaseApartment).toBeTruthy();
  await page.getByRole("button", { name: new RegExp(design.code) }).click();
  await expect(page.locator("#boq-active-sheet")).toHaveValue("site");
  const oldResponse = page.waitForResponse((response) =>
    response.url().includes(`/material-rate-catalogs/${apartment.id}/revisions`),
  );
  releaseApartment!();
  await oldResponse;
  await expect(page.locator("#boq-active-sheet")).toHaveValue("site");
});
