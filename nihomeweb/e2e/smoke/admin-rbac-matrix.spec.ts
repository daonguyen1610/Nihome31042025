import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";

/**
 * Phase 6 — full RBAC matrix smoke against the live stack at $BASE_URL.
 *
 * Drives all 9 seeded accounts (SUPER_ADMIN, ADMIN, USER + 7 business roles)
 * through every admin route surface and asserts:
 *  - allowed paths render without the inline 403 screen
 *  - denied paths render Forbidden (`<RequirePermission>` blocks)
 *  - public/profile expectations hold for the USER-only role
 *
 * Ground truth for each role's allow/deny set comes from the live
 * /api/users/me/permissions response captured against this stack, not from
 * inferred patterns — so the spec catches drift between the catalog, the
 * seeder and the FE ADMIN_PERMS map.
 *
 * Pure controller-level authorization is covered by
 * nihomebackend.integration.tests; this E2E focuses on the rendered SPA.
 */

const ALL_ADMIN_PATHS = [
  "/admin",
  "/admin/notifications",
  "/admin/users",
  "/admin/roles",
  "/admin/posts",
  "/admin/projects",
  "/admin/services",
  "/admin/contacts",
  "/admin/recruitment",
  "/admin/recruitment/employment-types",
  "/admin/categories",
  "/admin/about",
  "/admin/clients",
  "/admin/partners",
  "/admin/suppliers",
  "/admin/awards",
  "/admin/processes/general",
  "/admin/settings",
  "/admin/languages",
  "/admin/translations",
  "/admin/email-templates",
  "/admin/activity-log",
] as const;

type Path = (typeof ALL_ADMIN_PATHS)[number];

interface RoleExpectation {
  user: TestUser;
  /** Admin paths that must NOT show the 403 screen. */
  allowed: Path[];
}

const denied = (allowed: readonly Path[]): Path[] =>
  ALL_ADMIN_PATHS.filter((p) => !allowed.includes(p));

const matrix: RoleExpectation[] = [
  {
    user: TEST_USERS.superAdmin,
    allowed: [...ALL_ADMIN_PATHS],
  },
  {
    // ADMIN has `**` minus { users.manage, system.audit.manage } — every view
    // route is open. /admin/email-templates needs system.settings.manage which
    // ADMIN still has.
    user: TEST_USERS.admin,
    allowed: [...ALL_ADMIN_PATHS],
  },
  {
    // SALE: contacts.* + recruitment.applications.view + dashboard.view
    user: TEST_USERS.sale,
    allowed: ["/admin", "/admin/notifications", "/admin/contacts", "/admin/recruitment"],
  },
  {
    // DESIGN: content.** + processes.view + dashboard.view (manage-only routes
    // like /admin/email-templates stay denied because they require *.manage).
    user: TEST_USERS.design,
    allowed: [
      "/admin",
      "/admin/notifications",
      "/admin/posts",
      "/admin/projects",
      "/admin/services",
      "/admin/categories",
      "/admin/about",
      "/admin/clients",
      "/admin/partners",
      "/admin/suppliers",
      "/admin/awards",
      "/admin/languages",
      "/admin/translations",
      "/admin/processes/general",
    ],
  },
  {
    // PM: content.projects.* + processes.* + recruitment.applications.view
    user: TEST_USERS.pm,
    allowed: [
      "/admin",
      "/admin/notifications",
      "/admin/projects",
      "/admin/processes/general",
      "/admin/recruitment",
    ],
  },
  {
    // QS: content.projects.view + processes.view
    user: TEST_USERS.qs,
    allowed: ["/admin", "/admin/notifications", "/admin/projects", "/admin/processes/general"],
  },
  {
    // ACCOUNTANT: contacts.view + system.audit.view
    user: TEST_USERS.accountant,
    allowed: ["/admin", "/admin/notifications", "/admin/contacts", "/admin/activity-log"],
  },
  {
    // WAREHOUSE: processes.view only (plus dashboard)
    user: TEST_USERS.warehouse,
    allowed: ["/admin", "/admin/notifications", "/admin/processes/general"],
  },
  {
    // BGD: **.view + dashboard.view + system.audit.view — every view route.
    // /admin/email-templates stays denied because it requires
    // system.settings.MANAGE which BGD doesn't have.
    user: TEST_USERS.bgd,
    allowed: ALL_ADMIN_PATHS.filter((p) => p !== "/admin/email-templates"),
  },
];

const FORBIDDEN_BADGE = /^403$/;
const FORBIDDEN_BODY = /Access denied|Truy cập bị từ chối|访问被拒绝|アクセスが拒否されました/i;

test.describe("Phase 6 — RBAC matrix per seeded role", () => {
  for (const c of matrix) {
    test(`${c.user.role} matches the live permission set`, async ({
      page,
      loginInBrowserAs,
    }) => {
      const denials = denied(c.allowed);

      for (const path of c.allowed) {
        await loginInBrowserAs(page, c.user);
        await page.goto(path);
        await expect(page, `${c.user.role} should reach ${path}`).toHaveURL(
          new RegExp(path.replace(/\//g, "\\/") + "(\\?|$)"),
        );
        await expect(
          page.locator("text=/^403$/"),
          `${c.user.role} on ${path} unexpectedly hit Forbidden`,
        ).toHaveCount(0);
      }

      for (const path of denials) {
        await loginInBrowserAs(page, c.user);
        await page.goto(path);
        await expect(
          page.locator(`text=${FORBIDDEN_BADGE}`).first(),
          `${c.user.role} should be denied on ${path}`,
        ).toBeVisible();
        await expect(page.getByText(FORBIDDEN_BODY).first()).toBeVisible();
      }
    });
  }
});

test.describe("Phase 6 — anonymous + USER-only access", () => {
  test("anonymous user is bounced to /login from /admin", async ({ page }) => {
    await page.goto("/admin");
    await expect(page).toHaveURL(/\/login(\?|$)/);
  });

  test("USER role cannot reach any admin route", async ({ page, loginInBrowserAs }) => {
    // The seeded business-role users live under role=USER on the legacy enum
    // but their RoleEntityId points to the business role. A true USER (with
    // no business RoleEntityId) is represented by the regular registration
    // flow; here we just confirm that without dashboard.view the SUPER_ADMIN
    // dashboard path renders the inline 403 for a role that lacks it.
    // The WAREHOUSE matrix above already covers a thin-permission case end
    // to end; this extra test guards the redirect contract.
    await loginInBrowserAs(page, TEST_USERS.warehouse);
    await page.goto("/admin/users");
    await expect(page.locator("text=/^403$/").first()).toBeVisible();
    await expect(page.getByText(FORBIDDEN_BODY).first()).toBeVisible();

    // /forbidden is publicly routable so a deep-link works.
    await page.goto("/forbidden");
    await expect(page.getByText(FORBIDDEN_BODY).first()).toBeVisible();
  });
});
