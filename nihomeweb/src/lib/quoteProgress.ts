/**
 * Where an output quotation stands in NICON's quote flow (user guide 3.0.2):
 * input price from Procurement → output price by Sales → internal approval by
 * the Head of Procurement → Finance review → send to customer → customer
 * decision. Kept free of imports so it can be unit-tested directly.
 */
export const QUOTE_PROGRESS_STEPS = [
  "input",
  "output",
  "internal",
  "finance",
  "send",
  "customer",
] as const;

export type QuoteProgressStep = (typeof QUOTE_PROGRESS_STEPS)[number];

export type QuoteProgress =
  | { kind: "active"; step: QuoteProgressStep; index: number }
  | { kind: "done" }
  | { kind: "stopped" };

export type QuoteProgressInput = {
  status: string;
  grandTotal: number;
  /** Finance has approved the review for this quote version. */
  financeApproved: boolean;
};

const active = (step: QuoteProgressStep): QuoteProgress => ({
  kind: "active",
  step,
  index: QUOTE_PROGRESS_STEPS.indexOf(step),
});

export function getQuoteProgress({
  status,
  grandTotal,
  financeApproved,
}: QuoteProgressInput): QuoteProgress {
  switch (status) {
    case "Draft":
      return active(grandTotal === 0 ? "input" : "output");
    case "PendingApproval":
      return active("internal");
    case "Approved":
      return active(financeApproved ? "send" : "finance");
    case "SentToCustomer":
      return active("customer");
    case "CustomerApproved":
      return { kind: "done" };
    default:
      return { kind: "stopped" };
  }
}
