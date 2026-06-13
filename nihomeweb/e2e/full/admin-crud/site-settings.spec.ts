import { test, expect, TEST_USERS } from "../../fixtures/auth";

test("admin can update and read site settings", async ({ api, loginAs }) => {
  const token = await loginAs(TEST_USERS.admin);
  const auth = { headers: { Authorization: `Bearer ${token}` } };

  // OTP settings round-trip.
  const original = await api.get("/api/site-settings/otp-settings");
  expect(original.status()).toBe(200);
  const originalBody = await original.json();

  const updated = await api.put("/api/site-settings/otp-settings", {
    ...auth,
    data: {
      enableOtpForRegistration: !originalBody.enableOtpForRegistration,
      enableOtpForForgotPassword: !originalBody.enableOtpForForgotPassword,
    },
  });
  expect(updated.status()).toBe(200);

  // Restore.
  const restored = await api.put("/api/site-settings/otp-settings", {
    ...auth,
    data: originalBody,
  });
  expect(restored.status()).toBe(200);

  // Email templates is admin-only.
  const templates = await api.get("/api/site-settings/email-templates", auth);
  expect(templates.status()).toBe(200);
});
