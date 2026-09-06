import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";

const uid = () => Math.random().toString(36).slice(2, 8);

const expectOk = async (
  response: { ok: () => boolean; status: () => number; text: () => Promise<string> },
  action: string,
) => {
  const body = await response.text();
  expect(response.ok(), `${action} (${response.status()}): ${body}`).toBeTruthy();
  return body ? JSON.parse(body) as Record<string, unknown> : {};
};

test.describe("NIH-461 — Project operational reports", () => {
  test("renders scoped partial data, URL filters, exports, and permission states", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();

    const adminToken = await loginAs(TEST_USERS.superAdmin);
    const adminHeaders = { Authorization: `Bearer ${adminToken}` };
    const usersResponse = await api.get(
      `/api/users?role=PM&search=${TEST_USERS.pm.phoneNumber}&take=10`,
      { headers: adminHeaders },
    );
    const users = await expectOk(usersResponse, "load PM") as {
      items?: Array<{ id: number; phoneNumber: string }>;
    };
    const projectManager = users.items?.find((user) => user.phoneNumber === TEST_USERS.pm.phoneNumber);
    expect(projectManager?.id).toBeGreaterThan(0);

    const customerId = await createOwnCustomer(api, adminHeaders, "reports");
    const suffix = uid();
    const projectName = `E2E report ${suffix}`;
    const secondProjectName = `E2E report secondary ${suffix}`;
    const project = await expectOk(await api.post("/api/operational-projects", {
      headers: adminHeaders,
      data: {
        name: projectName,
        customerId,
        projectManagerUserId: projectManager!.id,
        startDate: "2026-09-01",
        endDate: "2026-12-31",
      },
    }), "create operational project") as { id: number };
    const secondProject = await expectOk(await api.post("/api/operational-projects", {
      headers: adminHeaders,
      data: {
        name: secondProjectName,
        customerId,
        projectManagerUserId: projectManager!.id,
        startDate: "2026-09-01",
        endDate: "2026-12-31",
      },
    }), "create second operational project") as { id: number };

    await loginInBrowserAs(page, TEST_USERS.bgd);
    await page.goto(`${baseURL}/admin/reports/projects`, { waitUntil: "networkidle" });
    await page.evaluate(() => document.querySelector('[aria-label*="Notifications ("]')?.remove());

    const reportsPage = page.getByTestId("project-reports-page");
    await expect(reportsPage).toBeVisible();
    await expect(reportsPage).not.toContainText("projectReports.");
    await expect(reportsPage.getByText(projectName, { exact: true })).toBeVisible();
    await expect(reportsPage.getByText(/Dữ liệu một phần|Partial data/i).first()).toBeVisible();
    const projectTrigger = reportsPage.getByTestId(`project-report-trigger-${project.id}`);
    const projectContent = reportsPage.getByTestId(`project-report-content-${project.id}`);
    const secondProjectContent = reportsPage.getByTestId(`project-report-content-${secondProject.id}`);
    await expect(projectTrigger).toHaveAttribute("aria-expanded", "false");
    await expect(projectContent).toBeHidden();
    await expect(secondProjectContent).toBeHidden();

    await projectTrigger.click();
    await expect(projectTrigger).toHaveAttribute("aria-expanded", "true");
    await expect(projectContent).toBeVisible();
    await expect(secondProjectContent).toBeHidden();
    await expect(projectContent.getByText(/Tài chính theo hợp đồng|Contractual finance/i)).toBeVisible();
    await expect(projectContent.getByText(/không phải dòng tiền|not cash, revenue/i)).toBeVisible();

    await projectTrigger.focus();
    await page.keyboard.press("Enter");
    await expect(projectTrigger).toHaveAttribute("aria-expanded", "false");
    await expect(projectContent).toBeHidden();

    await reportsPage.locator("#report-project").click();
    await page.getByRole("option", { name: new RegExp(projectName) }).click();
    await reportsPage.locator("#report-from").fill("2026-09-01");
    await reportsPage.locator("#report-to").fill("2026-09-30");
    await expect.poll(() => new URL(page.url()).searchParams.get("project")).toBe(String(project.id));
    await expect.poll(() => new URL(page.url()).searchParams.get("from")).toBe("2026-09-01");
    await expect.poll(() => new URL(page.url()).searchParams.get("to")).toBe("2026-09-30");
    await expect(reportsPage.getByTestId(`project-report-trigger-${project.id}`)).toHaveAttribute("aria-expanded", "true");
    await expect(reportsPage.getByTestId(`project-report-content-${project.id}`)).toBeVisible();

    const [download] = await Promise.all([
      page.waitForEvent("download"),
      page.getByTestId("project-reports-export-xlsx").click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/project-operational-report-.*\.xlsx$/);

    await page.setViewportSize({ width: 390, height: 844 });
    await expect(reportsPage).toBeVisible();
    await reportsPage.getByTestId(`project-report-trigger-${project.id}`).click();
    await expect(reportsPage.getByTestId(`project-report-content-${project.id}`)).toBeHidden();
    await expect(reportsPage.getByText(projectName, { exact: true })).toBeVisible();
    await expect.poll(() => page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    )).toBe(true);

    await loginInBrowserAs(page, TEST_USERS.pm);
    await page.goto(`${baseURL}/admin/reports/projects`, { waitUntil: "networkidle" });
    await page.evaluate(() => document.querySelector('[aria-label*="Notifications ("]')?.remove());
    await expect(page.getByTestId("project-reports-page")).toBeVisible();
    await expect(page.getByText(projectName, { exact: true })).toBeVisible();
    await expect(page.getByTestId("project-reports-export-xlsx")).toHaveCount(0);
    await expect(page.getByTestId("project-reports-export-pdf")).toHaveCount(0);

    await loginInBrowserAs(page, TEST_USERS.warehouse);
    await page.goto(`${baseURL}/admin/reports/projects`, { waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: "403" })).toBeVisible();
    await expect(page.getByTestId("project-reports-page")).toHaveCount(0);
  });
});