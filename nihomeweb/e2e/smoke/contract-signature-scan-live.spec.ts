import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("signing without a scan opens Documents and preserves the draft in every language", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const customerId = await createOwnCustomer(api, headers, "NICON – Hồ sơ ký hợp đồng thiết kế");
  let projectId: number | undefined;
  let contractId: number | undefined;
  try {
    const project = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Thiết kế văn phòng trước ký hợp đồng" },
    });
    expect(project.status(), await project.text()).toBe(201);
    projectId = (await project.json()).id;
    const created = await api.post("/api/contracts", {
      headers,
      data: {
        customerId,
        operationalProjectId: projectId,
        direction: "Upstream",
        type: "Design",
        value: 250_000_000,
        status: "Draft",
      },
    });
    expect(created.status(), await created.text()).toBe(201);
    contractId = (await created.json()).id;
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto("/");
    for (const scenario of [
      {
        language: "vi",
        sign: "Đánh dấu Đã ký",
        documents: "Tài liệu",
        scan: "Bản scan hợp đồng đã ký",
        width: 390,
      },
      {
        language: "en",
        sign: "Mark as Signed",
        documents: "Documents",
        scan: "Signed contract scan",
        width: 768,
      },
      {
        language: "zh",
        sign: "标记为已签署",
        documents: "文件",
        scan: "已签署合同扫描件",
        width: 1440,
      },
      {
        language: "ja",
        sign: "署名済にする",
        documents: "資料",
        scan: "署名済契約スキャン",
        width: 390,
      },
    ]) {
      await page.evaluate(
        (language) => localStorage.setItem("nicon_lang", language),
        scenario.language,
      );
      await page.setViewportSize({ width: scenario.width, height: 900 });
      await page.goto(`/admin/contracts/${contractId}`);
      await page.getByRole("button", { name: scenario.sign, exact: true }).click();
      await expect(
        page.getByRole("tab", { name: scenario.documents, exact: true }),
      ).toHaveAttribute("aria-selected", "true");
      await expect(
        page.getByRole("status").filter({ hasText: /scan|扫描|スキャン/ }),
      ).toBeVisible();
      await expect(page.getByRole("tabpanel").getByRole("combobox").first()).toHaveText(
        scenario.scan,
      );
      const current = await api.get(`/api/contracts/${contractId}`, { headers });
      expect(current.ok()).toBe(true);
      expect((await current.json()).status).toBe("Draft");
      await page.screenshot({
        path: testInfo.outputPath(`scan-required-${scenario.language}.png`),
      });
    }
  } finally {
    if (contractId) await hardDeleteBusinessRoot(api, headers, `/api/contracts/${contractId}`);
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
