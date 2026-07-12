/**
 * Locale-aware number formatters shared across admin pages. Kept in one place
 * so VND / percent / date formatting stays consistent — swap once here and
 * every table + card + dialog picks it up.
 */

const vndFormatter = new Intl.NumberFormat("vi-VN");

/** Format an integer VND amount as "1.234.567" (no currency symbol). */
export function formatVnd(value: number | null | undefined): string {
    if (value == null || Number.isNaN(value)) return "—";
    return vndFormatter.format(value);
}

/** Format an integer VND amount as "1.234.567 ₫" for headings / totals. */
export function formatVndWithSymbol(value: number | null | undefined): string {
    if (value == null || Number.isNaN(value)) return "—";
    return `${vndFormatter.format(value)} ₫`;
}

/**
 * Parse a user-typed currency string ("1.234.567", "1,234,567", "1234567 đ",
 * even a raw "150tr" → 150 not 150_000_000; the multiplier suffix is left
 * for a follow-up when the sales team asks for it) into a plain integer.
 *
 * Returns 0 for empty input, or NaN for anything unparseable. Consumers
 * that accept the value into a numeric field should coerce NaN → 0 or
 * flag validation.
 */
export function parseVnd(input: string): number {
    if (!input) return 0;
    const digitsOnly = input.replace(/[^0-9]/g, "");
    if (digitsOnly.length === 0) return 0;
    const parsed = Number(digitsOnly);
    return Number.isFinite(parsed) ? parsed : Number.NaN;
}
