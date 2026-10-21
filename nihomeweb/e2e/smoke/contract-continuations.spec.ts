import { randomUUID } from "node:crypto";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

for (const width of [390, 768, 1440]) {
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
    await page.addInitScript(() => {
      if (!localStorage.getItem("nicon_lang")) localStorage.setItem("nicon_lang", "vi");
    });
    await page.setViewportSize({ width, height: 1000 });
    await page.goto(`/admin/contracts/${sourceId}?tab=care`);
    const region = page.getByRole("region", { name: "Tái ký / Phát triển hợp đồng", exact: true });
    await expect(region).toBeVisible();
    await region.getByRole("link", { name: "Lập hợp đồng tiếp nối", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.locator("#continuation-reason")).toBeVisible();
    if (width === 390) await dialog.getByRole("button", { name: "Huỷ", exact: true }).click();
    else if (width === 768) await page.keyboard.press("Escape");
    else await dialog.getByRole("button", { name: "Đóng", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await expect(page).not.toHaveURL(/sourceContractId=/);
    await page.getByRole("button", { name: "Thêm hợp đồng", exact: true }).first().click();
    await expect(dialog.locator("#c-customer-form")).toBeEnabled();
    await expect(dialog.locator("#continuation-reason")).toHaveCount(0);
    if (width === 390) await page.keyboard.press("Escape");
    else if (width === 768) await dialog.getByRole("button", { name: "Đóng", exact: true }).click();
    else await dialog.getByRole("button", { name: "Huỷ", exact: true }).click();
    await page.goBack();
    await expect(region).toBeVisible();
    if (width === 390) await page.context().setOffline(true);
    await region.getByRole("link", { name: "Lập hợp đồng tiếp nối", exact: true }).click();
    if (width === 390) {
      await expect(dialog.getByRole("alert").first()).toBeVisible();
      await page.context().setOffline(false);
      await dialog.getByRole("button", { name: "Tải lại", exact: true }).click();
      if (await dialog.getByRole("button", { name: "Thử lại", exact: true }).count())
        await dialog.getByRole("button", { name: "Thử lại", exact: true }).click();
    }
    await expect(dialog.locator("#continuation-reason")).toBeVisible();
    await dialog.locator("#c-value").fill("350000000");
    await dialog.locator("#c-value").blur();
    await expect(dialog.locator("#c-value")).toHaveValue("350.000.000");
    await dialog.getByRole("button", { name: /Lưu|Save/, exact: true }).click();
    await expect(dialog.locator('[role="alert"][tabindex="-1"]')).toBeFocused();
    await dialog.locator("#continuation-kind").focus();
    await page.keyboard.press("p");
    await page.keyboard.press("Tab");
    await expect(dialog.locator("#continuation-reason")).toBeFocused();
    await expect(dialog.locator("#continuation-kind")).toHaveValue("Expansion");
    const reason =
      "Thiết kế nhà điều hành giai đoạn hai.\n" + "Mở rộng phạm vi hợp tác. ".repeat(80);
    await dialog.locator("#continuation-reason").fill(reason.slice(0, 2000));
    execSql(`UPDATE contracts SET Note='Updated during negotiation' WHERE Id=${sourceId};`);
    const conflict = page.waitForResponse(
      (r) => r.url().endsWith("/api/contracts") && r.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: /Lưu|Save/, exact: true }).click();
    expect((await conflict).status()).toBe(409);
    await expect(dialog.locator('[role="alert"][tabindex="-1"]')).toBeFocused();
    await dialog.getByRole("button", { name: "Cập nhật hợp đồng nguồn", exact: true }).click();
    await expect(dialog.locator("#continuation-reason")).toHaveValue(reason.slice(0, 2000));
    await expect(dialog.locator("#c-value")).toHaveValue("350.000.000");
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
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(page).not.toHaveURL(/sourceContractId=/);
    await page.getByRole("button", { name: "Tạo Cơ hội mới", exact: true }).first().click();
    await expect(dialog.getByRole("combobox").first()).toBeEnabled();
    await expect(dialog.locator("#continuation-reason")).toHaveCount(0);
    await dialog.getByRole("button", { name: "Đóng", exact: true }).click();
    await page.goto(`/admin/contracts/${sourceId}?tab=care`);
    await region
      .getByRole("link", { name: "Theo dõi cơ hội tái ký / phát triển", exact: true })
      .click();
    await expect(dialog.locator("#continuation-reason")).toBeVisible();
    await dialog
      .locator("#continuation-reason")
      .fill("Trao đổi giai đoạn mở rộng nhà xưởng tiếp theo.");
    const opportunityName = await dialog.locator("input").first().inputValue();
    await dialog.getByRole("button", { name: "Cập nhật hợp đồng nguồn", exact: true }).click();
    await expect(dialog.locator("#continuation-reason")).toHaveValue(
      "Trao đổi giai đoạn mở rộng nhà xưởng tiếp theo.",
    );
    await expect(dialog.locator("input").first()).toHaveValue(opportunityName);
    const pursue = page.waitForResponse(
      (r) => r.url().endsWith("/api/opportunities") && r.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: /Lưu|Save/, exact: true }).click();
    const pursued = await pursue;
    expect(pursued.ok(), await pursued.text()).toBeTruthy();
    const opportunity = await pursued.json();
    await expect(page).toHaveURL(new RegExp(`/admin/opportunities/${opportunity.id}`));
    await expect(page.getByRole("link", { name: "Hợp đồng nguồn", exact: true })).toBeVisible();
    await dialog.evaluate((element) =>
      Promise.all(element.getAnimations().map((animation) => animation.finished)),
    );
    const bounds = await dialog.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.x).toBeGreaterThanOrEqual(0);
    expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`continuation-${width}.png`),
      fullPage: false,
    });
    await page.getByTestId("opportunity-create-contract").click();
    await expect(dialog.locator("#continuation-kind")).toBeDisabled();
    await expect(dialog.locator("#continuation-reason")).toHaveAttribute("readonly", "");
    await expect(dialog.locator("#continuation-reason")).toHaveValue(
      "Trao đổi giai đoạn mở rộng nhà xưởng tiếp theo.",
    );
    await dialog.getByRole("button", { name: "Đóng", exact: true }).click();
    await expect(page).not.toHaveURL(/sourceContractId=|opportunityId=/);
    await page.getByRole("button", { name: "Thêm hợp đồng", exact: true }).first().click();
    await expect(dialog.locator("#c-customer-form")).toBeEnabled();
    await expect(dialog.locator("#continuation-reason")).toHaveCount(0);
    await page.keyboard.press("Escape");
    expect(errors).toEqual([]);
    if (width === 1440) {
      for (const [language, title, action, sourceLabel] of [
        ["en", "Renewal / Contract development", "Create follow-on contract", "Source contract"],
        ["zh", "续签 / 合同拓展", "创建后续合同", "来源合同"],
        ["ja", "更新・契約拡大", "後続契約を作成", "元契約"],
      ]) {
        await page.evaluate((lang) => localStorage.setItem("nicon_lang", lang), language);
        await page.goto(`/admin/contracts/${sourceId}?tab=care`);
        const localizedRegion = page.getByRole("region", { name: title, exact: true });
        await expect(localizedRegion).toBeVisible();
        await localizedRegion.getByRole("link", { name: action, exact: true }).click();
        await expect(dialog.getByText(new RegExp(`^${sourceLabel}:`))).toBeVisible();
        await expect(dialog.locator("#continuation-kind option")).toHaveCount(2);
        await dialog.evaluate((element) =>
          Promise.all(element.getAnimations().map((animation) => animation.finished)),
        );
        expect(await dialog.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBe(
          true,
        );
        await page.screenshot({ path: testInfo.outputPath(`continuation-${language}.png`) });
        await page.keyboard.press("Escape");
        await expect(page).not.toHaveURL(/sourceContractId=/);
      }
    }
    // Keep auditable follow-on fixtures; the application deliberately blocks deleting provenance.
  });
}
