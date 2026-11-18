import type { APIRequestContext } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

async function deleteSurvey(api: APIRequestContext, headers: Record<string, string>, id: number) {
  const impact = await api.get(`/api/surveys/${id}/deletion-impact`, { headers });
  expect(impact.status(), await impact.text()).toBe(200);
  const plan = (await impact.json()) as { planToken: string; requiredConfirmation: string };
  const deleted = await api.delete(`/api/surveys/${id}`, {
    headers,
    data: { planToken: plan.planToken, confirmation: plan.requiredConfirmation },
  });
  expect(deleted.status(), await deleted.text()).toBe(204);
}

/**
 * NICON wants to see when a survey is booked and whether it took place
 * (feedback sheet r28). Someone on the survey records it as surveyed; a planned
 * survey past its day without a record is flagged.
 */
test("a survey shows its time, is recorded as surveyed and can be reopened", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const token = await loginAs(TEST_USERS.admin);
  const headers = { Authorization: `Bearer ${token}` };
  const unique = Date.now().toString();
  let customerId = 0;
  let operationalProjectId = 0;
  const surveyIds: number[] = [];

  try {
    const customer = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Khách lịch khảo sát ${unique}`,
        sourceCode: "marketing",
        primaryContact: { fullName: "Chị Hoa", phone: `09${unique.slice(-8)}`, isPrimary: true },
      },
    });
    expect(customer.status(), await customer.text()).toBe(201);
    customerId = (await customer.json()).id as number;
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { name: `Biệt thự khảo sát ${unique}`, customerId },
    });
    expect(project.status(), await project.text()).toBe(201);
    operationalProjectId = (await project.json()).id as number;

    const create = async (location: string, surveyDate: string, surveyTime?: string) => {
      const response = await api.post("/api/surveys", {
        headers,
        data: { location, surveyDate, surveyTime, operationalProjectId },
      });
      expect(response.status(), await response.text()).toBe(201);
      const survey = (await response.json()) as { id: number; code: string };
      surveyIds.push(survey.id);
      return survey;
    };
    const upcoming = await create(
      `Lô D${unique.slice(-3)} Thảo Điền`,
      "2099-03-10T00:00:00.000Z",
      "08:30:00",
    );
    const missed = await create(`Hẻm ${unique.slice(-3)} Bình Thạnh`, "2020-01-15T00:00:00.000Z");

    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/surveys", { waitUntil: "networkidle" });
    await page.getByPlaceholder(/Tìm mã|Search/i).fill(upcoming.code);
    const row = page.locator("table tbody tr").filter({ hasText: upcoming.code });
    await expect(row).toContainText("08:30");
    await expect(row).toContainText("Chưa khảo sát");

    // Record it as surveyed from the detail page.
    await page.goto(`/admin/surveys/${upcoming.id}`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Ghi nhận đã khảo sát" }).click();
    await expect(page.getByRole("button", { name: "Mở lại: chưa khảo sát" })).toBeVisible();
    await expect(page.getByText(/Đã khảo sát · .+ ghi nhận lúc/)).toBeVisible();

    // Recording twice is refused with a reason.
    const twice = await api.post(`/api/surveys/${upcoming.id}/complete`, { headers });
    expect(twice.status()).toBe(400);
    expect(await twice.text()).toContain("đã được ghi nhận");

    // The history names the step in words, not as an audit code.
    await expect
      .poll(
        async () => {
          await page.getByRole("tab", { name: /Thông tin|Info/i }).click();
          const loaded = page.waitForResponse((response) =>
            response.url().includes(`/api/surveys/${upcoming.id}/timeline`),
          );
          await page.getByRole("tab", { name: /Nhật ký|History|Timeline/i }).click();
          await loaded;
          await expect(page.getByRole("tabpanel")).not.toContainText("Đang tải");
          return page.getByRole("tabpanel").innerText();
        },
        { timeout: 15_000 },
      )
      .toContain("Ghi nhận đã khảo sát");
    await expect(page.getByRole("tabpanel")).not.toContainText("survey.complete");

    await page.getByRole("button", { name: "Mở lại: chưa khảo sát" }).click();
    await expect(page.getByRole("button", { name: "Ghi nhận đã khảo sát" })).toBeVisible();

    // A planned survey past its day is flagged with what to do next.
    await page.goto(`/admin/surveys/${missed.id}`, { waitUntil: "networkidle" });
    await expect(page.getByText("Quá ngày, chưa ghi nhận").first()).toBeVisible();
    await expect(page.getByRole("status").filter({ hasText: "chưa ai ghi nhận" })).toBeVisible();
  } finally {
    for (const id of surveyIds) await deleteSurvey(api, headers, id);
    if (operationalProjectId)
      await hardDeleteBusinessRoot(
        api,
        headers,
        `/api/operational-projects/${operationalProjectId}`,
      );
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
