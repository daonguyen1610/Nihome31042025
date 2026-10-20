import { expect, test, TEST_USERS } from "../fixtures/auth";

test("UI only: delayed preview locks the schedule and refresh clears a recovered error", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/contracts?direction=Upstream&pageSize=1", { headers });
  expect(response.ok()).toBe(true);
  const id = (await response.json()).items[0].id as number;
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => {
    if (window === window.top) localStorage.setItem("nicon_lang", "vi");
  });
  let firstRead = true;
  await page.route(`**/contracts/${id}/reminders`, async (route) => {
    if (firstRead) {
      firstRead = false;
      await route.fulfill({ status: 500, json: { message: "Reminder history unavailable" } });
    } else await route.fulfill({ json: [] });
  });
  let release!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let requested = false;
  await page.route(`**/contracts/${id}/reminders/preview`, async (route) => {
    requested = true;
    const request = route.request().postDataJSON();
    await gate;
    await route.fulfill({
      json: {
        previewToken: "ui-only-not-schedulable",
        kind: request.kind,
        reminderDate: `${request.reminderDate}T00:00:00`,
        contactId: 1,
        toEmail: "review@example.test",
        subject: "NICON preview",
        body: "<p>Preview</p>",
        contacts: [{ id: 1, name: "An Phú", email: "review@example.test" }],
      },
    });
  });
  await page.goto(`/admin/contracts/${id}?tab=care`);
  const panel = page.getByRole("region", { name: "Email nhắc thời hạn hợp đồng" });
  await expect(panel.getByRole("alert")).toContainText("Reminder history unavailable");
  await panel.getByRole("button", { name: "Tải lại", exact: true }).click();
  await expect(panel.getByRole("alert")).toHaveCount(0);
  const day = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(new Date());
  await panel.locator("#reminder-date").fill(day);
  await panel.getByRole("button", { name: "Xem trước email", exact: true }).click();
  await expect.poll(() => requested).toBe(true);
  await expect(panel.locator("#reminder-date")).toBeDisabled();
  await expect(panel.locator("#reminder-kind")).toBeDisabled();
  release();
  await expect(panel.getByTestId("reminder-preview-schedule")).toContainText(day);
  await expect(panel.getByTestId("reminder-preview-schedule")).toContainText("Nhắc gia hạn");
  await panel.locator("#reminder-kind").selectOption("End");
  await expect(panel.getByRole("button", { name: "Xác nhận lịch gửi tự động" })).toHaveCount(0);
});
