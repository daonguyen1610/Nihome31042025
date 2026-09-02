/**
 * Locale-aware number formatters shared across admin pages. Kept in one place
 * so VND / percent / date formatting stays consistent — swap once here and
 * every table + card + dialog picks it up.
 */

const vndFormatter = new Intl.NumberFormat("vi-VN");

function formatDecimalString(value: string): string | null {
    const match = /^(-?)(\d+)(?:\.(\d+))?$/.exec(value.trim());
    if (!match) return null;

    const [, sign, integerDigits, fractionDigits = ""] = match;
    let integer = BigInt(integerDigits);
    let fraction = fractionDigits.slice(0, 3);
    if (fractionDigits.length > 3 && fractionDigits[3] >= "5") {
        const rounded = BigInt(fraction || "0") + 1n;
        if (rounded === 1000n) {
            integer += 1n;
            fraction = "";
        } else {
            fraction = rounded.toString().padStart(3, "0");
        }
    }
    fraction = fraction.replace(/0+$/, "");
    const formattedInteger = vndFormatter.format(sign ? -integer : integer);
    return fraction ? `${formattedInteger},${fraction}` : formattedInteger;
}

/** Format a VND amount without losing decimal-string precision. */
export function formatVnd(value: number | string | null | undefined): string {
    if (value == null || (typeof value === "number" && Number.isNaN(value))) return "—";
    if (typeof value === "string") return formatDecimalString(value) ?? "—";
    return vndFormatter.format(value);
}

/** Format an integer VND amount as "1.234.567 ₫" for headings / totals. */
export function formatVndWithSymbol(value: number | string | null | undefined): string {
    const formatted = formatVnd(value);
    return formatted === "—" ? formatted : `${formatted} ₫`;
}

export function formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
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
