import { test, expect } from "../../fixtures/auth";

const languages = ["vi", "en", "zh", "ja"];

test("/api/translations/{lang} returns a payload for all 4 languages", async ({ api }) => {
  for (const lang of languages) {
    const res = await api.get(`/api/translations/${lang}`);
    expect(res.status(), `translations for ${lang}`).toBe(200);
    const body = await res.json();
    expect(body, `non-empty payload for ${lang}`).toBeTruthy();
  }
});
