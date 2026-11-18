import type { APIRequestContext } from "@playwright/test";
import { test, expect, TEST_USERS } from "../fixtures/auth";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

async function me(api: APIRequestContext, token: string) {
  const response = await api.get("/api/users/me", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as { id: number; fullName: string };
}

async function surveyNotifications(api: APIRequestContext, token: string, surveyId: number) {
  const response = await api.get("/api/notifications?take=50", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status(), await response.text()).toBe(200);
  const items = (await response.json()) as {
    templateCode?: string;
    refEntityType?: string;
    refEntityId?: number;
  }[];
  return items.filter(
    (item) =>
      item.templateCode === "crm.survey.assigned" &&
      item.refEntityType === "Survey" &&
      item.refEntityId === surveyId,
  );
}

const SEARCH_PLACEHOLDER = /Tìm|Search/i;

/**
 * NICON sends several people on a survey (feedback sheet r28). The lead
 * surveyor stays accountable; colleagues going along are listed on the survey,
 * can open it, and each is notified when added.
 */
test("people going on a survey are listed, can open it and are notified", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const adminToken = await loginAs(TEST_USERS.admin);
  const headers = { Authorization: `Bearer ${adminToken}` };
  const saleToken = await loginAs(TEST_USERS.sale);
  const managerToken = await loginAs(TEST_USERS.salesManager);
  const sale = await me(api, saleToken);
  const manager = await me(api, managerToken);
  const unique = Date.now().toString();
  let customerId = 0;
  let operationalProjectId = 0;
  let surveyId = 0;

  try {
    const customer = await api.post("/api/customers", {
      headers,
      data: {
        type: "Individual",
        name: `Khách khảo sát ${unique}`,
        sourceCode: "marketing",
        primaryContact: { fullName: "Anh Bình", phone: `09${unique.slice(-8)}`, isPrimary: true },
      },
    });
    expect(customer.status(), await customer.text()).toBe(201);
    customerId = (await customer.json()).id as number;
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { name: `Nhà phố khảo sát ${unique}`, customerId },
    });
    expect(project.status(), await project.text()).toBe(201);
    operationalProjectId = (await project.json()).id as number;

    const created = await api.post("/api/surveys", {
      headers,
      data: {
        location: `Số ${unique.slice(-3)} Lê Lợi, Quận 1`,
        surveyDate: "2026-12-01T00:00:00.000Z",
        surveyorUserId: manager.id,
        participantUserIds: [sale.id],
        operationalProjectId,
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    const survey = (await created.json()) as {
      id: number;
      code: string;
      participants: { userId: number }[];
    };
    surveyId = survey.id;
    expect(survey.participants.map((item) => item.userId)).toEqual([sale.id]);

    // The colleague going along is told and can open the survey from their own scope.
    await expect
      .poll(async () => (await surveyNotifications(api, saleToken, survey.id)).length)
      .toBe(1);
    expect(await surveyNotifications(api, managerToken, survey.id)).toHaveLength(1);
    const asSale = await api.get(`/api/surveys/${survey.id}`, {
      headers: { Authorization: `Bearer ${saleToken}` },
    });
    expect(asSale.status(), await asSale.text()).toBe(200);

    // The list shows the lead and the people going along.
    await loginInBrowserAs(page, TEST_USERS.admin);
    await page.goto("/admin/surveys", { waitUntil: "networkidle" });
    await page.getByPlaceholder(SEARCH_PLACEHOLDER).fill(survey.code);
    const row = page.locator("table tbody tr").filter({ hasText: survey.code });
    await expect(row).toContainText(manager.fullName);
    await expect(row).toContainText(sale.fullName);

    // Editing shows who is going; saving without changes tells nobody again.
    await row.getByRole("button", { name: /Sửa|Edit/i }).click();
    const dialog = page.getByRole("dialog");
    const companions = dialog.getByTestId("survey-participants");
    const saleBox = companions
      .locator("label")
      .filter({ hasText: sale.fullName })
      .getByRole("checkbox");
    await expect(saleBox).toBeChecked();
    // The lead cannot also be picked as a companion.
    await expect(companions.locator("label").filter({ hasText: manager.fullName })).toHaveCount(0);
    await dialog.getByRole("button", { name: /Lưu|Save/i }).click();
    await expect(dialog).toBeHidden();
    expect(await surveyNotifications(api, saleToken, survey.id)).toHaveLength(1);
  } finally {
    // Deleting the survey also removes who was going on it.
    if (surveyId) {
      const impact = await api.get(`/api/surveys/${surveyId}/deletion-impact`, { headers });
      expect(impact.status(), await impact.text()).toBe(200);
      const plan = (await impact.json()) as { planToken: string; requiredConfirmation: string };
      const deleted = await api.delete(`/api/surveys/${surveyId}`, {
        headers,
        data: { planToken: plan.planToken, confirmation: plan.requiredConfirmation },
      });
      expect(deleted.status(), await deleted.text()).toBe(204);
    }
    if (operationalProjectId)
      await hardDeleteBusinessRoot(
        api,
        headers,
        `/api/operational-projects/${operationalProjectId}`,
      );
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
