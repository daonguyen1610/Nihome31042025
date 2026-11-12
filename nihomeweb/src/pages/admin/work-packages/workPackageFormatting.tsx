import { formatMoney, formatNumber } from "@/lib/displaySettings";

/**
 * Input-price offers keep the currency they were quoted in; display converts
 * them to the active currency at the configured exchange rate.
 */
export const money = (value: number, currency?: string | null) => formatMoney(value, currency);

export const formatPriceNumber = (value: number) =>
  formatNumber(value, undefined, { maximumFractionDigits: 4 });

export const newPriceLine = () => ({
  itemCode: "",
  description: "",
  unit: "",
  quantity: 1,
  unitPrice: 0,
  vatPercent: 0,
  sortOrder: 0,
});
