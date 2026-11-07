import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("design assignment keeps one subtask level and accepts attachments", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON subtask attachments");
  let designProjectId: number | undefined;
  let operationalProjectId: number | undefined;
  try {
    designProjectId = await createDesignProject(api, {
      headers,
      name: `NICON – Design subtask ${Date.now()}`,
      customerId,
    });
    const detail = await api.get(`/api/design-projects/${designProjectId}`, { headers });
    expect(detail.ok(), await detail.text()).toBeTruthy();
    operationalProjectId = (await detail.json()).operationalProjectId;
    const users = await api.get("/api/users?take=200", { headers });
    const architect = (await users.json()).items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.architect.phoneNumber,
    );
    const memberResponse = await api.post(
      `/api/operational-projects/${operationalProjectId}/team/members`,
      {
        headers: { ...headers, "Idempotency-Key": `e2e-subtask-member-${Date.now()}` },
        data: {
          userId: architect.id,
          position: "Architect",
          startedAt: new Date(Date.now() - 86_400_000).toISOString(),
          roles: [{ roleCode: "Architect", scope: "Module", scopeValue: "Design" }],
          designProjectId,
        },
      },
    );
    expect(memberResponse.status(), await memberResponse.text()).toBe(201);
    const member = await memberResponse.json();
    const rootWorkKey = `DES-E2E-ROOT-${Date.now()}`;
    const rootResponse = await api.post(
      `/api/operational-projects/${operationalProjectId}/team/assignments`,
      {
        headers: { ...headers, "Idempotency-Key": `e2e-subtask-root-${Date.now()}` },
        data: {
          workKey: rootWorkKey,
          title: "Root work package",
          module: "Design",
          assigneeMemberId: member.id,
          status: "Planned",
          designProjectId,
        },
      },
    );
    expect(rootResponse.status(), await rootResponse.text()).toBe(201);
    const root = await rootResponse.json();

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(`/admin/design-projects/${designProjectId}?tab=team`);
    const rootCard = page.locator("article").filter({ hasText: rootWorkKey });
    await rootCard.getByRole("button", { name: "Add subtask" }).click();

    const dialog = page.getByRole("dialog");
    const subtaskWorkKey = `DES-E2E-SUB-${Date.now()}`;
    await dialog.locator('input[maxlength="120"]').fill(subtaskWorkKey);
    await dialog.locator('input[maxlength="300"]').fill("Detailed shop drawing");
    const created = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname ===
          `/api/operational-projects/${operationalProjectId}/team/assignments` &&
        response.request().method() === "POST",
    );
    await dialog.getByRole("button", { name: "Save changes" }).click();
    const response = await created;
    expect(response.status(), await response.text()).toBe(201);
    const subtask = await response.json();
    expect(subtask.parentAssignmentId).toBe(root.id);

    const subtaskCard = page.locator('article[data-subtask="true"]').filter({
      hasText: subtaskWorkKey,
    });
    await expect(subtaskCard).toBeVisible();
    await expect(rootCard.getByRole("button", { name: "Add subtask" })).toBeVisible();
    await expect(subtaskCard.getByRole("button", { name: "Add subtask" })).toHaveCount(0);

    await subtaskCard.getByRole("button", { name: /Attachments/ }).click();
    await subtaskCard.locator(`[data-testid="assignment-${subtask.id}-attachment"]`).setInputFiles([
      { name: "spec-a.pdf", mimeType: "application/pdf", buffer: Buffer.from("first") },
      { name: "spec-b.pdf", mimeType: "application/pdf", buffer: Buffer.from("second") },
    ]);
    await expect(subtaskCard.locator("ul li")).toHaveCount(2);
    await expect(subtaskCard.getByText("spec-a.pdf")).toBeVisible();
    await expect(subtaskCard.getByText("spec-b.pdf")).toBeVisible();

    const listed = await api.get(
      `/api/operational-projects/${operationalProjectId}/team/assignments/${subtask.id}/attachments?designProjectId=${designProjectId}`,
      { headers },
    );
    expect(listed.status(), await listed.text()).toBe(200);
    expect(
      (await listed.json()).map((item: { originalFileName: string }) => item.originalFileName),
    ).toEqual(["spec-a.pdf", "spec-b.pdf"]);
  } finally {
    if (designProjectId)
      await hardDeleteBusinessRoot(api, headers, `/api/design-projects/${designProjectId}`);
    if (operationalProjectId)
      await hardDeleteBusinessRoot(
        api,
        headers,
        `/api/operational-projects/${operationalProjectId}`,
      );
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
