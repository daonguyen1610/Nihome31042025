import { test as base, expect, request, APIRequestContext, Page } from "@playwright/test";

/**
 * Known seeded accounts. Must match nihomebackend Data/DbSeeder.cs.
 * The full docker-compose stack runs the production seeder, so these credentials exist.
 */
export const TEST_USERS = {
  superAdmin: { phoneNumber: "0335240370", password: "Admin@123", role: "SUPER_ADMIN" },
  admin: { phoneNumber: "0911111111", password: "Admin@123", role: "ADMIN" },
  sale: { phoneNumber: "0911000003", password: "Admin@123", role: "SALE" },
  design: { phoneNumber: "0911000004", password: "Admin@123", role: "DESIGN" },
  pm: { phoneNumber: "0911000005", password: "Admin@123", role: "PM" },
  qs: { phoneNumber: "0911000006", password: "Admin@123", role: "QS" },
  accountant: { phoneNumber: "0911000007", password: "Admin@123", role: "ACCOUNTANT" },
  warehouse: { phoneNumber: "0911000008", password: "Admin@123", role: "WAREHOUSE" },
  bgd: { phoneNumber: "0911000009", password: "Admin@123", role: "BGD" },
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

  loginInBrowserAs: async ({ api, baseURL }, use) => {
    await use(async (page, user) => {
      // Hit the real login endpoint so we get matching access + refresh tokens.
      const res = await api.post("/api/auth/login", {
        data: { phoneNumber: user.phoneNumber, password: user.password },
      });
      expect(res.status(), `browser login as ${user.role}`).toBe(200);
      const body = await res.json();
      // authSlice reads these cookies on store init (lib/auth + store/authSlice).
      const url = new URL(baseURL!);
      await page.context().addCookies([
        {
          name: "nicon_access_token",
          value: body.accessToken,
          domain: url.hostname,
          path: "/",
          sameSite: "Strict",
        },
        {
          name: "nicon_refresh_token",
          value: body.refreshToken,
          domain: url.hostname,
          path: "/",
          sameSite: "Strict",
        },
      ]);
    });
  },
});

export { expect };
