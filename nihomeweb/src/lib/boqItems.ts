import type { QuoteItemInput } from "@/services/adminApi";

export function normalizeBoqSortOrder(items: QuoteItemInput[]): QuoteItemInput[] {
  return items.map((item, index) => ({ ...item, sortOrder: index + 1 }));
}
