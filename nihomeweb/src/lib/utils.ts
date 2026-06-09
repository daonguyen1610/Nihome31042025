import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** Lowercase + strip Vietnamese diacritics for accent-insensitive search. */
export function normalizeSearch(value: string | null | undefined): string {
  if (!value) return "";
  return value
    .toString()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

/** True when haystack contains needle (both diacritic-insensitive). Empty needle returns true. */
export function matchesSearch(haystack: string | null | undefined, needle: string): boolean {
  const n = normalizeSearch(needle);
  if (!n) return true;
  return normalizeSearch(haystack).includes(n);
}
