import { test, expect, TEST_USERS } from "../fixtures/auth";

test("reload restores a valid session without rotating its refresh token", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const original = (await page.context().cookies()).find(
    (cookie) => cookie.name === "nicon_refresh_token",
  )!.value;
  let refreshes = 0;
  page.on("request", (request) => {
    if (new URL(request.url()).pathname === "/api/auth/refresh") refreshes++;
  });
  await page.goto("/admin/contracts");
  for (let attempt = 0; attempt < 4; attempt++) {
    await expect(page.getByRole("link", { name: "NICON", exact: true })).toBeVisible();
    const profile = page.waitForResponse(
      (response) => new URL(response.url()).pathname === "/api/users/me",
    );
    await page.reload();
    expect((await profile).status()).toBe(200);
  }
  await expect(page).toHaveURL(/\/admin\/contracts/);
  expect(refreshes).toBe(0);
  expect(
    (await page.context().cookies()).find((cookie) => cookie.name === "nicon_refresh_token")!.value,
  ).toBe(original);
});

test("invalid access token refreshes once and restores the authenticated page", async ({
  page,
  baseURL,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page
    .context()
    .addCookies([{ name: "nicon_access_token", value: "expired-access-token", url: baseURL! }]);
  let refreshCount = 0;
  page.on("request", (request) => {
    if (new URL(request.url()).pathname === "/api/auth/refresh") refreshCount++;
  });
  const refresh = page.waitForResponse(
    (response) => new URL(response.url()).pathname === "/api/auth/refresh",
  );
  await page.goto("/admin/contracts");
  expect((await refresh).status()).toBe(200);
  await expect(page.getByRole("link", { name: "NICON", exact: true })).toBeVisible();
  await expect(page).toHaveURL(/\/admin\/contracts/);
  expect(refreshCount).toBe(1);
});

test("temporary profile failure preserves tokens and offers retry", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  const original = (await page.context().cookies()).find(
    (cookie) => cookie.name === "nicon_refresh_token",
  )!.value;
  await page.route("**/api/users/me", (route) =>
    route.fulfill({ status: 503, json: { message: "Session temporarily unavailable" } }),
  );
  await page.goto("/admin/contracts");
  await expect(page.getByText("Session temporarily unavailable")).toBeVisible();
  expect(
    (await page.context().cookies()).find((cookie) => cookie.name === "nicon_refresh_token")!.value,
  ).toBe(original);
  await page.unroute("**/api/users/me");
  await page.getByRole("button", { name: /retry/i }).click();
  await expect(page.getByRole("link", { name: "NICON", exact: true })).toBeVisible();
});

test("invalid access and refresh tokens return to login", async ({ page, baseURL }) => {
  await page.context().addCookies(
    ["nicon_access_token", "nicon_refresh_token"].map((name) => ({
      name,
      value: "invalid-session",
      url: baseURL!,
    })),
  );
  await page.goto("/admin/contracts");
  await expect(page).toHaveURL(/\/login/);
  expect(
    (await page.context().cookies()).some((cookie) => cookie.name === "nicon_refresh_token"),
  ).toBe(false);
});

test("an inactive account cannot restore a session with a valid access token", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.route("**/api/users/me", async (route) => {
    const response = await route.fetch();
    await route.fulfill({ json: { ...(await response.json()), isActive: false } });
  });
  await page.goto("/admin/contracts");
  await expect(page).toHaveURL(/\/login/);
  expect(
    (await page.context().cookies()).some((cookie) => cookie.name === "nicon_access_token"),
  ).toBe(false);
});
