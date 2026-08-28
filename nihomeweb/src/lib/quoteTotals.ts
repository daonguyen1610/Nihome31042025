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
  | "percentRange"
  | "amountTooLarge";

// Browser numbers must stay inside their exact-integer range; the API keeps
// the wider decimal(18,*) database guard as the final authority.
export const MAX_QUOTE_MONEY = Number.MAX_SAFE_INTEGER;
export const MAX_QUOTE_QUANTITY = 99_999_999_999_999;

const roundMoney = (value: number) => Math.round(value * 100) / 100;

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
  const subtotal = method === "Boq"
    ? (values.items ?? []).reduce(
        (sum, item) => sum + roundMoney(item.quantity * item.unitPrice),
        0,
      )
    : roundMoney((values.areaSqm ?? 0) * (values.unitPricePerSqm ?? 0));
  const roundedSubtotal = roundMoney(subtotal);
  const discountAmount = roundMoney(
    roundedSubtotal * (values.discountPercent / 100),
  );
  const afterDiscount = roundedSubtotal * (1 - values.discountPercent / 100);
  const vatAmount = roundMoney(afterDiscount * (values.vatPercent / 100));

  return {
    subtotal: roundedSubtotal,
    discountAmount,
    vatAmount,
    grandTotal: roundMoney(afterDiscount + vatAmount),
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

  if (method === "UnitCost") {
    if (!values.areaSqm || !values.unitPricePerSqm) return "unitCostRequired";
    if (!Number.isFinite(values.areaSqm) || !Number.isFinite(values.unitPricePerSqm) ||
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
