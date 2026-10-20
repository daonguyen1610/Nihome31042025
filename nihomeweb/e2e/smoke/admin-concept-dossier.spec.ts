import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";

test("Concept dossier renders the current revision and draft file controls", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
  baseURL,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  const customerId = await createOwnCustomer(api, headers, "concept-dossier");
  const name = `Concept browser ${Date.now().toString(36)}`;
  const projectId = await createDesignProject(api, { headers, name, customerId });
  const option = await api.post("/api/concept-options", {
    headers,
    data: { designProjectId: projectId, name: `${name} option` },
  });
  expect(option.ok(), await option.text()).toBeTruthy();
  const optionId = ((await option.json()) as { id: number }).id;

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`${baseURL}/admin/design-projects/${projectId}?tab=concept`, {
    waitUntil: "networkidle",
  });
  await page.getByRole("button", { name: /Hồ sơ và ý kiến|Files and feedback/i }).click();

  const dossier = page.getByTestId(`concept-dossier-${optionId}`);
  await expect(dossier).toBeVisible();
  await expect(dossier.getByText("R00")).toBeVisible();
  await expect(dossier.locator('input[type="file"]')).toBeVisible();
  await expect(
    dossier.getByRole("button", { name: /Gắn vào phiên bản|Attach to revision/i }),
  ).toBeDisabled();
  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 900 });
    await expect(dossier).toBeVisible();
    await expect(dossier.locator('input[type="file"]')).toBeVisible();
  }
});
