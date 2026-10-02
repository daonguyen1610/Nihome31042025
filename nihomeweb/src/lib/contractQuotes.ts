import type { QuoteStatus } from "@/services/adminApi";

/**
 * Quote statuses a customer contract may be raised from or linked to.
 * Mirrors ContractService.ContractReadyQuoteStatuses on the server.
 */
export const CONTRACT_READY_QUOTE_STATUSES: readonly QuoteStatus[] = [
  "Approved",
  "SentToCustomer",
  "CustomerApproved",
];

export const isContractReadyQuote = (status: QuoteStatus): boolean =>
  CONTRACT_READY_QUOTE_STATUSES.includes(status);
