import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("project, contract and internal work package tabs render without browser errors", async ({
  page,
  loginInBrowserAs,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error" && !/WebSocket|\[vite\]/i.test(message.text()))
      errors.push(message.text());
  });

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.setViewportSize({ width: 390, height: 844 });
  const response = await page.goto("/admin/work-packages");
  expect(response?.status()).toBeLessThan(400);
  await expect(page.getByTestId("work-packages-page")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole("tab", { name: /Theo dự án|Project work/i })).toBeVisible();

  await page.getByRole("tab", { name: /Theo hợp đồng|Contract work/i }).click();
  await expect(page).toHaveURL(/kind=Contract/);
  await expect(page.getByTestId("work-packages-page")).toBeVisible();

  await page.getByRole("tab", { name: /Công việc nội bộ|Internal work/i }).click();
  await expect(page).toHaveURL(/kind=Internal/);
  await expect(page.getByTestId("work-packages-page")).toBeVisible();
  expect(errors).toEqual([]);
});

for (const width of [390, 768])
  test(`ordinary work subtasks render as a hierarchy at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const writeHeaders = () => ({ ...headers, "Idempotency-Key": randomUUID() });
    const options = await (await api.get("/api/work-packages/options", { headers })).json();
    const department = options.departments.find(
      (item: { code: string }) => item.code === "PROCUREMENT",
    );
    const lead = options.users.find(
      (item: { roleGroupId: number; canLead: boolean }) =>
        item.roleGroupId === department.id && item.canLead,
    );
    const assignee = options.users.find(
      (item: { roleGroupId: number; userId: number }) =>
        item.roleGroupId === department.id && item.userId !== lead.userId,
    );
    expect(lead).toBeTruthy();
    expect(assignee).toBeTruthy();
    const projects = await (
      await api.get("/api/operational-projects?pageSize=1", { headers })
    ).json();
    const created = await api.post("/api/work-packages", {
      headers: writeHeaders(),
      data: {
        kind: "Project",
        purpose: "General",
        name: `Điều phối vật tư dự án NICON ${Date.now()}`,
        operationalProjectId: projects.items[0].id,
        departmentRoleGroupId: department.id,
        leadUserId: lead.userId,
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    const workPackage = await created.json();
    try {
      const parentResponse = await api.post(`/api/work-packages/${workPackage.id}/tasks`, {
        headers: writeHeaders(),
        data: {
          wbsCode: "VT-01",
          title: "Tổng hợp yêu cầu vật tư thi công",
          assigneeUserId: assignee.userId,
          approverUserId: lead.userId,
          plannedStart: "2026-10-23T00:00:00Z",
          plannedEnd: "2026-10-30T00:00:00Z",
        },
      });
      expect(parentResponse.status(), await parentResponse.text()).toBe(201);
      const parent = await parentResponse.json();
      const childResponse = await api.post(`/api/work-packages/${workPackage.id}/tasks`, {
        headers: writeHeaders(),
        data: {
          parentTaskId: parent.id,
          wbsCode: "VT-01.1",
          title: "Kiểm tra khối lượng vật tư",
          assigneeUserId: assignee.userId,
          approverUserId: lead.userId,
          plannedStart: "2026-10-23T00:00:00Z",
          plannedEnd: "2026-10-30T00:00:00Z",
        },
      });
      expect(childResponse.status(), await childResponse.text()).toBe(201);
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.setViewportSize({ width, height: 844 });
      await page.goto(`/admin/work-packages/${workPackage.id}`);
      const rows = page.getByTestId("work-task-row");
      await expect(rows).toHaveCount(2);
      await expect(rows.nth(0)).toContainText("Tổng hợp yêu cầu vật tư thi công");
      await expect(rows.nth(0)).toContainText(/Tự tổng hợp từ con|Rolled up from subtasks/);
      await expect(rows.nth(1)).toContainText("Kiểm tra khối lượng vật tư");
      await rows
        .nth(0)
        .getByRole("button", { name: /Thêm công việc con|Add subtask/ })
        .click();
      await expect(page.getByRole("dialog")).toContainText("VT-01");
    } finally {
      await hardDeleteBusinessRoot(api, headers, `/api/work-packages/${workPackage.id}`);
    }
  });

test("project package picker offers every active receiving department", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/work-packages/departments", { headers });
  expect(response.status()).toBe(200);
  const departments: { id: number; code: string }[] = await response.json();
  expect(departments.length).toBeGreaterThan(2);

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin/work-packages");
  await page.getByRole("button", { name: "Tạo nhóm công việc" }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByTestId("work-package-department").click();
  await expect(page.getByRole("option")).toHaveCount(departments.length);
});

test("package manager can edit package metadata in the browser", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const list = await api.get("/api/work-packages?purpose=LegalPermitting&pageSize=100", {
    headers,
  });
  expect(list.status()).toBe(200);
  const { items } = await list.json();
  const packageId = items.find((item: { status: string }) => item.status === "Active")?.id;
  expect(packageId, "active seeded legal package required").toBeTruthy();
  const currentResponse = await api.get(`/api/work-packages/${packageId}`, { headers });
  expect(currentResponse.status()).toBe(200);
  const original = await currentResponse.json();
  const updatedName = `${original.name} - kiểm thử sửa`;

  try {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/admin/work-packages/${packageId}`);
    await page.getByTestId("work-package-edit").click();
    const dialog = page.getByRole("dialog");
    await dialog.locator('input[maxlength="300"]').fill(updatedName);
    await dialog.getByRole("button", { name: "Lưu thay đổi" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page.getByRole("heading", { name: updatedName })).toBeVisible();
    const saved = await api.get(`/api/work-packages/${packageId}`, { headers });
    expect((await saved.json()).name).toBe(updatedName);
  } finally {
    const latestResponse = await api.get(`/api/work-packages/${packageId}`, { headers });
    if (latestResponse.ok()) {
      const latest = await latestResponse.json();
      if (latest.name !== original.name) {
        const restored = await api.put(`/api/work-packages/${packageId}`, {
          headers: { ...headers, "Idempotency-Key": randomUUID() },
          data: {
            name: original.name,
            description: original.description,
            contractId: original.contractId,
            status: original.status,
            plannedStart: original.plannedStart,
            plannedEnd: original.plannedEnd,
            rowVersion: latest.rowVersion,
          },
        });
        expect(restored.status()).toBe(200);
      }
    }
  }
});

test("general project package can link and unlink its contract from the edit dialog", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const contractsResponse = await api.get("/api/contracts?direction=Upstream&pageSize=100", {
    headers,
  });
  expect(contractsResponse.status()).toBe(200);
  const contracts = (await contractsResponse.json()).items as {
    id: number;
    contractNumber: string;
    operationalProjectId: number | null;
  }[];
  const contract = contracts.find((item) => item.operationalProjectId);
  expect(contract, "a seeded upstream contract linked to a project is required").toBeTruthy();
  const optionsResponse = await api.get("/api/work-packages/options", { headers });
  expect(optionsResponse.status()).toBe(200);
  const options = await optionsResponse.json();
  const lead = options.users.find(
    (user: { canLead: boolean; roleGroupId: number }) => user.canLead && user.roleGroupId,
  );
  expect(lead, "an active department lead is required").toBeTruthy();
  const created = await api.post("/api/work-packages", {
    headers: { ...headers, "Idempotency-Key": randomUUID() },
    data: {
      kind: "Project",
      purpose: "General",
      name: `Phối hợp hồ sơ hợp đồng dự án NICON ${Date.now()}`,
      operationalProjectId: contract!.operationalProjectId,
      departmentRoleGroupId: lead.roleGroupId,
      leadUserId: lead.userId,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const packageId = (await created.json()).id as number;

  try {
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(`/admin/work-packages/${packageId}`);
    await page.getByTestId("work-package-edit").click();
    let dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox").click();
    await page.getByRole("option", { name: contract!.contractNumber }).click();
    await dialog.getByRole("button", { name: "Save changes" }).click();
    await expect(dialog).toHaveCount(0);
    let saved = await api.get(`/api/work-packages/${packageId}`, { headers });
    expect(saved.status()).toBe(200);
    expect(await saved.json()).toMatchObject({ contractId: contract!.id, kind: "Contract" });
    await page.goto("/admin/work-packages?kind=Contract");
    await expect(
      page.getByTestId("work-package-card").filter({ hasText: contract!.contractNumber }),
    ).toBeVisible();
    await page.goto(`/admin/work-packages/${packageId}`);

    await page.getByTestId("work-package-edit").click();
    dialog = page.getByRole("dialog");
    await dialog.getByRole("combobox").click();
    await page.getByRole("option", { name: "No contract" }).click();
    await dialog.getByRole("button", { name: "Save changes" }).click();
    await expect(dialog).toHaveCount(0);
    saved = await api.get(`/api/work-packages/${packageId}`, { headers });
    expect(saved.status()).toBe(200);
    expect(await saved.json()).toMatchObject({ contractId: null, kind: "Project" });
  } finally {
    await hardDeleteBusinessRoot(api, headers, `/api/work-packages/${packageId}`);
  }
});

test("editing a task excludes descendants from predecessor choices", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const list = await api.get("/api/work-packages?purpose=LegalPermitting&pageSize=100", {
    headers,
  });
  expect(list.status()).toBe(200);
  const { items } = await list.json();
  const packageId = items.find((item: { status: string }) => item.status === "Active")?.id;
  expect(packageId, "active seeded legal package required").toBeTruthy();
  const detailResponse = await api.get(`/api/work-packages/${packageId}`, { headers });
  expect(detailResponse.status()).toBe(200);
  const detail = await detailResponse.json();
  const task = detail.tasks.find(
    (candidate: { id: number; status: string }) =>
      ["Planned", "InProgress"].includes(candidate.status) &&
      detail.tasks.some((other: { predecessorTaskIds: number[] }) =>
        other.predecessorTaskIds.includes(candidate.id),
      ),
  );
  expect(task, "an editable task with a dependent task is required").toBeTruthy();
  const descendant = detail.tasks.find((candidate: { predecessorTaskIds: number[] }) =>
    candidate.predecessorTaskIds.includes(task.id),
  );

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.setViewportSize({ width: 768, height: 900 });
  await page.goto(`/admin/work-packages/${packageId}`);
  await page.locator(`#task-${task.id}`).getByRole("button", { name: "Sửa" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText(`${descendant.wbsCode} · ${descendant.title}`)).toHaveCount(0);
  await expect(dialog.getByText("Người theo dõi (CC)")).toBeVisible();
});

test("work package workspace remains usable at mobile and tablet widths", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);

  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    await page.goto("/admin/work-packages");
    await expect(page.getByTestId("work-packages-page")).toBeVisible({ timeout: 15_000 });
    const dimensions = await page.locator("html").evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }));
    expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth);
  }
});

test("back from a work package detail restores the package list", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/work-packages");
  await expect(page.getByTestId("work-packages-page")).toBeVisible({ timeout: 15_000 });

  const firstPackage = page.getByTestId("work-package-card").first();
  await expect(firstPackage.or(page.getByText("Chưa có nhóm công việc phù hợp."))).toBeVisible();
  test.skip(
    (await firstPackage.count()) === 0,
    "The seeded environment has no work package to open",
  );
  await firstPackage.click();
  await expect(page).toHaveURL(/\/admin\/work-packages\/\d+$/);

  await page.getByTestId("work-package-back").click();

  await expect(page).toHaveURL(/\/admin\/work-packages$/);
  await expect(page.getByRole("heading", { name: /Nhóm công việc|Work packages/i })).toBeVisible();
  await expect(page.getByTestId("work-package-back")).toHaveCount(0);
});

for (const width of [390, 1440]) {
  test(`work package filters combine and survive reload at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const response = await api.get("/api/work-packages?kind=Project&pageSize=100", { headers });
    expect(response.status()).toBe(200);
    const { items } = await response.json();
    expect(items.length, "seeded project packages required").toBeGreaterThan(0);
    const fixture = items[0];
    await page.setViewportSize({ width, height: 900 });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/work-packages");
    const expectFiltered = async (action: () => Promise<unknown>, key: string, value: string) => {
      const loaded = page.waitForResponse((res) => {
        const url = new URL(res.url());
        return url.pathname === "/api/work-packages" && url.searchParams.get(key) === value;
      });
      await action();
      const result = await loaded;
      expect(result.status()).toBe(200);
      const body = await result.json();
      await expect(page.getByTestId("work-package-card")).toHaveCount(body.items.length);
      return body.items;
    };
    const departments = await expectFiltered(
      () => page.locator("#wp-department").selectOption(String(fixture.departmentRoleGroupId)),
      "departmentRoleGroupId",
      String(fixture.departmentRoleGroupId),
    );
    expect(departments.length).toBeGreaterThan(0);
    expect(
      departments.every(
        (item: { departmentRoleGroupId: number }) =>
          item.departmentRoleGroupId === fixture.departmentRoleGroupId,
      ),
    ).toBe(true);
    await expectFiltered(
      () => page.locator("#wp-status").selectOption(fixture.status),
      "status",
      fixture.status,
    );
    await expectFiltered(
      () => page.locator("#wp-purpose").selectOption(fixture.purpose),
      "purpose",
      fixture.purpose,
    );
    await expectFiltered(
      () => page.locator("#wp-project").selectOption(String(fixture.operationalProjectId)),
      "operationalProjectId",
      String(fixture.operationalProjectId),
    );
    await expectFiltered(
      () => page.locator("#wp-search").fill(fixture.code),
      "search",
      fixture.code,
    );
    await expect(page.getByTestId("work-package-card")).toHaveCount(1);
    await page.reload();
    await expect(page.locator("#wp-department")).toHaveValue(String(fixture.departmentRoleGroupId));
    await expect(page.locator("#wp-status")).toHaveValue(fixture.status);
    await expect(page.locator("#wp-purpose")).toHaveValue(fixture.purpose);
    await expect(page.locator("#wp-project")).toHaveValue(String(fixture.operationalProjectId));
    await expect(page.getByTestId("work-package-card")).toHaveCount(1);
    await page.screenshot({
      path: testInfo.outputPath(`work-package-filters-${width}.png`),
      fullPage: true,
    });
    await expectFiltered(
      () => page.locator("#wp-search").fill("NICON_NO_MATCH_74918"),
      "search",
      "NICON_NO_MATCH_74918",
    );
    await expect(page.getByText("Chưa có nhóm công việc phù hợp.")).toBeVisible();
    await page.getByRole("tab", { name: /Công việc nội bộ|Internal work/i }).click();
    await expect(page.locator("#wp-project")).toHaveCount(0);
    await expect(page.locator("#wp-department")).toHaveValue(String(fixture.departmentRoleGroupId));
    await expect(page).not.toHaveURL(/projectId=/);
    await page.getByRole("button", { name: "Xóa bộ lọc", exact: true }).click();
    await expect(page).toHaveURL(/\?kind=Internal$/);
    await expect(page.locator("#wp-department")).toHaveValue("");
    await expect(page.locator("#wp-status")).toHaveValue("");
    await expect(page.locator("#wp-purpose")).toHaveValue("");
    await expect(page.locator("#wp-search")).toHaveValue("");
    const dimensions = await page
      .locator("html")
      .evaluate((element) => ({ width: element.clientWidth, scroll: element.scrollWidth }));
    expect(dimensions.scroll).toBeLessThanOrEqual(dimensions.width);
  });
}

test("department filter works without project-catalog permission", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const code = `WORK_FILTER_${Date.now()}`;
  const role = await api.post("/api/admin/rbac/roles", {
    headers,
    data: {
      code,
      name: "NICON lọc công việc nội bộ",
      permissions: ["dashboard.view", "work.packages.view"],
    },
  });
  expect(role.status()).toBe(201);
  const { id: roleId } = await role.json();
  const user = {
    phoneNumber: `09${String(Date.now()).slice(-8)}`,
    password: "Admin@123",
    role: code,
  };
  let userId: number | undefined;
  try {
    const created = await api.post("/api/users", {
      headers,
      data: {
        ...user,
        fullName: "NICON kiểm tra bộ lọc phòng ban",
        email: `${code.toLowerCase()}@e2e.nihome.local`,
      },
    });
    expect(created.status()).toBe(201);
    userId = (await created.json()).id;
    const denied: string[] = [];
    page.on("response", (response) => {
      if (response.status() === 403) denied.push(response.url());
    });
    await loginInBrowserAs(page, user);
    await page.goto("/admin/work-packages?kind=Internal");
    await expect(page.locator("#wp-department option").nth(1)).toBeAttached();
    await expect(page.locator("#wp-project")).toHaveCount(0);
    const departmentId = await page.locator("#wp-department option").nth(1).getAttribute("value");
    const loaded = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/api/work-packages" &&
        new URL(response.url()).searchParams.get("departmentRoleGroupId") === departmentId,
    );
    await page.locator("#wp-department").selectOption(departmentId!);
    expect((await loaded).status()).toBe(200);
    expect(denied).toEqual([]);
  } finally {
    if (Number.isInteger(userId)) {
      execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${userId}`);
      execSql(`DELETE FROM refresh_tokens WHERE UserId = ${userId}`);
      execSql(`DELETE FROM users WHERE Id = ${userId}`);
    }
    expect((await api.delete(`/api/admin/rbac/roles/${roleId}`, { headers })).status()).toBe(200);
  }
});

test("project filter loads all catalog pages without requesting an extra page", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/operational-projects?page=1&pageSize=100", { headers });
  expect(response.status()).toBe(200);
  const catalog = await response.json();
  expect(Number.isInteger(catalog.total)).toBe(true);
  const requestedPages: number[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname === "/api/operational-projects" && url.searchParams.get("pageSize") === "100")
      requestedPages.push(Number(url.searchParams.get("page")));
  });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/work-packages");
  await expect(page.locator("#wp-project option")).toHaveCount(catalog.total + 1);
  const expectedPages = Array.from(
    { length: Math.max(1, Math.ceil(catalog.total / 100)) },
    (_, index) => index + 1,
  );
  expect(requestedPages).toEqual(expectedPages);
});
