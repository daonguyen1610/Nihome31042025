import type { QuoteApprovalCandidate, QuoteApprovalChainStep } from "./quotes";
export type ContractStatus =
  | "Draft"
  | "UnderReview"
  | "Approved"
  | "Signed"
  | "InProgress"
  | "OnHold"
  | "Completed"
  | "Cancelled";

export const CONTRACT_STATUSES: ContractStatus[] = [
  "Draft",
  "UnderReview",
  "Approved",
  "Signed",
  "InProgress",
  "OnHold",
  "Completed",
  "Cancelled",
];

/** Who a contract can be sent to for internal review (GET /contracts/{id}/review-chain). */
export interface ContractReviewChain {
  steps: QuoteApprovalChainStep[];
  reviewers: QuoteApprovalCandidate[];
}
