import { expect, test, TEST_USERS } from "../fixtures/auth";

test("Design overview opens its customer and contract and respects CRM permissions", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/design-projects?pageSize=100", { headers });
  expect(response.status()).toBe(200);
  const project = (await response.json()).items.find(
    (item: { contractId?: number }) => item.contractId,
  );
  expect(project, "seed must include a Design project linked to a contract").toBeTruthy();
  const detailResponse = await api.get(`/api/design-projects/${project.id}`, { headers });
  expect(detailResponse.status()).toBe(200);
  const detail = await detailResponse.json();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.goto(`/admin/design-projects/${project.id}?tab=overview`);
  const customerLink = page.locator(`a[href="/admin/customers?open=${detail.customerId}"]`);
  const contractLink = page.locator(`a[href="/admin/contracts/${detail.contractId}"]`);
  await expect(customerLink).toContainText(detail.customerName);
  await expect(contractLink).toContainText(detail.contractNumber);
  await page.screenshot({ path: testInfo.outputPath("design-source-navigation.png") });
  await customerLink.click();
  await expect(page.getByRole("dialog")).toContainText(detail.customerName);
  await page.goto(`/admin/design-projects/${project.id}?tab=overview`);
  await contractLink.click();
  await expect(page).toHaveURL(new RegExp(`/admin/contracts/${detail.contractId}$`));
  await expect(
    page.getByRole("heading", { name: detail.contractNumber, exact: true }),
  ).toBeVisible();

  const leadHeaders = { Authorization: `Bearer ${await loginAs(TEST_USERS.designLead)}` };
  const leadsProjects = await api.get("/api/design-projects?search=DP-SAMPLE-003&pageSize=10", {
    headers: leadHeaders,
  });
  expect(leadsProjects.status()).toBe(200);
  const leadProject = (await leadsProjects.json()).items[0];
  expect(leadProject).toBeTruthy();
  const permissions = await api.get("/api/users/me/permissions", { headers: leadHeaders });
  expect(permissions.status()).toBe(200);
  const granted = (await permissions.json()).permissions as string[];
  expect(granted).not.toContain("crm.customers.view");
  expect(granted).not.toContain("crm.contracts.view");
  await loginInBrowserAs(page, TEST_USERS.designLead);
  await page.goto(`/admin/design-projects/${leadProject.id}?tab=overview`);
  await expect(page.getByRole("heading", { name: leadProject.projectCode })).toBeVisible();
  await expect(page.locator('a[href^="/admin/customers?open="]')).toHaveCount(0);
  await expect(page.locator('a[href^="/admin/contracts/"]')).toHaveCount(0);
});
