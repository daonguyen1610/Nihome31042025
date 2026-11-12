import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

const PROJECT_NAME = "NICON – Điều hành thi công nhà xưởng Bình Dương";

const AREAS = [
  { path: "schedule", testId: "cm-schedule-page", labelIndex: 0 },
  { path: "commercial", testId: "cm-commercial-page", labelIndex: 1 },
  { path: "quality", testId: "cm-quality-page", labelIndex: 2 },
  { path: "safety", testId: "cm-safety-page", labelIndex: 3 },
  { path: "contracts", testId: "cm-contracts-page", labelIndex: 4 },
] as const;

test("CM groups controls into five areas and keeps linked pages on the same project", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON – Kiểm soát CM nhà xưởng");
  let projectId: number | undefined;
  try {
    const users = await api.get(`/api/users?role=PM&search=${TEST_USERS.pm.phoneNumber}&take=10`, {
      headers,
    });
    expect(users.ok()).toBe(true);
    const manager = (await users.json()).items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.pm.phoneNumber,
    );
    expect(manager).toBeTruthy();
    const created = await api.post("/api/operational-projects", {
      headers,
      data: {
        customerId,
        name: PROJECT_NAME,
        projectManagerUserId: manager.id,
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    projectId = (await created.json()).id;
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/");
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    for (const scenario of [
      {
        language: "vi",
        width: 390,
        labels: ["Tiến độ", "Khối lượng và chi phí", "Chất lượng", "An toàn", "Hợp đồng"],
      },
      {
        language: "en",
        width: 768,
        labels: ["Schedule", "Quantity and cost", "Quality", "Safety", "Contracts"],
      },
      { language: "zh", width: 1440, labels: ["进度", "工程量与成本", "质量", "安全", "合同"] },
      { language: "ja", width: 390, labels: ["工程", "数量とコスト", "品質", "安全", "契約"] },
    ]) {
      await page.evaluate(
        (language) => localStorage.setItem("nicon_lang", language),
        scenario.language,
      );
      await page.setViewportSize({ width: scenario.width, height: 900 });
      // The legacy CM entry point redirects to the first reachable area while
      // keeping the project scope.
      await page.goto(`/admin/construction/cm?projectId=${projectId}`);
      await expect(page).toHaveURL(
        new RegExp(`/admin/construction/cm/schedule\\?projectId=${projectId}`),
      );
      for (const area of AREAS) {
        await page.goto(`/admin/construction/cm/${area.path}?projectId=${projectId}`);
        const areaPage = page.getByTestId(area.testId);
        await expect(areaPage).toBeVisible();
        await expect(areaPage.getByRole("heading", { level: 1 })).toHaveText(
          scenario.labels[area.labelIndex],
        );
        await expect(areaPage.getByTestId(`${area.testId}-project`)).toContainText(PROJECT_NAME);
        if (area.path === "contracts")
          await expect(page.getByTestId("cm-contracts-panel")).toBeVisible();
      }
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
        ),
      ).toBe(true);
      await page.screenshot({ path: testInfo.outputPath(`cm-areas-${scenario.language}.png`) });
    }
    expect(errors).toEqual([]);

    // The contracts area reads the ledger scoped to the selected project.
    const contractResponse = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return (
        url.pathname.endsWith("/api/contracts") &&
        url.searchParams.get("operationalProjectId") === String(projectId)
      );
    });
    await page.goto(`/admin/construction/cm/contracts?projectId=${projectId}`);
    expect((await contractResponse).ok()).toBe(true);

    await loginInBrowserAs(page, TEST_USERS.pm);
    await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(`/admin/construction/cm?projectId=${projectId}`);
    await expect(page).toHaveURL(
      new RegExp(`/admin/construction/cm/schedule\\?projectId=${projectId}`),
    );
    for (const area of AREAS) {
      await page.goto(`/admin/construction/cm/${area.path}?projectId=${projectId}`);
      const areaPage = page.getByTestId(area.testId);
      await expect(areaPage).toBeVisible();
      await expect(areaPage.getByRole("heading", { level: 1 })).toHaveText(
        ["Schedule", "Quantity and cost", "Quality", "Safety", "Contracts"][area.labelIndex],
      );
    }
    const permissionsResponse = await api.get("/api/users/me/permissions", {
      headers: { Authorization: `Bearer ${await loginAs(TEST_USERS.pm)}` },
    });
    expect(permissionsResponse.ok()).toBe(true);
    const permissions = (await permissionsResponse.json()).permissions as string[];
    await page.goto(`/admin/construction/cm/quality?projectId=${projectId}`);
    const createTemplate = page
      .getByRole("button", {
        name: "Create inspection template",
        exact: true,
      })
      .first();
    if (permissions.includes("construction.qa-qc.template-manage"))
      await expect(createTemplate).toBeVisible();
    else await expect(createTemplate).toHaveCount(0);
  } finally {
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});

// Permission rendering check: only the permission response is overridden;
// business APIs remain real. This does not prove server authorization.
test("template management stays reachable for a CM reviewer", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON – Quản lý mẫu chất lượng CM");
  let projectId: number | undefined;
  try {
    const created = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Mẫu kiểm soát chất lượng nhà xưởng" },
    });
    expect(created.status(), await created.text()).toBe(201);
    projectId = (await created.json()).id;
    await page.route("**/api/users/me/permissions", async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      body.permissions = [
        "operations.projects.view",
        "construction.qa-qc.cm-review",
        "construction.qa-qc.template-manage",
      ];
      await route.fulfill({ response, json: body });
    });
    const dossierReads: string[] = [];
    page.on("request", (request) => {
      if (request.method() === "GET" && request.url().includes("/construction/qa-qc"))
        dossierReads.push(request.url());
    });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(`/admin/construction/cm?projectId=${projectId}`);
    await expect(page).toHaveURL(
      new RegExp(`/admin/construction/cm/quality\\?projectId=${projectId}`),
    );
    const workspace = page.getByTestId("cm-quality-page");
    await expect(workspace).toBeVisible();
    await expect(workspace.getByRole("heading", { level: 1 })).toHaveText("Quality");
    await workspace
      .getByRole("button", { name: "Create inspection template", exact: true })
      .first()
      .click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await page.keyboard.press("Escape");
    // The reviewer may read dossiers, but every read stays inside the project
    // the workspace is scoped to.
    expect(dossierReads.length).toBeGreaterThan(0);
    expect(dossierReads.every((url) => url.includes(`/operational-projects/${projectId}/`))).toBe(
      true,
    );
  } finally {
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
