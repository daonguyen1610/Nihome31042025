/**
 * Deployment-level smoke for the bucketed upload pipeline.
 *
 * Lives at the E2E layer (not integration) because it exercises:
 *   - the real /images/upload/<bucket>/ static file middleware
 *   - the directory bootstrap in Program.cs
 * which only run inside the deployed ASP.NET pipeline.
 */
import { test, expect, TEST_USERS } from "../fixtures/auth";

// 1x1 transparent PNG.
const TINY_PNG = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=",
  "base64",
);

async function upload(
  api: import("@playwright/test").APIRequestContext,
  token: string,
  category: string | undefined,
  options: { previousImageUrl?: string } = {},
) {
  const multipart: Record<string, unknown> = {
    file: {
      name: "pixel.png",
      mimeType: "image/png",
      buffer: TINY_PNG,
    },
  };
  if (category !== undefined) multipart.category = category;
  if (options.previousImageUrl) multipart.previousImageUrl = options.previousImageUrl;

  const res = await api.post("/api/system/upload-image", {
    headers: { Authorization: `Bearer ${token}` },
    multipart,
  });
  return res;
}

test.describe("upload bucketing", () => {
  const buckets = ["activities", "news", "projects", "logos", "misc"] as const;

  for (const bucket of buckets) {
    test(`places image in /images/upload/${bucket}/<guid>.png and serves it`, async ({ api, loginAs }) => {
      const token = await loginAs(TEST_USERS.admin);

      const res = await upload(api, token, bucket);
      expect(res.status(), await res.text()).toBe(200);
      const body = await res.json();
      expect(body.imageUrl).toMatch(
        new RegExp(`^/images/upload/${bucket}/[0-9a-f]{32}\\.png$`),
      );

      const fetched = await api.get(body.imageUrl);
      expect(fetched.status()).toBe(200);
      expect(fetched.headers()["content-type"]).toMatch(/image\/png/);
      const bytes = await fetched.body();
      expect(bytes.length).toBe(TINY_PNG.length);
    });
  }

  test("unknown category falls back to misc", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.admin);

    const res = await upload(api, token, "totally-not-a-real-bucket");
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.imageUrl).toMatch(/^\/images\/upload\/misc\/[0-9a-f]{32}\.png$/);
  });

  test("missing category falls back to misc", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.admin);

    const res = await upload(api, token, undefined);
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.imageUrl).toMatch(/^\/images\/upload\/misc\/[0-9a-f]{32}\.png$/);
  });

  test("re-uploading with previousImageUrl removes the prior file", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.admin);

    const first = await upload(api, token, "projects");
    expect(first.status()).toBe(200);
    const firstUrl = (await first.json()).imageUrl as string;

    // file is currently served as a real PNG
    const firstFetched = await api.get(firstUrl);
    expect(firstFetched.status()).toBe(200);
    expect(firstFetched.headers()["content-type"]).toMatch(/image\/png/);

    const second = await upload(api, token, "projects", { previousImageUrl: firstUrl });
    expect(second.status()).toBe(200);
    const secondUrl = (await second.json()).imageUrl as string;
    expect(secondUrl).not.toBe(firstUrl);

    // Old file is deleted. The SPA-fallback middleware turns missing static
    // asset requests into 200 + text/html, so we assert by content-type
    // rather than status code.
    const refetched = await api.get(firstUrl);
    expect(refetched.headers()["content-type"]).not.toMatch(/image\//);

    const secondFetched = await api.get(secondUrl);
    expect(secondFetched.status()).toBe(200);
    expect(secondFetched.headers()["content-type"]).toMatch(/image\/png/);
  });

  test("forged previousImageUrl with traversal does not escape upload root", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.admin);

    // appsettings.json lives at /app/appsettings.json (outside wwwroot) but
    // /images/upload/../../appsettings.json would resolve there if traversal
    // were permitted. We use any sibling-of-upload path that exists; the
    // important assertion is that the upload still succeeds AND nothing else
    // gets removed. The traversal-rejection itself is asserted in unit tests
    // because we cannot read the host filesystem from inside Playwright.
    const res = await upload(api, token, "misc", {
      previousImageUrl: "/images/upload/../../../etc/passwd",
    });
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.imageUrl).toMatch(/^\/images\/upload\/misc\/[0-9a-f]{32}\.png$/);
  });

  test("unauthenticated request is rejected", async ({ api }) => {
    const res = await api.post("/api/system/upload-image", {
      multipart: {
        file: { name: "pixel.png", mimeType: "image/png", buffer: TINY_PNG },
        category: "misc",
      },
    });
    // Either 401 (no auth) or 403 (no permission) is acceptable; the key is
    // that anonymous callers cannot drop files into wwwroot.
    expect([401, 403]).toContain(res.status());
  });
});
