import { test, expect, TEST_USERS } from "../fixtures/auth";
import type { APIRequestContext } from "@playwright/test";

// E2E for /api/admin/rbac/* — verifies the runtime matrix is reachable
// through the deployed stack with real JWT auth and that the security
// guardrails (SUPER_ADMIN immunity, anti-escalation) hold end-to-end.

async function authed(api: APIRequestContext, token: string) {
  return {
    get: (path: string) => api.get(path, { headers: { Authorization: `Bearer ${token}` } }),
    put: (path: string, data: unknown) =>
      api.put(path, { headers: { Authorization: `Bearer ${token}` }, data }),
  };
}

test("SUPER_ADMIN can list roles and permissions catalog", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const c = await authed(api, token);

  const roles = await c.get("/api/admin/rbac/roles");
  expect(roles.status()).toBe(200);
  const rolesBody = await roles.json() as Array<{ code: string; isSystem: boolean }>;
  expect(rolesBody.map(r => r.code)).toEqual(expect.arrayContaining(["SUPER_ADMIN", "ADMIN", "USER"]));

  const perms = await c.get("/api/admin/rbac/permissions");
  expect(perms.status()).toBe(200);
  const permsBody = await perms.json() as Array<{ code: string }>;
  expect(permsBody.map(p => p.code)).toEqual(expect.arrayContaining([
    "dashboard.view", "users.manage", "rbac.roles.manage",
  ]));
});

test("SUPER_ADMIN role itself is immune to permission edits (403)", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const c = await authed(api, token);

  const roles = await c.get("/api/admin/rbac/roles").then(r => r.json()) as Array<{ id: number; code: string }>;
  const sa = roles.find(r => r.code === "SUPER_ADMIN")!;

  const res = await c.put(`/api/admin/rbac/roles/${sa.id}/permissions`, {
    permissions: ["dashboard.view"],
  });
  expect(res.status()).toBe(403);
});

test("ADMIN and USER role matrices are immune (prevents self-lockout)", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const c = await authed(api, token);

  const roles = await c.get("/api/admin/rbac/roles").then(r => r.json()) as Array<{ id: number; code: string }>;
  for (const code of ["ADMIN", "USER"]) {
    const sys = roles.find(r => r.code === code)!;
    const res = await c.put(`/api/admin/rbac/roles/${sys.id}/permissions`, {
      permissions: ["dashboard.view"],
    });
    expect(res.status(), `${code} matrix should be immune`).toBe(403);
    const body = await res.json();
    expect(body.error).toBe("system_role_immutable");
  }
});

test("ADMIN cannot escalate privileges via the matrix (403)", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const c = await authed(api, token);

  const roles = await c.get("/api/admin/rbac/roles").then(r => r.json()) as Array<{ id: number; code: string; isSystem: boolean }>;
  const target = roles.find(r => !r.isSystem)!;

  const res = await c.put(`/api/admin/rbac/roles/${target.id}/permissions`, {
    permissions: ["dashboard.view", "users.manage"],
  });
  expect(res.status()).toBe(403);
  const body = await res.json();
  expect(body.error).toBe("privilege_escalation_blocked");
  expect(body.offending).toContain("users.manage");
});

test("Unknown permission codes return 400 with offending list", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.superAdmin);
  const c = await authed(api, token);

  const roles = await c.get("/api/admin/rbac/roles").then(r => r.json()) as Array<{ id: number; code: string; isSystem: boolean }>;
  const target = roles.find(r => !r.isSystem)!;

  const res = await c.put(`/api/admin/rbac/roles/${target.id}/permissions`, {
    permissions: ["dashboard.view", "does.not.exist"],
  });
  expect(res.status()).toBe(400);
  const body = await res.json();
  expect(body.error).toBe("unknown_permission_codes");
  expect(body.offending).toContain("does.not.exist");
});
