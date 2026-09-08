import { test, expect, TEST_USERS } from "../fixtures/auth";

// Real SQL-backed stack. API calls only establish authentication and locate
// the dedicated demo project; the assertions exercise the browser UI.
test("Procurement can create an RFQ through the browser", async ({ page, api, loginAs, loginInBrowserAs }, testInfo) => {
  const token = await loginAs(TEST_USERS.procurement);
  const response = await api.get("/api/operational-projects?pageSize=100", { headers: { Authorization: `Bearer ${token}` } });
  const project = (await response.json()).items.find((x: { code: string }) => x.code === "PJ-SAMPLE-RFQ");
  expect(project, "RFQ demo project seeded").toBeTruthy();
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.procurement);
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}`);
  await expect(page.getByRole("heading", { name: "RFQs & bid comparison", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Create RFQ", exact: true }).click();
  const dialog = page.getByRole("dialog");
  const title = `Browser RFQ ${Date.now()}`;
  await dialog.getByLabel("Package title", { exact: true }).fill(title);
  await dialog.getByRole("combobox", { name: "Source BOQ", exact: true }).selectOption({ index: 1 });
  await dialog.getByRole("combobox", { name: "Procurement owner", exact: true }).selectOption({ index: 1 });
  await dialog.getByLabel("Quotation deadline", { exact: true }).fill("2035-01-01T09:00");
  await dialog.getByLabel("Quantity CABLE-01", { exact: true }).fill("20");
  await dialog.getByRole("checkbox").first().check();
  await dialog.getByRole("button", { name: "Save draft", exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Issue RFQ", exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Award and create draft contract", exact: true })).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath("rfq-created-desktop.png"), fullPage: true });
  expect(errors).toEqual([]);
});

for (const viewport of [{ width: 390, height: 844 }, { width: 768, height: 1024 }]) {
  test(`RFQ list and comparison remain usable at ${viewport.width}px`, async ({ page, api, loginAs, loginInBrowserAs }, testInfo) => {
    await page.setViewportSize(viewport);
    const token = await loginAs(TEST_USERS.pm);
    const response = await api.get("/api/operational-projects?pageSize=100", { headers: { Authorization: `Bearer ${token}` } });
    const project = (await response.json()).items.find((x: { code: string }) => x.code === "PJ-SAMPLE-RFQ");
    expect(project).toBeTruthy();
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await loginInBrowserAs(page, TEST_USERS.pm);
    const errors: string[] = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}`);
    await expect(page.getByRole("heading", { name: "RFQs & bid comparison", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Create RFQ", exact: true })).toHaveCount(0);
    await page.getByLabel("Search code, title or vendor", { exact: true }).fill("RFQ-SAMPLE-002");
    await page.getByRole("button", { name: "[SAMPLE] Electrical package 2", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Bid comparison matrix", exact: true })).toBeVisible();
    const matrix = page.getByRole("region", { name: "Bid comparison matrix", exact: true });
    await expect(matrix.getByText("Not quoted", { exact: true })).toBeVisible();
    await expect(matrix.getByText("Partial quote", { exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`rfq-matrix-${viewport.width}.png`), fullPage: true });
    await page.getByRole("button", { name: "RFQ list", exact: true }).click();
    await expect(page.getByLabel("Search code, title or vendor", { exact: true })).toBeVisible();
    expect(errors).toEqual([]);
  });
}

for (const [language, title, matrixTitle] of [
  ["vi", "RFQ & so sánh giá", "Ma trận so sánh báo giá"],
  ["zh", "询价与比价", "报价比较矩阵"],
  ["ja", "見積依頼・比較", "見積比較表"],
]) {
  test(`RFQ detail renders translated content in ${language}`, async ({ page, api, loginAs, loginInBrowserAs }) => {
    const token = await loginAs(TEST_USERS.pm);
    const response = await api.get("/api/operational-projects?pageSize=100", { headers: { Authorization: `Bearer ${token}` } });
    const project = (await response.json()).items.find((x: { code: string }) => x.code === "PJ-SAMPLE-RFQ");
    const list = await api.get(`/api/operational-projects/${project.id}/procurement/rfqs?search=RFQ-SAMPLE-002`, { headers: { Authorization: `Bearer ${token}` } });
    const rfqId = (await list.json()).items[0].id;
    await page.addInitScript(value => localStorage.setItem("nicon_lang", value), language);
    await loginInBrowserAs(page, TEST_USERS.pm);
    await page.goto(`/admin/procurement-control/rfqs?projectId=${project.id}&rfqId=${rfqId}`);
    await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: matrixTitle, exact: true })).toBeVisible();
    await expect(page.getByRole("main")).not.toContainText(/rfq\.[a-z]/);
    await expect(page.getByRole("link", { name: title, exact: true })).toHaveAttribute("aria-current", "page");
  });
}
