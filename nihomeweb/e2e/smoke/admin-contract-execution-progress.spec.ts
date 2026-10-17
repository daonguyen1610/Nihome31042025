import { test, expect, TEST_USERS } from "../fixtures/auth";

for (const width of [390, 768, 1440]) {
  test(`contract execution measures render from real sources at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const list = await api.get("/api/contracts?pageSize=100", { headers });
    expect(list.ok()).toBe(true);
    const contract = (await list.json()).items.find(
      (item) => item.direction === "Upstream" && item.operationalProjectId != null,
    );
    expect(contract).toBeTruthy();
    const result = await api.get(`/api/contracts/${contract.id}/execution-progress`, { headers });
    expect(result.ok()).toBe(true);
    const report = await result.json();
    await page.setViewportSize({ width, height: 1000 });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await page.goto(`/admin/contracts/${contract.id}`);
    const panel = page.getByTestId("contract-execution-progress");
    await expect(
      panel.getByRole("heading", { name: "Contract execution progress", exact: true }),
    ).toBeVisible();
    await expect(panel).toContainText("No combined completion percentage.");
    await expect(
      panel.getByText("Payment milestones", { exact: true }).locator(".."),
    ).toContainText(String(report.payment.milestoneCount));
    await expect(panel.getByRole("button", { name: "View payment schedule" })).toBeVisible();
    await panel.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: testInfo.outputPath(`execution-progress-${width}.png`),
      fullPage: true,
    });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await panel.getByRole("button", { name: "Refresh execution progress" }).click();
    await expect(panel.getByRole("button", { name: "Refresh execution progress" })).toBeEnabled();
    await panel.getByRole("button", { name: "View payment schedule" }).click();
    await expect(page).toHaveURL(/tab=schedule/);
    await expect(page.getByRole("tab", { name: /schedule/i })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(errors).toEqual([]);
  });
}

test("execution panel handles restricted, empty, retry and translated source previews", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  const contract = (
    await (await api.get("/api/contracts?pageSize=100", { headers })).json()
  ).items.find((item) => item.operationalProjectId != null);
  let mode = "error";
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => {
    if (!localStorage.getItem("nicon_lang")) localStorage.setItem("nicon_lang", "en");
  });
  await page.route(`**/api/contracts/${contract.id}/execution-progress`, (route) => {
    if (mode === "error")
      return route.fulfill({ status: 503, json: { message: "Progress temporarily unavailable" } });
    return route.fulfill({
      json: {
        calculatedAt: "2026-10-17T12:00:00Z",
        work:
          mode === "restricted"
            ? null
            : {
                packageCount: mode === "empty" ? 0 : 12,
                cancelledPackageCount: 1,
                taskCount: 5,
                completedTaskCount: 1,
                verifiedTaskCount: 2,
                cancelledTaskCount: 1,
                sources:
                  mode === "empty"
                    ? []
                    : [
                        {
                          id: 91,
                          code: "WP-CONSTRUCTION-01",
                          name: "Chuẩn bị hồ sơ thi công nhà máy",
                          status: "Active",
                        },
                      ],
              },
        qs:
          mode === "restricted"
            ? null
            : {
                dossierCount: mode === "empty" ? 0 : 2,
                activeApprovedCount: 1,
                pendingReviewCount: 1,
                sources:
                  mode === "empty"
                    ? []
                    : [
                        {
                          id: 92,
                          code: "QS-STRUCTURE-01",
                          name: "Nghiệm thu kết cấu tầng một",
                          status: "CmApproved",
                        },
                      ],
              },
        payment: {
          milestoneCount: 3,
          paidCount: 1,
          requestedCount: 1,
          scheduledAmount: 1000000000,
          paidScheduledAmount: 200000000,
        },
      },
    });
  });
  await page.goto(`/admin/contracts/${contract.id}`);
  const panel = page.getByTestId("contract-execution-progress");
  await expect(panel.getByRole("alert")).toContainText("Progress temporarily unavailable");
  mode = "restricted";
  await panel.getByRole("button", { name: /retry/i }).click();
  await expect(
    panel.getByText("Source access or a valid project is unavailable; figures are not disclosed."),
  ).toHaveCount(2);
  mode = "empty";
  await panel.getByRole("button", { name: "Refresh execution progress" }).click();
  await expect(panel).toContainText("No directly linked work packages within your viewing scope.");
  await expect(panel).toContainText("No QS dossiers directly linked to this contract.");
  mode = "populated";
  await panel.getByRole("button", { name: "Refresh execution progress" }).click();
  await expect(panel.getByRole("link", { name: /WP-CONSTRUCTION-01/ })).toHaveAttribute(
    "href",
    "/admin/work-packages/91",
  );
  await expect(panel.getByRole("link", { name: /QS-STRUCTURE-01/ })).toHaveAttribute(
    "href",
    `/admin/construction/bch/qs?projectId=${contract.operationalProjectId}&contractId=${contract.id}#qs-92`,
  );
  const projectA = contract.operationalProjectId;
  const projectB = projectA + 100000;
  await page.route("**/api/operational-projects?*", (route) =>
    route.fulfill({
      json: {
        items: [
          { id: projectA, code: "PJ-FACTORY", name: "Factory construction" },
          { id: projectB, code: "PJ-WAREHOUSE", name: "Warehouse construction" },
        ],
        totalCount: 2,
      },
    }),
  );
  await page.route("**/construction/qs-references", (route) =>
    route.fulfill({ json: { contracts: [], approvedBoqRevisions: [] } }),
  );
  await page.route("**/construction/qs-batches", (route) => {
    const first = route.request().url().includes(`/operational-projects/${projectA}/`);
    return route.fulfill({
      json: [
        {
          id: first ? 92 : 93,
          code: first ? "QS-STRUCTURE-01" : "QS-WAREHOUSE-01",
          contractId: first ? contract.id : contract.id + 1,
          direction: "Upstream",
          status: "CmApproved",
          wbsCode: "02.01",
          certifiedAmount: 200000000,
          periodFrom: "2026-10-01",
          periodTo: "2026-10-15",
          description: "Structural acceptance",
        },
      ],
    });
  });
  await panel.getByRole("link", { name: /QS-STRUCTURE-01/ }).click();
  await expect(page.getByTestId("qs-row-92")).toBeInViewport();
  await page.getByTestId("qs-page-project").click();
  await page.getByRole("option", { name: /PJ-WAREHOUSE/ }).click();
  await expect(page.getByTestId("qs-row-93")).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(new RegExp(`projectId=${projectA}&contractId=${contract.id}#qs-92`));
  await expect(page.getByTestId("qs-row-92")).toBeInViewport();
  await expect(page.getByTestId("qs-row-93")).toHaveCount(0);
  await page.goForward();
  await expect(page.getByTestId("qs-row-93")).toBeVisible();
  await page.goBack();
  await page.getByRole("button", { name: "Reset filters", exact: true }).click();
  await expect(page).not.toHaveURL(/contractId=/);
  await page.goto(`/admin/contracts/${contract.id}`);
  for (const [lang, title] of [
    ["vi", "Tiến độ thực hiện hợp đồng"],
    ["zh", "合同履约进度"],
    ["ja", "契約履行の進捗"],
  ]) {
    await page.evaluate((value) => localStorage.setItem("nicon_lang", value), lang);
    await page.reload();
    await expect(panel.getByRole("heading", { name: title, exact: true })).toBeVisible();
    await expect(panel).not.toContainText("contracts.progress.");
  }
});
