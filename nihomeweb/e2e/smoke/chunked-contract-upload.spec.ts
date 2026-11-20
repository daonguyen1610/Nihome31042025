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

// Large contract files go in 8 MB chunks: a dropped chunk is sent again on its own,
// and the server ends up with the whole file.
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
      (response) => response.url().endsWith(`/api/contracts/${contractId}/attachments/from-upload`),
      { timeout: 60_000 },
    );
    await page.getByRole("button", { name: "Tải lên tài liệu" }).click();

    expect((await completed).status()).toBe(201);
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
      (response) => response.url().endsWith(`/api/contracts/${contractId}/attachments/from-upload`),
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
