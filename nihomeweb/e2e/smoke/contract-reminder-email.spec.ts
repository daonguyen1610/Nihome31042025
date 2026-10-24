import { randomUUID } from "node:crypto";
import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

function htmlFromMessage(raw: string): string {
  const boundary = raw.indexOf("\r\n\r\n");
  const headers = raw.slice(0, boundary);
  const body = raw.slice(boundary + 4);
  if (/Content-Transfer-Encoding: base64/i.test(headers))
    return Buffer.from(body.replace(/\s/g, ""), "base64").toString("utf8");
  if (/Content-Transfer-Encoding: quoted-printable/i.test(headers)) {
    const bytes = body
      .replace(/=\r?\n/g, "")
      .replace(/=([\da-f]{2})/gi, (_, hex) => String.fromCharCode(parseInt(hex, 16)));
    return Buffer.from(bytes, "latin1").toString("utf8");
  }
  return body;
}

for (const width of [375, 768]) {
  test(`Live contract reminder preview and automatic delivery at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    test.setTimeout(480_000);
    const sink = process.env.MAIL_SINK_URL;
    test.skip(!sink, "Run with docker-compose-e2e.yaml and MAIL_SINK_URL=http://127.0.0.1:8025");
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const suffix = randomUUID();
    const recipient = `reminder-${suffix}@example.test`;
    let customerId = 0,
      projectId = 0,
      contractId = 0;
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    try {
      const customer = await api.post("/api/customers", {
        headers,
        data: {
          name: `An Phú reminder ${suffix}`,
          type: "Individual",
          sourceCode: "marketing",
          primaryContact: { fullName: "Mai Anh", email: recipient, isPrimary: true },
        },
      });
      expect(customer.status(), await customer.text()).toBe(201);
      customerId = (await customer.json()).id;
      const project = await api.post("/api/operational-projects", {
        headers,
        data: { name: `An Phú factory ${suffix}`, customerId },
      });
      expect(project.status(), await project.text()).toBe(201);
      projectId = (await project.json()).id;
      const contract = await api.post("/api/contracts", {
        headers,
        data: {
          customerId,
          operationalProjectId: projectId,
          direction: "Upstream",
          type: "DesignAndBuild",
          value: 1_500_000_000,
          scopeOfWork: "Nhà máy An Phú — thông báo thời hạn hợp đồng",
        },
      });
      expect(contract.status(), await contract.text()).toBe(201);
      contractId = (await contract.json()).id;
      // Fixture precondition only: contract signing/legal gates have separate
      // integration coverage. Browser actions below use the real API and SQL.
      execSql(
        `UPDATE contracts SET Status = 'Signed', SignedDate = DATEADD(day,-30,GETUTCDATE()), EndDate = DATEADD(day,30,GETUTCDATE()) WHERE Id = ${contractId};`,
      );
      await loginInBrowserAs(page, TEST_USERS.superAdmin);
      await page.addInitScript(() => {
        if (window === window.top) localStorage.setItem("nicon_lang", "vi");
      });
      await page.setViewportSize({ width, height: 900 });
      await page.goto(`/admin/contracts/${contractId}?tab=care`);
      const panel = page.getByRole("region", { name: "Email nhắc thời hạn hợp đồng" });
      await expect(panel).toBeVisible();
      const date = new Date();
      if (width === 375) date.setDate(date.getDate() + 1);
      const day = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(date);
      await panel.locator("#reminder-date").fill(day);
      if (width === 768) await panel.locator("#reminder-kind").selectOption("End");
      await panel.getByRole("button", { name: "Xem trước email", exact: true }).click();
      await expect(panel.locator("#reminder-contact")).toContainText(recipient);
      const frame = page.frameLocator('iframe[title="Xem trước email"]');
      await expect(frame.locator("body")).toContainText(`An Phú reminder ${suffix}`);
      const previewText = await frame.locator("body").innerText();
      await page.screenshot({
        path: testInfo.outputPath(`reminder-preview-${width}.png`),
        fullPage: true,
      });
      await panel.getByRole("button", { name: "Xác nhận lịch gửi tự động" }).click();
      await expect(panel.getByText("Đã lên lịch", { exact: false })).toBeVisible();
      await page.reload();
      await expect(panel.getByText(recipient, { exact: true })).toBeVisible();
      if (width === 375) {
        await panel.getByRole("button", { name: "Huỷ", exact: true }).click();
        await expect(panel.getByText("Đã hủy lịch", { exact: false })).toBeVisible();
      } else {
        // Observe the real background worker and SMTP sink; no API mocking,
        // no direct worker invocation, no shortened production interval.
        await expect
          .poll(
            async () => {
              const response = await api.get(`/api/contracts/${contractId}/reminders`, { headers });
              expect(response.status()).toBe(200);
              return (await response.json())[0]?.status;
            },
            { timeout: 360_000, intervals: [5000] },
          )
          .toBe("Sent");
        const messages = await api.get(`${sink}/messages`);
        expect(messages.ok()).toBe(true);
        const own = ((await messages.json()) as { recipient: string; raw: string }[]).filter(
          (message) => message.recipient === recipient,
        );
        expect(own).toHaveLength(1);
        const persisted = await (
          await api.get(`/api/contracts/${contractId}/reminders`, { headers })
        ).json();
        expect(persisted[0].toEmail).toBe(recipient);
        expect(htmlFromMessage(own[0].raw).replace(/\r\n/g, "\n").trim()).toBe(
          persisted[0].body.replace(/\r\n/g, "\n").trim(),
        );
        expect(previewText).toContain(`An Phú reminder ${suffix}`);
        await page.reload();
        await expect(panel.getByText("SMTP đã tiếp nhận", { exact: false })).toBeVisible();
        const care = await (
          await api.get(`/api/contracts/${contractId}/care-activities`, { headers })
        ).json();
        expect(care.items).toHaveLength(1);
        expect(care.items[0].type).toBe("Email");
      }
      expect(errors).toEqual([]);
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
      await page.screenshot({
        path: testInfo.outputPath(`reminder-result-${width}.png`),
        fullPage: true,
      });
    } finally {
      if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
      if (projectId)
        await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
      if (customerId) await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
    }
  });
}

test("Live admin shows both editable contract email templates", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => {
    if (window === window.top) localStorage.setItem("nicon_lang", "vi");
  });
  await page.goto("/admin/email-templates");
  await page
    .getByRole("button", { name: "Mẫu email gia hạn và kết thúc hợp đồng", exact: true })
    .click();
  await expect(page.locator("#renewal-subject")).toHaveValue(/\{\{contractNumber\}\}/);
  await page.getByRole("button", { name: "Thông báo kết thúc", exact: true }).click();
  await expect(page.locator("#end-body")).toHaveValue(/\{\{endDate\}\}/);
  for (const width of [375, 768]) {
    await page.setViewportSize({ width, height: 900 });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  }
});
