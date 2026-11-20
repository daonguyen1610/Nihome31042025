import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * SPA smoke for NIH-225 — workflow config admin page. Full API and RBAC
 * behaviour is covered by nihomebackend.integration.tests/WorkflowsControllerTests;
 * this spec only verifies the deployed SPA renders the page for SUPER_ADMIN
 * without JS errors and that the search + create button surface correctly.
 */
test("SPA renders /admin/workflows without console errors for SUPER_ADMIN", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const jsErrors: string[] = [];
  page.on("pageerror", (err) => jsErrors.push(err.message));

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`${baseURL}/admin/workflows`, { waitUntil: "networkidle" });

  await expect(
    page.getByRole("heading", {
      name: /Cấu hình luồng duyệt|Approval workflows|审批流程配置|承認フロー設定/i,
    }),
  ).toBeVisible();

  await expect(page.locator("#wf-search")).toBeVisible();

  expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});

// Non-technical administrators pick the business action by name; technical codes only
// appear for an action outside the list, and an unnamed step is named after its approver.
test("an approval workflow is set up by business action and approver, without codes", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const action = `kiem-tra-${Date.now()}`;
  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
  await page.goto("/admin/workflows", { waitUntil: "networkidle" });

  try {
    await page.getByRole("button", { name: "Thêm luồng duyệt" }).first().click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.locator("#wf-module")).toHaveCount(0);

    // Saving without a business action says what to choose.
    await dialog.getByRole("button", { name: "Lưu luồng duyệt" }).click();
    await expect(dialog.getByRole("alert")).toContainText("Chọn nghiệp vụ cần duyệt");

    // One workflow per business action: the quote approval already has one.
    await dialog.getByTestId("wf-business").click();
    await expect(
      page.getByRole("option", { name: /Báo giá – duyệt nội bộ.*đã có luồng/ }),
    ).toHaveAttribute("data-disabled", "");
    await page.getByRole("option", { name: "Nghiệp vụ khác (nhập mã kỹ thuật)" }).click();
    await dialog.locator("#wf-module").fill("e2e.workflow");
    await dialog.locator("#wf-action").fill(action);
    await dialog.locator("#wf-name").fill("Kiểm tra luồng duyệt");

    // The approver is the only thing a step needs.
    await dialog.getByTestId("wf-step-0").getByRole("combobox").click();
    await page.getByRole("option", { name: "Trưởng phòng Cung ứng", exact: true }).click();
    const saved = page.waitForResponse(
      (response) =>
        response.request().method() === "POST" && response.url().endsWith("/api/workflows"),
    );
    await dialog.getByRole("button", { name: "Lưu luồng duyệt" }).click();
    expect((await saved).status()).toBe(201);

    const created = (
      (await (await api.get("/api/workflows?includeInactive=true", { headers })).json()) as {
        action: string;
        module: string;
        steps: { name: string; approverRoleCode: string }[];
      }[]
    ).find((item) => item.action === action)!;
    expect(created.module).toBe("e2e.workflow");
    expect(created.steps).toEqual([
      expect.objectContaining({
        name: "Trưởng phòng Cung ứng duyệt",
        approverRoleCode: "PROCUREMENT_MANAGER",
      }),
    ]);

    // The quote workflow states the rule the app applies.
    await page
      .getByRole("row", { name: /Duyệt báo giá/ })
      .getByRole("button", { name: "Sửa" })
      .click();
    await expect(page.getByRole("dialog")).toContainText(
      "người gửi chọn một Trưởng phòng Cung ứng để duyệt",
    );
  } finally {
    const all = (await (
      await api.get("/api/workflows?includeInactive=true", { headers })
    ).json()) as {
      id: number;
      action: string;
    }[];
    for (const item of all.filter((entry) => entry.action === action))
      await api.delete(`/api/workflows/${item.id}`, { headers });
  }
});
