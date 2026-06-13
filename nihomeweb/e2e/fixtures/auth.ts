import { test as base, expect, request, APIRequestContext, Page } from "@playwright/test";

/**
 * Known seeded accounts. Must match nihomebackend Data/DbSeeder.cs.
 * The full docker-compose stack runs the production seeder, so these credentials exist.
 */
export const TEST_USERS = {
  superAdmin: { phoneNumber: "0335240370", password: "Admin@123", role: "SUPER_ADMIN" },
  admin: { phoneNumber: "0911111111", password: "Admin@123", role: "ADMIN" },
} as const;

export type TestUser = (typeof TEST_USERS)[keyof typeof TEST_USERS];

export interface AuthFixtures {
  api: APIRequestContext;
  loginAs: (user: TestUser) => Promise<string>;
  loginInBrowserAs: (page: Page, user: TestUser) => Promise<void>;
}

export const test = base.extend<AuthFixtures>({
  api: async ({ playwright, baseURL }, use) => {
    const ctx = await request.newContext({ baseURL });
    await use(ctx);
    await ctx.dispose();
  },

  loginAs: async ({ api }, use) => {
    await use(async (user) => {
      const res = await api.post("/api/auth/login", {
        data: { phoneNumber: user.phoneNumber, password: user.password },
      });
      expect(res.status(), `login as ${user.role}`).toBe(200);
      const body = await res.json();
      return body.accessToken as string;
    });
  },

  loginInBrowserAs: async ({ loginAs }, use) => {
    await use(async (page, user) => {
      const token = await loginAs(user);
      // App stores auth in Redux + localStorage; seed both before navigation.
      await page.addInitScript(
        ({ token, phone, role }) => {
          window.localStorage.setItem(
            "nihome.auth",
            JSON.stringify({ accessToken: token, phoneNumber: phone, role }),
          );
        },
        { token, phone: user.phoneNumber, role: user.role },
      );
    });
  },
});

export { expect };
