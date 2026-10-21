import { execSql } from "../fixtures/db";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";

test("Concept dossier renders drafts and persists customer feedback", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
  baseURL,
}, testInfo) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const headers = { Authorization: `Bearer ${token}` };
  const customerId = await createOwnCustomer(api, headers, "concept-dossier");
  const name = `Concept browser ${Date.now().toString(36)}`;
  const projectId = await createDesignProject(api, { headers, name, customerId });
  const option = await api.post("/api/concept-options", {
    headers,
    data: { designProjectId: projectId, name: `${name} option` },
  });
  expect(option.ok(), await option.text()).toBeTruthy();
  const optionId = ((await option.json()) as { id: number }).id;

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`${baseURL}/admin/design-projects/${projectId}?tab=concept`, {
    waitUntil: "networkidle",
  });
  await page.getByRole("button", { name: /Hồ sơ và ý kiến|Files and feedback/i }).click();

  const dossier = page.getByTestId(`concept-dossier-${optionId}`);
  await expect(dossier).toBeVisible();
  await expect(dossier.getByText("R00")).toBeVisible();
  await expect(dossier.locator('input[type="file"]')).toBeVisible();
  await expect(
    dossier.getByRole("button", { name: /Gắn vào phiên bản|Attach to revision/i }),
  ).toBeDisabled();
  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 900 });
    await expect
      .poll(async () => (await page.locator("aside").boundingBox())?.x ?? 0)
      .toBeLessThan(-280);
    await expect(dossier).toBeVisible();
    await expect(dossier.locator('input[type="file"]')).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`concept-${width}.png`), fullPage: true });
  }

  // Set up a submitted dossier as the precondition for the browser feedback
  // journey. API lifecycle and evidence rules are covered by integration tests.
  try {
    execSql(`
    SET QUOTED_IDENTIFIER ON;
    SET ANSI_NULLS ON;
    UPDATE concept_revisions SET FrozenAt = SYSUTCDATETIME()
    WHERE ConceptOptionId = ${optionId};
    UPDATE concept_options SET Status = 'PresentedToClient' WHERE Id = ${optionId};
    INSERT INTO design_reviews
      (DesignProjectId, Stage, RevisionNumber, ConceptRevisionId, Title, Status,
       DesignManagerUserId, ClientSubmittedAt, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId)
    SELECT ${projectId}, 'Concept', 1, r.Id, N'Concept browser feedback fixture', 'SubmittedToClient',
      o.CreatedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(), o.CreatedByUserId,
      SYSUTCDATETIME(), o.CreatedByUserId
    FROM concept_revisions r JOIN concept_options o ON o.Id = r.ConceptOptionId
    WHERE o.Id = ${optionId};
    `);
  } catch (error) {
    const failure = error as Error & { stdout?: Buffer };
    throw new Error(failure.stdout?.toString() || failure.message);
  }
  await page.reload({ waitUntil: "networkidle" });
  await page.getByRole("button", { name: /Hồ sơ và ý kiến|Files and feedback/i }).click();
  await dossier
    .getByPlaceholder(/Tên người góp ý|Client representative name/i)
    .fill("Ông Nguyễn Minh - Chủ đầu tư");
  await dossier.getByPlaceholder(/Kênh nhận ý kiến|Feedback channel/i).fill("Họp Concept");
  await dossier
    .getByPlaceholder(/Nguồn bằng chứng|Evidence source/i)
    .fill("Biên bản họp Concept, mục 3");
  await dossier.locator('input[type="datetime-local"]').fill("2026-10-20T10:00");
  await dossier
    .getByPlaceholder(/Nội dung ý kiến|Feedback text/i)
    .fill("Giữ sân trong để lấy sáng tự nhiên.");
  await dossier.getByRole("button", { name: /Ghi nhận ý kiến|Record feedback/i }).click();
  await expect(
    dossier.getByText("Giữ sân trong để lấy sáng tự nhiên.", { exact: true }),
  ).toBeVisible();
  await expect(dossier.getByText(/Biên bản họp Concept, mục 3/)).toBeVisible();
  await dossier
    .getByPlaceholder(/Phản hồi xử lý|^Response$/i)
    .fill("Đã kiểm tra phương án sân trong với nhóm kiến trúc.");
  const respond = dossier.getByRole("button", { name: /Ghi phản hồi|Record response/i });
  await expect(respond).toBeDisabled();
  await dossier
    .getByPlaceholder(/Kết quả xử lý|Resolution outcome/i)
    .fill("Giữ sân trong theo thống nhất với chủ đầu tư.");
  await respond.click();
  await expect(dossier.getByText(/Giữ sân trong theo thống nhất với chủ đầu tư/)).toBeVisible();
  await page.reload({ waitUntil: "networkidle" });
  await page.getByRole("button", { name: /Hồ sơ và ý kiến|Files and feedback/i }).click();
  await expect(dossier.getByText(/Giữ sân trong theo thống nhất với chủ đầu tư/)).toBeVisible();
  await expect(dossier.getByPlaceholder(/Phản hồi xử lý|^Response$/i)).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 900 });
  await expect
    .poll(async () => (await page.locator("aside").boundingBox())?.x ?? 0)
    .toBeLessThan(-280);
  await page.screenshot({ path: testInfo.outputPath("concept-feedback-390.png"), fullPage: true });
});
