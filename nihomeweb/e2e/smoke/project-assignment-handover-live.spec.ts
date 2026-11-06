import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("project assignment can be handed over and returned to its first assignee", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  test.setTimeout(90_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON handover test");
  let projectId: number | undefined;
  try {
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { name: "NICON handover test project", customerId },
    });
    expect(project.ok(), await project.text()).toBeTruthy();
    projectId = (await project.json()).id;
    const candidates = await api.get(`/api/operational-projects/${projectId}/team/candidates`, {
      headers,
    });
    expect(candidates.ok()).toBeTruthy();
    const people = (await candidates.json()).slice(0, 2) as { userId: number; name: string }[];
    expect(people).toHaveLength(2);
    const members: number[] = [];
    for (const person of people) {
      const response = await api.post(`/api/operational-projects/${projectId}/team/members`, {
        headers: { ...headers, "Idempotency-Key": randomUUID() },
        data: {
          userId: person.userId,
          position: "Project team member",
          startedAt: "2026-01-01T00:00:00Z",
          roles: [{ roleCode: "Observer", scope: "Project" }],
        },
      });
      expect(response.ok(), await response.text()).toBeTruthy();
      members.push((await response.json()).id);
    }
    const assignment = await api.post(`/api/operational-projects/${projectId}/team/assignments`, {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      data: {
        workKey: `HANDOVER-${Date.now()}`,
        title: "Prepare project documents",
        module: "Design",
        assigneeMemberId: members[0],
        status: "Planned",
      },
    });
    expect(assignment.ok(), await assignment.text()).toBeTruthy();
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/operational-projects/${projectId}`);
    for (const index of [1, 0]) {
      const active = page
        .locator("article")
        .filter({ hasText: "Prepare project documents" })
        .filter({ has: page.getByRole("button", { name: "Edit assignment" }) });
      await active.getByRole("button", { name: "Edit assignment" }).click();
      const dialog = page.getByRole("dialog", { name: "Edit work assignment" });
      await expect(
        dialog.getByText(
          "Changing the assignee keeps the previous assignment and creates a new one.",
        ),
      ).toBeVisible();
      await dialog.getByRole("combobox", { name: "Assignee" }).click();
      await page.getByRole("option").filter({ hasText: people[index].name }).click();
      await dialog.getByRole("button", { name: "Save changes", exact: true }).click();
      await expect(dialog).toBeHidden();
      await expect(active).toContainText(people[index].name);
    }
    await expect(
      page.locator("article").filter({ hasText: "Prepare project documents" }),
    ).toHaveCount(3);
  } finally {
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
