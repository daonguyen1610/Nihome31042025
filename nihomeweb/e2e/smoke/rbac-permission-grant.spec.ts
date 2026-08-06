import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

/**
 * End-to-end RBAC permission grant flow tests.
 *
 * Tests the real-world scenario:
 * 1. Create a role with minimal permissions
 * 2. Create a user with that role
 * 3. Verify user is denied access to certain pages
 * 4. Grant additional permissions to the role
 * 5. Re-login and verify user can now access the newly granted pages
 *
 * This validates that permission changes take effect immediately after re-authentication.
 */
test.describe.configure({ mode: "serial" });

const ROLE_CODE = "TESTER_RBAC_GRANT";
const ROLE_NAME = "RBAC Grant Tester";
const TEST_PHONE = "0911000098";
const TEST_PASSWORD = "Admin@123";
const TEST_FULL_NAME = "RBAC Grant Test User";
const TEST_EMAIL = "rbac-grant-tester@e2e.nihome.local";

// Start with minimal permissions - only dashboard access
const INITIAL_PERMISSIONS = ["dashboard.view"];

// Additional permissions to grant later
const ADDITIONAL_PERMISSIONS = [
  "dashboard.view",
  "content.projects.view",
  "content.projects.manage",
  "crm.contracts.view",
];

// Page that should be accessible with content.projects permissions
const PROJECT_PAGE = "/admin/projects";
// Page that should be accessible with crm.contracts permissions
const CONTRACTS_PAGE = "/admin/contracts";

const FORBIDDEN_BADGE = /^403$/;
const FORBIDDEN_BODY = /Access denied|Truy cập bị từ chối|访问被拒绝|アクセスが拒否されました/i;

let createdRoleId: number | null = null;
let createdUserId: number | null = null;
let saToken = "";

test.beforeAll(async ({ api }) => {
  // Cleanup any leftover fixture from a previous failed run
  execSql(`
    DELETE FROM refresh_tokens WHERE UserId IN (SELECT Id FROM users WHERE PhoneNumber = '${TEST_PHONE}');
    DELETE FROM users WHERE PhoneNumber = '${TEST_PHONE}';
    DELETE rp FROM role_permissions rp JOIN roles r ON r.Id = rp.RoleId WHERE r.Code = '${ROLE_CODE}';
    DELETE FROM roles WHERE Code = '${ROLE_CODE}';
  `);

  // Login as SUPER_ADMIN
  const loginRes = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_USERS.superAdmin.phoneNumber, password: TEST_USERS.superAdmin.password },
  });
  expect(loginRes.status(), "SA login").toBe(200);
  saToken = (await loginRes.json()).accessToken as string;

  // Create role with minimal permissions
  const roleRes = await api.post("/api/admin/rbac/roles", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      code: ROLE_CODE,
      name: ROLE_NAME,
      permissions: INITIAL_PERMISSIONS,
    },
  });
  expect(roleRes.status(), `create ${ROLE_CODE}`).toBe(201);
  createdRoleId = (await roleRes.json()).id as number;

  // Create user with the new role
  const userRes = await api.post("/api/users", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      phoneNumber: TEST_PHONE,
      fullName: TEST_FULL_NAME,
      email: TEST_EMAIL,
      password: TEST_PASSWORD,
      role: ROLE_CODE,
    },
  });
  expect(userRes.status(), "create test user").toBe(201);
  createdUserId = (await userRes.json()).id as number;
});

test.afterAll(async ({ api }) => {
  // Cleanup
  if (createdUserId != null) {
    execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${createdUserId};`);
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

test("Step 1: User with minimal permissions is denied on projects and contracts pages", async ({
  page,
  loginInBrowserAs,
}) => {
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_CODE,
  } as unknown as TestUser;

  // User should be able to access dashboard
  await loginInBrowserAs(page, user);
  await page.goto("/admin");
  await expect(page.locator(`text=${FORBIDDEN_BADGE}`).first()).toHaveCount(0);

  // User should be DENIED on projects page
  await loginInBrowserAs(page, user);
  await page.goto(PROJECT_PAGE);
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User should be denied on ${PROJECT_PAGE} before permission grant`,
  ).toBeVisible();

  // User should be DENIED on contracts page
  await loginInBrowserAs(page, user);
  await page.goto(CONTRACTS_PAGE);
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User should be denied on ${CONTRACTS_PAGE} before permission grant`,
  ).toBeVisible();
});

test("Step 2: Grant additional permissions to the role via API", async ({ api }) => {
  expect(createdRoleId, "Role should exist").not.toBeNull();

  // Update role permissions
  const updateRes = await api.put(`/api/admin/rbac/roles/${createdRoleId}/permissions`, {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      permissions: ADDITIONAL_PERMISSIONS,
    },
  });
  expect(updateRes.status(), "Update role permissions").toBe(200);

  // Verify permissions were updated
  const roleRes = await api.get(`/api/admin/rbac/roles/${createdRoleId}`, {
    headers: { Authorization: `Bearer ${saToken}` },
  });
  expect(roleRes.status()).toBe(200);
  const roleBody = await roleRes.json();
  expect(
    new Set(roleBody.permissions as string[]),
    "Role should have updated permissions",
  ).toEqual(new Set(ADDITIONAL_PERMISSIONS));
});

test("Step 3: After re-login, user can now access projects and contracts pages", async ({
  page,
  loginInBrowserAs,
}) => {
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_CODE,
  } as unknown as TestUser;

  // Re-login to get fresh token with updated permissions
  await loginInBrowserAs(page, user);

  // User should now be ALLOWED on projects page
  await page.goto(PROJECT_PAGE);
  await expect(page, `User should reach ${PROJECT_PAGE} after permission grant`).toHaveURL(
    new RegExp(PROJECT_PAGE.replace(/\//g, "\\/") + "(\\?|$)"),
  );
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User should NOT see 403 on ${PROJECT_PAGE} after permission grant`,
  ).toHaveCount(0);
  // Verify page content renders
  await expect(
    page.getByRole("heading", { name: /dự án|project|项目|プロジェクト/i }).first(),
  ).toBeVisible();

  // User should now be ALLOWED on contracts page
  await loginInBrowserAs(page, user);
  await page.goto(CONTRACTS_PAGE);
  await expect(page, `User should reach ${CONTRACTS_PAGE} after permission grant`).toHaveURL(
    new RegExp(CONTRACTS_PAGE.replace(/\//g, "\\/") + "(\\?|$)"),
  );
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User should NOT see 403 on ${CONTRACTS_PAGE} after permission grant`,
  ).toHaveCount(0);
  // Verify page content renders
  await expect(
    page.getByRole("heading", { name: /hợp đồng|contract|合同|契約/i }).first(),
  ).toBeVisible();
});

test("Step 4: Verify /api/users/me/permissions reflects the updated permissions", async ({ api }) => {
  // Login as the test user
  const loginRes = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_PHONE, password: TEST_PASSWORD },
  });
  expect(loginRes.status()).toBe(200);
  const token = (await loginRes.json()).accessToken as string;

  // Check /me/permissions endpoint
  const meRes = await api.get("/api/users/me/permissions", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(meRes.status()).toBe(200);
  const meBody = await meRes.json();

  expect(meBody.role, "Role code should match").toBe(ROLE_CODE);
  expect(meBody.roleId, "Role ID should match").toBe(createdRoleId);

  // Verify all granted permissions are present
  const permissions = new Set(meBody.permissions as string[]);
  for (const perm of ADDITIONAL_PERMISSIONS) {
    expect(permissions.has(perm), `Should have permission: ${perm}`).toBe(true);
  }
});
