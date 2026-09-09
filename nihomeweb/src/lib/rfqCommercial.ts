export interface RfqCommercialPreview {
  subtotal: number;
  freight: number;
  discount: number;
  taxable: number;
  vat: number;
  totalOriginal: number;
  totalVnd: number;
}

const round4 = (value: number) => Math.round((value + Number.EPSILON) * 10_000) / 10_000;

export function calculateRfqCommercialPreview(
  quantities: Record<number, number>,
  prices: Record<number, string>,
  freight: string,
  discountPercent: string,
  discountAmount: string,
  vatPercent: string,
  exchangeRateToVnd: string,
): RfqCommercialPreview {
  const subtotal = round4(Object.entries(prices).reduce((sum, [lineId, rawPrice]) =>
    sum + (quantities[Number(lineId)] ?? 0) * (Number(rawPrice) || 0), 0));
  const freightValue = Number(freight) || 0;
  const percent = Number(discountPercent) || 0;
  const fixedDiscount = Number(discountAmount) || 0;
  const discount = round4(percent > 0 ? subtotal * percent / 100 : fixedDiscount);
  const taxable = round4(Math.max(0, subtotal + freightValue - discount));
  const vat = round4(taxable * (Number(vatPercent) || 0) / 100);
  const totalOriginal = round4(taxable + vat);
  const totalVnd = round4(totalOriginal * (Number(exchangeRateToVnd) || 0));
  return { subtotal, freight: freightValue, discount, taxable, vat, totalOriginal, totalVnd };
}