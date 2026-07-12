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
