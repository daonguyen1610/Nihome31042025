import type { Lang } from "@/lib/i18n";

export type LocalizedNameFields = {
  name?: string | null;
  nameVi?: string | null;
  nameEn?: string | null;
  nameZh?: string | null;
  nameJa?: string | null;
};

export function localizedName(item: LocalizedNameFields, lang: Lang): string {
  if (lang === "en") return item.nameEn || item.nameVi || item.name || "";
  if (lang === "zh") return item.nameZh || item.nameVi || item.name || "";
  if (lang === "ja") return item.nameJa || item.nameVi || item.name || "";
  return item.nameVi || item.name || "";
}
