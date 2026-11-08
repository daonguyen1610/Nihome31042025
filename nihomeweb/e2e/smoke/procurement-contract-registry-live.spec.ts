import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("Procurement opens the downstream contract registry for its selected project", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON – Hợp đồng cung ứng nhà xưởng");
  let projectId: number | undefined;
  try {
    const created = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Cung ứng kết cấu nhà xưởng Long Thành" },
    });
    expect(created.status(), await created.text()).toBe(201);
    projectId = (await created.json()).id;
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/");
    for (const scenario of [
      { language: "vi", width: 390, label: "Sổ hợp đồng" },
      { language: "en", width: 768, label: "Contract registry" },
      { language: "zh", width: 1440, label: "合同台账" },
      { language: "ja", width: 390, label: "契約台帳" },
    ]) {
      await page.evaluate(
        (language) => localStorage.setItem("nicon_lang", language),
        scenario.language,
      );
      await page.setViewportSize({ width: scenario.width, height: 900 });
      await page.goto(`/admin/procurement-control?projectId=${projectId}&tab=contracts`);
      const registry = page
        .getByRole("tabpanel")
        .getByRole("link", { name: scenario.label, exact: true });
      await expect(registry).toBeVisible();
      await expect(registry).toHaveAttribute(
        "href",
        `/admin/contracts?direction=Downstream&operationalProjectId=${projectId}`,
      );
      await page.screenshot({
        path: testInfo.outputPath(`procurement-contracts-${scenario.language}.png`),
      });
    }
    const list = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return (
        url.pathname.endsWith("/api/contracts") &&
        url.searchParams.get("direction") === "Downstream" &&
        url.searchParams.get("operationalProjectId") === String(projectId)
      );
    });
    await page.getByRole("tabpanel").getByRole("link", { name: "契約台帳", exact: true }).click();
    expect((await list).ok()).toBe(true);
    await expect(page).toHaveURL(/direction=Downstream/);

    // Permission rendering only: APIs remain real; this is not an API auth test.
    await page.route("**/api/users/me/permissions", async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      body.permissions = body.permissions.filter(
        (permission: string) => permission !== "crm.contracts.view",
      );
      await route.fulfill({ response, json: body });
    });
    await page.goto(`/admin/procurement-control?projectId=${projectId}&tab=contracts`);
    await expect(page.getByRole("tabpanel")).toBeVisible();
    await expect(
      page.locator(`a[href*="direction=Downstream&operationalProjectId=${projectId}"]`),
    ).toHaveCount(0);
  } finally {
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
