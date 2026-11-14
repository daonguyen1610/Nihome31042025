import { AlertTriangle, FileSpreadsheet } from "lucide-react";
import { Button } from "@/components/ui/button";
import { formatNumber } from "@/lib/displaySettings";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import type { QuoteProjectInvestmentRate } from "@/services/adminApi";

// A unit-cost quote may price from the project's latest approved investment-rate form instead of a
// company catalog. Shown only when the project has one; `selectedRevisionId` is the form the edit uses.
export function QuoteProjectRateSource({
  latest,
  selectedRevisionId,
  savedRevisionId,
  savedRevisionNumber,
  onApply,
  onUseCatalog,
}: {
  latest: QuoteProjectInvestmentRate;
  selectedRevisionId?: number | null;
  savedRevisionId?: number | null;
  savedRevisionNumber?: number | null;
  onApply: (rate: QuoteProjectInvestmentRate) => void;
  onUseCatalog: () => void;
}) {
  const { t } = useI18n();
  const applied = selectedRevisionId != null;
  const outdated =
    applied && selectedRevisionId === savedRevisionId && savedRevisionId !== latest.revisionId;

  return (
    <section
      className="space-y-3 rounded-md border bg-muted/20 p-3 text-sm"
      data-testid="quote-project-rate"
    >
      <div className="flex items-center gap-2 font-medium">
        <FileSpreadsheet className="h-4 w-4 text-primary" />
        {t("quotes.projectRate.title")}
      </div>
      <p className="break-words text-muted-foreground">
        {t("quotes.projectRate.summary", {
          revision: latest.revisionNumber,
          file: latest.sourceFileName,
          rate: formatVnd(latest.ratePerSqm),
          area: formatNumber(latest.convertedAreaSqm, undefined, { maximumFractionDigits: 2 }),
        })}
      </p>
      {applied && (
        <p className="font-medium" data-testid="quote-project-rate-applied">
          {t("quotes.projectRate.applied", {
            revision:
              selectedRevisionId === latest.revisionId
                ? latest.revisionNumber
                : (savedRevisionNumber ?? "?"),
          })}
        </p>
      )}
      {outdated && (
        <p className="flex gap-2 rounded-md bg-amber-50 p-2 text-xs text-amber-900">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          {t("quotes.projectRate.newer", { revision: latest.revisionNumber })}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        {(!applied || outdated) && (
          <Button
            type="button"
            size="sm"
            onClick={() => onApply(latest)}
            data-testid="quote-project-rate-apply"
          >
            {t("quotes.projectRate.apply")}
          </Button>
        )}
        {applied && (
          <Button type="button" size="sm" variant="outline" onClick={onUseCatalog}>
            {t("quotes.projectRate.useCatalog")}
          </Button>
        )}
      </div>
    </section>
  );
}
