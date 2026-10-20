import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

for (const width of [375, 768]) {
  // Mock-based UI coverage; the live journey below owns deployed-stack wiring.
  test(`UI only: contract care retains a failed draft at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    await page.setViewportSize({ width, height: 1024 });
    const token = await loginAs(TEST_USERS.superAdmin);
    const contracts = await api.get("/api/contracts?direction=Upstream&pageSize=1", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(contracts.ok()).toBe(true);
    const contractId = (await contracts.json()).items[0]?.id as number;
    expect(contractId).toBeGreaterThan(0);
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    let failRead = true;
    let retryKey: string | undefined;
    let posts = 0;
    const entries: object[] = [];
    await page.route(
      new RegExp(`/api/(?:v1/)?contracts/${contractId}/care-activities(?:\\?.*)?$`),
      async (route) => {
        const request = route.request();
        if (request.method() === "GET") {
          if (failRead) {
            failRead = false;
            await route.fulfill({
              status: 500,
              json: { message: "Care history temporarily unavailable" },
            });
          } else {
            await route.fulfill({
              json: { total: entries.length, page: 1, pageSize: 20, items: entries },
            });
          }
          return;
        }
        expect(request.method()).toBe("POST");
        posts += 1;
        const key = request.headers()["idempotency-key"];
        expect(key).toBeTruthy();
        expect(request.postDataJSON()).toEqual({
          type: "Call",
          content: "Confirm the An Phú factory progress meeting.",
        });
        if (posts === 1) {
          retryKey = key;
          entries.push({
            id: 901,
            contractId,
            type: "Call",
            content: "Confirm the An Phú factory progress meeting.",
            occurredAt: "2026-10-20T03:00:00Z",
            createdAt: "2026-10-20T03:00:00Z",
            createdByUserId: 1,
            createdByName: "NICON CRM",
          });
          // A lost response after persistence must retain the draft and retry key.
          await route.abort("failed");
        } else {
          expect(key).toBe(retryKey);
          await route.fulfill({ status: 201, json: entries[0] });
        }
      },
    );

    await page.goto(`${baseURL}/admin/contracts/${contractId}?tab=care`, {
      waitUntil: "domcontentloaded",
    });
    const care = page.getByTestId("contract-care");
    await expect(care.getByRole("alert")).toContainText("Care history temporarily unavailable");
    await care.getByRole("button", { name: /Thử lại|Retry|重试|再試行/ }).click();
    await expect(
      care.getByText(/chưa có lịch sử|No customer-care entries|暂无客户维护|まだありません/),
    ).toBeVisible();
    const content = care.locator("#contract-care-content");
    const add = care.getByRole("button", {
      name: /Thêm mục lịch sử|Add entry|添加跟进|記録を追加/,
    });
    await content.fill("   ");
    await expect(add).toBeDisabled();
    await content.fill("  Confirm the An Phú factory progress meeting.  ");
    await add.click();
    await expect(care.getByRole("alert")).toBeVisible();
    await expect(content).toHaveValue("  Confirm the An Phú factory progress meeting.  ");
    await add.click();
    await expect(content).toHaveValue("");
    await expect(
      care.getByText("Confirm the An Phú factory progress meeting.", { exact: true }),
    ).toHaveCount(1);
    await expect(care.getByText(/NICON CRM/)).toBeVisible();
    expect(posts).toBe(2);
    expect(entries).toHaveLength(1);
    expect(pageErrors).toEqual([]);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  });
}

for (const width of [375, 768]) {
  test(`Live contract customer care persists after reload at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }, testInfo) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    const suffix = crypto.randomUUID();
    let customerId: number | undefined;
    let projectId: number | undefined;
    let contractId: number | undefined;
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("console", (message) => {
      const text = message.text();
      // Compose exposes HTTP but not the development browser-reload socket.
      const isDevSocketConnectionRefused =
        /^WebSocket connection to 'wss?:\/\/(?:localhost|127\.0\.0\.1):\d+\/' failed: .*net::ERR_CONNECTION_REFUSED$/.test(
          text,
        );
      if (message.type() === "error" && !isDevSocketConnectionRefused) errors.push(text);
    });
    try {
      const customer = await api.post("/api/customers", {
        headers,
        data: {
          name: `An Phú contract-care journey ${suffix}`,
          type: "Individual",
          sourceCode: "marketing",
          primaryContact: {
            fullName: "Factory investor",
            email: `care-${suffix}@example.test`,
            isPrimary: true,
          },
        },
      });
      expect(customer.status(), await customer.text()).toBe(201);
      customerId = (await customer.json()).id;
      const project = await api.post("/api/operational-projects", {
        headers,
        data: {
          name: `An Phú factory care journey ${suffix}`,
          customerId,
        },
      });
      expect(project.status(), await project.text()).toBe(201);
      projectId = (await project.json()).id;
      const contract = await api.post("/api/contracts", {
        headers,
        data: {
          customerId,
          operationalProjectId: projectId,
          direction: "Upstream",
          type: "DesignAndBuild",
          value: 1_500_000_000,
          scopeOfWork: "Factory contract customer follow-up browser validation",
        },
      });
      expect(contract.status(), await contract.text()).toBe(201);
      contractId = (await contract.json()).id;

      await page.setViewportSize({ width, height: 1024 });
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.goto(`${baseURL}/admin/contracts/${contractId}?tab=care`, {
        waitUntil: "domcontentloaded",
      });
      const care = page.getByTestId("contract-care");
      await expect(
        care.getByText(/chưa có lịch sử|No customer-care entries|暂无客户维护|まだありません/),
      ).toBeVisible();
      const details = `Confirm An Phú owner progress meeting ${suffix}`;
      await care.locator("#contract-care-content").fill(details);
      const created = page.waitForResponse(
        (response) =>
          response.request().method() === "POST" &&
          new URL(response.url()).pathname.endsWith(`/contracts/${contractId}/care-activities`),
      );
      await care
        .getByRole("button", { name: /Thêm mục lịch sử|Add entry|添加跟进|記録を追加/ })
        .click();
      expect((await created).status()).toBe(201);
      await expect(care.locator("#contract-care-content")).toHaveValue("");
      await expect(care.getByText(details, { exact: true })).toHaveCount(1);
      await page.reload();
      await expect(care.getByText(details, { exact: true })).toBeVisible();
      const persisted = await api.get(`/api/contracts/${contractId}/care-activities`, { headers });
      expect(persisted.status()).toBe(200);
      const result = await persisted.json();
      expect(result.total).toBe(1);
      expect(result.items[0].content).toBe(details);
      expect(result.items[0].contractId).toBe(contractId);
      expect(errors).toEqual([]);
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
      await page.screenshot({
        path: testInfo.outputPath(`contract-care-${width}.png`),
        fullPage: true,
      });
    } finally {
      if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
      if (projectId)
        await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
      if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
    }
  });
}
