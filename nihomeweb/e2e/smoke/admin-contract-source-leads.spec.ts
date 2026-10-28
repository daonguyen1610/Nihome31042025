import { test, expect, TEST_USERS } from "../fixtures/auth";

for (const width of [390, 768, 1440]) {
  test(`contract traces real converted lead at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const response = await api.get("/api/contracts?pageSize=100", { headers });
    expect(response.ok()).toBe(true);
    const contracts = (await response.json()).items;
    let matched: { id: number; leads: { id: number; name: string }[] } | undefined;
    for (const contract of contracts.filter(
      (item) => item.direction === "Upstream" && item.opportunityId != null,
    )) {
      const source = await api.get(`/api/contracts/${contract.id}/source-leads`, { headers });
      expect(source.ok()).toBe(true);
      const leads = await source.json();
      if (leads.length) {
        matched = { id: contract.id, leads };
        break;
      }
    }
    expect(
      matched,
      "Seed must provide a converted Lead → Opportunity → Contract journey",
    ).toBeTruthy();
    await page.setViewportSize({ width, height: 1000 });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await page.goto(`/admin/contracts/${matched!.id}`);
    const panel = page.getByTestId("contract-source-leads");
    await expect(panel).toBeVisible();
    const lead = matched!.leads[0];
    const link = panel.locator(`a[href="/admin/leads/${lead.id}"]`);
    await expect(link).toContainText(lead.name);
    await link.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: testInfo.outputPath(`contract-source-${width}.png`),
      fullPage: true,
    });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await link.click();
    await expect(page).toHaveURL(new RegExp(`/admin/leads/${lead.id}$`));
    await expect(
      page
        .getByRole("dialog")
        .getByRole("heading", { name: new RegExp(lead.name) })
        .first(),
    ).toBeVisible();
    expect(errors).toEqual([]);
  });
}

test("source lead panel retries and explains empty results", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const response = await api.get("/api/contracts?pageSize=100", {
    headers: { Authorization: `Bearer ${token}` },
  });
  const contracts = (await response.json()).items;
  const linked = contracts.find(
    (item) => item.direction === "Upstream" && item.opportunityId != null,
  );
  expect(linked).toBeTruthy();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  let fail = true;
  await page.route(`**/api/contracts/${linked.id}/source-leads`, (route) =>
    fail
      ? route.fulfill({ status: 503, json: { message: "Source leads temporarily unavailable" } })
      : route.fulfill({ status: 200, json: [] }),
  );
  await page.goto(`/admin/contracts/${linked.id}`);
  const panel = page.getByTestId("contract-source-leads");
  await expect(panel.getByRole("alert")).toContainText("Source leads temporarily unavailable");
  fail = false;
  await panel.getByRole("button", { name: /retry/i }).click();
  await expect(panel).toContainText("No converted source leads within your viewing scope.");
});

test("source lead panel handles direct contracts, multiple leads, languages and hidden permission", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  const response = await api.get("/api/contracts?pageSize=100", { headers });
  const linked = (await response.json()).items.find(
    (item) => item.direction === "Upstream" && item.opportunityId != null,
  );
  expect(linked).toBeTruthy();
  const detail = await (await api.get(`/api/contracts/${linked.id}`, { headers })).json();
  let direct = false;
  let hide = false;
  let sourceRequests = 0;
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => {
    if (!localStorage.getItem("nicon_lang")) localStorage.setItem("nicon_lang", "en");
  });
  await page.route(`**/api/contracts/${linked.id}`, (route) =>
    route.fulfill({ json: { ...detail, opportunityId: direct ? null : detail.opportunityId } }),
  );
  await page.route(`**/api/contracts/${linked.id}/source-leads`, (route) => {
    sourceRequests++;
    return route.fulfill({
      json: [
        { id: 991, name: "Đầu mối kinh doanh Long Thành", convertedAt: "2026-10-01T00:00:00Z" },
        { id: 992, name: "Đầu mối kỹ thuật Long Thành", convertedAt: "2026-10-02T00:00:00Z" },
      ],
    });
  });
  await page.route("**/api/users/me/permissions", async (route) => {
    const result = await route.fetch();
    const body = await result.json();
    if (hide)
      body.permissions = body.permissions.filter(
        (permission: string) => permission !== "crm.leads.view",
      );
    await route.fulfill({ response: result, json: body });
  });
  await page.goto(`/admin/contracts/${linked.id}`);
  await expect(page.getByTestId("contract-source-leads").getByRole("link")).toHaveCount(2);
  for (const [language, title] of [
    ["vi", "Lead nguồn"],
    ["zh", "来源线索"],
    ["ja", "元のリード"],
    ["en", "Source leads"],
  ]) {
    await page.evaluate((value) => localStorage.setItem("nicon_lang", value), language);
    await page.reload();
    await expect(
      page.getByTestId("contract-source-leads").getByRole("heading", { name: title, exact: true }),
    ).toBeVisible();
    await expect(page.getByTestId("contract-source-leads")).not.toContainText(
      "contracts.sourceLeads.",
    );
  }
  direct = true;
  const beforeDirect = sourceRequests;
  await page.reload();
  await expect(page.getByTestId("contract-source-leads")).toContainText(
    "No source opportunity is linked.",
  );
  expect(sourceRequests).toBe(beforeDirect);
  direct = false;
  hide = true;
  await page.reload();
  await expect(
    page.getByRole("heading", { name: detail.contractNumber, exact: true }),
  ).toBeVisible();
  await expect(page.getByTestId("contract-source-leads")).toHaveCount(0);
  expect(sourceRequests).toBe(beforeDirect);
});

test("contract detail shows and links the won source tender", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  const response = await api.get("/api/contracts?pageSize=100", { headers });
  expect(response.ok()).toBe(true);
  const linked = (await response.json()).items.find(
    (item) => item.direction === "Upstream" && item.opportunityId != null,
  );
  expect(linked).toBeTruthy();
  const detailResponse = await api.get(`/api/contracts/${linked.id}`, { headers });
  expect(detailResponse.ok()).toBe(true);
  const detail = await detailResponse.json();

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.route(new RegExp(`/api/(?:v1/)?contracts/${linked.id}$`), (route) =>
    route.fulfill({
      json: {
        ...detail,
        sourceTenderId: 991,
        sourceTenderCode: "TD-2026-0991",
        sourceTenderName: "Gói thầu nhà máy nguồn",
      },
    }),
  );

  await page.goto(`/admin/contracts/${linked.id}`);
  const tenderLink = page.locator('a[href="/admin/tenders/991"]');
  await expect(tenderLink).toHaveText("TD-2026-0991");
  await expect(page.getByText("Gói thầu nhà máy nguồn", { exact: false })).toBeVisible();
  await tenderLink.click();
  await expect(page).toHaveURL(/\/admin\/tenders\/991$/);
});
