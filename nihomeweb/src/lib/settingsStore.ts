// Language list persistence for the /admin/languages page. Localstorage-only
// (there is no backend endpoint for admin language management yet) — the
// consuming page treats it as a demo persistence layer.
//
// The rest of the old settings store (General / Media / Email accounts) was
// removed as unused mock code; keep this file focused on Languages so a
// future migration to a real API is a single-file change.

const read = <T,>(key: string, fallback: T): T => {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
};

const write = <T,>(key: string, value: T) => {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    window.dispatchEvent(new CustomEvent(`${key}:changed`));
  } catch {
    /* ignore */
  }
};

export type Language = {
  id: string;
  name: string;
  flag: string; // emoji or short code
  culture: string;
  displayOrder: number;
  published: boolean;
};

const LANG_KEY = "nicon_admin_languages_v1";
const langSeed: Language[] = [
  { id: "lg-1", name: "English", flag: "🇺🇸", culture: "en-US", displayOrder: 0, published: true },
  { id: "lg-2", name: "Viet Nam", flag: "🇻🇳", culture: "vi-VN", displayOrder: 1, published: true },
  { id: "lg-3", name: "China", flag: "🇨🇳", culture: "zh-CN", displayOrder: 2, published: true },
  { id: "lg-4", name: "Korea", flag: "🇰🇷", culture: "ko-KR", displayOrder: 3, published: true },
  { id: "lg-5", name: "Japan", flag: "🇯🇵", culture: "ja-JP", displayOrder: 4, published: true },
];
export const getLanguages = () => read(LANG_KEY, langSeed);
export const saveLanguages = (rows: Language[]) => write(LANG_KEY, rows);

export const newId = () => `id-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
