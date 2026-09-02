import { expect, test, TEST_USERS } from "../fixtures/auth";
import type { APIRequestContext, Page } from "@playwright/test";
import { readFile } from "node:fs/promises";

const uid = () => Math.random().toString(36).slice(2, 9);
const createdSurveyIds = new Set<number>();
const pngBytes = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII=",
  "base64",
);

const connectionStatus = /Đã kết nối|Connected|已连接|接続済み|Không thể kết nối|Unavailable|无法连接|接続不可/i;
const previewLabel = /Xem tệp|Preview|预览|プレビュー/i;
const deleteLabel = /Xoá|Delete|删除|削除/i;
const saveLabel = /Lưu|Save|保存/i;
const exportPdfLabel = /Xuất PDF|Export PDF|导出 PDF|PDF.*出力/i;

async function createSurvey(
  api: APIRequestContext,
  authHeader: { Authorization: string },
) {
  const suffix = uid();
  const projectsResponse = await api.get("/api/operational-projects?page=1&pageSize=100", {
    headers: authHeader,
  });
  expect(projectsResponse.status(), await projectsResponse.text()).toBe(200);
  const projects = (await projectsResponse.json()) as { items: Array<{ id: number }> };
  expect(projects.items.length, "Survey Media E2E requires a seeded Operational Project").toBeGreaterThan(0);
  const operationalProjectId = [...projects.items].sort((left, right) => left.id - right.id)[0].id;

  const location = `E2E Survey Media ${suffix}`;
  const response = await api.post("/api/surveys", {
    headers: authHeader,
    data: {
      location,
      surveyDate: new Date().toISOString(),
      operationalProjectId,
      note: "Playwright Survey Media verification",
    },
  });
  expect(response.status(), await response.text()).toBe(201);
  const survey = (await response.json()) as { id: number; code: string; location: string };
  createdSurveyIds.add(survey.id);
  return survey;
}

async function openMediaTab(page: Page, surveyId: number) {
  await page.goto(`/admin/surveys/${surveyId}`, { waitUntil: "networkidle" });
  await page.getByRole("tab").nth(1).click();
  await expect(page.getByTestId("survey-media-panel")).toBeVisible();
}

type SurveyDetail = {
  id: number;
  media: Array<{
    id: number;
    originalFileName: string;
    contentType: string;
    size: number;
    note?: string | null;
    latitude?: number | null;
    longitude?: number | null;
    syncStatus: string;
    syncAttemptCount: number;
    contentUrl: string;
  }>;
  checklistResults: Array<{
    id: number;
    status?: string | null;
    note?: string | null;
  }>;
};

async function getSurveyDetail(
  api: APIRequestContext,
  surveyId: number,
  headers: { Authorization: string },
) {
  const response = await api.get(`/api/surveys/${surveyId}`, { headers });
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as SurveyDetail;
}

test.describe.serial("NIH-101 — Survey Media browser flow", () => {
  test.afterEach(async ({ api, loginAs }) => {
    if (createdSurveyIds.size === 0) return;
    const token = await loginAs(TEST_USERS.superAdmin);
    const headers = { Authorization: `Bearer ${token}` };
    try {
      for (const surveyId of createdSurveyIds) {
        const detailResponse = await api.get(`/api/surveys/${surveyId}`, { headers });
        if (detailResponse.status() === 404) continue;
        expect(detailResponse.status(), `cleanup: read survey ${surveyId}`).toBe(200);
        const detail = (await detailResponse.json()) as SurveyDetail;
        for (const media of detail.media) {
          const mediaDelete = await api.delete(`/api/surveys/${surveyId}/media/${media.id}`, { headers });
          expect(mediaDelete.status(), `cleanup: delete media ${media.id} from survey ${surveyId}`).toBe(204);
        }
        const surveyDelete = await api.delete(`/api/surveys/${surveyId}`, { headers });
        expect(surveyDelete.status(), `cleanup: delete survey ${surveyId}`).toBe(204);
        expect((await api.get(`/api/surveys/${surveyId}`, { headers })).status(), `cleanup: verify survey ${surveyId}`).toBe(404);
      }
    } finally {
      createdSurveyIds.clear();
    }
  });

  test("manager uploads, previews, updates checklist, exports PDF, and deletes media", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    test.slow();
    const jsErrors: string[] = [];
    page.on("pageerror", (error) => jsErrors.push(error.message));

    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    const survey = await createSurvey(api, authHeader);
    const fileName = `survey-${uid()}.png`;
    const note = `Browser upload ${uid()}`;
    const checklistNote = `Checked ${uid()}`;

    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await openMediaTab(page, survey.id);

    const connection = page.getByTestId("survey-drive-connection");
    await expect(connection).toContainText(connectionStatus);
    await expect(connection).not.toContainText(/ClientSecret|RefreshToken|client_secret|refresh_token/i);

    const fileInput = page.locator('input[type="file"]').first();
    await fileInput.setInputFiles({ name: fileName, mimeType: "image/png", buffer: pngBytes });
    await page.getByTestId("survey-media-panel").locator("textarea").first().fill(note);
    await page.getByRole("textbox", { name: /Vĩ độ|Latitude|纬度|緯度/i }).fill("10.776900");
    await page.getByRole("textbox", { name: /Kinh độ|Longitude|经度|経度/i }).fill("106.700900");

    const uploadResponsePromise = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/surveys/${survey.id}/media` &&
        response.request().method() === "POST",
    );
    await page.getByTestId("survey-media-upload").click();
    const uploadResponse = await uploadResponsePromise;
    const uploaded = (await uploadResponse.json()) as SurveyDetail["media"][number];
    expect(uploadResponse.status(), JSON.stringify(uploaded)).toBe(201);
    expect(uploaded).toMatchObject({
      originalFileName: fileName,
      contentType: "image/png",
      size: pngBytes.length,
      note,
      latitude: 10.7769,
      longitude: 106.7009,
      syncStatus: "Pending",
      syncAttemptCount: 0,
    });

    const persistedAfterUpload = await getSurveyDetail(api, survey.id, authHeader);
    expect(persistedAfterUpload.media).toEqual([
      expect.objectContaining({ id: uploaded.id, originalFileName: fileName, note }),
    ]);
    const storedContent = await api.get(uploaded.contentUrl, { headers: authHeader });
    expect(storedContent.status()).toBe(200);
    expect(Buffer.from(await storedContent.body())).toEqual(pngBytes);

    const card = page.getByTestId("survey-media-card").filter({ hasText: fileName });
    await expect(card).toBeVisible();
    await expect(card).toContainText(note);
    await expect(card.getByRole("link", { name: /Xem trên bản đồ|View on map|在地图中查看|地図で表示/i }))
      .toHaveAttribute("href", /mlat=10\.7769.*mlon=106\.7009/);
    const thumbnail = card.getByRole("img", { name: fileName });
    await expect
      .poll(() => thumbnail.evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0))
      .toBe(true);

    await card.getByRole("button", { name: previewLabel }).click();
    const previewDialog = page.getByRole("dialog");
    const previewImage = previewDialog.getByRole("img", { name: fileName });
    await expect(previewImage).toBeVisible();
    await expect
      .poll(() => previewImage.evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0))
      .toBe(true);
    await previewDialog.getByRole("button", { name: /Đóng|Close|关闭|閉じる/i }).first().click();

    const checklist = page.getByTestId("survey-checklist");
    const firstChecklistRow = checklist.locator(".grid.rounded-lg").first();
    await expect(firstChecklistRow).toContainText("Geology");
    await firstChecklistRow.getByRole("combobox").click();
    await page.getByRole("option").first().click();
    await firstChecklistRow.getByRole("textbox").fill(checklistNote);
    const checklistResponsePromise = page.waitForResponse(
      (response) =>
        /\/api\/surveys\/\d+\/checklist\/\d+$/.test(new URL(response.url()).pathname) &&
        response.request().method() === "PUT",
    );
    await firstChecklistRow.getByRole("button", { name: saveLabel }).click();
    const checklistResponse = await checklistResponsePromise;
    expect(checklistResponse.status()).toBe(200);
    const savedChecklist = (await checklistResponse.json()) as { id: number; status: string; note: string };
    expect(savedChecklist).toMatchObject({ status: "Ok", note: checklistNote });

    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("tab").nth(1).click();
    const persistedAfterReload = await getSurveyDetail(api, survey.id, authHeader);
    expect(persistedAfterReload.checklistResults).toContainEqual(
      expect.objectContaining({ id: savedChecklist.id, status: "Ok", note: checklistNote }),
    );
    const reloadedChecklistRow = page.getByTestId("survey-checklist").locator(".grid.rounded-lg").first();
    await expect(reloadedChecklistRow.getByRole("textbox")).toHaveValue(checklistNote);
    await expect(reloadedChecklistRow.getByRole("combobox")).toContainText(/Đạt|OK|合格|適合/i);

    const syncLog = page.getByTestId("survey-sync-log");
    const syncLogResponsePromise = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/surveys/${survey.id}/sync-log` &&
        response.request().method() === "GET",
    );
    await syncLog.getByRole("button").click();
    expect((await syncLogResponsePromise).status()).toBe(200);
    await expect(syncLog).toContainText(fileName);

    const downloadResponsePromise = page.waitForResponse((response) => {
      const url = new URL(response.url());
      return url.pathname === `/api/surveys/${survey.id}/export.pdf` && url.searchParams.get("lang") === "en";
    });
    const downloadPromise = page.waitForEvent("download");
    await page.getByRole("button", { name: exportPdfLabel }).click();
    expect((await downloadResponsePromise).status()).toBe(200);
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe(`survey-${survey.code}.pdf`);
    const downloadPath = await download.path();
    expect(downloadPath).not.toBeNull();
    const pdfBytes = await readFile(downloadPath!);
    expect(pdfBytes.subarray(0, 5).toString("ascii")).toBe("%PDF-");
    expect(pdfBytes.length).toBeGreaterThan(500);

    await card.getByRole("button", { name: deleteLabel }).click();
    const deleteDialog = page.getByRole("alertdialog");
    const deleteResponsePromise = page.waitForResponse(
      (response) =>
        /\/api\/surveys\/\d+\/media\/\d+$/.test(new URL(response.url()).pathname) &&
        response.request().method() === "DELETE",
    );
    await deleteDialog.getByRole("button", { name: deleteLabel }).click();
    expect((await deleteResponsePromise).status()).toBe(204);
    await expect(card).toHaveCount(0);
    const persistedAfterDelete = await getSurveyDetail(api, survey.id, authHeader);
    expect(persistedAfterDelete.media).toHaveLength(0);
    expect((await api.get(uploaded.contentUrl, { headers: authHeader })).status()).toBe(404);

    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("tab").nth(1).click();
    await expect(page.getByTestId("survey-media-card")).toHaveCount(0);

    expect(jsErrors, jsErrors.join("\n")).toHaveLength(0);
  });

  test("view-only user can inspect media but cannot mutate it", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    const adminToken = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${adminToken}` };
    const survey = await createSurvey(api, authHeader);
    const fileName = `view-only-${uid()}.png`;
    const upload = await api.post(`/api/surveys/${survey.id}/media`, {
      headers: authHeader,
      multipart: {
        file: { name: fileName, mimeType: "image/png", buffer: pngBytes },
        note: "View-only browser verification",
      },
    });
    expect(upload.status(), await upload.text()).toBe(201);

    await loginInBrowserAs(page, TEST_USERS.pm);
    await openMediaTab(page, survey.id);

    await expect(page.getByTestId("survey-media-card").filter({ hasText: fileName })).toBeVisible();
    await expect(page.getByTestId("survey-media-choose-file")).toHaveCount(0);
    await expect(page.getByTestId("survey-media-camera")).toHaveCount(0);
    await expect(page.getByTestId("survey-media-upload")).toHaveCount(0);
    await expect(page.getByTestId("survey-media-card").getByRole("button", { name: deleteLabel })).toHaveCount(0);
    await expect(page.getByTestId("survey-checklist").getByRole("combobox").first()).toBeDisabled();
    await expect(page.getByTestId("survey-checklist").getByRole("button", { name: saveLabel })).toHaveCount(0);
  });

  test("mobile panel renders without horizontal page overflow or browser errors", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (error) => jsErrors.push(error.message));
    await page.setViewportSize({ width: 390, height: 844 });

    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    const survey = await createSurvey(api, authHeader);

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await openMediaTab(page, survey.id);
    await expect(page.getByTestId("survey-drive-connection")).toContainText(connectionStatus);
    await expect(page.getByTestId("survey-checklist")).toBeVisible();
    await expect(page.getByTestId("survey-sync-log")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    expect(jsErrors, jsErrors.join("\n")).toHaveLength(0);
  });

  test("live Drive connection reports Connected and syncs an uploaded file", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }) => {
    test.skip(
      !process.env.GOOGLE_DRIVE_LIVE_E2E,
      "Requires protected OAuth credential and RootFolderId; absent in this workspace.",
    );
    test.slow();

    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    const connection = await api.get("/api/surveys/drive-connection", { headers: authHeader });
    expect(connection.status(), await connection.text()).toBe(200);
    expect((await connection.json()).status).toBe("Connected");

    const survey = await createSurvey(api, authHeader);
    const fileName = `live-drive-${uid()}.png`;
    const upload = await api.post(`/api/surveys/${survey.id}/media`, {
      headers: authHeader,
      multipart: {
        file: { name: fileName, mimeType: "image/png", buffer: pngBytes },
        note: "Live Drive Playwright verification",
      },
    });
    expect(upload.status(), await upload.text()).toBe(201);
    const mediaId = (await upload.json()).id as number;

    await expect
      .poll(
        async () => {
          const detail = await api.get(`/api/surveys/${survey.id}`, { headers: authHeader });
          if (!detail.ok()) return "RequestFailed";
          const media = (await detail.json()).media as Array<{ id: number; syncStatus: string }>;
          return media.find((item) => item.id === mediaId)?.syncStatus ?? "Missing";
        },
        { timeout: 90_000, intervals: [1_000, 2_000, 5_000] },
      )
      .toBe("Synced");

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await openMediaTab(page, survey.id);
    await expect(page.getByTestId("survey-drive-connection")).toContainText(/Đã kết nối|Connected|已连接|接続済み/i);
    await expect(page.getByTestId("survey-media-card").filter({ hasText: fileName })).toContainText(/Đã đồng bộ|Synced|已同步|同期済み/i);

    expect((await api.delete(`/api/surveys/${survey.id}/media/${mediaId}`, { headers: authHeader })).status()).toBe(204);
  });
});
