import { test, expect } from "../fixtures/auth";

test("contact form submission stores a message", async ({ api }) => {
  const payload = {
    name: "E2E Tester",
    email: `e2e-${Date.now()}@nihome.test`,
    phone: "0123456789",
    subject: "Smoke test",
    message: "This is an automated E2E smoke test message.",
  };

  const res = await api.post("/api/contacts", { data: payload });
  expect([200, 201]).toContain(res.status());
});
