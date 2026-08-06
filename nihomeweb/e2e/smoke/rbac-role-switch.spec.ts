import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

/**
 * End-to-end RBAC role assignment flow tests.
 *
 * Tests the scenario where an admin changes a user's role and verifies
 * that the user's access rights change accordingly after re-authentication.
 *
 * Flow:
 * 1. Create two roles with different permission sets
 * 2. Create a user with role A (limited access)
 * 3. Verify user can only access role A's allowed pages
 * 4. Change user's role to role B (broader access)
 * 5. Re-login and verify user can now access role B's pages
 */
test.describe.configure({ mode: "serial" });

const ROLE_A_CODE = "ROLE_A_LIMITED";
const ROLE_A_NAME = "Limited Role A";
const ROLE_B_CODE = "ROLE_B_EXTENDED";
const ROLE_B_NAME = "Extended Role B";

const TEST_PHONE = "0911000097";
const TEST_PASSWORD = "Admin@123";
const TEST_FULL_NAME = "Role Switch Test User";
const TEST_EMAIL = "role-switch-tester@e2e.nihome.local";

// Role A: Only dashboard access
const ROLE_A_PERMISSIONS = ["dashboard.view"];

// Role B: Dashboard + CRM access
const ROLE_B_PERMISSIONS = [
  "dashboard.view",
  "crm.leads.view",
  "crm.leads.manage",
  "crm.customers.view",
  "crm.opportunities.view",
];

const LEADS_PAGE = "/admin/leads";
const CUSTOMERS_PAGE = "/admin/customers";

const FORBIDDEN_BADGE = /^403$/;

let roleAId: number | null = null;
let roleBId: number | null = null;
let createdUserId: number | null = null;
let saToken = "";

test.beforeAll(async ({ api }) => {
  // Cleanup any leftover fixture
  execSql(`
    DELETE FROM refresh_tokens WHERE UserId IN (SELECT Id FROM users WHERE PhoneNumber = '${TEST_PHONE}');
    DELETE FROM users WHERE PhoneNumber = '${TEST_PHONE}';
    DELETE rp FROM role_permissions rp JOIN roles r ON r.Id = rp.RoleId WHERE r.Code IN ('${ROLE_A_CODE}', '${ROLE_B_CODE}');
    DELETE FROM roles WHERE Code IN ('${ROLE_A_CODE}', '${ROLE_B_CODE}');
  `);

  // Login as SUPER_ADMIN
  const loginRes = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_USERS.superAdmin.phoneNumber, password: TEST_USERS.superAdmin.password },
  });
  expect(loginRes.status(), "SA login").toBe(200);
  saToken = (await loginRes.json()).accessToken as string;

  // Create Role A (limited)
  const roleARes = await api.post("/api/admin/rbac/roles", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      code: ROLE_A_CODE,
      name: ROLE_A_NAME,
      permissions: ROLE_A_PERMISSIONS,
    },
  });
  expect(roleARes.status(), `create ${ROLE_A_CODE}`).toBe(201);
  roleAId = (await roleARes.json()).id as number;

  // Create Role B (extended)
  const roleBRes = await api.post("/api/admin/rbac/roles", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      code: ROLE_B_CODE,
      name: ROLE_B_NAME,
      permissions: ROLE_B_PERMISSIONS,
    },
  });
  expect(roleBRes.status(), `create ${ROLE_B_CODE}`).toBe(201);
  roleBId = (await roleBRes.json()).id as number;

  // Create user with Role A
  const userRes = await api.post("/api/users", {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      phoneNumber: TEST_PHONE,
      fullName: TEST_FULL_NAME,
      email: TEST_EMAIL,
      password: TEST_PASSWORD,
      role: ROLE_A_CODE,
    },
  });
  expect(userRes.status(), "create test user with Role A").toBe(201);
  const userBody = await userRes.json();
  createdUserId = userBody.id as number;
  expect(userBody.role).toBe(ROLE_A_CODE);
});

test.afterAll(async ({ api }) => {
  // Cleanup
  if (createdUserId != null) {
    execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${createdUserId};`);
    execSql(`DELETE FROM refresh_tokens WHERE UserId = ${createdUserId};`);
    execSql(`DELETE FROM users WHERE Id = ${createdUserId};`);
  }
  if (roleAId != null && saToken) {
    await api.delete(`/api/admin/rbac/roles/${roleAId}`, {
      headers: { Authorization: `Bearer ${saToken}` },
    });
  }
  if (roleBId != null && saToken) {
    await api.delete(`/api/admin/rbac/roles/${roleBId}`, {
      headers: { Authorization: `Bearer ${saToken}` },
    });
  }
});

test("Step 1: User with Role A is denied on CRM pages", async ({ page, loginInBrowserAs }) => {
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_A_CODE,
  } as unknown as TestUser;

  // User should be able to access dashboard
  await loginInBrowserAs(page, user);
  await page.goto("/admin");
  await expect(page.locator(`text=${FORBIDDEN_BADGE}`).first()).toHaveCount(0);

  // User should be DENIED on leads page
  await loginInBrowserAs(page, user);
  await page.goto(LEADS_PAGE);
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User with Role A should be denied on ${LEADS_PAGE}`,
  ).toBeVisible();

  // User should be DENIED on customers page
  await loginInBrowserAs(page, user);
  await page.goto(CUSTOMERS_PAGE);
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User with Role A should be denied on ${CUSTOMERS_PAGE}`,
  ).toBeVisible();
});

test("Step 2: Admin changes user's role from A to B via API", async ({ api }) => {
  expect(createdUserId, "User should exist").not.toBeNull();
  expect(roleBId, "Role B should exist").not.toBeNull();

  // Update user's role to Role B
  const updateRes = await api.put(`/api/users/${createdUserId}`, {
    headers: { Authorization: `Bearer ${saToken}` },
    data: {
      fullName: TEST_FULL_NAME,
      email: TEST_EMAIL,
      role: ROLE_B_CODE,
    },
  });
  expect(updateRes.status(), "Update user role to B").toBe(200);

  // Verify user now has Role B
  const userRes = await api.get(`/api/users/${createdUserId}`, {
    headers: { Authorization: `Bearer ${saToken}` },
  });
  expect(userRes.status()).toBe(200);
  const userBody = await userRes.json();
  expect(userBody.role, "User should now have Role B").toBe(ROLE_B_CODE);
  expect(userBody.roleId, "User roleId should match Role B").toBe(roleBId);
});

test("Step 3: After re-login with new role, user can access CRM pages", async ({
  page,
  loginInBrowserAs,
}) => {
  // Note: User now has Role B, but we still pass the old role code here
  // because loginInBrowserAs just uses credentials, not role for login
  const user = {
    phoneNumber: TEST_PHONE,
    password: TEST_PASSWORD,
    role: ROLE_B_CODE, // Updated role
  } as unknown as TestUser;

  // Re-login to get fresh token with new role
  await loginInBrowserAs(page, user);

  // User should now be ALLOWED on leads page
  await page.goto(LEADS_PAGE);
  await expect(page, `User should reach ${LEADS_PAGE} with Role B`).toHaveURL(
    new RegExp(LEADS_PAGE.replace(/\//g, "\\/") + "(\\?|$)"),
  );
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User should NOT see 403 on ${LEADS_PAGE} with Role B`,
  ).toHaveCount(0);
  // Verify page content renders
  await expect(
    page.getByRole("heading", { name: /lead|tiềm năng|潜在客户|リード/i }).first(),
  ).toBeVisible();

  // User should now be ALLOWED on customers page
  await loginInBrowserAs(page, user);
  await page.goto(CUSTOMERS_PAGE);
  await expect(page, `User should reach ${CUSTOMERS_PAGE} with Role B`).toHaveURL(
    new RegExp(CUSTOMERS_PAGE.replace(/\//g, "\\/") + "(\\?|$)"),
  );
  await expect(
    page.locator(`text=${FORBIDDEN_BADGE}`).first(),
    `User should NOT see 403 on ${CUSTOMERS_PAGE} with Role B`,
  ).toHaveCount(0);
  // Verify page content renders
  await expect(
    page.getByRole("heading", { name: /khách hàng|customer|客户|顧客/i }).first(),
  ).toBeVisible();
});

test("Step 4: Verify login response and /me/permissions reflect the new role", async ({ api }) => {
  // Login as the test user
  const loginRes = await api.post("/api/auth/login", {
    data: { phoneNumber: TEST_PHONE, password: TEST_PASSWORD },
  });
  expect(loginRes.status()).toBe(200);
  const loginBody = await loginRes.json();

  // Login response should show new role
  expect(loginBody.role, "Login response should show Role B").toBe(ROLE_B_CODE);
  expect(loginBody.roleId, "Login response roleId should match Role B").toBe(roleBId);

  const token = loginBody.accessToken as string;

  // Check /me/permissions endpoint
  const meRes = await api.get("/api/users/me/permissions", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(meRes.status()).toBe(200);
  const meBody = await meRes.json();

  expect(meBody.role, "/me/permissions role should be Role B").toBe(ROLE_B_CODE);
  expect(meBody.roleId, "/me/permissions roleId should match Role B").toBe(roleBId);

  // Verify all Role B permissions are present
  const permissions = new Set(meBody.permissions as string[]);
  for (const perm of ROLE_B_PERMISSIONS) {
    expect(permissions.has(perm), `Should have permission: ${perm}`).toBe(true);
  }
});
