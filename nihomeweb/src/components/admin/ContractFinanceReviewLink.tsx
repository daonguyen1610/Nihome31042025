import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { adminApi, type DepartmentWorkPackageResponse } from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";

/** Reuse the quotation's existing commercial review; never create another approval cycle. */
export default function ContractFinanceReviewLink({ quoteId }: { quoteId?: number | null }) {
  const { has } = usePermissions();
  const { t } = useI18n();
  const canView = has("work.packages.view");
  const [review, setReview] = useState<DepartmentWorkPackageResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    if (!quoteId || !canView) return;
    setLoading(true);
    setError("");
    try {
      const response = await adminApi.listWorkPackages({
        sourceQuoteId: quoteId,
        purpose: "FinanceCommercialReview",
        pageSize: 100,
      });
      setReview(response.data.items.find((item) => item.status !== "Cancelled") ?? null);
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [canView, quoteId]);
  useEffect(() => { void load(); }, [load]);
  if (!quoteId || !canView) return null;
  if (error) return (
    <div role="alert" className="space-y-2 text-sm text-destructive">
      <p>{error}</p>
      <Button variant="outline" onClick={() => void load()}>{t("common.retry")}</Button>
    </div>
  );
  if (loading) return <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (!review && !has("crm.quotes.view")) return null;
  return (
    <Button asChild variant="outline" className="max-w-full whitespace-normal text-left">
      <Link data-testid="contract-finance-review-link" to={review ? `/admin/work-packages/${review.id}` : `/admin/quotes/${quoteId}`}>
        {t(review ? "quotes.action.viewFinanceReview" : "quotes.action.requestFinanceReview")}
      </Link>
    </Button>
  );
}
