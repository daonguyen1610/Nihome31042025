import path from "node:path";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

// NICON feedback row 15 and PRD v2 answer 11: a master schedule made in MS Project is saved
// as XML and imported; the rows become a draft that is checked, saved and sent for approval.
// The file is NICON's own MMV master schedule (docs/Nicon/MoMs/2026-10-06/ms_project).
const MMV_XML = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../../../nihomebackend.tests/TestData/MsProject/nicon-mmv-master-schedule.xml",
);

test("an MS Project master schedule is imported into a draft with its links", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const admin = { Authorization: `Bearer ${await loginAs(TEST_USERS.admin)}` };
  const suffix = randomUUID().slice(0, 6);
  const cleanup: string[] = [];

  try {
    const customer = await api.post("/api/customers", {
      headers: admin,
      data: {
        type: "Company",
        name: `Công ty TNHH MMV Việt Nam ${suffix}`,
        sourceCode: "referral",
        primaryContact: {
          fullName: "Anh Minh",
          phone: `09${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
          isPrimary: true,
        },
      },
    });
    expect(customer.status(), await customer.text()).toBe(201);
    cleanup.push(`/api/customers/${(await customer.json()).id}`);
    const project = await api.post("/api/operational-projects", {
      headers: admin,
      data: { name: `Nhà máy MMV ${suffix}`, customerId: (await customer.json()).id },
    });
    expect(project.status(), await project.text()).toBe(201);
    const projectId = (await project.json()).id as number;
    cleanup.unshift(`/api/operational-projects/${projectId}`);

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto(`/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });
    const schedule = page.locator("#project-master-schedule");
    await expect(schedule.getByRole("button", { name: "Nhập từ MS Project (.xml)" })).toBeVisible();
    await schedule.getByTestId("schedule-import-input").setInputFiles(MMV_XML);

    await expect(
      page.getByText("Đã nhập 40 hạng mục từ nicon-mmv-master-schedule.xml").first(),
    ).toBeVisible();
    await expect(schedule.getByLabel("Lý do cập nhật từ khách hàng")).toHaveValue(
      "Nhập từ MS Project: nicon-mmv-master-schedule.xml",
    );
    // 1.2 follows 1.1 finish-to-start, as in MS Project.
    await expect(schedule.getByText("Sau: 1.1 FS").first()).toBeVisible();

    const saved = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/operational-projects/${projectId}/master-schedule/revisions`) &&
        response.request().method() === "POST",
    );
    await schedule.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
    expect((await saved).status()).toBe(200);

    const body = await (
      await api.get(`/api/operational-projects/${projectId}/master-schedule`, { headers: admin })
    ).json();
    const draft = body.revisions[0];
    expect(draft.status).toBe("Draft");
    expect(draft.items).toHaveLength(40);
    expect(
      draft.items.reduce(
        (sum: number, item: { predecessors: unknown[] }) => sum + item.predecessors.length,
        0,
      ),
    ).toBe(40);
  } finally {
    for (const resource of cleanup)
      await hardDeleteBusinessRoot(api, admin, resource).catch(() =>
        hardDeleteBusinessRoot(api, admin, resource),
      );
  }
});
