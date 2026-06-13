import { test, expect } from "../fixtures/auth";

const languages = ["vi", "en", "zh", "ja"];

for (const lang of languages) {
  test(`translations are available for ${lang}`, async ({ api }) => {
    const res = await api.get(`/api/translations/${lang}`);
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body, `non-empty payload for ${lang}`).toBeTruthy();
  });
}
