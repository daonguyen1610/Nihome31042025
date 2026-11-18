import type { SearchableSelectOption } from "@/components/ui/searchable-select";

type CustomerLike = { id: number; name: string; taxId?: string | null };

/**
 * Customer picker options with the tax ID under the name, so customers with
 * similar names can be told apart and found by typing their tax ID.
 */
export function customerSelectOptions(
  customers: CustomerLike[],
  taxIdLabel: string,
): SearchableSelectOption[] {
  return customers.map((customer) => ({
    value: String(customer.id),
    label: customer.name,
    hint: customer.taxId ? `${taxIdLabel}: ${customer.taxId}` : undefined,
  }));
}
