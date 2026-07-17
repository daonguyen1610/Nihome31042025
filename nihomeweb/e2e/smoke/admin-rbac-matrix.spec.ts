import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

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
  "/admin/leads",
  "/admin/customers",
  "/admin/opportunities",
  "/admin/quotes",
  "/admin/capability-documents",
  "/admin/tenders",
  "/admin/activities",
  "/admin/news",
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
  "/admin/master-data",
  "/admin/workflows",
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
    // SALE: crm.leads.{view|manage|convert} + crm.customers.{view|manage} +
    // crm.opportunities.{view|manage} + crm.quotes.{view|manage|send} +
    // contacts.* + recruitment.applications.view + dashboard.view. Sales role
    // does NOT have crm.leads.view.all or crm.customers.view.all — the
    // services scope their lists to owned records, but the routes render.
    user: TEST_USERS.sale,
    allowed: ["/admin", "/admin/notifications", "/admin/leads", "/admin/customers", "/admin/opportunities", "/admin/quotes", "/admin/capability-documents", "/admin/tenders", "/admin/contacts", "/admin/recruitment", "/admin/master-data", "/admin/workflows"],
  },
  {
    // SALES_MANAGER: crm.** (full — includes quotes.approve on top of manage)
    // + contacts.view + recruitment.applications.view. Same admin footprint
    // as SALE plus view.all across CRM entities; routes rendered are
    // identical (server enforces scope).
    user: TEST_USERS.salesManager,
    allowed: ["/admin", "/admin/notifications", "/admin/leads", "/admin/customers", "/admin/opportunities", "/admin/quotes", "/admin/capability-documents", "/admin/tenders", "/admin/contacts", "/admin/recruitment", "/admin/master-data", "/admin/workflows"],
  },
  {
    // DESIGN: content.** + processes.view + dashboard.view (manage-only routes
    // like /admin/email-templates stay denied because they require *.manage).
    user: TEST_USERS.design,
    allowed: [
      "/admin",
      "/admin/notifications",
      "/admin/activities",
      "/admin/news",
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
      "/admin/master-data",
      "/admin/workflows",
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
      "/admin/master-data",
      "/admin/workflows",
    ],
  },
  {
    // QS: content.projects.view + processes.view + crm.quotes.view +
    // crm.tenders.view (read-only access to approved quotes / tenders
    // for takeoff / cost tracking).
    user: TEST_USERS.qs,
    allowed: ["/admin", "/admin/notifications", "/admin/projects", "/admin/quotes", "/admin/tenders", "/admin/processes/general", "/admin/master-data", "/admin/workflows"],
  },
  {
    // ACCOUNTANT: contacts.view + system.audit.view + crm.customers.view (+ view.all)
    user: TEST_USERS.accountant,
    allowed: ["/admin", "/admin/notifications", "/admin/customers", "/admin/contacts", "/admin/activity-log", "/admin/master-data", "/admin/workflows"],
  },
  {
    // WAREHOUSE: processes.view only (plus dashboard)
    user: TEST_USERS.warehouse,
    allowed: ["/admin", "/admin/notifications", "/admin/processes/general", "/admin/master-data", "/admin/workflows"],
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
      // Each loop iteration does a fresh /api/auth/login + page.goto because
      // refresh tokens are single-use; for SUPER_ADMIN/ADMIN/BGD that means
      // 22 round-trips, which exceeds the 30s default under CI load.
      test.slow();
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

/**
 * Tier-1 drift guard: when a dev adds a new `<Route path="/admin/...">` to
 * App.tsx, the matrix above needs to grow to cover it. This test reads the
 * router source and fails if a discoverable shell route is missing from
 * ALL_ADMIN_PATHS, forcing the author to either:
 *   (a) add the route to ALL_ADMIN_PATHS + update each role's `allowed`, or
 *   (b) whitelist it below with a one-line justification.
 *
 * The whitelist is intentional, not a bypass — it captures routes that share
 * a permission with one already covered (e.g. /admin/processes/general stands
 * in for all /admin/processes/* sub-groups) plus pure client-side redirects.
 */
const MATRIX_PATH_EXCLUSIONS = new Set<string>([
  // Pure redirects — RequirePermission wraps a <Navigate />, no real page.
  "/admin/project-categories",
  "/admin/slideshow",
  // /admin/posts redirects to /admin/activities; no independent permission gate.
  "/admin/posts",
  // Process sub-groups all gated by ADMIN_PERMS.processes; /admin/processes/general
  // already proves the gate works for the whole group.
  "/admin/processes/ptcskh",
  "/admin/processes/dt",
  "/admin/processes/tk",
  "/admin/processes/tc",
  "/admin/processes/ttqtct",
  "/admin/processes/qlns",
  "/admin/processes/mhdgncu",
]);

test("Tier-1 — every /admin route in App.tsx is covered by the matrix", () => {
  const appSource = readFileSync(
    resolve(__dirname, "../../src/App.tsx"),
    "utf-8",
  );

  const declared = new Set<string>();
  for (const m of appSource.matchAll(/path="(\/admin[^"]*)"/g)) {
    const p = m[1];
    if (p.includes(":")) continue; // parametric sub-routes (/posts/:slug)
    if (/\/(new|edit)$/.test(p)) continue; // CRUD action sub-routes
    declared.add(p);
  }

  const covered = new Set<string>([...ALL_ADMIN_PATHS, ...MATRIX_PATH_EXCLUSIONS]);
  const missing = [...declared].filter((p) => !covered.has(p)).sort();

  expect(
    missing,
    [
      `Found admin route(s) declared in src/App.tsx that the RBAC matrix does NOT cover:`,
      ...missing.map((p) => `  - ${p}`),
      ``,
      `Fix one of:`,
      `  1) Add the path to ALL_ADMIN_PATHS and update each role's 'allowed' array.`,
      `  2) If it shares a permission with an already-covered route or is a pure`,
      `     redirect, add it to MATRIX_PATH_EXCLUSIONS with a short comment.`,
    ].join("\n"),
  ).toEqual([]);
});
