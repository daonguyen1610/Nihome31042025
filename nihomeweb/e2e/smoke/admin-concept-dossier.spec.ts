import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { execSql } from "../fixtures/db";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";

test.use({ timezoneId: "Asia/Ho_Chi_Minh" });

test("Concept dossier preserves context and supports accessible responsive forms", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
  baseURL,
}, testInfo) => {
  test.setTimeout(120000);
  const browserErrors: string[] = [];
  page.on("pageerror", (error) => browserErrors.push(error.message));
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
  await expect(dossier.getByText(/Chưa gắn tài liệu|No documents attached/i)).toBeVisible();
  await expect(dossier.getByText(/Chưa có ý kiến khách hàng|No client feedback/i)).toBeVisible();
  await expect(dossier.getByLabel(/Tên người góp ý|Client representative name/i)).toHaveCount(0);
  await expect(
    dossier.getByRole("button", { name: /Tạo phiên bản sửa đổi|Create revised version/i }),
  ).toHaveCount(0);
  for (const width of [1440, 768, 390]) {
    await page.setViewportSize({ width, height: 900 });
    if (width < 1024) {
      await expect
        .poll(async () => (await page.locator("aside").boundingBox())?.x ?? 0)
        .toBeLessThan(-280);
    }
    const panelWidth = await dossier.evaluate((element) => element.getBoundingClientRect().width);
    if (width === 768) expect(panelWidth).toBeGreaterThan(600);
    if (width === 1440) expect(panelWidth).toBeGreaterThan(900);
    await expect(dossier).toBeVisible();
    await expect(dossier.locator('input[type="file"]')).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`concept-${width}.png`),
      fullPage: true,
      animations: "disabled",
    });
  }

  const project = (await (
    await api.get(`/api/design-projects/${projectId}`, { headers })
  ).json()) as { operationalProjectId: number };
  const operationalId = project.operationalProjectId;
  const backend = process.env.E2E_BACKEND_CONTAINER ?? "nihome31042025-backend";
  const revisionId = (
    (await (
      await api.get(`/api/concept-options/${optionId}/revisions`, { headers })
    ).json()) as Array<{ id: number }>
  )[0].id;
  const image = readFileSync(
    new URL(
      "../../../nihomebackend/wwwroot/images/news/xay-dung-concept-san-pham-cho-du-an-thiet-ke/img-11.jpg",
      import.meta.url,
    ),
  );
  const pdf = readFileSync(
    new URL("../../../nihomebackend/wwwroot/files/design/basic/kt-bd-001.pdf", import.meta.url),
  );
  const fixtureFiles = [
    {
      name: `concept-${optionId}-perspective.jpg`,
      bytes: image,
      contentType: "image/jpeg",
      kind: "Perspective",
    },
    {
      name: `concept-${optionId}-presentation.pdf`,
      bytes: pdf,
      contentType: "application/pdf",
      kind: "Presentation",
    },
    {
      name: `concept-${optionId}-TK-M03.pdf`,
      bytes: pdf,
      contentType: "application/pdf",
      kind: null,
    },
  ];
  const fixtureIds: number[] = [];
  for (const fixture of fixtureFiles) {
    const path = `/files/project-documents/${operationalId}/${fixture.name}`;
    execFileSync(
      "docker",
      [
        "exec",
        "-i",
        backend,
        "sh",
        "-c",
        `mkdir -p /app/wwwroot/files/project-documents/${operationalId} && cat > /app/wwwroot${path}`,
      ],
      { input: fixture.bytes },
    );
    const digest = createHash("sha256").update(fixture.bytes).digest("hex");
    const output = execSql(`SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
      INSERT INTO project_documents
        (OperationalProjectId, Category, SourceModule, SourceType, SourceEntityType, SourceRecordId,
         LocalPath, OriginalFileName, ContentType, Size, Sha256, Origin, Generation, DesiredOperation,
         SyncStatus, SyncAttemptCount, IsDownloadable, ConflictState, CreatedAt, UpdatedAt)
      OUTPUT INSERTED.Id
      VALUES (${operationalId}, 'DesignConcept', 'Design', 'ManualUpload', 'ConceptOption', ${optionId},
        '${path}', '${fixture.name}', '${fixture.contentType}', ${fixture.bytes.length}, '${digest}', 'Nicon', 1, 'None',
        'Synced', 0, 1, 'None', SYSUTCDATETIME(), SYSUTCDATETIME());`);
    const documentId = Number(output.trim());
    fixtureIds.push(documentId);
    if (fixture.kind) {
      const attached = await api.post(
        `/api/concept-options/${optionId}/revisions/${revisionId}/artifacts`,
        {
          headers: { ...headers, "Idempotency-Key": `preview-${documentId}` },
          data: { projectDocumentId: documentId, kind: fixture.kind },
        },
      );
      expect(attached.ok(), await attached.text()).toBeTruthy();
    }
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
  for (const [index, selector] of [
    [0, "img"],
    [1, "canvas"],
  ] as const) {
    await dossier.getByRole("button", { name: new RegExp(fixtureFiles[index].name) }).click();
    const preview = page.getByRole("dialog");
    await expect(preview.locator(selector)).toBeVisible();
    if (selector === "img")
      await expect
        .poll(() => preview.locator("img").evaluate((img: HTMLImageElement) => img.naturalWidth))
        .toBe(1000);
    await page.screenshot({
      path: testInfo.outputPath(`concept-preview-${index}.png`),
      fullPage: true,
      animations: "disabled",
    });
    await page.keyboard.press("Escape");
    await expect(preview).toHaveCount(0);
  }
  const discard = page.getByRole("button", { name: /^Loại bỏ$|^Discard$/i });
  await discard.click();
  const discardDialog = page.getByRole("alertdialog");
  await expect(
    discardDialog.getByRole("heading", { name: /Xác nhận loại bỏ|Discard this option/i }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("concept-discard-dialog-390.png"),
    fullPage: true,
    animations: "disabled",
  });
  await page.keyboard.press("Escape");
  await expect(discardDialog).toHaveCount(0);
  await expect(discard).toBeFocused();
  await dossier
    .getByLabel(/Tên người góp ý|Client representative name/i)
    .fill("Ông Nguyễn Minh - Chủ đầu tư");
  await dossier.getByLabel(/Kênh nhận ý kiến|Feedback channel/i).fill("Họp Concept");
  await dossier.getByLabel(/Nguồn bằng chứng|Evidence source/i).fill("Biên bản họp Concept, mục 3");
  await expect(
    page.getByRole("button", { name: /Chốt phương án|Finalize option/i }),
  ).toBeDisabled();
  await expect(
    dossier.getByRole("button", { name: /Tạo phiên bản sửa đổi|Create revised version/i }),
  ).toHaveCount(0);
  await dossier
    .getByLabel(/Thời điểm khách góp ý|Feedback date and time/i)
    .fill("2026-10-20T10:00");
  await dossier
    .getByLabel(/Nội dung ý kiến|Feedback text/i)
    .fill("Giữ sân trong để lấy sáng tự nhiên.");
  await dossier.getByRole("button", { name: /Ghi nhận ý kiến|Record feedback/i }).click();
  await expect(
    dossier.getByText("Giữ sân trong để lấy sáng tự nhiên.", { exact: true }),
  ).toBeVisible();
  await expect(
    dossier.getByRole("definition").filter({ hasText: "Biên bản họp Concept, mục 3" }),
  ).toBeVisible();
  await expect(dossier.getByRole("status")).toHaveText(/Đã lưu thay đổi|Changes saved/i);
  await expect(dossier.getByLabel(/Tên người góp ý|Client representative name/i)).toHaveValue(
    "Ông Nguyễn Minh - Chủ đầu tư",
  );
  await expect(dossier.getByLabel(/Nguồn bằng chứng|Evidence source/i)).toHaveValue(
    "Biên bản họp Concept, mục 3",
  );
  await page.context().setOffline(true);
  await dossier.getByRole("button", { name: /Tải lại|Refresh/i }).click();
  await expect(dossier.getByRole("alert")).toBeVisible();
  await expect(dossier.getByLabel(/Tên người góp ý|Client representative name/i)).toHaveValue(
    "Ông Nguyễn Minh - Chủ đầu tư",
  );
  await page.context().setOffline(false);
  await dossier.getByRole("button", { name: /Thử lại|Retry/i }).click();
  await expect(dossier.getByRole("alert")).toHaveCount(0);
  await expect(dossier.getByLabel(/Tên người góp ý|Client representative name/i)).toHaveValue(
    "Ông Nguyễn Minh - Chủ đầu tư",
  );
  await dossier
    .getByLabel(/Phản hồi xử lý|^Response$/i)
    .fill("Đã kiểm tra phương án sân trong với nhóm kiến trúc.");
  const respond = dossier.getByRole("button", { name: /Ghi phản hồi|Record response/i });
  await expect(respond).toBeDisabled();
  await dossier
    .getByLabel(/Kết quả xử lý|Resolution outcome/i)
    .fill("Giữ sân trong theo thống nhất với chủ đầu tư.");
  await respond.click();
  await expect(dossier.getByLabel(/Phản hồi xử lý|^Response$/i)).toHaveCount(0);
  await expect(dossier.getByRole("status")).toHaveText(/Đã lưu thay đổi|Changes saved/i);
  await expect(
    dossier.locator("dd").filter({ hasText: "Giữ sân trong theo thống nhất với chủ đầu tư." }),
  ).toBeVisible();
  await page.reload({ waitUntil: "networkidle" });
  await page.getByRole("button", { name: /Hồ sơ và ý kiến|Files and feedback/i }).click();
  await expect(dossier.getByText(/Giữ sân trong theo thống nhất với chủ đầu tư/)).toBeVisible();
  await expect(dossier.getByLabel(/Phản hồi xử lý|^Response$/i)).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 900 });
  await expect
    .poll(async () => (await page.locator("aside").boundingBox())?.x ?? 0)
    .toBeLessThan(-280);
  await page.screenshot({
    path: testInfo.outputPath("concept-feedback-390.png"),
    fullPage: true,
    animations: "disabled",
  });
  await page.getByRole("button", { name: /^Trình duyệt$|^Reviews$/i }).click();
  await expect(page).toHaveURL(new RegExp(`design-projects/${projectId}\\?tab=reviews`));
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("tab", { name: /^Trình duyệt$|^Reviews$/i })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await page.goBack();
  await expect(page.getByRole("tab", { name: /^Thiết kế ý tưởng$|^Concept$/i })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  await page.goForward();
  await expect(page.getByRole("tab", { name: /^Trình duyệt$|^Reviews$/i })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  const reviews = page.getByTestId("design-reviews-tab");
  await expect(reviews.getByText(/Đợt trình nội bộ|Internal submission/i)).toBeVisible();
  await expect(reviews.getByLabel(/Ngày ký biên bản|Signing date/i)).toBeVisible();
  await expect(reviews.getByLabel(/Người ký phía khách hàng|Client signatory/i)).toBeVisible();
  await expect(
    reviews.getByRole("button", {
      name: /Khách duyệt|Client approved|Ghi nhận khách duyệt|Khách hàng duyệt/i,
    }),
  ).toBeDisabled();
  await page.screenshot({
    path: testInfo.outputPath("concept-review-390.png"),
    fullPage: true,
    animations: "disabled",
  });
  const createReview = reviews.getByRole("button", { name: /Tạo hồ sơ trình|Create review/i });
  await createReview.click();
  const reviewDialog = page.getByRole("dialog");
  await expect(reviewDialog.getByLabel(/Giai đoạn|Stage/i)).toBeVisible();
  await expect(reviewDialog.getByLabel(/Tiêu đề|Title/i)).toBeVisible();
  await expect(
    reviewDialog.getByRole("button", {
      name: /Tạo hồ sơ nháp|Create draft dossier/i,
      exact: true,
    }),
  ).toBeDisabled();
  await page.screenshot({
    path: testInfo.outputPath("concept-review-dialog-390.png"),
    fullPage: true,
    animations: "disabled",
  });
  const dialogBounds = await reviewDialog.boundingBox();
  expect(dialogBounds?.x).toBeGreaterThanOrEqual(0);
  expect(dialogBounds?.y).toBeGreaterThanOrEqual(0);
  expect((dialogBounds?.x ?? 0) + (dialogBounds?.width ?? 0)).toBeLessThanOrEqual(390);
  await page.keyboard.press("Escape");
  await expect(reviewDialog).toHaveCount(0);
  await expect(createReview).toBeFocused();
  for (const [language, signingLabel] of [
    ["English", "Signing date"],
    ["中文", "签署日期"],
    ["日本語", "署名日"],
  ]) {
    await page.getByRole("button", { name: "Change language" }).click();
    await page.getByRole("menuitem", { name: language, exact: true }).click();
    await expect(reviews.getByLabel(signingLabel, { exact: true })).toBeVisible();
    await expect(page.locator('[role="menu"]')).toHaveCount(0);
    await expect(reviews.getByText(/concepts\.dossier\./)).toHaveCount(0);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({
      path: testInfo.outputPath(`concept-review-${language}-390.png`),
      fullPage: true,
      animations: "disabled",
    });
  }
  execSql(`SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
    UPDATE design_reviews SET Status='ClientApproved', ClientAcceptanceDocumentId=${fixtureIds[2]},
      ClientAcceptanceSignedBy=N'Đại diện chủ đầu tư', ClientAcceptanceSignedAt='2026-10-19T17:00:00'
    WHERE DesignProjectId=${projectId} AND ConceptRevisionId=${revisionId};`);
  await page.reload({ waitUntil: "networkidle" });
  const expectedDate = await page.evaluate(() =>
    new Date("2026-10-19T17:00:00Z").toLocaleDateString("ja"),
  );
  await expect(
    page.getByText(new RegExp(`Đại diện chủ đầu tư.*${expectedDate.replaceAll("/", "\\/")}`)),
  ).toBeVisible();
  await page.goto(`${baseURL}/admin/design-projects/${projectId}?tab=concept`, {
    waitUntil: "networkidle",
  });
  await page
    .getByRole("button", { name: /資料と意見|Files and feedback|Hồ sơ và ý kiến/i })
    .click();
  await page.getByRole("button", { name: "破棄", exact: true }).click();
  await page
    .getByRole("alertdialog")
    .getByRole("button", { name: "破棄を確定", exact: true })
    .click();
  await expect(page.getByRole("alertdialog")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "破棄", exact: true })).toHaveCount(0);
  await expect(dossier.getByText(/Giữ sân trong theo thống nhất với chủ đầu tư/)).toBeVisible();
  expect(browserErrors).toEqual([]);
});
