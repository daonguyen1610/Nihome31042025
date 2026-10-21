import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

for (const width of [390, 768]) {
  test(`Live contract continuation and negotiation at ${width}px`, async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const suffix = randomUUID();
    const customerResponse = await api.post("/api/customers", {
      headers,
      data: {
        name: `An Phú follow-on ${suffix}`,
        type: "Individual",
        sourceCode: "marketing",
        primaryContact: { fullName: "Mai Anh", email: "mai.anh@example.test", isPrimary: true },
      },
    });
    expect(customerResponse.ok(), await customerResponse.text()).toBeTruthy();
    const customerId = (await customerResponse.json()).id;
    const projectResponse = await api.post("/api/operational-projects", {
      headers,
      data: { name: `An Phú expansion ${suffix}`, customerId },
    });
    expect(projectResponse.ok(), await projectResponse.text()).toBeTruthy();
    const projectId = (await projectResponse.json()).id;
    const sourceResponse = await api.post("/api/contracts", {
      headers,
      data: {
        customerId,
        operationalProjectId: projectId,
        direction: "Upstream",
        type: "Design",
        value: 500000000,
        status: "Draft",
      },
    });
    expect(sourceResponse.ok(), await sourceResponse.text()).toBeTruthy();
    const sourceId = (await sourceResponse.json()).id;
    execSql(
      `UPDATE contracts SET Status='Signed', SignedDate=DATEADD(day,-30,GETUTCDATE()) WHERE Id=${sourceId};`,
    );
    const errors: string[] = [];
    page.on("pageerror", (e) => errors.push(e.message));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.setViewportSize({ width, height: 1000 });
    await page.goto(`/admin/contracts/${sourceId}?tab=care`);
    const region = page.getByRole("region", { name: "Tái ký / Phát triển hợp đồng", exact: true });
    await expect(region).toBeVisible();
    await region.getByRole("link", { name: "Lập hợp đồng tiếp nối", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.locator("#continuation-reason")).toBeVisible();
    await dialog.locator("#continuation-kind").selectOption("Expansion");
    await dialog
      .locator("#continuation-reason")
      .fill("Thiết kế nhà điều hành giai đoạn hai theo nhu cầu Chủ đầu tư.");
    await dialog.locator("#c-value").fill("350000000");
    const create = page.waitForResponse(
      (r) => r.url().endsWith("/api/contracts") && r.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: /Lưu|Save/, exact: true }).click();
    const created = await create;
    expect(created.ok(), await created.text()).toBeTruthy();
    const successor = await created.json();
    await expect(page).toHaveURL(new RegExp(`/admin/contracts/${successor.id}`));
    await page.goto(`/admin/contracts/${sourceId}?tab=care`);
    await expect(
      region.getByRole("link", { name: successor.contractNumber, exact: true }),
    ).toBeVisible();
    await page.screenshot({ path: testInfo.outputPath(`contract-links-${width}.png`) });
    await region
      .getByRole("link", { name: "Theo dõi cơ hội tái ký / phát triển", exact: true })
      .click();
    await expect(dialog.locator("#continuation-reason")).toBeVisible();
    await dialog
      .locator("#continuation-reason")
      .fill("Trao đổi giai đoạn mở rộng nhà xưởng tiếp theo.");
    const pursue = page.waitForResponse(
      (r) => r.url().endsWith("/api/opportunities") && r.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: /Lưu|Save/, exact: true }).click();
    const pursued = await pursue;
    expect(pursued.ok(), await pursued.text()).toBeTruthy();
    const opportunity = await pursued.json();
    await expect(page).toHaveURL(new RegExp(`/admin/opportunities/${opportunity.id}`));
    await expect(page.getByRole("link", { name: "Hợp đồng nguồn", exact: true })).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`continuation-${width}.png`),
      fullPage: false,
    });
    expect(errors).toEqual([]);
    // Keep auditable follow-on fixtures; the application deliberately blocks deleting provenance.
  });
}
