import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin can read audit logs and config", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const list = await api.get("/api/audit-logs", auth);
  expect(list.status()).toBe(200);

  const config = await api.get("/api/audit-logs/config", auth);
  expect(config.status()).toBe(200);
});
