import { test, expect, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

test("live legal draft, independent approval and mandatory project setup", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const errors: string[] = [];
  let expectedConflictApi = "";
  let expectedLoggedConflicts = 0;
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() !== "error" || /WebSocket|\[vite\]|409 \(Conflict\)/i.test(message.text()))
      return;
    try {
      const log = JSON.parse(message.text());
      if (
        log.properties?.api_name === expectedConflictApi &&
        log.message?.includes("failed with status 409")
      ) {
        expectedLoggedConflicts += 1;
        return;
      }
    } catch {
      /* Non-JSON browser errors are checked below. */
    }
    errors.push(message.text());
  });
  const author = await loginAs(TEST_USERS.superAdmin);
  const reviewer = await loginAs(TEST_USERS.admin);
  const headers = { Authorization: `Bearer ${author}` };
  const code = `E2E-LEGAL-${Date.now()}`;
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/permits");
  const templates = page.getByTestId("legal-templates");
  await templates.locator("summary").click();
  await templates
    .getByRole("button", { name: /Soạn mẫu mới|Draft a template|起草模板|テンプレートを作成/ })
    .click();
  await page.locator("#legal-code").fill(code);
  for (const [name, scope] of [
    ["nameVi", "scopeVi"],
    ["name", "scope"],
    ["nameZh", "scopeZh"],
    ["nameJa", "scopeJa"],
  ]) {
    await page.locator(`#legal-${name}`).fill(`NICON factory legal checklist ${code}`);
    await page
      .locator(`#legal-${scope}`)
      .fill("E2E-only factory permit scope; independent review fixture.");
  }
  const row = page.getByTestId("template-permit-gpxd");
  await row.getByRole("checkbox").nth(0).check();
  await row.getByRole("checkbox").nth(1).check();
  const saved = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/legal-checklist-templates") &&
      response.request().method() === "POST",
  );
  await templates
    .locator("form")
    .getByRole("button", { name: /Lưu|Save|保存/ })
    .click();
  const created = await saved;
  expect(created.ok(), await created.text()).toBeTruthy();
  const template = await created.json();
  await templates.getByRole("button", { name: new RegExp(code) }).click();
  await expect(
    templates.getByRole("button", {
      name: /Duyệt và xuất bản|Approve and publish|审批并发布|承認して公開/,
    }),
  ).toBeDisabled();
  expect(
    await page.locator("html").evaluate((element) => element.scrollWidth <= element.clientWidth),
  ).toBe(true);
  await page.screenshot({ path: testInfo.outputPath("legal-template-mobile.png"), fullPage: true });

  const published = await api.post(`/api/legal-checklist-templates/${template.id}/publish`, {
    headers: { Authorization: `Bearer ${reviewer}`, "Idempotency-Key": crypto.randomUUID() },
    data: {
      rowVersion: template.rowVersion,
      approvalReference: `E2E independent CM review ${code}`,
    },
  });
  expect(published.ok(), await published.text()).toBeTruthy();
  const approved = await published.json();
  const customers = await (await api.get("/api/customers?pageSize=1", { headers })).json();
  const users = await (
    await api.get(`/api/users?search=${TEST_USERS.legalOfficer.phoneNumber}`, { headers })
  ).json();
  const legal = users.items[0];
  expect(legal?.id).toBeTruthy();
  const superUser = await (
    await api.get(`/api/users?search=${TEST_USERS.pm.phoneNumber}`, { headers })
  ).json();
  const pmId = superUser.items[0].id;
  const projectResponse = await api.post("/api/operational-projects", {
    headers: { ...headers, "Idempotency-Key": crypto.randomUUID() },
    data: {
      name: `NICON factory — legal checklist validation ${code}`,
      customerId: customers.items[0].id,
      projectManagerUserId: pmId,
    },
  });
  expect(projectResponse.ok(), await projectResponse.text()).toBeTruthy();
  const project = await projectResponse.json();
  expectedConflictApi = `POST /operational-projects/${project.id}/legal-setup`;
  const team = await (
    await api.get(`/api/operational-projects/${project.id}/team`, { headers })
  ).json();
  for (const member of [
    { userId: legal.id, roleCode: "LegalOfficer" },
    { userId: pmId, roleCode: "ProjectManager" },
  ]) {
    if (team.members.some((existing: { userId: number }) => existing.userId === member.userId))
      continue;
    const response = await api.post(`/api/operational-projects/${project.id}/team/members`, {
      headers: { ...headers, "Idempotency-Key": crypto.randomUUID() },
      data: {
        userId: member.userId,
        position: `NICON legal validation ${member.roleCode}`,
        startedAt: "2026-01-01T00:00:00Z",
        roles: [{ roleCode: member.roleCode, scope: "Project" }],
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
  }
  await page.setViewportSize({ width: 768, height: 1024 });
  await page.goto(`/admin/permits?operationalProjectId=${project.id}`);
  await page
    .getByRole("button", {
      name: /Thiết lập Pháp lý|Set up legal work|设置法务工作|法務業務を設定/i,
    })
    .click();
  const dialog = page.getByRole("dialog");
  const next = dialog.getByRole("button", { name: /Sau|Next|下一步|次へ/ });
  await next.click();
  await page.locator("#legal-template-select").selectOption(String(template.id));
  const required = dialog.locator("label").filter({ hasText: "gpxd" }).getByRole("checkbox");
  await expect(required).toBeChecked();
  await expect(required).toBeDisabled();
  await next.click();
  await next.click();
  const configured = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/operational-projects/${project.id}/legal-setup`) &&
      response.request().method() === "POST",
  );
  await page.getByTestId("legal-activation-confirm").click();
  const setup = await configured;
  expect(setup.ok(), await setup.text()).toBeTruthy();
  const configuredPackage = await setup.json();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByText(/Bắt buộc|Required|必需|必須/).first()).toBeVisible();
  expect(
    await page.locator("html").evaluate((element) => element.scrollWidth <= element.clientWidth),
  ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("legal-checklist-tablet.png"),
    fullPage: true,
  });
  // Isolated fixture: simulate this project's legacy package and a real SQL rowversion conflict.
  execSql(`SET QUOTED_IDENTIFIER ON; UPDATE department_work_packages SET LegalChecklistTemplateId=NULL, LegalChecklistSnapshotJson=NULL, LegalChecklistAppliedAt=NULL, LegalChecklistAppliedByUserId=NULL WHERE Id=${Number(configuredPackage.workPackageId)};
    UPDATE permit_checklist_items SET IsRequired=0 WHERE OperationalProjectId=${Number(project.id)};`);
  await page.reload();
  await page
    .getByRole("button", {
      name: /Thiết lập Pháp lý|Set up legal work|设置法务工作|法務業務を設定/i,
    })
    .click();
  await next.click();
  await page.locator("#legal-template-select").selectOption(String(template.id));
  const additional = dialog.locator("label").filter({ hasText: "pccc" }).getByRole("checkbox");
  await additional.check();
  await next.click();
  const agency = dialog.locator("input:not([type]):visible").first();
  await agency.fill(`CM retained agency ${code}`);
  await next.click();
  execSql(
    `SET QUOTED_IDENTIFIER ON; UPDATE department_work_packages SET UpdatedAt=SYSUTCDATETIME() WHERE Id=${Number(configuredPackage.workPackageId)};`,
  );
  const rejected = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/operational-projects/${project.id}/legal-setup`) &&
      response.request().method() === "POST",
  );
  await page.getByTestId("legal-activation-confirm").click();
  expect((await rejected).status()).toBe(409);
  await dialog
    .getByRole("button", {
      name: /Tải phiên bản mới|Load latest version|加载最新版本|確認用に最新版/,
    })
    .click();
  await expect(additional).toBeChecked();
  await expect(required).toBeDisabled();
  await next.click();
  await expect(agency).toHaveValue(`CM retained agency ${code}`);
  await next.click();
  const retried = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/operational-projects/${project.id}/legal-setup`) &&
      response.request().method() === "POST",
  );
  await page.getByTestId("legal-activation-confirm").click();
  expect((await retried).ok()).toBeTruthy();
  await expect(dialog).toHaveCount(0);
  const retired = await api.post(`/api/legal-checklist-templates/${template.id}/retire`, {
    headers: { Authorization: `Bearer ${reviewer}`, "Idempotency-Key": crypto.randomUUID() },
    data: { rowVersion: approved.rowVersion },
  });
  expect(retired.ok(), await retired.text()).toBeTruthy();
  const preview = await (
    await api.get(`/api/operational-projects/${project.id}/legal-setup`, { headers })
  ).json();
  expect(preview.appliedTemplate.id).toBe(template.id);
  expect(preview.appliedTemplate.items[0].isRequired).toBe(true);
  await page.goto(`/admin/work-packages?create=1&purpose=LegalPermitting&projectId=${project.id}`);
  await expect(page).toHaveURL(new RegExp(`/admin/permits\\?operationalProjectId=${project.id}$`));
  expect(errors).toEqual([]);
  expect(expectedLoggedConflicts).toBe(1);
});

test("live stale template editing preserves local draft through review and retry", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(60_000);
  const author = await loginAs(TEST_USERS.superAdmin);
  const reviewer = await loginAs(TEST_USERS.admin);
  const code = `E2E-STALE-${Date.now()}`;
  const input = {
    code,
    nameVi: "Pháp lý nhà máy",
    name: "Factory permits",
    nameZh: "工厂许可",
    nameJa: "工場許可",
    scopeVi: "Mẫu kiểm thử",
    scope: "Test fixture",
    scopeZh: "测试模板",
    scopeJa: "テスト用",
    items: [{ permitTypeCode: "gpxd", isRequired: true }],
  };
  const created = await api.post("/api/legal-checklist-templates", {
    headers: { Authorization: `Bearer ${author}`, "Idempotency-Key": crypto.randomUUID() },
    data: input,
  });
  expect(created.ok(), await created.text()).toBeTruthy();
  const template = await created.json();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/permits");
  const templates = page.getByTestId("legal-templates");
  await templates.locator("summary").click();
  await templates.getByRole("button", { name: new RegExp(code) }).click();
  await templates.getByRole("button", { name: /Sửa|Edit|编辑|編集/ }).click();
  await page.locator("#legal-nameVi").fill(`Local legal draft ${code}`);
  const external = await api.put(`/api/legal-checklist-templates/${template.id}`, {
    headers: { Authorization: `Bearer ${reviewer}` },
    data: { ...input, scopeVi: `CM revised scope ${code}`, rowVersion: template.rowVersion },
  });
  expect(external.ok(), await external.text()).toBeTruthy();
  const conflict = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/legal-checklist-templates/${template.id}`) &&
      response.request().method() === "PUT",
  );
  await templates
    .locator("form")
    .getByRole("button", { name: /Lưu|Save|保存/ })
    .click();
  expect((await conflict).status()).toBe(409);
  await templates.getByTestId("legal-template-refresh").click();
  await expect(page.locator("#legal-nameVi")).toHaveValue(`Local legal draft ${code}`);
  await expect(templates.getByText(`CM revised scope ${code}`, { exact: false })).toBeVisible();
  await templates
    .getByRole("button", { name: /Đã đối chiếu|Reviewed latest|已核对|最新内容を確認済み/ })
    .click();
  const saved = page.waitForResponse(
    (response) =>
      response.url().endsWith(`/api/legal-checklist-templates/${template.id}`) &&
      response.request().method() === "PUT",
  );
  await templates
    .locator("form")
    .getByRole("button", { name: /Lưu|Save|保存/ })
    .click();
  const response = await saved;
  expect(response.ok(), await response.text()).toBeTruthy();
  expect((await response.json()).nameVi).toBe(`Local legal draft ${code}`);
});
