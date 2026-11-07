export const money = (value: number, currency: string) =>
  `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value)} ${currency}`;

export const formatPriceNumber = (value: number) =>
  new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 4 }).format(value);

export const newPriceLine = () => ({
  itemCode: "",
  description: "",
  unit: "",
  quantity: 1,
  unitPrice: 0,
  vatPercent: 0,
  sortOrder: 0,
});
