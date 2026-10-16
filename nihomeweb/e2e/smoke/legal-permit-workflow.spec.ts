import { test, expect, TEST_USERS } from "../fixtures/auth";

test("signed upstream contract uses the editable legal activation wizard", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  const contractId = 8123;
  const projectId = 456;
  let activationPosts = 0;
  let status = "Signed";
  const contract = () => ({
    id: contractId,
    contractNumber: "HD-NICON-LEGAL-8123",
    customerId: 10,
    customerName: "Công ty Sản xuất NICON",
    direction: "Upstream",
    type: "DesignAndBuild",
    vendorId: null,
    vendorCode: null,
    vendorName: null,
    operationalProjectId: projectId,
    operationalProjectCode: "OP-NICON-LEGAL",
    operationalProjectName: "Nhà máy logistics NICON",
    opportunityId: null,
    opportunityTitle: null,
    quoteId: null,
    quoteCode: null,
    designProjectId: null,
    designProjectCode: null,
    designProjectName: null,
    designProjectCurrentStage: null,
    ownerUserId: 11,
    ownerName: "PM NICON",
    status,
    signedDate: "2026-10-01T00:00:00Z",
    startDate: "2026-10-15T00:00:00Z",
    endDate: "2027-04-15T00:00:00Z",
    value: 5_000_000_000,
    approvedVoTotal: 0,
    currentValue: 5_000_000_000,
    hasSignedScan: true,
    attachmentCount: 1,
    appendixCount: 0,
    scopeOfWork: "Thiết kế và thi công nhà máy",
    note: null,
    createdAt: "2026-10-01T00:00:00Z",
    updatedAt: "2026-10-01T00:00:00Z",
    rowVersion: "AAAAAAAAAAE=",
    paymentMilestones: [],
  });

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route(new RegExp(`/api/(?:v1/)?contracts/${contractId}(?:/.*)?$`), async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET" && path.endsWith("/legal-activation-preview")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          contractId,
          operationalProjectId: projectId,
          projectCode: "OP-NICON-LEGAL",
          projectName: "Nhà máy logistics NICON",
          projectManagerUserId: 11,
          eligible: true,
          alreadyActivated: false,
          existingWorkPackageId: null,
          reasonCode: null,
          message: null,
          permitTypes: [
            {
              code: "gpxd",
              name: "Giấy phép xây dựng",
              alreadyExists: false,
              requiresSetup: false,
            },
            { code: "pccc", name: "Thẩm duyệt PCCC", alreadyExists: false, requiresSetup: false },
          ],
          projectMembers: [
            {
              userId: 22,
              name: "Chủ trì Pháp lý",
              email: "legal@nicon.test",
              roleGroupId: 3,
              roleGroupCode: "CONSTRUCTION",
              canLeadLegal: true,
            },
            {
              userId: 33,
              name: "Kiến trúc sư dự án",
              email: "architect@nicon.test",
              roleGroupId: 4,
              roleGroupCode: "DESIGN",
              canLeadLegal: false,
            },
            {
              userId: 11,
              name: "PM NICON",
              email: "pm@nicon.test",
              roleGroupId: 3,
              roleGroupCode: "CONSTRUCTION",
              canLeadLegal: false,
            },
          ],
        }),
      });
      return;
    }
    if (request.method() === "POST" && path.endsWith("/activate-with-legal")) {
      activationPosts += 1;
      const payload = request.postDataJSON() as {
        rowVersion: string;
        permits: Array<{
          permitTypeCode: string;
          issuingAgency: string;
          assigneeUserId: number;
          approverUserId: number;
        }>;
      };
      expect(payload.rowVersion).toBe("AAAAAAAAAAE=");
      expect(payload.permits).toEqual([
        expect.objectContaining({
          permitTypeCode: "gpxd",
          issuingAgency: "Sở Xây dựng TP.HCM",
          assigneeUserId: 33,
          approverUserId: 22,
        }),
      ]);
      status = "InProgress";
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          contract: contract(),
          workPackageId: 9001,
          permitIds: [7001],
          reusedExistingPackage: false,
        }),
      });
      return;
    }
    if (
      request.method() === "GET" &&
      ["/appendices", "/attachments", "/timeline"].some((suffix) => path.endsWith(suffix))
    ) {
      await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(contract()),
    });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${baseURL}/admin/contracts/${contractId}`, { waitUntil: "domcontentloaded" });
  await page
    .getByRole("button", {
      name: /Chuyển.*Đang thực hiện|Move to In progress|转为进行中|進行中にする/i,
    })
    .click();
  await expect(page.getByRole("dialog")).toBeVisible();
  expect(activationPosts).toBe(0);

  const next = page.getByRole("button", { name: /Sau|Next|下一步|次へ/i });
  await next.click();
  const permitChecks = page.getByRole("dialog").getByRole("checkbox");
  await expect(permitChecks).toHaveCount(2);
  await permitChecks.nth(0).click();
  await permitChecks.nth(1).click();
  await next.click();
  await expect(
    page.getByText(/ít nhất một giấy phép|at least one permit|至少选择一个|少なくとも1つ/i),
  ).toBeVisible();
  expect(activationPosts).toBe(0);

  await permitChecks.nth(0).click();
  await next.click();
  const agency = page.getByRole("dialog").locator("input:not([type]):visible").first();
  await agency.fill("Sở Xây dựng TP.HCM");
  await expect(page.getByText("Kiến trúc sư dự án").last()).toBeVisible();
  await page.setViewportSize({ width: 768, height: 1024 });
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
      ),
    )
    .toBe(true);
  await page.setViewportSize({ width: 1280, height: 900 });
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
      ),
    )
    .toBe(true);
  await next.click();
  await expect(page.getByRole("dialog")).toContainText("Sở Xây dựng TP.HCM");
  expect(activationPosts).toBe(0);

  await page.getByTestId("legal-activation-confirm").click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page).toHaveURL(new RegExp(`/admin/contracts/${contractId}$`));
  await expect(page.getByText(/Đang thực hiện|In progress|进行中|進行中/i).first()).toBeVisible();
  expect(activationPosts).toBe(1);
});

test("legal workspace updates progress and preserves input on concurrency conflict", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  let taskStatus = "InProgress";
  let progressPercent = 25;
  let rowVersion = "task-v1";
  let rejectReason = "";
  let conflictOnce = true;
  const permit = () => ({
    id: 7001,
    operationalProjectId: 456,
    operationalProjectCode: "OP-NICON-LEGAL",
    operationalProjectName: "NhÃ  mÃ¡y logistics NICON",
    designProjectId: 88,
    designProjectCode: "TK-88",
    designProjectName: "Thiáº¿t káº¿ nhÃ  mÃ¡y",
    permitTypeCode: "gpxd",
    permitTypeLabel: "Giáº¥y phÃ©p xÃ¢y dá»±ng",
    issuingAgency: "Sá»Ÿ XÃ¢y dá»±ng TP.HCM",
    ownerUserId: 33,
    ownerName: "Kiáº¿n trÃºc sÆ° dá»± Ã¡n",
    workTaskId: 8001,
    workPackageId: 9001,
    approverUserId: 22,
    approverName: "Chá»§ trÃ¬ PhÃ¡p lÃ½",
    progressPercent,
    workTaskStatus: taskStatus,
    workTaskRowVersion: rowVersion,
    targetDeadline: "2026-10-20T00:00:00Z",
    submittedAt: "2026-10-05T00:00:00Z",
    issuedAt: "2026-10-10T00:00:00Z",
    expiresAt: "2026-10-25T00:00:00Z",
    submittedFilePath: "/files/business-documents/permits/submitted.pdf",
    issuedFilePath: "/files/business-documents/permits/issued.pdf",
    status: "Issued",
    note: "Há»“ sÆ¡ Ä‘Ã£ Ä‘Æ°á»£c cáº¥p",
    isOverdue: false,
    isDueSoon: false,
    isExpiringSoon: true,
    createdAt: "2026-10-01T00:00:00Z",
    updatedAt: "2026-10-10T00:00:00Z",
  });
  const task = () => ({
    id: 8001,
    workPackageId: 9001,
    wbsCode: "PL.GPXD",
    title: "Xin giáº¥y phÃ©p xÃ¢y dá»±ng",
    departmentRoleGroupId: 3,
    departmentCode: "CONSTRUCTION",
    departmentLabelKey: "departments.construction",
    assigneeUserId: 33,
    assigneeName: "Kiáº¿n trÃºc sÆ° dá»± Ã¡n",
    approverUserId: 22,
    approverName: "Chá»§ trÃ¬ PhÃ¡p lÃ½",
    status: taskStatus,
    progressPercent,
    plannedStart: "2026-10-01T00:00:00Z",
    plannedEnd: "2026-10-20T00:00:00Z",
    ccUserIds: [],
    predecessorTaskIds: [],
    successorTaskIds: [],
    canUpdateProgress: taskStatus === "Planned" || taskStatus === "InProgress",
    canVerify: taskStatus === "Completed",
    rowVersion,
  });

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith("/operational-projects")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          total: 1,
          page: 1,
          pageSize: 200,
          items: [{ id: 456, code: "OP-NICON-LEGAL", name: "NhÃ  mÃ¡y logistics NICON" }],
        }),
      });
      return;
    }
    if (path.endsWith("/master-data/permit_type")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([{ code: "gpxd", name: "Giáº¥y phÃ©p xÃ¢y dá»±ng", isActive: true }]),
      });
      return;
    }
    if (path.endsWith("/users")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ total: 0, items: [] }),
      });
      return;
    }
    if (path.endsWith("/permits") && request.method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          total: 1,
          page: 1,
          pageSize: 20,
          items: [permit()],
          risk: { overdue: 0, dueSoon: 0, expiringSoon: 1, totalOpen: 0 },
        }),
      });
      return;
    }
    if (path.endsWith("/permits/7001/history")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            id: 1,
            fromStatus: "UnderReview",
            toStatus: "Issued",
            action: "StatusChanged",
            snapshotJson: "{}",
            changedAt: "2026-10-10T00:00:00Z",
            changedByUserId: 22,
            changedByName: "Chá»§ trÃ¬ PhÃ¡p lÃ½",
          },
        ]),
      });
      return;
    }
    if (path.endsWith("/permits/7001")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(permit()),
      });
      return;
    }
    if (path.endsWith("/work-packages/9001") && request.method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          id: 9001,
          purpose: "LegalPermitting",
          status: "Active",
          tasks: [task()],
        }),
      });
      return;
    }
    if (path.endsWith("/work-packages/9001/tasks/8001/progress")) {
      const body = request.postDataJSON() as { progressPercent: number; status: string };
      if (conflictOnce) {
        conflictOnce = false;
        await route.fulfill({
          status: 409,
          contentType: "application/json",
          body: JSON.stringify({ message: "Dá»¯ liá»‡u Ä‘Ã£ Ä‘Æ°á»£c thay Ä‘á»•i" }),
        });
        return;
      }
      progressPercent = body.progressPercent;
      taskStatus = body.status;
      rowVersion = `${rowVersion}-next`;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(task()),
      });
      return;
    }
    if (path.endsWith("/work-packages/9001/tasks/8001/verify")) {
      const body = request.postDataJSON() as { confirm: boolean; reason?: string };
      if (body.confirm) {
        taskStatus = "Verified";
      } else {
        rejectReason = body.reason ?? "";
        taskStatus = "InProgress";
        progressPercent = 99;
      }
      rowVersion = `${rowVersion}-decision`;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(task()),
      });
      return;
    }
    await route.fallback();
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${baseURL}/admin/permits`, { waitUntil: "domcontentloaded" });
  await expect(page.getByText("Giáº¥y phÃ©p xÃ¢y dá»±ng").first()).toBeVisible();
  await expect(page.getByRole("button", { name: /30.*1/ })).toBeVisible();
  await page.locator("article button").first().click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("PL.GPXD");
  for (const viewport of [
    { width: 390, height: 844 },
    { width: 768, height: 1024 },
    { width: 1920, height: 900 },
    { width: 844, height: 390 },
  ]) {
    await page.setViewportSize(viewport);
    await expect
      .poll(() =>
        dialog.evaluate((element) => {
          const bounds = element.getBoundingClientRect();
          return (
            bounds.top >= 0 &&
            bounds.left >= 0 &&
            bounds.bottom <= window.innerHeight &&
            bounds.right <= window.innerWidth &&
            element.scrollWidth <= element.clientWidth
          );
        }),
      )
      .toBe(true);
    const details = dialog.locator("dl");
    await details.evaluate((element) => {
      element.scrollTop = element.scrollHeight;
    });
    await expect(dialog.getByText("StatusChanged", { exact: false })).toBeInViewport();
    await expect(dialog.locator("button").last()).toBeInViewport();
    await details.evaluate((element) => {
      element.scrollTop = 0;
    });
    await expect(dialog.getByRole("heading").first()).toBeInViewport();
  }
  await page.setViewportSize({ width: 390, height: 844 });
  const taskSection = dialog.locator("section").filter({ hasText: "PL.GPXD" });
  const progress = taskSection.locator("#permit-task-progress");
  await progress.fill("60");
  await taskSection.getByRole("button").nth(0).click();
  await expect(dialog).toBeVisible();
  await expect(progress).toHaveValue("60");
  await taskSection.getByRole("button").nth(0).click();
  await expect(dialog).toContainText("60%");
  await taskSection.getByRole("button").nth(1).click();
  const reason = taskSection.locator("#permit-task-reject-reason");
  await reason.fill("Bá»• sung biÃªn báº£n Ä‘á»‘i chiáº¿u báº£n gá»‘c");
  await taskSection.getByRole("button").nth(0).click();
  expect(rejectReason).toContain("biÃªn báº£n");
  await expect(dialog).toContainText("InProgress");
  await dialog.getByRole("button", { name: /Edit|S\u1eeda|\u7f16\u8f91|\u7de8\u96c6/i }).click();
  const editDialog = page.getByTestId("permit-form-dialog");
  await expect(editDialog).toBeVisible();
  for (const viewport of [
    { width: 390, height: 844 },
    { width: 768, height: 1024 },
    { width: 1920, height: 900 },
    { width: 844, height: 390 },
  ]) {
    await page.setViewportSize(viewport);
    await expect
      .poll(() =>
        editDialog.evaluate((element) => {
          const bounds = element.getBoundingClientRect();
          return (
            bounds.top >= 0 &&
            bounds.left >= 0 &&
            bounds.bottom <= window.innerHeight &&
            bounds.right <= window.innerWidth &&
            element.scrollWidth <= element.clientWidth
          );
        }),
      )
      .toBe(true);
    await editDialog.getByTestId("permit-issued-file-upload").scrollIntoViewIfNeeded();
    await expect(editDialog.getByTestId("permit-issued-file-upload")).toBeInViewport();
    await expect(editDialog.getByRole("heading")).toBeInViewport();
    await expect(editDialog.locator("button").nth(-2)).toBeInViewport();
  }
  await editDialog.locator("button").last().click();
  await expect(editDialog).toHaveCount(0);
  await expect
    .poll(() =>
      page.evaluate(
        () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
      ),
    )
    .toBe(true);
});
