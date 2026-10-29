type FinancePackage = {
  status: string;
  financeReview?: { reviewedQuoteVersion: number | null; status: string } | null;
};

/** Use the source quotation's existing version-aware Finance action. */
export function selectQuoteFinanceReview<T extends FinancePackage>(
  packages: T[],
  quoteVersion: number,
): T | undefined {
  return packages.find(
    (item) =>
      item.status !== "Cancelled" &&
      (item.financeReview?.reviewedQuoteVersion === quoteVersion ||
        (item.status !== "Completed" &&
          (!item.financeReview ||
            item.financeReview.status === "Draft" ||
            item.financeReview.status === "Rejected"))),
  );
}
