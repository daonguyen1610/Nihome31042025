import { expect, test, TEST_USERS } from "../fixtures/auth";

const configuration = {
  enabled: true,
  clientId: "123.apps.googleusercontent.com",
  hasClientSecret: true,
  hasRefreshToken: true,
  oAuthRedirectUri: "https://nicon.example.com/api/site-settings/google-drive/oauth/callback",
  frontendReturnUrl: "/admin/settings?tab=drive",
  rootFolderId: "1234567890root",
  instanceId: "nicon-e2e",
  applicationName: "Nicon Google Drive Integration",
  folders: {
    surveyMedia: "01_Khao_sat",
    crmPreDesign: "01_CRM_PreDesign",
    designConcept: "02_Thiet_ke/01_So_bo_Concept",
    designBasic: "02_Thiet_ke/02_Co_so",
    designShopDrawing: "02_Thiet_ke/03_Chi_tiet_ShopDrawing",
    legalPermits: "03_Xin_phep_Phap_ly",
    constructionAcceptance: "04_Thi_cong_Nghiem_thu",
    procurement: "05_Cung_ung_Vat_tu",
    financeContracts: "06_Tai_chinh_Hop_dong",
  },
  supportsAllDrives: true,
  pollIntervalSeconds: 15,
  accountEmail: "current@nicon.test",
  connectedAt: "2026-08-31T10:00:00Z",
  rowVersion: "AAAAAAAAB9Q=",
};

const connectedStatus = (accountEmail: string) => ({
  status: "Connected",
  oauthConfigured: true,
  hasStoredCredential: true,
  accountEmail,
  connectedAt: "2026-08-31T10:00:00Z",
  rootFolderName: "Nicon Projects",
  rootFolderLink: "https://drive.google.com/drive/folders/root",
  error: null,
});

test("first-time setup enables and saves configuration before connection", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let saved = false;

  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/configuration$"), async route => {
    if (route.request().method() === "PUT") {
      const request = route.request().postDataJSON();
      saved = request.enabled === true;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ ...configuration, enabled: saved, hasRefreshToken: false, accountEmail: null }),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ ...configuration, enabled: false, hasRefreshToken: false, accountEmail: null }),
    });
  });
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/status$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        ...connectedStatus(""),
        status: "ReconnectRequired",
        oauthConfigured: saved,
        hasStoredCredential: false,
        accountEmail: null,
      }),
    }));

  await page.goto(`${baseURL}/admin/settings?tab=drive`, { waitUntil: "networkidle" });
  const connectButton = page.getByRole("button", { name: /Connect Google Drive|Kết nối Google Drive|连接 Google Drive|Google Drive に接続/i });
  await expect(connectButton).toBeDisabled();

  await page.getByLabel(/Enable integration|Bật tích hợp|启用集成|連携を有効化/i).click();
  await page.getByRole("button", { name: /Save configuration|Lưu cấu hình|保存配置|設定を保存/i }).click();

  await expect.poll(() => saved).toBe(true);
  await expect(connectButton).toBeEnabled();
});

test("switching Google Drive account disconnects before popup authorization", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const operations: string[] = [];
  let accountEmail = "current@nicon.test";

  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/configuration$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      ...configuration,
      hasRefreshToken: accountEmail !== "",
      accountEmail: accountEmail || null,
    }) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/status$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(
      accountEmail ? connectedStatus(accountEmail) : {
        ...connectedStatus(""),
        status: "ReconnectRequired",
        hasStoredCredential: false,
        accountEmail: null,
      },
    ) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/oauth/disconnect$"), async route => {
    operations.push("disconnect");
    accountEmail = "";
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ hadStoredCredential: true, providerRevoked: true }),
    });
  });
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/oauth/start$"), async route => {
    operations.push("start");
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ authorizationUrl: "https://accounts.google.com/o/oauth2/v2/auth?state=e2e" }),
    });
  });
  await page.context().route("https://accounts.google.com/**", route => {
    operations.push("google");
    accountEmail = "replacement@nicon.test";
    return route.fulfill({
      status: 200,
      contentType: "text/html",
      body: `<script>location.replace(${JSON.stringify(`${baseURL}/admin/settings?tab=drive&driveOAuth=success`)})</script>`,
    });
  });

  await page.goto(`${baseURL}/admin/settings?tab=drive`, { waitUntil: "networkidle" });
  await expect(page.getByText("current@nicon.test")).toBeVisible();
  const setupGuide = page.getByRole("button", {
    name: /Step-by-step configuration guide|Hướng dẫn lấy thông tin cấu hình từng bước|分步配置指南|設定情報の取得手順/i,
  });
  await expect(setupGuide).toBeVisible();
  await setupGuide.click();
  await expect(page.getByText(/enable Google Drive API|bật Google Drive API|启用 Google Drive API|Google Drive API を有効/i)).toBeVisible();
  await expect(page.getByText(/\.apps\.googleusercontent\.com/)).toBeVisible();

  page.once("dialog", dialog => dialog.accept());
  const popupPromise = page.waitForEvent("popup");
  await page.getByRole("button", { name: /Switch Google account|Đổi tài khoản Google|切换 Google 帐户|Google アカウントを切り替え/i }).click();
  const popup = await popupPromise;
  await expect.poll(() => popup.isClosed()).toBe(true);

  await expect(page.getByText("replacement@nicon.test")).toBeVisible();
  expect(operations).toEqual(["disconnect", "start", "google"]);
});

test("closing replacement popup refreshes the disconnected status", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let accountEmail = "current@nicon.test";

  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/configuration$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      ...configuration,
      hasRefreshToken: accountEmail !== "",
      accountEmail: accountEmail || null,
    }) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/status$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(
      accountEmail ? connectedStatus(accountEmail) : {
        ...connectedStatus(""),
        status: "ReconnectRequired",
        hasStoredCredential: false,
        accountEmail: null,
      },
    ) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/oauth/disconnect$"), async route => {
    accountEmail = "";
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ hadStoredCredential: true, providerRevoked: true }),
    });
  });
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/oauth/start$"), route =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ authorizationUrl: "https://accounts.google.com/o/oauth2/v2/auth?state=closed" }),
    }));
  await page.context().route("https://accounts.google.com/**", route => route.fulfill({
    status: 200,
    contentType: "text/html",
    body: "<script>window.close()</script>",
  }));

  await page.goto(`${baseURL}/admin/settings?tab=drive`, { waitUntil: "networkidle" });
  await expect(page.getByText("current@nicon.test")).toBeVisible();

  page.once("dialog", dialog => dialog.accept());
  const popupPromise = page.waitForEvent("popup");
  await page.getByRole("button", { name: /Switch Google account|Đổi tài khoản Google|切换 Google 帐户|Google アカウントを切り替え/i }).click();
  const popup = await popupPromise;
  await expect.poll(() => popup.isClosed()).toBe(true);

  await expect(page.getByText("current@nicon.test")).not.toBeVisible();
  await expect(page.getByRole("button", { name: /Connect Google Drive|Kết nối Google Drive|连接 Google Drive|Google Drive に接続/i })).toBeVisible();
});

test("manual disconnect warns when Google does not confirm revocation", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let accountEmail = "current@nicon.test";

  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/configuration$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      ...configuration,
      hasRefreshToken: accountEmail !== "",
      accountEmail: accountEmail || null,
    }) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/status$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(
      accountEmail ? connectedStatus(accountEmail) : {
        ...connectedStatus(""),
        status: "ReconnectRequired",
        hasStoredCredential: false,
        accountEmail: null,
      },
    ) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/oauth/disconnect$"), async route => {
    accountEmail = "";
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ hadStoredCredential: true, providerRevoked: false }),
    });
  });

  await page.goto(`${baseURL}/admin/settings?tab=drive`, { waitUntil: "networkidle" });
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: /Disconnect|Ngắt kết nối|断开连接|接続解除/i }).click();

  await expect(page.getByText(/Google did not confirm revocation|Google chưa xác nhận thu hồi quyền|Google 未确认撤销授权|Google は取り消しを確認していません/i)).toBeVisible();
  await expect(page.getByText("current@nicon.test")).not.toBeVisible();
  await expect(page.getByRole("button", { name: /Connect Google Drive|Kết nối Google Drive|连接 Google Drive|Google Drive に接続/i })).toBeVisible();
});

test("manual disconnect error reloads authoritative disconnected status", async ({
  page,
  loginInBrowserAs,
  baseURL,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  let accountEmail = "current@nicon.test";

  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/configuration$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      ...configuration,
      hasRefreshToken: accountEmail !== "",
      accountEmail: accountEmail || null,
    }) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/status$"), route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(
      accountEmail ? connectedStatus(accountEmail) : {
        ...connectedStatus(""),
        status: "ReconnectRequired",
        hasStoredCredential: false,
        accountEmail: null,
      },
    ) }));
  await page.context().route(new RegExp("/api/(?:v1/)?site-settings/google-drive/oauth/disconnect$"), async route => {
    accountEmail = "";
    await route.fulfill({ status: 500, contentType: "application/json", body: "{}" });
  });

  await page.goto(`${baseURL}/admin/settings?tab=drive`, { waitUntil: "networkidle" });
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: /Disconnect|Ngắt kết nối|断开连接|接続解除/i }).click();

  await expect(page.getByText("current@nicon.test")).not.toBeVisible();
  await expect(page.getByRole("button", { name: /Connect Google Drive|Kết nối Google Drive|连接 Google Drive|Google Drive に接続/i })).toBeVisible();
});
