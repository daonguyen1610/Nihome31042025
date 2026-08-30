import type { Page } from "@playwright/test";

import { expect, test, TEST_USERS } from "../fixtures/auth";

const projectId = 455000;

const detail = {
  id: projectId,
  code: "PJ-2026-0455",
  name: "NIH-455 timeline project",
  customerId: 1,
  customerName: "NICON",
  projectManagerUserId: 1,
  projectManagerName: "Project Manager",
  status: "Active",
  startDate: "2026-08-01T00:00:00Z",
  endDate: "2026-12-31T00:00:00Z",
  opportunityCount: 0,
  quoteCount: 0,
  contractCount: 2,
  updatedAt: "2026-08-30T00:00:00Z",
  note: null,
  designProjectId: null,
  designProjectCode: null,
  rowVersion: "AAAAAAAAB9M=",
  createdAt: "2026-08-01T00:00:00Z",
  opportunities: [],
  quotes: [],
  contracts: [],
};

const localeCopy = {
  vi: { title: "Timeline thanh toán", empty: "Chưa có mốc thanh toán hợp đồng trong dự án.", planned: "Ngày kế hoạch", actual: "Ngày hoàn tất thực tế", updated: "Cập nhật gần nhất", sourceLabel: "Nguồn cập nhật", source: "Mốc thanh toán hợp đồng", status: "Đã thanh toán", percent: "50% giá trị hợp đồng", seed: "Mốc thanh toán dài " },
  en: { title: "Payment timeline", empty: "No contract payment milestones in this project yet.", planned: "Planned date", actual: "Actual completion date", updated: "Last updated", sourceLabel: "Update source", source: "Contract payment milestone", status: "Paid", percent: "50% of contract value", seed: "Long payment milestone " },
  zh: { title: "付款时间线", empty: "此项目暂无合同付款里程碑。", planned: "计划日期", actual: "实际完成日期", updated: "最近更新", sourceLabel: "更新来源", source: "合同付款里程碑", status: "已付款", percent: "合同金额的 50%", seed: "长期付款里程碑" },
  ja: { title: "支払タイムライン", empty: "このプロジェクトには契約の支払マイルストーンがまだありません。", planned: "予定日", actual: "実際の完了日", updated: "最終更新", sourceLabel: "更新元", source: "契約支払マイルストーン", status: "支払済", percent: "契約金額の 50%", seed: "長い支払マイルストーン" },
} as const;

type TimelineItem = {
  id: number;
  contractId: number;
  contractNumber: string;
  order: number;
  name: string;
  percentValue: number;
  amount: number;
  plannedDate: string;
  actualDate: null;
  status: "Paid";
  source: "ContractPaymentMilestone";
  note: string;
  updatedAt: string;
};

function longTimelineItem(locale: keyof typeof localeCopy): TimelineItem {
  const seed = localeCopy[locale].seed;
  return {
    id: 1,
    contractId: 101,
    contractNumber: "HD-2026-LONG-CONTRACT-NUMBER-0455",
    order: 1,
    name: seed.repeat(Math.ceil(200 / seed.length)).slice(0, 200),
    percentValue: 50,
    amount: 500_000_000,
    plannedDate: "2026-09-01T00:00:00Z",
    actualDate: null,
    status: "Paid",
    source: "ContractPaymentMilestone",
    note: seed.repeat(Math.ceil(500 / seed.length)).slice(0, 500),
    updatedAt: "2026-09-04T10:00:00Z",
  };
}

async function setLocale(page: Page, locale: keyof typeof localeCopy) {
  if (page.url() === "about:blank") {
    await page.goto("/", { waitUntil: "domcontentloaded" });
  }
  await page.evaluate((value) => localStorage.setItem("nicon_lang", value), locale);
}

test("project payment timeline renders empty and maximum-length content in all locales", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  test.slow();
  await page.setViewportSize({ width: 390, height: 844 });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);

  let timelineItems: TimelineItem[] = [];
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(timelineItems) }));

  for (const locale of Object.keys(localeCopy) as Array<keyof typeof localeCopy>) {
    await setLocale(page, locale);
    await page.goto(`${baseURL}/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });
    await expect(page.getByText(localeCopy[locale].title, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].empty, { exact: true })).toBeVisible();

    const timelineItem = longTimelineItem(locale);
    timelineItems = [timelineItem];
    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByText(timelineItem.name, { exact: true })).toBeVisible();
    await expect(page.getByText(timelineItem.note, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].planned, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].actual, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].updated, { exact: true }).last()).toBeVisible();
    await expect(page.getByText(localeCopy[locale].sourceLabel, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].source, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].status, { exact: true })).toBeVisible();
    await expect(page.getByText(localeCopy[locale].percent, { exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: /HD-2026-LONG-CONTRACT-NUMBER-0455/ })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);

    timelineItems = [];
  }
});

test("project viewer without Contract permission sees a non-clickable Contract number", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.design);
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(detail) }));
  await page.route(new RegExp(`/api/(?:v1/)?operational-projects/${projectId}/timeline$`), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([longTimelineItem("vi")]) }));

  await page.goto(`${baseURL}/admin/operational-projects/${projectId}`, { waitUntil: "networkidle" });

  await expect(page.getByText("HD-2026-LONG-CONTRACT-NUMBER-0455", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: /HD-2026-LONG-CONTRACT-NUMBER-0455/ })).toHaveCount(0);
});