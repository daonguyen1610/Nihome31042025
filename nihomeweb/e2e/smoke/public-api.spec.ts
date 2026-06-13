import { test, expect } from "../fixtures/auth";

test("public API: GET /api/projects returns a list", async ({ api }) => {
  const res = await api.get("/api/projects");
  expect(res.status()).toBe(200);
  const body = await res.json();
  expect(Array.isArray(body)).toBe(true);
});

test("public API: GET /api/services returns a list", async ({ api }) => {
  const res = await api.get("/api/services");
  expect(res.status()).toBe(200);
  expect(Array.isArray(await res.json())).toBe(true);
});

test("public API: GET /api/news returns a list", async ({ api }) => {
  const res = await api.get("/api/news");
  expect(res.status()).toBe(200);
  expect(Array.isArray(await res.json())).toBe(true);
});

test("public API: GET /api/translations/{lang} returns a translation map", async ({ api }) => {
  const res = await api.get("/api/translations/en");
  expect(res.status()).toBe(200);
});

test("public API: GET /api/site-settings/otp-settings exposes OTP toggles", async ({ api }) => {
  const res = await api.get("/api/site-settings/otp-settings");
  expect(res.status()).toBe(200);
});
