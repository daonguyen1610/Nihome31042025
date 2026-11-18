/**
 * Picker search as staff expect it: every word typed must appear in the
 * option, ignoring case and Vietnamese accents. Unlike fuzzy matching, a tax
 * ID or project code never matches a different record whose characters merely
 * appear in the same order. Free of imports so it can be unit-tested.
 */
export function normalizeSearchText(text: string): string {
  return text
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase();
}

export function matchesAllTerms(text: string, search: string): boolean {
  const haystack = normalizeSearchText(text);
  return normalizeSearchText(search)
    .split(/\s+/)
    .filter(Boolean)
    .every((term) => haystack.includes(term));
}
