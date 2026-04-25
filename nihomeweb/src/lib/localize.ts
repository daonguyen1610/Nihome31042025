// Multi-language helpers for dynamic data records.
// Each entity may carry an optional `i18n` map keyed by Lang code (excluding "vi"),
// where "vi" is treated as the source/default. Missing translations gracefully
// fall back to the source value.

import type { Lang } from "@/lib/i18n";

export type LocalizedFields<T> = Partial<Record<Exclude<Lang, "vi">, Partial<T>>>;

/**
 * Pick a localized string field from an entity carrying an optional `i18n` map.
 * Falls back to the source field when translation is missing.
 */
export function pickLocalized<
  T extends { i18n?: LocalizedFields<T> },
  K extends keyof T,
>(item: T, key: K, lang: Lang): T[K] {
  if (lang === "vi") return item[key];
  const translated = item.i18n?.[lang]?.[key];
  return (translated ?? item[key]) as T[K];
}

/** Localize an arbitrary string by category translation map. */
export function localizeCategory(
  category: string,
  lang: Lang,
  map: Record<string, Partial<Record<Lang, string>>>
): string {
  if (lang === "vi") return category;
  return map[category]?.[lang] ?? category;
}
