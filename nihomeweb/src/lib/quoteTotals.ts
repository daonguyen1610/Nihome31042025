import type { QuoteItemInput, QuoteMethod } from "@/services/adminApi";

export interface QuoteTotals {
  subtotal: number;
  discountAmount: number;
  vatAmount: number;
  grandTotal: number;
}

export type QuoteValidationIssue =
  | "unitCostRequired"
  | "boqRequired"
  | "boqInvalidRow"
  | "boqPrecision"
  | "numericPrecision"
  | "percentRange"
  | "amountTooLarge";

// Browser numbers must stay inside their exact-integer range; the API keeps
// the wider decimal(18,*) database guard as the final authority.
export const MAX_QUOTE_MONEY = Number.MAX_SAFE_INTEGER;
export const MAX_QUOTE_QUANTITY = 99_999_999_999_999;

const toScaledInteger = (value: number, scale: number): bigint => {
  if (!Number.isFinite(value)) return 0n;
  if (Math.abs(value) >= 1e21) {
    return BigInt(Math.trunc(value)) * (10n ** BigInt(scale));
  }
  return BigInt(value.toFixed(scale).replace(".", ""));
};

const divideRoundHalfAwayFromZero = (numerator: bigint, denominator: bigint): bigint =>
  numerator >= 0n
    ? (numerator + denominator / 2n) / denominator
    : -((-numerator + denominator / 2n) / denominator);

const centsToNumber = (cents: bigint): number => Number(cents) / 100;

export const roundQuoteMoney = (value: number): number => centsToNumber(
  divideRoundHalfAwayFromZero(toScaledInteger(value, 4), 100n),
);

const calculateQuoteLineAmountCents = (quantity: number, unitPrice: number): bigint =>
  divideRoundHalfAwayFromZero(
    toScaledInteger(quantity, 4) * toScaledInteger(unitPrice, 2),
    10_000n,
  );

export const calculateQuoteLineAmount = (quantity: number, unitPrice: number): number =>
  centsToNumber(calculateQuoteLineAmountCents(quantity, unitPrice));

const hasScale = (value: number, scale: number): boolean =>
  Number.isFinite(value) && Number(value.toFixed(scale)) === value;

export const calculateQuoteTotals = (
  method: QuoteMethod,
  values: {
    items?: QuoteItemInput[];
    areaSqm?: number | null;
    unitPricePerSqm?: number | null;
    discountPercent: number;
    vatPercent: number;
  },
): QuoteTotals => {
  const subtotalCents = method === "Boq"
    ? (values.items ?? []).reduce((sum, item) => {
        return sum + calculateQuoteLineAmountCents(item.quantity, item.unitPrice);
      }, 0n)
    : divideRoundHalfAwayFromZero(
        toScaledInteger(values.areaSqm ?? 0, 2) * toScaledInteger(values.unitPricePerSqm ?? 0, 2),
        100n,
      );
  const discountBasisPoints = toScaledInteger(values.discountPercent, 2);
  const vatBasisPoints = toScaledInteger(values.vatPercent, 2);
  const discountAmountCents = divideRoundHalfAwayFromZero(
    subtotalCents * discountBasisPoints,
    10_000n,
  );
  const vatAmountCents = divideRoundHalfAwayFromZero(
    subtotalCents * (10_000n - discountBasisPoints) * vatBasisPoints,
    100_000_000n,
  );
  const grandTotalCents = divideRoundHalfAwayFromZero(
    subtotalCents * (10_000n - discountBasisPoints) * (10_000n + vatBasisPoints),
    100_000_000n,
  );

  return {
    subtotal: centsToNumber(subtotalCents),
    discountAmount: centsToNumber(discountAmountCents),
    vatAmount: centsToNumber(vatAmountCents),
    grandTotal: centsToNumber(grandTotalCents),
  };
};

export const validateQuoteValues = (
  method: QuoteMethod,
  values: {
    items?: QuoteItemInput[];
    areaSqm?: number | null;
    unitPricePerSqm?: number | null;
    discountPercent: number;
    vatPercent: number;
  },
): QuoteValidationIssue | null => {
  if (!Number.isFinite(values.discountPercent) || values.discountPercent < 0 || values.discountPercent > 100 ||
      !Number.isFinite(values.vatPercent) || values.vatPercent < 0 || values.vatPercent > 100) {
    return "percentRange";
  }
  if (!hasScale(values.discountPercent, 2) || !hasScale(values.vatPercent, 2)) {
    return "numericPrecision";
  }

  if (method === "UnitCost") {
    if (!Number.isFinite(values.areaSqm) || !Number.isFinite(values.unitPricePerSqm) ||
        (values.areaSqm ?? 0) <= 0 || (values.unitPricePerSqm ?? 0) <= 0) {
      return "unitCostRequired";
    }
    if (!hasScale(values.areaSqm, 2) || !hasScale(values.unitPricePerSqm, 2)) return "numericPrecision";
    if (values.areaSqm > MAX_QUOTE_MONEY || values.unitPricePerSqm > MAX_QUOTE_MONEY ||
        values.areaSqm * values.unitPricePerSqm > MAX_QUOTE_MONEY) {
      return "amountTooLarge";
    }
  } else {
    const items = values.items ?? [];
    if (items.length === 0) return "boqRequired";
    if (items.some((item) => !item.name.trim() || !item.unit.trim() ||
        !Number.isFinite(item.quantity) || !Number.isFinite(item.unitPrice) ||
        item.quantity <= 0 || item.unitPrice < 0)) {
      return "boqInvalidRow";
    }
    if (items.some((item) => !hasScale(item.quantity, 4) || !hasScale(item.unitPrice, 2))) {
      return "boqPrecision";
    }
    if (items.some((item) => item.quantity > MAX_QUOTE_QUANTITY ||
        item.unitPrice > MAX_QUOTE_MONEY ||
        item.quantity * item.unitPrice > MAX_QUOTE_MONEY)) {
      return "amountTooLarge";
    }
  }

  return calculateQuoteTotals(method, values).grandTotal > MAX_QUOTE_MONEY
    ? "amountTooLarge"
    : null;
};
