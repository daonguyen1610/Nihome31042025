type Translate = (key: string) => string;

/**
 * A master-data option's name in the user's language: the translation of its label key when
 * there is one, otherwise the name an admin gave it (options added later have no key).
 */
export function masterDataLabel(
  t: Translate,
  option: { labelKey?: string | null; name?: string | null; code?: string | null },
): string {
  if (option.labelKey) {
    const translated = t(option.labelKey);
    if (translated && translated !== option.labelKey) return translated;
  }
  return option.name || option.code || "";
}
