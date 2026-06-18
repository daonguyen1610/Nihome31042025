import { test, expect, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

/**
 * Phase 6 follow-up — covers a freshly-minted business role with a custom
 * permission set, exercising the whole RBAC chain end-to-end:
 *   SUPER_ADMIN → POST /api/admin/rbac/roles (custom permission set)
 *   SUPER_ADMIN → POST /api/users         (USER-enum stub)
 *   SQL        → users.RoleEntityId = new role.Id (no API for this yet)
 *   browser    → login as the new user, prove allow/deny via the SPA guards.
 *
 * The public API does not yet expose business-role assignment when creating a
 * user (POST /api/users only accepts the legacy 3-value UserRole enum), so
 * the wiring step uses a direct SQL UPDATE. Cleanup undoes it.
 */
test.describe.configure({ mode: "serial" });

const ROLE_CODE = "AUDITOR_E2E";
const ROLE_NAME = "Auditor (E2E)";
const TEST_PHONE = "0911000099";
const TEST_PASSWORD = "Admin@123";
const TEST_FULL_NAME = "Custom Role Tester";
const TEST_EMAIL = "custom-role-tester@e2e.nihome.local";

// Permission set picked to be distinguishable from every existing seed role:
//  - dashboard.view: ride the admin shell
//  - content.news.view + content.news.manage: write news (unlike BGD)
//  - system.audit.view: see activity-log (unlike DESIGN)
const ROLE_PERMISSIONS = [
  "dashboard.view",
  "content.news.view",
  "content.news.manage",
  "system.audit.view",
];

const ALLOWED_PATHS = [
  "/admin",
  "/admin/notifications",
  "/admin/posts",
  "/admin/activity-log",
];

const DENIED_PATHS = [
  "/admin/users",
  "/admin/roles",
  "/admin/projects",
  "/admin/contacts",
  "/admin/services",
  "/admin/about",
  "/admin/processes/general",
  "/admin/settings",
  "/admin/email-templates",
  "/admin/translations",
  "/admin/clients",
];

const FORBIDDEN_BADGE = /^403$/;
const FORBIDDEN_BODY = /Access denied|Truy cập bị từ chối|访问被拒绝|アクセスが拒否されました/i;

let createdRoleId: number | null = null;
let createdUserId: number | null = null;
let saToken = "";

test.beforeAll(async ({ api }) => {
  // Best-effort cleanup of any leftover fixture from a previous failed run.
  execSql(`
    DELETE FROM refresh_tokens WHERE UserId IN (SELECT Id FROM users WHERE PhoneNumber = '${TEST_PHONE}');
    DELETE FROM users WHERE PhoneNumber = '${TEST_PHONE}';
    DELETE rp FROM role_permissions rp JOIN roles r ON r.Id = rp.RoleId WHERE r.Code = '${ROLE_CODE}';
    DELETE FROM roles WHERE Code = '${ROLE_CODE}';
  `);

  const loginRes = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_USERS.superAdmin.phoneNumber, password: TEST_USERS.superAdmin.password },
  });
  expect(loginRes.status(), "SA login").toBe(200);
  saToken = (await loginRes.json()).accessToken as string;

  const roleRes = await api.post("/api/admin/rbac/roles", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      code: ROLE_CODE,
      name: ROLE_NAME,
      permissions: ROLE_PERMISSIONS,
    },
  });
  expect(roleRes.status(), "create AUDITOR_E2E").toBe(201);
  createdRoleId = (await roleRes.json()).id as number;

  const userRes = await api.post("/api/users", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      phoneNumber: TEST_PHONE,
      fullName: TEST_FULL_NAME,
      email: TEST_EMAIL,
      password: TEST_PASSWORD,
      role: "USER",
    },
  });
  expect(userRes.status(), "create test user").toBe(201);
  createdUserId = (await userRes.json()).id as number;

  // Wire the user to the new business role. UsersController doesn't accept
  // RoleEntityId yet, so do it directly.
  execSql(`UPDATE users SET RoleEntityId = ${createdRoleId} WHERE Id = ${createdUserId};`);
});

test.afterAll(async ({ api }) => {
  // Detach user from role before deleting role (DELETE /api/admin/rbac/roles/{id}
  // refuses while any user — active or not — still references it).
  if (createdUserId != null) {
    execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${createdUserId};`);
    // Soft delete is fine for the user; integration suite does not depend on
    // this phone number. We also clear refresh tokens so the next run starts clean.
    execSql(`DELETE FROM refresh_tokens WHERE UserId = ${createdUserId};`);
    execSql(`DELETE FROM users WHERE Id = ${createdUserId};`);
  }
  if (createdRoleId != null && saToken) {
    const del = await api.delete(`/api/admin/rbac/roles/${createdRoleId}`, {
      headers: { Authorization: `Bearer ${saToken}` },
    });
    expect([200, 204, 404]).toContain(del.status());
  }
});

test("AUDITOR_E2E /api/users/me/permissions matches the assigned set", async ({ api }) => {
  const login = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_PHONE, password: TEST_PASSWORD },
  });
  expect(login.status()).toBe(200);
  const token = (await login.json()).accessToken as string;

  const me = await api.get("/api/users/me/permissions", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(me.status()).toBe(200);
  const body = await me.json();
  // Server includes profile.me.* implicitly through... actually it doesn't —
  // a custom role only gets what was assigned. So just match the explicit set.
  expect(new Set(body.permissions as string[])).toEqual(new Set(ROLE_PERMISSIONS));
});

test("AUDITOR_E2E can reach allowed admin pages and is denied on the rest", async ({
  page,
  loginInBrowserAs,
}) => {
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_CODE,
  } as const;

  for (const path of ALLOWED_PATHS) {
    await loginInBrowserAs(page, user);
    await page.goto(path);
    await expect(page, `AUDITOR_E2E should reach ${path}`).toHaveURL(
      new RegExp(path.replace(/\//g, "\\/") + "(\\?|$)"),
    );
    await expect(
      page.locator("text=/^403$/"),
      `AUDITOR_E2E on ${path} unexpectedly hit Forbidden`,
    ).toHaveCount(0);
  }

  for (const path of DENIED_PATHS) {
    await loginInBrowserAs(page, user);
    await page.goto(path);
    await expect(
      page.locator(`text=${FORBIDDEN_BADGE}`).first(),
      `AUDITOR_E2E should be denied on ${path}`,
    ).toBeVisible();
    await expect(page.getByText(FORBIDDEN_BODY).first()).toBeVisible();
  }
});
