import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";
import { execSql } from "../fixtures/db";

test("input price entry is labelled and spacious and sent quotation headers stay responsive", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(180_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const writeHeaders = () => ({ ...headers, "Idempotency-Key": crypto.randomUUID() });
  const customerId = await createOwnCustomer(api, headers, "NICON quotation layout");
  let projectId: number | undefined;
  let opportunityId: number | undefined;
  let packageId: number | undefined;
  let quoteId: number | undefined;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  try {
    const project = await api.post("/api/operational-projects", {
      headers: writeHeaders(),
      data: { name: "NICON nhà xưởng - kiểm tra bố cục báo giá", customerId },
    });
    expect(project.ok(), await project.text()).toBeTruthy();
    projectId = (await project.json()).id;
    const opportunity = await api.post("/api/opportunities", {
      headers: writeHeaders(),
      data: {
        name: "NICON cải tạo nhà xưởng và hệ thống MEP",
        customerId,
        operationalProjectId: projectId,
        estimatedValue: 500000000,
        winProbability: 50,
      },
    });
    expect(opportunity.ok(), await opportunity.text()).toBeTruthy();
    opportunityId = (await opportunity.json()).id;
    const optionsResponse = await api.get("/api/work-packages/options", { headers });
    expect(optionsResponse.ok()).toBeTruthy();
    const options = await optionsResponse.json();
    const department = options.departments.find(
      (item: { code: string }) => item.code === "PROCUREMENT",
    );
    const lead = options.users.find(
      (item: { roleGroupId: number; canLead: boolean }) =>
        item.roleGroupId === department.id && item.canLead,
    );
    const vendor = options.vendors[0];
    expect(lead).toBeTruthy();
    expect(vendor).toBeTruthy();
    const work = await api.post("/api/work-packages", {
      headers: writeHeaders(),
      data: {
        kind: "Project",
        purpose: "ProcurementInputPricing",
        operationalProjectId: projectId,
        sourceOpportunityId: opportunityId,
        departmentRoleGroupId: department.id,
        leadUserId: lead.userId,
        name: "Báo giá đầu vào NICON - kết cấu nhà xưởng, hoàn thiện và hệ thống cơ điện "
          .repeat(3)
          .trim(),
      },
    });
    expect(work.ok(), await work.text()).toBeTruthy();
    packageId = (await work.json()).id;
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/work-packages/${packageId}`);
    await page.getByRole("button", { name: "Thêm báo giá đầu vào", exact: true }).click();
    const dialog = page.getByTestId("input-price-dialog");
    await dialog.getByLabel("Nhà cung cấp / thầu phụ", { exact: true }).click();
    await page.getByRole("option", { name: new RegExp(vendor.code) }).click();
    await dialog.getByLabel("Số báo giá nhà cung cấp", { exact: true }).fill("NICON-BG-UI-01");
    await dialog
      .locator('input[type="date"]')
      .fill(new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10));
    const line = page.getByTestId("input-price-line-0");
    for (const label of [
      "Mã hạng mục",
      "Mô tả",
      "Đơn vị",
      "Khối lượng",
      "Đơn giá",
      "VAT (%)",
      "Thành tiền",
    ]) {
      await expect(line.getByLabel(label, { exact: true })).toBeVisible();
    }
    await line.getByLabel("Mã hạng mục", { exact: true }).fill("KC-THEP-01");
    await line
      .getByLabel("Mô tả", { exact: true })
      .fill("Kết cấu thép nhà xưởng mạ kẽm nhúng nóng, bao gồm vận chuyển đến công trường NICON");
    await line.getByLabel("Đơn vị", { exact: true }).fill("kg");
    await line.getByLabel("Khối lượng", { exact: true }).fill("0,0001");
    await line.getByLabel("Đơn giá", { exact: true }).focus();
    await expect(line.getByLabel("Khối lượng", { exact: true })).toHaveValue("0,0001");
    await line.getByLabel("Khối lượng", { exact: true }).fill("0,001");
    await line.getByLabel("Đơn giá", { exact: true }).focus();
    await expect(line.getByLabel("Khối lượng", { exact: true })).toHaveValue("0,001");
    await line.getByLabel("Khối lượng", { exact: true }).fill("12500");
    await line.getByLabel("Đơn giá", { exact: true }).fill("21500");
    await line.getByLabel("VAT (%)", { exact: true }).fill("8");
    await dialog.getByRole("heading").click();
    await expect(line.getByLabel("Khối lượng", { exact: true })).toHaveValue("12.500");
    await expect(line.getByLabel("Đơn giá", { exact: true })).toHaveValue("21.500");
    await expect(page.getByTestId("input-price-amount-0")).toHaveText("268.750.000 VND");
    await dialog.getByRole("button", { name: "Thêm dòng", exact: true }).click();
    await expect(page.getByTestId("input-price-line-1")).toBeVisible();
    await page.getByTestId("input-price-line-1").getByRole("button").click();
    await expect(page.getByTestId("input-price-line-1")).toHaveCount(0);
    for (const width of [320, 390, 768, 1440]) {
      await page.setViewportSize({ width, height: width < 768 ? 740 : 1000 });
      await dialog.evaluate(async (element) => {
        await Promise.all(
          element.getAnimations().map((animation) => animation.finished.catch(() => undefined)),
        );
      });
      const dimensions = await dialog.evaluate((element) => {
        const box = element.getBoundingClientRect();
        return {
          left: box.left,
          right: box.right,
          top: box.top,
          bottom: box.bottom,
          clientWidth: element.clientWidth,
          scrollWidth: element.scrollWidth,
        };
      });
      expect(dimensions.left).toBeGreaterThanOrEqual(0);
      expect(dimensions.right).toBeLessThanOrEqual(width);
      expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth);
      const save = dialog.getByRole("button", { name: "Lưu thay đổi", exact: true });
      await expect(save).toBeInViewport();
      if (width === 1440) expect(dimensions.right - dimensions.left).toBeGreaterThan(1100);
      await dialog.getByTestId("input-price-dialog-body").evaluate((element) => {
        element.scrollTop = 0;
      });
      await dialog.screenshot({
        path: testInfo.outputPath(`input-price-${width}.png`),
        animations: "disabled",
      });
      await line.getByLabel("Khối lượng", { exact: true }).scrollIntoViewIfNeeded();
      await expect(line.getByLabel("Khối lượng", { exact: true })).toBeInViewport();
      await expect(save).toBeInViewport();
    }
    await line.getByLabel("VAT (%)", { exact: true }).fill("101");
    await expect(line.getByRole("alert")).toContainText("VAT (%) ≤ 100");
    await dialog.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
    await expect(dialog).toBeVisible();
    await expect(
      page
        .getByText(
          "Chọn nhà cung cấp, nhập số báo giá, hiệu lực tương lai và ít nhất một dòng giá hợp lệ.",
        )
        .first(),
    ).toBeVisible();
    await line.getByLabel("VAT (%)", { exact: true }).fill("8");
    const saved = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/work-packages/${packageId}/input-price-offers`) &&
        response.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
    expect((await saved).ok()).toBeTruthy();
    await expect(dialog).toHaveCount(0);
    const persisted = await (await api.get(`/api/work-packages/${packageId}`, { headers })).json();
    expect(persisted.inputPriceOffers[0].lines[0]).toMatchObject({
      itemCode: "KC-THEP-01",
      quantity: 12500,
      unitPrice: 21500,
      vatPercent: 8,
      amount: 268750000,
    });
    for (const width of [320, 390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      const header = page.getByTestId("work-package-detail-header");
      expect(await header.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(
        true,
      );
    }
    const quote = await api.post("/api/quotes", {
      headers: writeHeaders(),
      data: {
        opportunityId,
        method: "Boq",
        packageDescription: "NICON báo giá thi công nhà xưởng",
        items: [
          {
            itemCode: "KC-01",
            name: "Kết cấu thép nhà xưởng NICON",
            unit: "kg",
            quantity: 12500,
            unitPrice: 21500,
          },
        ],
        vatPercent: 8,
      },
    });
    expect(quote.ok(), await quote.text()).toBeTruthy();
    quoteId = (await quote.json()).id;
    expect(Number.isInteger(quoteId)).toBe(true);
    // Isolated rendering fixture for a sent quote; no customer email is sent.
    const longCode = `NICON-${"Q".repeat(20)}-${Date.now()}`;
    execSql(
      `UPDATE quotes SET Code='${longCode}', Status='SentToCustomer', SentAt=SYSUTCDATETIME() WHERE Id=${quoteId};`,
    );
    await page.goto(`/admin/quotes/${quoteId}`);
    await expect(page.getByTestId("quote-detail-header")).toContainText("Đã gửi khách");
    for (const width of [320, 390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      const header = page.getByTestId("quote-detail-header");
      if (width < 1024) await expect(page.locator("aside").first()).not.toBeInViewport();
      await expect(header.getByRole("heading")).toContainText(longCode);
      const geometry = await header.getByRole("heading").evaluate((element) => {
        const box = element.getBoundingClientRect();
        return { width: box.width, scroll: element.scrollWidth, client: element.clientWidth };
      });
      expect(geometry.width).toBeGreaterThan(200);
      expect(geometry.scroll).toBeLessThanOrEqual(geometry.client);
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        width,
      );
      await header.screenshot({
        path: testInfo.outputPath(`sent-quote-header-${width}.png`),
        animations: "disabled",
      });
    }
    expect(errors).toEqual([]);
  } finally {
    if (quoteId) await hardDeleteBusinessRoot(api, headers, `/api/quotes/${quoteId}`);
    if (packageId)
      await hardDeleteBusinessRoot(api, writeHeaders(), `/api/work-packages/${packageId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
