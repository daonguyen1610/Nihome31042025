import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin can submit, list, and delete a contact message", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  const submitted = await api.post("/api/contacts", {
    data: {
      name: "E2E Visitor",
      email: `e2e-${Date.now()}@example.com`,
      phone: "0900000001",
      subject: `E2E ${Date.now()}`,
      message: "Hello from e2e suite",
    },
  });
  expect(submitted.status()).toBe(201);
  const item = await submitted.json();

  const fetched = await api.get(`/api/contacts/${item.id}`, auth);
  expect(fetched.status()).toBe(200);

  const list = await api.get("/api/contacts", auth);
  expect(list.status()).toBe(200);

  expect((await api.delete(`/api/contacts/${item.id}`, auth)).status()).toBe(204);
});
