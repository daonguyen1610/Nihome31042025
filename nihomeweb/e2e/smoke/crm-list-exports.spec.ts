import { readFile } from "node:fs/promises";
import type { Page } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const EXPORT_BUTTON = /Xuất Excel|Export Excel/i;

async function downloadExport(page: Page) {
  const downloading = page.waitForEvent("download");
  await page.getByRole("button", { name: EXPORT_BUTTON }).click();
  const download = await downloading;
  const text = await readFile((await download.path())!, "utf8");
  return { name: download.suggestedFilename(), text };
}

/**
 * NICON asked for every CRM list to export to Excel with the filters on screen
 * (feedback sheet r21, r25, r26). The export is a UTF-8 CSV with a BOM so
 * Excel keeps Vietnamese text.
 */
test("customer export keeps the search filter, tax ID and readable labels", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.salesManager);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  const name = `Công ty Xuất Excel ${unique}`;
  const taxId = `07${unique.slice(-8)}`;
  let customerId = 0;

  try {
    const created = await api.post("/api/customers", {
      headers,
      data: {
        type: "Company",
        name,
        taxId,
        sourceCode: "marketing",
        primaryContact: { fullName: "Chị Lan", phone: `09${unique.slice(-8)}`, isPrimary: true },
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    customerId = (await created.json()).id as number;

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/admin/customers", { waitUntil: "networkidle" });
    // The search box is debounced; export what the filtered list shows.
    const filtered = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/api/customers" &&
        new URL(response.url()).searchParams.get("search") === unique,
    );
    await page.locator("#customer-search").fill(unique);
    await filtered;
    await expect(page.locator("table tbody tr")).toHaveCount(1);

    const { name: fileName, text } = await downloadExport(page);
    expect(fileName).toMatch(/^customers-\d{4}-\d{2}-\d{2}\.csv$/);
    expect(text.startsWith("﻿")).toBe(true);
    const lines = text.slice(1).split("\r\n");
    expect(lines[0]).toContain("Mã số thuế");
    // Only the filtered customer, with labels instead of codes.
    expect(lines).toHaveLength(2);
    expect(lines[1]).toContain(name);
    expect(lines[1]).toContain(taxId);
    expect(lines[1]).toContain("Doanh nghiệp");
    expect(lines[1]).not.toContain(",Company,");
  } finally {
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});

const LISTS = [
  { path: "/admin/leads", file: "leads", header: "Hạn phản hồi" },
  { path: "/admin/opportunities", file: "opportunities", header: "Giá trị dự kiến" },
  { path: "/admin/quotes", file: "quotes", header: "Hạn hiệu lực" },
  { path: "/admin/tenders", file: "tenders", header: "Nguồn nhận HSMT" },
  { path: "/admin/surveys", file: "surveys", header: "Ngày khảo sát" },
  { path: "/admin/operational-projects", file: "projects", header: "Mã dự án" },
  { path: "/admin/capability-documents", file: "capability-documents", header: "Tên hồ sơ" },
];

for (const list of LISTS) {
  test(`${list.path} exports its list to Excel`, async ({ page, loginInBrowserAs }) => {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(list.path, { waitUntil: "networkidle" });
    await expect(page.getByRole("button", { name: EXPORT_BUTTON })).toBeEnabled();

    const { name, text } = await downloadExport(page);
    expect(name).toMatch(new RegExp(`^${list.file}-\\d{4}-\\d{2}-\\d{2}\\.csv$`));
    expect(text.startsWith("﻿")).toBe(true);
    const [header, ...rows] = text.slice(1).split("\r\n");
    expect(header).toContain(list.header);
    expect(rows.length).toBeGreaterThan(0);
  });
}
