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

export const isContractReadyQuote = (quote: { status: QuoteStatus; validUntil: string }): boolean =>
  CONTRACT_READY_QUOTE_STATUSES.includes(quote.status) &&
  (quote.status === "CustomerApproved" || new Date(quote.validUntil).getTime() >= Date.now());
