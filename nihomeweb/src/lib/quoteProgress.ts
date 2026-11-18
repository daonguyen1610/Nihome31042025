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
  /** `ready`: the step's own work is done and it only waits for the hand-off (e.g. submit). */
  | { kind: "active"; step: QuoteProgressStep; index: number; ready: boolean }
  | { kind: "done" }
  | { kind: "stopped" };

export type QuoteProgressInput = {
  status: string;
  grandTotal: number;
  /** Finance has approved the review for this quote version. */
  financeApproved: boolean;
  /** An approved input price or approved project BOQ is available to price from. */
  priceSourceReady: boolean;
};

const active = (step: QuoteProgressStep, ready = false): QuoteProgress => ({
  kind: "active",
  step,
  index: QUOTE_PROGRESS_STEPS.indexOf(step),
  ready,
});

export function getQuoteProgress({
  status,
  grandTotal,
  financeApproved,
  priceSourceReady,
}: QuoteProgressInput): QuoteProgress {
  switch (status) {
    case "Draft":
      if (grandTotal > 0) return active("output", true);
      return active(priceSourceReady ? "output" : "input");
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
