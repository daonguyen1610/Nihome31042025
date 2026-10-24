import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("template tabs preserve drafts and submission delivers the confirmed approval email", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.skip(!process.env.MAIL_SINK_URL, "Requires an isolated, non-relaying SMTP sink");
  test.setTimeout(180_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const writes = () => ({ ...headers, "Idempotency-Key": crypto.randomUUID() });
  const marker = `APPROVAL-${Date.now()}`;
  const templatePath = "/api/site-settings/work-package-approval-template";
  const original = await (await api.get(templatePath, { headers })).json();
  let customerId = 0,
    projectId = 0,
    opportunityId = 0,
    packageId = 0;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  try {
    await page.addInitScript(() => {
      if (window === window.top) localStorage.setItem("nicon_lang", "vi");
    });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/email-templates");
    const contractTab = page.getByRole("button", {
      name: "Mẫu email gia hạn và kết thúc hợp đồng",
      exact: true,
    });
    const approvalTab = page.getByRole("button", { name: "Duyệt nhóm công việc", exact: true });
    await expect(contractTab).toBeVisible();
    await expect(page.locator("#renewal-subject")).not.toBeVisible();
    await contractTab.click();
    await expect(page.locator("#renewal-subject")).toBeVisible();
    const renewal = await page.locator("#renewal-subject").inputValue();
    await page.locator("#renewal-subject").fill("Draft retained {{contractNumber}}");
    await page.getByRole("button", { name: "Báo giá", exact: true }).click();
    await expect(page.locator("#email-subject")).toBeVisible();
    await contractTab.click();
    await expect(page.locator("#renewal-subject")).toHaveValue("Draft retained {{contractNumber}}");
    await page.locator("#renewal-subject").fill(renewal);
    await approvalTab.click();
    await page.locator("#approval-email-subject").fill(`${marker} {{workPackageCode}}`);
    await page
      .locator("#approval-email-body")
      .fill(
        `<div><p>${marker}</p><p>{{recipientName}} – {{senderName}}</p><p>{{workPackageCode}} {{workPackageName}}</p><p>{{recordType}} {{recordReference}}</p></div>`,
      );
    await page.getByRole("button", { name: "Lưu thay đổi", exact: true }).click();
    await expect(page.getByRole("status").filter({ hasText: "Đã lưu" })).toBeVisible();
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      if (width < 1024) {
        await expect(page.locator("aside").first()).not.toBeInViewport();
      }
      await page.evaluate(() => window.scrollTo(0, 0));
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        width,
      );
      await page.screenshot({
        path: testInfo.outputPath(`templates-${width}.png`),
        fullPage: true,
      });
    }

    customerId = await createOwnCustomer(api, headers, "NICON approval email");
    let response = await api.post("/api/operational-projects", {
      headers: writes(),
      data: { name: "NICON nhà máy An Phú – duyệt giá MEP", customerId },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    projectId = (await response.json()).id;
    response = await api.post("/api/opportunities", {
      headers: writes(),
      data: {
        name: "Cải tạo cơ điện nhà máy An Phú",
        customerId,
        operationalProjectId: projectId,
        estimatedValue: 500000000,
        winProbability: 50,
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    opportunityId = (await response.json()).id;
    const options = await (await api.get("/api/work-packages/options", { headers })).json();
    const department = options.departments.find(
      (item: { code: string }) => item.code === "PROCUREMENT",
    );
    const lead = options.users.find(
      (item: { roleGroupId: number; canLead: boolean }) =>
        item.roleGroupId === department.id && item.canLead,
    );
    expect(lead).toBeTruthy();
    response = await api.post("/api/work-packages", {
      headers: writes(),
      data: {
        kind: "Project",
        purpose: "ProcurementInputPricing",
        name: "Cung ứng lập giá đầu vào MEP An Phú",
        operationalProjectId: projectId,
        sourceOpportunityId: opportunityId,
        departmentRoleGroupId: department.id,
        leadUserId: lead.userId,
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    packageId = (await response.json()).id;
    response = await api.post(`/api/work-packages/${packageId}/input-price-offers`, {
      headers: writes(),
      data: {
        vendorId: options.vendors[0].id,
        supplierReference: marker,
        currency: "VND",
        validUntil: new Date(Date.now() + 30 * 86400000).toISOString(),
        lines: [
          {
            itemCode: "MEP-01",
            description: "Cáp điện chống cháy",
            unit: "m",
            quantity: 1000,
            unitPrice: 85000,
            vatPercent: 8,
          },
        ],
      },
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    await page.goto(`/admin/work-packages/${packageId}`);
    await page.getByRole("button", { name: "Gửi duyệt", exact: true }).click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByLabel("Người nhận", { exact: true })).toHaveValue(lead.email);
    await expect(dialog).toContainText(marker);
    await expect(dialog.frameLocator("iframe").locator("body")).toContainText(marker);
    await expect(dialog.frameLocator("iframe").locator("body")).not.toContainText("{{");
    for (const width of [390, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      expect(
        await dialog.evaluate((element) => element.scrollWidth <= element.clientWidth),
      ).toBeTruthy();
      await dialog.screenshot({ path: testInfo.outputPath(`approval-preview-${width}.png`) });
    }
    // Cancel has no workflow or SMTP side effect.
    await dialog.getByRole("button", { name: "Huỷ", exact: true }).click();
    const before = await (await api.get(`/api/work-packages/${packageId}`, { headers })).json();
    expect(before.inputPriceOffers[0].status).toBe("Draft");
    expect(before.approvalEmails).toEqual([]);
    await page.getByRole("button", { name: "Gửi duyệt", exact: true }).click();
    const toEmail = `approval-to-${Date.now()}@example.test`;
    const ccEmails = [`cm-${Date.now()}@example.test`, `qs-${Date.now()}@example.test`];
    const confirm = dialog.getByRole("button", { name: "Gửi duyệt và thông báo", exact: true });
    await dialog.getByLabel("Người nhận", { exact: true }).fill("invalid");
    await expect(confirm).toBeDisabled();
    await expect(dialog.getByRole("alert")).toBeVisible();
    await dialog.getByLabel("Người nhận", { exact: true }).fill(toEmail);
    await dialog
      .getByLabel("CC (không bắt buộc)", { exact: true })
      .fill(`${ccEmails[0]}; ${ccEmails[1]}\n${ccEmails[0]}`);
    await expect(confirm).toBeDisabled();
    await dialog.getByRole("button", { name: "Cập nhật xem trước", exact: true }).click();
    await expect(dialog.getByLabel("CC (không bắt buộc)", { exact: true })).toHaveValue(
      ccEmails.join(", "),
    );
    await expect(confirm).toBeEnabled();
    await dialog.getByRole("button", { name: "Gửi duyệt và thông báo", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await expect(page.getByTestId("approval-email-feedback")).toContainText(
      /Đã gửi duyệt|Đã gửi thành công/,
    );
    await expect
      .poll(
        async () => {
          const result = await (
            await api.get(`/api/work-packages/${packageId}`, { headers })
          ).json();
          return result.approvalEmails[0]?.status;
        },
        { timeout: 60_000, intervals: [1000, 3000] },
      )
      .toBe("Sent");
    const mailbox = await api.get(`${process.env.MAIL_SINK_URL}/messages`);
    expect(mailbox.ok()).toBeTruthy();
    const messages = (await mailbox.json()) as {
      recipient: string;
      recipients: string[];
      raw: string;
    }[];
    const delivered = messages.filter(
      (message) => message.recipient === toEmail && message.raw.includes(marker),
    );
    expect(delivered).toHaveLength(1);
    expect(delivered[0].recipients.sort()).toEqual([toEmail, ...ccEmails].sort());
    expect(delivered[0].raw).toContain("Cc:");
    for (const address of ccEmails) expect(delivered[0].raw).toContain(address);
    await expect(page.getByTestId("approval-email-feedback")).toContainText("Đã gửi thành công");
    await page.reload();
    await expect(page.getByText("Đã gửi", { exact: true })).toBeVisible();
    await expect(page.getByText(`CC: ${ccEmails.join(", ")}`, { exact: true })).toBeVisible();
    // A real SMTP rejection keeps the dossier submitted and explains the email
    // failure on screen; it must not be reported as successfully delivered.
    const rejectedOffer = await api.post(`/api/work-packages/${packageId}/input-price-offers`, {
      headers: writes(),
      data: {
        vendorId: options.vendors[0].id,
        supplierReference: `${marker}-FAIL`,
        currency: "VND",
        validUntil: new Date(Date.now() + 30 * 86400000).toISOString(),
        lines: [
          {
            itemCode: "MEP-02",
            description: "Tủ điện tổng",
            unit: "bộ",
            quantity: 1,
            unitPrice: 85000000,
            vatPercent: 8,
          },
        ],
      },
    });
    expect(rejectedOffer.ok(), await rejectedOffer.text()).toBeTruthy();
    const rejectedId = (await rejectedOffer.json()).id;
    await page.reload();
    await page.getByRole("button", { name: "Gửi duyệt", exact: true }).click();
    await dialog
      .getByLabel("Người nhận", { exact: true })
      .fill(`reject-approval-${Date.now()}@example.test`);
    await dialog.getByRole("button", { name: "Cập nhật xem trước", exact: true }).click();
    await expect(confirm).toBeEnabled();
    await confirm.click();
    await expect(dialog).not.toBeVisible();
    await expect(page.getByTestId("approval-email-feedback")).toContainText(
      "email thông báo cần kiểm tra",
      { timeout: 60_000 },
    );
    const failedDetail = await (
      await api.get(`/api/work-packages/${packageId}`, { headers })
    ).json();
    expect(
      failedDetail.inputPriceOffers.find((offer: { id: number }) => offer.id === rejectedId).status,
    ).toBe("Submitted");
    expect(failedDetail.approvalEmails[0].status).toBe("NeedsReview");
    expect(errors).toEqual([]);
  } finally {
    const restore = await api.put(templatePath, { headers, data: original });
    expect(restore.ok(), await restore.text()).toBeTruthy();
    if (packageId) await hardDeleteBusinessRoot(api, writes(), `/api/work-packages/${packageId}`);
    if (opportunityId)
      await hardDeleteBusinessRoot(api, headers, `/api/opportunities/${opportunityId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
