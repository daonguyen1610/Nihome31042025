import { test, expect } from "@playwright/test";
import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";

// Browser behavior is isolated from backend business-rule tests: no live account
// or business mutations are needed to verify layout, filters and recovery.
const seedDir = path.resolve(process.cwd(), "../nihomebackend/Data/Seeds/i18n");
const translations = readdirSync(seedDir)
  .filter((name) => name.endsWith(".json"))
  .flatMap((name) => JSON.parse(readFileSync(path.join(seedDir, name), "utf8")));

for (const width of [390, 768, 1440]) {
  test(`tender statistics filters and reconciles at ${width}px`, async ({
    page,
    baseURL,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 1000 });
    await page.context().addCookies(
      ["nicon_access_token", "nicon_refresh_token"].map((name) => ({
        name,
        value: "browser-fixture",
        url: baseURL!,
      })),
    );
    await page.addInitScript(() => {
      if (!localStorage.getItem("nicon_lang")) localStorage.setItem("nicon_lang", "en");
    });
    let mode = "normal";
    let latestList = "";
    const reportQueries: URLSearchParams[] = [];
    await page.route(/\/api\//, async (route) => {
      const url = new URL(route.request().url());
      const endpoint = url.pathname.replace(/^\/api(?:\/v1)?/, "");
      let body: unknown = [];
      if (endpoint === "/notifications/unread-count")
        return route.fulfill({ status: 200, json: { count: 0 } });
      if (endpoint === "/auth/refresh")
        body = {
          accessToken: "browser-fixture",
          refreshToken: "browser-fixture",
          userId: 1,
          fullName: "Nicon Tender Manager",
          role: "SALES_MANAGER",
          roleId: 2,
          isActive: true,
        };
      else if (endpoint === "/users/me")
        body = {
          id: 1,
          phoneNumber: "0911000010",
          fullName: "Nicon Tender Manager",
          role: "SALES_MANAGER",
          roleId: 2,
          isActive: true,
        };
      else if (endpoint === "/users/me/permissions")
        body = { role: "SALES_MANAGER", roleId: 2, permissions: ["crm.tenders.view"] };
      else if (endpoint.startsWith("/translations/")) {
        const lang = endpoint.split("/").at(-1)!;
        body = {
          languageCode: lang,
          translations: Object.fromEntries(translations.map((r) => [r.key, r[lang]])),
        };
      } else if (endpoint === "/tenders/report-customers")
        body = [{ id: 41, name: "Long Thanh Manufacturing" }];
      else if (endpoint === "/tenders/report-preparers")
        body = [{ id: 9, fullName: "Nguyen Minh Quan" }];
      else if (endpoint === "/tenders/statistics") {
        reportQueries.push(url.searchParams);
        if (mode === "error")
          return route.fulfill({
            status: 503,
            json: { message: "Statistics temporarily unavailable" },
          });
        body = {
          submittedCount: mode === "empty" ? 0 : 10,
          wonCount: 3,
          lostCount: 2,
          pendingCount: 4,
          cancelledCount: 1,
          winRate: mode === "empty" ? null : 30,
          missingSubmissionCount: 1,
          calculatedAt: "2026-10-17T03:00:00Z",
        };
      } else if (endpoint === "/tenders") {
        latestList = url.search;
        body = {
          items: [
            {
              id: 81,
              code: "TD-2026-0081",
              name: "Long Thanh structural works",
              customerId: 41,
              customerName: "Long Thanh Manufacturing",
              status: "Won",
              submissionDeadline: "2026-08-15T10:00:00Z",
              updatedAt: "2026-10-17T03:00:00Z",
              preparerUserId: 9,
              preparerName: "Nguyen Minh Quan",
              checklistCompletionPercent: 100,
              isDeadlineImminent: false,
            },
          ],
          total: url.searchParams.has("submittedOnly") ? 10 : 1,
          page: 1,
          pageSize: 20,
        };
      } else if (endpoint === "/customers" || endpoint === "/operational-projects")
        body = { items: [], total: 0 };
      return route.fulfill({ status: 200, json: body });
    });
    const errors: string[] = [];
    page.on("pageerror", (err) => errors.push(err.message));
    await page.goto("/admin/tenders");
    const panel = page.getByTestId("tender-statistics");
    await expect(panel.getByTestId("tender-win-rate")).toHaveText("30%");
    await expect(panel.getByTestId("tender-submitted-count")).toHaveText("10");
    await expect(page.getByRole("button", { name: "New tender" })).toHaveCount(0);
    await panel.getByLabel("Submitted from").fill("2026-08-01");
    await panel.getByLabel("Through date").fill("2026-08-31");
    await expect(
      panel.getByText("Filters changed. Select Apply to update the results."),
    ).toBeVisible();
    await expect(panel.getByRole("button", { name: "View included tenders" })).toBeDisabled();
    await panel.getByRole("combobox", { name: "Customer", exact: true }).click();
    await page.getByRole("option", { name: "Long Thanh Manufacturing" }).click();
    await panel.getByRole("combobox", { name: "Preparer", exact: true }).click();
    await page.getByRole("option", { name: "Nguyen Minh Quan" }).click();
    await panel.getByRole("button", { name: "Apply", exact: true }).click();
    await expect(panel.getByTestId("tender-win-rate")).toHaveText("30%");
    expect(reportQueries.at(-1)?.get("fromDate")).toBe("2026-08-01");
    expect(reportQueries.at(-1)?.get("customerId")).toBe("41");
    expect(reportQueries.at(-1)?.get("preparerUserId")).toBe("9");
    await panel.getByRole("button", { name: "View included tenders" }).click();
    await expect(page.getByRole("button", { name: "Clear report filter" })).toBeVisible();
    await expect.poll(() => latestList).toContain("submittedOnly=true");
    expect(latestList).toContain("preparerUserId=9");
    await expect(
      page.getByText("Long Thanh structural works", { exact: true }).filter({ visible: true }),
    ).toBeVisible();
    await page.screenshot({
      path: testInfo.outputPath(`tender-statistics-${width}.png`),
      fullPage: true,
    });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.getByRole("button", { name: "Clear report filter" }).click();
    await expect.poll(() => latestList).not.toContain("submittedOnly");

    const requestsBeforeRefresh = reportQueries.length;
    await page.getByRole("button", { name: "Refresh", exact: true }).first().click();
    await expect.poll(() => reportQueries.length).toBeGreaterThan(requestsBeforeRefresh);
    await expect(panel.getByTestId("tender-win-rate")).toHaveText("30%");

    mode = "error";
    await panel.getByRole("button", { name: "Refresh statistics" }).click();
    await expect(panel.getByRole("alert")).toContainText("Statistics temporarily unavailable");
    mode = "empty";
    await panel.getByRole("button", { name: /retry/i }).click();
    await expect(
      panel.getByText("No submitted tenders in the selected period and scope."),
    ).toBeVisible();
    await expect(panel.getByTestId("tender-win-rate")).toHaveCount(0);

    for (const lang of ["vi", "zh", "ja"]) {
      await page.evaluate((value) => localStorage.setItem("nicon_lang", value), lang);
      await page.reload();
      const title = translations.find((r) => r.key === "tenders.stats.title")[lang];
      await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
      await expect(page.getByTestId("tender-statistics")).not.toContainText("tenders.stats.");
    }
    expect(errors).toEqual([]);
  });
}
