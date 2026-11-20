import { randomBytes } from "node:crypto";
import { writeFile } from "node:fs/promises";
import { expect, test, TEST_USERS } from "../fixtures/auth";

const MB = 1024 * 1024;
const CHUNK = 8 * MB;

type ContractAttachment = { id: number; originalFileName: string; fileSize: number };

async function firstContractId(
  api: import("@playwright/test").APIRequestContext,
  headers: Record<string, string>,
) {
  const contracts = await (await api.get("/api/contracts?pageSize=1", { headers })).json();
  expect(contracts.items.length, "a seeded contract is needed").toBeGreaterThan(0);
  return contracts.items[0].id as number;
}

async function deleteAttachment(
  api: import("@playwright/test").APIRequestContext,
  headers: Record<string, string>,
  contractId: number,
  name: string,
) {
  const attachments = (await (
    await api.get(`/api/contracts/${contractId}/attachments`, { headers })
  ).json()) as ContractAttachment[];
  for (const item of attachments.filter((entry) => entry.originalFileName === name))
    await api.delete(`/api/contracts/${contractId}/attachments/${item.id}`, { headers });
}

/** The module's own upload request, carrying session ids instead of the large file. */
const uploadRequest = (path: string) => (response: import("@playwright/test").Response) =>
  response.request().method() === "POST" && new URL(response.url()).pathname === path;

// Large files in any module go in 8 MB chunks before the module's own upload request: a
// dropped chunk is sent again on its own, and the server ends up with the whole file.
test("a large contract file survives a dropped chunk", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const contractId = await firstContractId(api, headers);
  const name = `hop-dong-ky-${Date.now()}.pdf`;
  const path = testInfo.outputPath(name);
  await writeFile(path, randomBytes(2 * CHUNK + 4 * MB));

  try {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    const offsets: number[] = [];
    let dropped = false;
    await page.route("**/api/upload-sessions/*/chunks?*", async (route) => {
      const offset = Number(new URL(route.request().url()).searchParams.get("offset"));
      offsets.push(offset);
      // The connection drops once in the middle of the second chunk.
      if (offset === CHUNK && !dropped) {
        dropped = true;
        await route.abort("connectionreset");
        return;
      }
      await route.continue();
    });
    await page.goto(`/admin/contracts/${contractId}?tab=documents`, { waitUntil: "networkidle" });
    await page.getByTestId("contract-attachment-files").setInputFiles(path);
    const completed = page.waitForResponse(
      uploadRequest(`/api/contracts/${contractId}/attachments`),
      { timeout: 60_000 },
    );
    await page.getByRole("button", { name: "Tải lên tài liệu" }).click();

    const response = await completed;
    expect(response.status()).toBe(201);
    // The module's request carried the session, not the 20 MB.
    expect(response.request().headers()["x-upload-sessions"]).toMatch(/^file=[0-9a-f-]{36}$/);
    expect(response.request().postDataBuffer()?.length ?? 0).toBeLessThan(64 * 1024);
    expect(offsets).toEqual([0, CHUNK, CHUNK, 2 * CHUNK]);
    const attachments = (await (
      await api.get(`/api/contracts/${contractId}/attachments`, { headers })
    ).json()) as ContractAttachment[];
    expect(attachments.find((item) => item.originalFileName === name)?.fileSize).toBe(
      2 * CHUNK + 4 * MB,
    );
  } finally {
    await deleteAttachment(api, headers, contractId, name);
  }
});

// Reloading in the middle and choosing the same file again resumes where the server
// stopped instead of sending the first chunk again.
test("choosing the same file again after a reload resumes the upload", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const contractId = await firstContractId(api, headers);
  const name = `phu-luc-scan-${Date.now()}.pdf`;
  const path = testInfo.outputPath(name);
  await writeFile(path, randomBytes(CHUNK + 2 * MB));

  try {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    // Hold the second chunk so the page can be reloaded mid-upload.
    await page.route("**/api/upload-sessions/*/chunks?*", async (route) => {
      const offset = Number(new URL(route.request().url()).searchParams.get("offset"));
      if (offset === 0) {
        await route.continue();
        return;
      }
      await new Promise(() => undefined);
    });
    await page.goto(`/admin/contracts/${contractId}?tab=documents`, { waitUntil: "networkidle" });
    await page.getByTestId("contract-attachment-files").setInputFiles(path);
    // The first chunk is stored on the server before the reload.
    const stored = page.waitForResponse(
      (response) =>
        new URL(response.url()).searchParams.get("offset") === "0" && response.status() === 200,
    );
    await page.getByRole("button", { name: "Tải lên tài liệu" }).click();
    await stored;
    await page.unrouteAll({ behavior: "ignoreErrors" });

    await page.reload({ waitUntil: "networkidle" });
    // Count only what the reloaded page sends; the held request above dies with the reload.
    const sessionsOpened: string[] = [];
    const offsets: number[] = [];
    page.on("request", (request) => {
      const url = new URL(request.url());
      if (request.method() === "POST" && url.pathname.endsWith("/upload-sessions"))
        sessionsOpened.push(url.pathname);
      if (request.method() === "PUT" && url.pathname.endsWith("/chunks"))
        offsets.push(Number(url.searchParams.get("offset")));
    });
    await page.getByTestId("contract-attachment-files").setInputFiles(path);
    const completed = page.waitForResponse(
      uploadRequest(`/api/contracts/${contractId}/attachments`),
      { timeout: 60_000 },
    );
    await page.getByRole("button", { name: "Tải lên tài liệu" }).click();

    expect((await completed).status()).toBe(201);
    expect(sessionsOpened).toEqual([]);
    expect(offsets).toEqual([CHUNK]);
  } finally {
    await deleteAttachment(api, headers, contractId, name);
  }
});

// The same transport serves every module: a quote document above 8 MB is chunked too.
test("a large quote document is sent in chunks like any other module's file", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const quotes = await (await api.get("/api/quotes?pageSize=1", { headers })).json();
  expect(quotes.items.length, "a seeded quote is needed").toBeGreaterThan(0);
  const quoteId = quotes.items[0].id as number;
  const name = `ban-ve-bao-gia-${Date.now()}.pdf`;
  const path = testInfo.outputPath(name);
  await writeFile(path, randomBytes(CHUNK + 4 * MB));
  let documentId: number | undefined;

  try {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    const offsets: number[] = [];
    let dropped = false;
    await page.route("**/api/upload-sessions/*/chunks?*", async (route) => {
      const offset = Number(new URL(route.request().url()).searchParams.get("offset"));
      offsets.push(offset);
      if (offset === CHUNK && !dropped) {
        dropped = true;
        await route.abort("connectionreset");
        return;
      }
      await route.continue();
    });
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu/ }).click();
    await page.getByTestId("quote-document-file").setInputFiles(path);
    const completed = page.waitForResponse(uploadRequest(`/api/quotes/${quoteId}/documents`), {
      timeout: 60_000,
    });
    await page.getByRole("button", { name: "Tải lên", exact: true }).click();

    const response = await completed;
    expect(response.status()).toBe(201);
    expect(response.request().headers()["x-upload-sessions"]).toMatch(/^file=[0-9a-f-]{36}$/);
    expect(offsets).toEqual([0, CHUNK, CHUNK]);
    const document = await response.json();
    documentId = document.id;
    expect(document.originalFileName).toBe(name);
    expect(document.fileSize).toBe(CHUNK + 4 * MB);
  } finally {
    if (documentId) await api.delete(`/api/quotes/${quoteId}/documents/${documentId}`, { headers });
  }
});

// A file larger than every Settings limit is sent directly as before, so a module with
// its own fixed limit still decides instead of the chunk transport refusing it.
test("a file the chunk transport refuses is sent directly to the module", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const quotes = await (await api.get("/api/quotes?pageSize=1", { headers })).json();
  const quoteId = quotes.items[0].id as number;
  const name = `ho-so-ky-thuat-${Date.now()}.pdf`;
  const path = testInfo.outputPath(name);
  await writeFile(path, randomBytes(CHUNK + MB));
  let documentId: number | undefined;

  try {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    await page.route("**/api/upload-sessions", (route) =>
      route.fulfill({
        status: 400,
        contentType: "application/json",
        body: JSON.stringify({ code: "UPLOAD_TOO_LARGE", maxFileSizeMb: 5 }),
      }),
    );
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu/ }).click();
    await page.getByTestId("quote-document-file").setInputFiles(path);
    const completed = page.waitForResponse(uploadRequest(`/api/quotes/${quoteId}/documents`), {
      timeout: 60_000,
    });
    await page.getByRole("button", { name: "Tải lên", exact: true }).click();

    const response = await completed;
    expect(response.status()).toBe(201);
    expect(response.request().headers()["x-upload-sessions"]).toBeUndefined();
    const document = await response.json();
    documentId = document.id;
    expect(document.fileSize).toBe(CHUNK + MB);
  } finally {
    if (documentId) await api.delete(`/api/quotes/${quoteId}/documents/${documentId}`, { headers });
  }
});

// The upload request reached the server and stored the file, but its reply was lost.
// Sending it again returns the same document instead of a second copy or a re-upload.
test("a resend after a lost reply gets the stored document, not a duplicate", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}, testInfo) => {
  test.setTimeout(120_000);
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.salesManager)}` };
  const quotes = await (await api.get("/api/quotes?pageSize=1", { headers })).json();
  const quoteId = quotes.items[0].id as number;
  const target = `/api/quotes/${quoteId}/documents`;
  const name = `bien-ban-nghiem-thu-${Date.now()}.pdf`;
  const path = testInfo.outputPath(name);
  await writeFile(path, randomBytes(CHUNK + MB));
  const stored = async () =>
    (
      (await (await api.get(target, { headers })).json()) as {
        id: number;
        originalFileName: string;
      }[]
    ).filter((item) => item.originalFileName === name);

  try {
    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));
    let lost = false;
    await page.route(`**${target}`, async (route) => {
      if (route.request().method() !== "POST" || lost) return route.continue();
      lost = true;
      // Let the server store it, then drop the reply on the way back.
      await route.fetch();
      await route.abort("connectionreset");
    });
    await page.goto(`/admin/quotes/${quoteId}`, { waitUntil: "networkidle" });
    await page.getByRole("tab", { name: /Tài liệu/ }).click();
    await page.getByTestId("quote-document-file").setInputFiles(path);
    const firstTry = page.waitForEvent(
      "requestfailed",
      (request) => new URL(request.url()).pathname === target,
    );
    await page.getByRole("button", { name: "Tải lên", exact: true }).click();
    await firstTry;
    const first = await stored();
    expect(first).toHaveLength(1);

    const chunks: number[] = [];
    page.on("request", (request) => {
      if (request.method() === "PUT" && new URL(request.url()).pathname.endsWith("/chunks"))
        chunks.push(Number(new URL(request.url()).searchParams.get("offset")));
    });
    await page.getByTestId("quote-document-file").setInputFiles(path);
    const resent = page.waitForResponse(uploadRequest(target), { timeout: 60_000 });
    await page.getByRole("button", { name: "Tải lên", exact: true }).click();

    const response = await resent;
    expect(response.status()).toBe(201);
    expect((await response.json()).id).toBe(first[0].id);
    expect(chunks).toEqual([]);
    expect(await stored()).toHaveLength(1);
  } finally {
    for (const item of await stored()) await api.delete(`${target}/${item.id}`, { headers });
  }
});
