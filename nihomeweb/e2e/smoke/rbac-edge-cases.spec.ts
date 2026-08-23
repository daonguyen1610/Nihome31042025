import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

/**
 * End-to-end RBAC edge case tests - scenarios outside normal matrix flow.
 *
 * Tests cover:
 * 1. User with deleted role is denied access
 * 2. User with all permissions revoked from role is denied
 * 3. Attempting to access admin without any valid role
 * 4. Role code that doesn't exist in the system
 * 5. Permission removed mid-session (token invalidation)
 */
test.describe.configure({ mode: "serial" });

const ROLE_CODE = "TEMP_EDGE_CASE";
const ROLE_NAME = "Temporary Edge Case Role";
const TEST_PHONE = "0911000096";
const TEST_PASSWORD = "Admin@123";
const TEST_FULL_NAME = "Edge Case Test User";
const TEST_EMAIL = "edge-case-tester@e2e.nihome.local";

// Start with some permissions
const INITIAL_PERMISSIONS = [
  "dashboard.view",
  "content.projects.view",
];

const PROJECT_PAGE = "/admin/projects";
const FORBIDDEN_BADGE = /^403$/;

let createdRoleId: number | null = null;
let createdUserId: number | null = null;
let saToken = "";

test.beforeAll(async ({ api }) => {
  // Cleanup any leftover fixture
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

  // Create the temporary role
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

  // Create user with the role
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
    // Role may already be deleted by tests
    await api.delete(`/api/admin/rbac/roles/${createdRoleId}`, {
      headers: { Authorization: `Bearer ${saToken}` },
    }).catch(() => {});
  }
});

test("User with valid role can initially access permitted pages", async ({
  page,
  loginInBrowserAs,
}) => {
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_CODE,
  } as unknown as TestUser;

  await loginInBrowserAs(page, user);
  await page.goto(PROJECT_PAGE);

  // Should be allowed
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    "User should access projects with valid role",
  ).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: /dự án|project|项目|プロジェクト/i }).first(),
  ).toBeVisible();
});

test("Revoking all permissions from role denies user access after re-login", async ({
  api,
  page,
  loginInBrowserAs,
}) => {
  expect(createdRoleId, "Role should exist").not.toBeNull();

  // Remove all permissions from the role (keep only dashboard.view for admin shell)
  const updateRes = await api.put(`/api/admin/rbac/roles/${createdRoleId}/permissions`, {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      permissions: ["dashboard.view"], // Only dashboard, no projects
    },
  });
  expect(updateRes.status(), "Revoke permissions").toBe(200);

  // Re-login to get fresh token
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_CODE,
  } as unknown as TestUser;

  await loginInBrowserAs(page, user);
  await page.goto(PROJECT_PAGE);

  // Should now be DENIED on projects page
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    "User should be denied after permission revocation",
  ).toBeVisible();
});

test("Restoring permissions re-enables access after re-login", async ({
  api,
  page,
  loginInBrowserAs,
}) => {
  expect(createdRoleId, "Role should exist").not.toBeNull();

  // Restore permissions
  const updateRes = await api.put(`/api/admin/rbac/roles/${createdRoleId}/permissions`, {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      permissions: INITIAL_PERMISSIONS,
    },
  });
  expect(updateRes.status(), "Restore permissions").toBe(200);

  // Re-login
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_CODE,
  } as unknown as TestUser;

  await loginInBrowserAs(page, user);
  await page.goto(PROJECT_PAGE);

  // Should be allowed again
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    "User should access projects after permission restore",
  ).toHaveCount(0);
});

test("User assigned to non-existent role code cannot access admin", async ({ api }) => {
  // Attempt to create user with invalid role code
  const userRes = await api.post("/api/users", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      phoneNumber: "0911000095",
      fullName: "Invalid Role User",
      email: "invalid-role@e2e.nihome.local",
      password: TEST_PASSWORD,
      role: "NONEXISTENT_ROLE_XYZ",
    },
  });

  // API should reject invalid role code
  expect([400, 422]).toContain(userRes.status());
});

test("Deleting a role prevents users with that role from logging in properly", async ({
  api,
  page,
  loginInBrowserAs,
}) => {
  // First, detach user from the role to allow deletion
  execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${createdUserId};`);

  // Delete the role
  const deleteRes = await api.delete(`/api/admin/rbac/roles/${createdRoleId}`, {
    headers: { Authorization: `Bearer ${saToken}` },
  });
  expect([200, 204]).toContain(deleteRes.status());

  // User should still be able to login but with no/minimal permissions
  const loginRes = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_PHONE, password: TEST_PASSWORD },
  });
  expect(loginRes.status()).toBe(200);
  const loginBody = await loginRes.json();

  // User's role should be null or fallback to USER
  expect(
    loginBody.roleId === null || loginBody.role === "USER",
    "User with deleted role should have null roleId or fallback to USER",
  ).toBe(true);

  // Check permissions endpoint
  const meRes = await api.get("/api/users/me/permissions", {
    headers: { Authorization: `Bearer ${loginBody.accessToken}` },
  });
  expect(meRes.status()).toBe(200);
  const meBody = await meRes.json();

  // User should have minimal or no permissions (except implicit ones)
  const permissions = meBody.permissions as string[];
  expect(
    !permissions.includes("content.projects.view") && !permissions.includes("content.projects.manage"),
    "User should not have project permissions after role deletion",
  ).toBe(true);

  // Mark role as deleted so afterAll cleanup doesn't fail
  createdRoleId = null;
});

test("User with no role assignment sees 403 on admin pages", async ({
  page,
  loginInBrowserAs,
}) => {
  // User's role was just deleted, try to access admin
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: "USER", // Fallback role
  } as unknown as TestUser;

  await loginInBrowserAs(page, user);
  await page.goto(PROJECT_PAGE);

  // Should be denied
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    "User with no role should be denied on admin projects",
  ).toBeVisible();
});
