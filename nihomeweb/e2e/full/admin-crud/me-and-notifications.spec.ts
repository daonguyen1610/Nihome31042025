import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin can read their own profile and notifications", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const me = await api.get("/api/users/me", auth);
  expect(me.status()).toBe(200);
  const meBody = await me.json();
  expect(meBody.phoneNumber).toBeTruthy();

  const docs = await api.get("/api/users/me/documents", auth);
  expect(docs.status()).toBe(200);

  const list = await api.get("/api/notifications", auth);
  expect(list.status()).toBe(200);

  const unread = await api.get("/api/notifications/unread-count", auth);
  expect(unread.status()).toBe(200);
  const unreadBody = await unread.json();
  expect(typeof unreadBody.count).toBe("number");

  const markAll = await api.post("/api/notifications/mark-all-read", auth);
  expect(markAll.status()).toBe(200);
});
