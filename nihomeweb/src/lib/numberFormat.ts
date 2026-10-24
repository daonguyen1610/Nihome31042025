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

/**
 * Parse a localized monetary amount while preserving a limited decimal part.
 * Both Vietnamese ("85.000,50") and English ("85,000.50") separators are
 * accepted. A single three-digit suffix remains a thousands group, so the
 * established "85.000" currency entry continues to mean 85,000.
 * Localized quantity editors can opt into decimal commas to round-trip
 * three-digit fractions such as "0,001" without interpreting them as groups.
 */
export function parseVndDecimal(
  input: string,
  decimalScale = 2,
  preferDecimalComma = false,
): number {
  const compact = input.trim().replace(/[\s₫đ]/gi, "");
  if (!compact) return 0;
  if (!/^\d+(?:[.,]\d+)*$/.test(compact)) return Number.NaN;

  const dot = compact.lastIndexOf(".");
  const comma = compact.lastIndexOf(",");
  const lastSeparator = Math.max(dot, comma);
  if (lastSeparator < 0) return Number(compact);

  const separator = compact[lastSeparator];
  const occurrences = [...compact].filter((char) => char === separator).length;
  const fractionLength = compact.length - lastSeparator - 1;
  const hasOtherSeparator = dot >= 0 && comma >= 0;
  const isDecimal =
    hasOtherSeparator ||
    (occurrences === 1 &&
      fractionLength > 0 &&
      fractionLength <= decimalScale &&
      (fractionLength !== 3 || (preferDecimalComma && separator === ",")));

  if (isDecimal && fractionLength > decimalScale) return Number.NaN;

  const integerPart = isDecimal ? compact.slice(0, lastSeparator) : compact;
  const fractionPart = isDecimal ? compact.slice(lastSeparator + 1) : "";
  const groupingSeparator = integerPart.includes(".") ? "." : integerPart.includes(",") ? "," : "";
  if (groupingSeparator) {
    const groups = integerPart.split(groupingSeparator);
    if (!/^\d{1,3}$/.test(groups[0]) || groups.slice(1).some((group) => !/^\d{3}$/.test(group))) {
      return Number.NaN;
    }
  }
  const normalized = `${integerPart.replace(/[.,]/g, "")}${fractionPart ? `.${fractionPart}` : ""}`;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}
