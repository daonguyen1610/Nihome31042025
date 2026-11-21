import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

// Sales often meet a new job before its operational project exists; the
// opportunity form creates it in place instead of sending them to Projects.
test("creating an opportunity can create and select its operational project in place", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const unique = Date.now();
  const customerName = `Công ty Cơ khí Bình Dương ${unique}`;
  const opportunityName = `Nhà xưởng cơ khí giai đoạn 2 ${unique}`;
  let customerId = 0;
  let projectId = 0;
  let opportunityId = 0;

  try {
    customerId = await createOwnCustomer(api, headers, customerName);
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.goto("/admin/opportunities", { waitUntil: "networkidle" });
    await page
      .getByRole("button", { name: /Tạo Cơ hội mới/ })
      .first()
      .click();
    const dialog = page.getByRole("dialog", { name: "Tạo Cơ hội mới" });

    // Without a customer there is nothing to attach a project to.
    await expect(dialog.getByText("Chọn khách hàng trước để chọn hoặc tạo dự án.")).toBeVisible();
    await expect(dialog.getByTestId("opportunity-project-quick-create")).toHaveCount(0);

    await dialog.locator("input").first().fill(opportunityName);
    await dialog.getByRole("combobox").first().click();
    await page.getByRole("option", { name: new RegExp(customerName) }).click();
    await expect(dialog.getByText("Khách hàng này chưa có dự án nào.")).toBeVisible();

    await dialog.getByTestId("opportunity-project-quick-create").click();
    const form = dialog.getByTestId("opportunity-project-quick-form");
    const name = form.getByLabel("Tên dự án");
    // Prefilled with the opportunity's name; an empty name is refused without a request.
    await expect(name).toHaveValue(opportunityName);
    let projectPosts = 0;
    page.on("request", (request) => {
      if (
        request.method() === "POST" &&
        new URL(request.url()).pathname === "/api/operational-projects"
      )
        projectPosts++;
    });
    await name.fill("  ");
    await form.getByRole("button", { name: "Tạo dự án" }).click();
    await expect(form.getByRole("alert")).toHaveText("Nhập tên dự án trước khi tạo.");
    expect(projectPosts).toBe(0);

    // The code is prefilled with the next PJ code, as on the Projects page, and can be edited.
    const code = form.getByLabel("Mã dự án");
    await expect(code).toHaveValue(/^PJ-\d{4}-\d+$/);
    // A code another project already uses is refused inline, keeping what was typed.
    const existing = await (
      await api.get("/api/operational-projects?pageSize=1", { headers })
    ).json();
    const takenCode = existing.items[0].code as string;
    const projectName = `Nhà xưởng cơ khí Bình Dương ${unique}`;
    await name.fill(projectName);
    await code.fill(takenCode);
    await name.press("Enter");
    await expect(form.getByRole("alert")).toBeVisible();
    await expect(name).toHaveValue(projectName);

    const customCode = `bd-${String(unique).slice(-8)}`;
    await code.fill(customCode);
    await name.press("Enter");
    await expect(form).toHaveCount(0);
    // The new project is selected under the edited code, upper-cased like on the Projects page.
    await expect(dialog.getByRole("combobox", { name: "Dự án" })).toContainText(
      `${customCode.toUpperCase()} · ${projectName}`,
    );
    expect(projectPosts).toBe(2);

    await dialog.getByRole("button", { name: "Tạo Cơ hội mới" }).click();
    await expect(dialog).toHaveCount(0);
    const list = await (
      await api.get(`/api/opportunities?search=${encodeURIComponent(opportunityName)}`, { headers })
    ).json();
    const created = list.items.find((item: { name: string }) => item.name === opportunityName);
    opportunityId = created.id;
    projectId = created.operationalProjectId;
    const project = await (
      await api.get(`/api/operational-projects/${projectId}`, { headers })
    ).json();
    expect(project).toMatchObject({
      code: customCode.toUpperCase(),
      name: projectName,
      customerId,
    });
  } finally {
    if (opportunityId)
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
