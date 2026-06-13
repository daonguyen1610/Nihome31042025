import { test, expect } from "../../fixtures/auth";

test("system health endpoint is reachable", async ({ api }) => {
  const res = await api.get("/api/system/health");
  expect(res.status()).toBe(200);
});

test("upload endpoints reject empty form-data", async ({ api }) => {
  for (const path of ["/api/system/upload-image", "/api/system/upload-video", "/api/system/upload-cv"]) {
    const res = await api.post(path, { multipart: {} });
    expect(res.status(), `${path} with empty multipart`).toBe(400);
  }
});
