import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Security-critical E2E for the /api/users/me/permissions endpoint exposed
 * by the PermissionService. Runs against the live docker compose stack so
 * the full JWT + EF + RBAC seed pipeline is exercised end-to-end.
 *
 * What this catches that integration tests cannot:
 *   - The deployed seeder actually ran with the shipped JSON defaults.
 *   - JWT issued by the real container is accepted by the real container.
 *   - Permission set returned to the browser matches what the matrix UI
 *     (added in C7) will rely on.
 */

async function fetchPermissions(api: import("@playwright/test").APIRequestContext, token: string) {
  const res = await api.get("/api/users/me/permissions", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(res.status()).toBe(200);
  return (await res.json()) as { role: string; roleId: number | null; permissions: string[] };
}

test("unauthenticated request is rejected", async ({ api }) => {
  const res = await api.get("/api/users/me/permissions");
  expect(res.status()).toBe(401);
});

test("SUPER_ADMIN receives the full catalog", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const body = await fetchPermissions(api, token);

  expect(body.role).toBe("SUPER_ADMIN");
  expect(body.permissions).toEqual(expect.arrayContaining([
    "dashboard.view",
    "users.view",
    "users.manage",
    "rbac.roles.view",
    "rbac.roles.manage",
    "profile.me.view",
    "profile.me.update",
  ]));
});

test("ADMIN can manage roles but cannot manage users (no privilege escalation)", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const body = await fetchPermissions(api, token);

  expect(body.role).toBe("ADMIN");
  expect(body.permissions).toContain("rbac.roles.manage");
  expect(body.permissions).toContain("users.view");
  expect(body.permissions).not.toContain("users.manage");
});

test("permissions response shape is stable for FE consumers", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const body = await fetchPermissions(api, token);

  expect(typeof body.role).toBe("string");
  expect(Array.isArray(body.permissions)).toBe(true);
  body.permissions.forEach((code) => {
    expect(typeof code).toBe("string");
    expect(code).toMatch(/^[a-z][a-z0-9_.]*\.[a-z][a-z0-9_]*$/i);
  });
});
