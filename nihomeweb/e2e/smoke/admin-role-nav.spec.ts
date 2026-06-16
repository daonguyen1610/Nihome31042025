import { test, expect, TEST_USERS, type TestUser } from "../fixtures/auth";

/**
 * Phase 5 — per business-role smoke for the admin nav filter + route guard.
 * Asserts that each seeded business-role user can reach their permitted admin
 * pages and that direct navigation to a denied page renders the inline
 * Forbidden (403) screen.
 *
 * We assert reachability (URL + lack of 403) rather than nav-label text so
 * the spec is insensitive to copy changes; the unit tests already cover the
 * <Can> / <RequirePermission> rendering logic.
 */

interface RoleExpectation {
  user: TestUser;
  allowedPaths: string[]; // direct navigation must NOT hit Forbidden
  deniedPaths: string[];  // direct navigation MUST render the Forbidden page
}

const cases: RoleExpectation[] = [
  {
    user: TEST_USERS.sale,
    allowedPaths: ["/admin", "/admin/contacts", "/admin/recruitment"],
    deniedPaths: ["/admin/users", "/admin/roles", "/admin/activity-log"],
  },
  {
    user: TEST_USERS.accountant,
    allowedPaths: ["/admin", "/admin/contacts", "/admin/activity-log"],
    deniedPaths: ["/admin/users", "/admin/roles", "/admin/posts"],
  },
  {
    user: TEST_USERS.bgd,
    // BGD has **.view in rbac-defaults — every admin view route is reachable;
    // we still assert representative pages render without 403.
    allowedPaths: ["/admin", "/admin/posts", "/admin/projects", "/admin/activity-log", "/admin/roles"],
    deniedPaths: [],
  },
  {
    user: TEST_USERS.warehouse,
    allowedPaths: ["/admin", "/admin/processes/general"],
    deniedPaths: ["/admin/users", "/admin/contacts", "/admin/posts"],
  },
];

test.describe("Phase 5 — admin route guard per business role", () => {
  for (const c of cases) {
    test(`${c.user.role} can reach allowed pages and is denied on the rest`, async ({
      page,
      loginInBrowserAs,
    }) => {
      // Re-login before each nav: refresh tokens are single-use, so a fresh
      // page.goto using stale cookies would otherwise log the user out.
      for (const path of c.allowedPaths) {
        await loginInBrowserAs(page, c.user);
        await page.goto(path);
        await expect(page).toHaveURL(new RegExp(path.replace(/\//g, "\\/") + "(\\?|$)"));
        await expect(page.locator("text=/^403$/")).toHaveCount(0);
      }

      for (const path of c.deniedPaths) {
        await loginInBrowserAs(page, c.user);
        await page.goto(path);
        await expect(page.locator("text=/^403$/").first()).toBeVisible();
        await expect(
          page.getByText(/Access denied|Truy cập bị từ chối/i).first(),
        ).toBeVisible();
      }
    });
  }
});

