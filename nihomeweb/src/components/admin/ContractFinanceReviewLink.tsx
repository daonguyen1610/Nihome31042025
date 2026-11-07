import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  adminApi,
  type DepartmentWorkPackageResponse,
  type QuoteResponse,
} from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { selectQuoteFinanceReview } from "@/lib/financeReview";
import { Button } from "@/components/ui/button";

/** Reuse the quotation's existing commercial review; never create another approval cycle. */
export default function ContractFinanceReviewLink({ quoteId }: { quoteId?: number | null }) {
  const { has } = usePermissions();
  const { t } = useI18n();
  const canView = has("work.packages.view");
  const canRequest = has("work.packages.manage");
  const canViewQuote = has("crm.quotes.view");
  const [review, setReview] = useState<DepartmentWorkPackageResponse | null>(null);
  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    if (!quoteId || !canView) return;
    setLoading(true);
    setError("");
    setReview(null);
    setQuote(null);
    try {
      const packages: DepartmentWorkPackageResponse[] = [];
      let page = 1;
      while (true) {
        const response = await adminApi.listWorkPackages({
          sourceQuoteId: quoteId,
          purpose: "FinanceCommercialReview",
          pageSize: 100,
          page,
        });
        packages.push(...response.data.items);
        if (
          page * response.data.pageSize >= response.data.totalCount ||
          !response.data.items.length
        )
          break;
        page++;
      }
      const quoteVersion = packages.find(
        (item) => item.sourceQuoteVersion != null,
      )?.sourceQuoteVersion;
      const selected =
        quoteVersion != null ? (selectQuoteFinanceReview(packages, quoteVersion) ?? null) : null;
      setReview(selected);
      // Without a review the contract links straight to the work-package form, which
      // needs the quote's project and opportunity to prefill the request.
      if (!selected && canRequest && canViewQuote) {
        const response = await adminApi.getQuote(quoteId);
        setQuote(response.data);
      }
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [canRequest, canView, canViewQuote, quoteId]);
  useEffect(() => {
    void load();
  }, [load]);
  if (!quoteId || !canView) return null;
  if (error)
    return (
      <div role="alert" className="space-y-2 text-sm text-destructive">
        <p>{error}</p>
        <Button variant="outline" onClick={() => void load()}>
          {t("common.retry")}
        </Button>
      </div>
    );
  if (loading)
    return (
      <p role="status" className="text-sm text-muted-foreground">
        {t("common.loading")}
      </p>
    );
  if (!review && !canViewQuote) return null;
  const createHref =
    canRequest && quote?.operationalProjectId
      ? `/admin/work-packages?create=1&purpose=FinanceCommercialReview&projectId=${quote.operationalProjectId}&opportunityId=${quote.opportunityId}&quoteId=${quote.id}`
      : null;
  return (
    <Button asChild variant="outline" className="max-w-full whitespace-normal text-left">
      <Link
        data-testid="contract-finance-review-link"
        to={
          review ? `/admin/work-packages/${review.id}` : (createHref ?? `/admin/quotes/${quoteId}`)
        }
      >
        {t(review ? "quotes.action.viewFinanceReview" : "quotes.action.requestFinanceReview")}
      </Link>
    </Button>
  );
}
