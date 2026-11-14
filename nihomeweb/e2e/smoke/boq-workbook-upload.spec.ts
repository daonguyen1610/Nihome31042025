import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { approveProjectBoq } from "../fixtures/projectBoq";

// Procurement uploads the BOQ in the input-price work package (work-package-boq-upload.spec.ts).
// The project page only lists it; the BOQ detail shows its sheets and source file.
test("project BOQ is read-only on the project page and a tender BOQ never becomes an allowance", async ({
  page,
  api,
  baseURL,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const stamp = Date.now().toString(36).toUpperCase();
  const customerId = await createOwnCustomer(api, headers, "Căn hộ dịch vụ");
  const created = await api.post("/api/operational-projects", {
    headers,
    data: { name: `Căn hộ dịch vụ ${stamp}`, customerId },
  });
  expect(created.status(), await created.text()).toBe(201);
  const projectId = (await created.json()).id as number;
  const boq = await approveProjectBoq(api, headers, projectId, [
    { name: "Đầu báo khói", unit: "cái", quantity: 50, unitPrice: 420_000 },
    { name: "Đầu báo nhiệt", unit: "cái", quantity: 70, unitPrice: 265_000 },
  ]);

  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`${baseURL}/admin/operational-projects/${projectId}`, {
    waitUntil: "networkidle",
  });
  const projectBoq = page.getByTestId("project-boq");
  await expect(projectBoq).toContainText("input-price work package");
  await expect(projectBoq.getByRole("listitem")).toHaveCount(1);
  await expect(projectBoq.getByRole("listitem").first()).toContainText("Approved (tender)");
  await expect(projectBoq.getByRole("button", { name: "Upload BOQ from Excel" })).toHaveCount(0);
  await expect(page.locator("body")).not.toContainText("boqWorkbook.");
  await page.screenshot({ path: testInfo.outputPath("project-boq-section.png") });

  await projectBoq.getByRole("link", { name: "View" }).click();
  await page.waitForURL(new RegExp(`/boq/${boq.id}$`));
  const sheets = page.getByTestId("boq-workbook-sheets");
  await expect(sheets).toContainText("ptct");
  await expect(sheets).toContainText(`Source file: boq-du-an-${projectId}.xlsx`);
  await expect(page.getByRole("heading", { name: "Tender BOQ lines" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Edit" })).toHaveCount(0);
  const storedDownload = page.waitForEvent("download");
  await page.getByTestId("boq-workbook-download").click();
  expect((await storedDownload).suggestedFilename()).toBe(`boq-du-an-${projectId}.xlsx`);

  // An approved tender BOQ is published, never the material allowance.
  const workspace = await api.get(`/api/operational-projects/${projectId}/procurement`, {
    headers,
  });
  expect((await workspace.json()).currentApprovedBoq ?? null).toBeNull();

  // Procurement control no longer offers an upload entry.
  await page.goto(`${baseURL}/admin/procurement-control?projectId=${projectId}&tab=boq`, {
    waitUntil: "networkidle",
  });
  await expect(page.getByTestId("boq-workbook-upload-link")).toHaveCount(0);

  await page.goto(`${baseURL}/admin/procurement-control/projects/${projectId}/boq/${boq.id}`, {
    waitUntil: "networkidle",
  });
  for (const [name, width, height] of [
    ["mobile", 390, 844],
    ["tablet", 820, 1180],
  ] as const) {
    await page.setViewportSize({ width, height });
    await expect(page.getByTestId("boq-workbook-sheets")).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath(`boq-detail-${name}.png`), fullPage: true });
  }
});
