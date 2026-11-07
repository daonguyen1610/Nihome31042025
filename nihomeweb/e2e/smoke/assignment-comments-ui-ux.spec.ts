import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("assignment comment failures are localized and deletion can be cancelled", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON comment recovery");
  let designProjectId: number | undefined;
  let projectId: number | undefined;
  try {
    designProjectId = await createDesignProject(api, {
      headers,
      name: `NICON comment recovery ${Date.now()}`,
      customerId,
    });
    const detail = await (
      await api.get(`/api/design-projects/${designProjectId}`, { headers })
    ).json();
    projectId = detail.operationalProjectId;
    const users = await (await api.get("/api/users?take=200", { headers })).json();
    const architect = users.items.find(
      (user: { phoneNumber: string }) => user.phoneNumber === TEST_USERS.architect.phoneNumber,
    );
    const memberResponse = await api.post(`/api/operational-projects/${projectId}/team/members`, {
      headers,
      data: {
        userId: architect.id,
        position: "Architect",
        startedAt: new Date(Date.now() - 86400000).toISOString(),
        roles: [{ roleCode: "Architect", scope: "Module", scopeValue: "Design" }],
        designProjectId,
      },
    });
    expect(memberResponse.status(), await memberResponse.text()).toBe(201);
    const member = await memberResponse.json();
    const assignmentResponse = await api.post(
      `/api/operational-projects/${projectId}/team/assignments`,
      {
        headers,
        data: {
          workKey: `DES-COMMENT-${Date.now()}`,
          title: "Kiểm tra hồ sơ mặt bằng",
          module: "Design",
          assigneeMemberId: member.id,
          status: "Planned",
          designProjectId,
        },
      },
    );
    expect(assignmentResponse.status(), await assignmentResponse.text()).toBe(201);
    const assignment = await assignmentResponse.json();
    const commentUrl = `/api/operational-projects/${projectId}/team/assignments/${assignment.id}/comments?designProjectId=${designProjectId}`;
    const comment = await api.post(commentUrl, {
      headers,
      data: { body: "Đối chiếu hồ sơ mặt bằng trước khi trình duyệt." },
    });
    expect(comment.status(), await comment.text()).toBe(201);
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.setViewportSize({ width: 390, height: 844 });
    let failCommentLoad = true;
    await page.route(/\/team\/assignments\/\d+\/comments(?:\?|$)/, (route) => {
      if (failCommentLoad && route.request().method() === "GET")
        return route.abort("internetdisconnected");
      return route.continue();
    });
    await page.goto(`/admin/design-projects/${designProjectId}?tab=team`);
    const card = page.locator("article").filter({ hasText: assignment.workKey });
    await card.getByRole("button", { name: /Bình luận/ }).click();
    await expect(card.getByRole("alert")).toBeVisible();
    await expect(card.getByRole("alert")).not.toContainText("Network Error");
    await expect(card.getByText("Chưa có bình luận nào.")).toHaveCount(0);
    failCommentLoad = false;
    await card.getByRole("button", { name: "Thử lại" }).click();
    await expect(card.getByText("Đối chiếu hồ sơ mặt bằng trước khi trình duyệt.")).toBeVisible();
    const composer = card.getByRole("textbox", { name: "Viết bình luận..." });
    await composer.fill("Ghi chú chưa gửi khi mất kết nối.");
    await page.context().setOffline(true);
    await card.getByRole("button", { name: "Gửi bình luận" }).click();
    await expect(card.getByRole("alert")).toBeVisible();
    await expect(card.getByRole("alert")).not.toContainText("Network Error");
    await expect(composer).toHaveValue("Ghi chú chưa gửi khi mất kết nối.");
    await page.context().setOffline(false);
    await card.getByRole("button", { name: "Xoá bình luận", exact: true }).click();
    const dialog = page.getByRole("alertdialog");
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: /Hủy|Huỷ/ }).click();
    await expect(dialog).not.toBeVisible();
    await expect(card.getByRole("button", { name: "Xoá bình luận", exact: true })).toBeFocused();
    const comments = await (await api.get(commentUrl, { headers })).json();
    expect(comments).toHaveLength(1);
    await card.getByRole("button", { name: "Xoá bình luận", exact: true }).click();
    await page.context().setOffline(true);
    await dialog.getByRole("button", { name: "Xoá", exact: true }).click();
    await expect(dialog.getByRole("alert")).toBeVisible();
    await expect(dialog.getByRole("alert")).not.toContainText("Network Error");
    await expect(dialog).toBeVisible();
    await page.context().setOffline(false);
    await dialog.getByRole("button", { name: "Xoá", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await expect(card.getByText("Chưa có bình luận nào.")).toBeVisible();
    expect(await (await api.get(commentUrl, { headers })).json()).toHaveLength(0);
    await page.screenshot({ path: "test-results/assignment-comments-mobile.png", fullPage: true });
  } finally {
    await page.context().setOffline(false);
    if (designProjectId)
      await hardDeleteBusinessRoot(api, headers, `/api/design-projects/${designProjectId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
