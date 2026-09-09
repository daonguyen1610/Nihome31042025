import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import type { RfqDetail, RfqReferences } from "../../src/services/rfqApi";

// Public APIs prepare an isolated quotation fixture. The regression assertion
// belongs in the browser: identical numeric prices must not imply identical
// highlighting when one vendor becomes inactive.
test("RFQ matrix keeps active partial zero-price ties but removes inactive vendor highlights", async ({ page, api, loginAs, loginInBrowserAs }, testInfo) => {
  const token = await loginAs(TEST_USERS.procurement);
  const headers = { Authorization: `Bearer ${token}` };
  const projects = await api.get("/api/operational-projects?pageSize=100", { headers });
  expect(projects.ok()).toBe(true);
  const project = (await projects.json()).items.find((item: { code: string }) => item.code === "PJ-SAMPLE-RFQ");
  expect(project).toBeTruthy();
  const base = `/api/operational-projects/${project.id}/procurement/rfqs`;
  const referenceResponse = await api.get(`${base}/references`, { headers });
  expect(referenceResponse.ok()).toBe(true);
  const references: RfqReferences = await referenceResponse.json();
  const revision = references.revisions.find(item => item.lines.length >= 2);
  expect(revision).toBeTruthy();
  const suffix = randomUUID().slice(0, 8);
  const vendors: { id: number; vendorCode: string; companyName: string; vendorType: string; rowVersion: string }[] = [];
  for (const label of ["Complete", "Partial"]) {
    const phone = `090${randomUUID().replace(/\D/g, "").padEnd(7, "0").slice(0, 7)}`;
    const response = await api.post("/api/vendors", {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      // Email delivery is outside this matrix regression. Leaving it unset
      // keeps RFQ issue deterministic and avoids a synchronous SMTP call.
      data: { vendorCode: `HL-${label}-${suffix}`, companyName: `${label} highlight supplier ${suffix}`, vendorType: "Supplier", phone },
    });
    expect(response.status(), await response.text()).toBe(201);
    vendors.push(await response.json());
  }
  async function post(path: string, data: object): Promise<RfqDetail> {
    const response = await api.post(path, { headers: { ...headers, "Idempotency-Key": randomUUID() }, data });
    expect(response.ok(), await response.text()).toBe(true);
    return response.json();
  }
  let detail = await post(base, {
    title: `Active vendor highlight ${suffix}`, sourceBoqRevisionId: revision!.id,
    ownerUserId: references.owners[0].id, dueAt: "2035-01-01T09:00:00Z",
    vendorIds: vendors.map(vendor => vendor.id),
    lines: revision!.lines.slice(0, 2).map(line => ({ projectBoqLineId: line.id, quantity: Math.min(1, line.quantity) })),
  });
  const path = `${base}/${detail.header.id}`;
  detail = await post(`${path}/issue`, { rowVersion: detail.header.rowVersion });
  for (const [index, vendor] of vendors.entries()) {
    detail = await post(`${path}/bids`, {
      vendorId: vendor.id, rowVersion: detail.header.rowVersion, leadTimeDays: 7,
      paymentTerms: "Payment after delivery", validUntil: "2035-02-01T09:00:00Z", documentIds: [],
      lines: detail.lines.slice(0, index === 0 ? 2 : 1).map((line, lineIndex) => ({ rfqLineId: line.id, unitPrice: lineIndex === 0 ? 0 : 20 })),
    });
  }
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.pm);
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}&rfqId=${detail.header.id}`);
  const matrix = page.getByRole("region", { name: "Bid comparison matrix", exact: true });
  await expect(matrix).toBeVisible();
  await expect(page.getByText("Received / invited: 2/2", { exact: true })).toBeVisible();
  const firstRow = matrix.locator("tbody tr").nth(0);
  const secondRow = matrix.locator("tbody tr").nth(1);
  await expect(firstRow.locator("td").nth(0)).toHaveClass(/bg-emerald-50/);
  await expect(firstRow.locator("td").nth(1)).toHaveClass(/bg-emerald-50/);
  await expect(firstRow.locator("td").nth(1)).toContainText("0 VND");
  await expect(secondRow.locator("td").nth(1)).toHaveText("Not quoted");
  await expect(matrix).toContainText("Partial quote");

  const updated = await api.put(`/api/vendors/${vendors[0].id}`, {
    headers: { ...headers, "Idempotency-Key": randomUUID() }, data: { ...vendors[0], isActive: false },
  });
  expect(updated.ok(), await updated.text()).toBe(true);
  await page.reload();
  await expect(matrix).toBeVisible();
  await expect(firstRow.locator("td").nth(0)).not.toHaveClass(/bg-emerald-50/);
  await expect(firstRow.locator("td").nth(0)).toContainText("0 VND");
  await expect(firstRow.locator("td").nth(1)).toHaveClass(/bg-emerald-50/);
  await expect(secondRow.locator("td").nth(0)).not.toHaveClass(/bg-emerald-50/);
  await expect(secondRow.locator("td").nth(1)).toHaveText("Not quoted");
  await expect(page.getByText("Received / invited: 2/2", { exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("inactive-vendor-not-highlighted.png"), fullPage: true });
  for (const viewport of [{ width: 390, height: 844 }, { width: 768, height: 1024 }]) {
    await page.setViewportSize(viewport);
    await page.reload();
    await expect.poll(() => page.locator("aside").evaluate(sidebar => sidebar.getBoundingClientRect().right <= 0)).toBe(true);
    await expect(matrix).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`inactive-vendor-${viewport.width}.png`), fullPage: true });
  }
  expect(errors).toEqual([]);
});
