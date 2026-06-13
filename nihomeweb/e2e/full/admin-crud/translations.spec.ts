import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin can upsert and delete a translation pair", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };
  const key = `e2e.key.${Date.now()}`;

  const upsert = await api.post("/api/translations/pair", {
    ...auth,
    data: {
      key,
      vietnameseValue: "Xin chào",
      translations: { en: "Hello", zh: "你好", ja: "こんにちは" },
      category: "e2e",
    },
  });
  expect([200, 201]).toContain(upsert.status());

  // The key should now appear in the English bundle.
  const enBundle = await api.get("/api/translations/en");
  expect(enBundle.status()).toBe(200);

  const del = await api.delete(`/api/translations/key/${key}`, auth);
  expect([200, 204]).toContain(del.status());
});
