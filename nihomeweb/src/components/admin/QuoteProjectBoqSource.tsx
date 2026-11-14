import { Link } from "react-router-dom";
import { AlertTriangle, FileSpreadsheet } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import type { QuoteProjectBoq } from "@/services/adminApi";

// A BOQ quote takes its lines from the project's latest approved BOQ; this panel shows which
// revision the quote uses and lets Sales apply a newer one. The page loads `latest`
// (undefined while loading, null when the project has no approved BOQ).
export function QuoteProjectBoqSource({
  latest,
  error,
  projectId,
  currentRevisionNumber,
  selectedRevisionId,
  savedRevisionId,
  editing,
  onApply,
}: {
  latest: QuoteProjectBoq | null | undefined;
  error: string | null;
  projectId?: number | null;
  currentRevisionNumber?: number | null;
  selectedRevisionId?: number | null;
  savedRevisionId?: number | null;
  editing: boolean;
  onApply: (revision: QuoteProjectBoq) => void;
}) {
  const { t } = useI18n();

  const applied = latest != null && selectedRevisionId === latest.revisionId;
  const outdated =
    latest != null && savedRevisionId != null && savedRevisionId !== latest.revisionId;

  return (
    <section
      className="space-y-3 rounded-lg border bg-card p-4 text-sm"
      data-testid="quote-project-boq"
    >
      <div className="flex items-center gap-2 font-medium">
        <FileSpreadsheet className="h-4 w-4 text-primary" />
        {t("quotes.projectBoq.title")}
      </div>
      <p className="text-xs text-muted-foreground">{t("quotes.projectBoq.hint")}</p>
      {savedRevisionId != null && (
        <p>{t("quotes.projectBoq.using", { revision: `R${currentRevisionNumber ?? "?"}` })}</p>
      )}
      {error ? (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      ) : latest === undefined ? (
        <p className="text-muted-foreground">{t("common.loading")}</p>
      ) : latest === null ? (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-amber-900">
          <p>{t("quotes.projectBoq.none")}</p>
          {projectId && (
            <Link
              className="mt-1 inline-block font-medium underline underline-offset-2"
              to={`/admin/operational-projects/${projectId}#project-boq`}
            >
              {t("quotes.projectBoq.openProject")}
            </Link>
          )}
        </div>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3">
          <div className="min-w-0 space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">R{latest.revisionNumber}</span>
              <Badge variant="outline">{t(`procurement.status.${latest.status}`)}</Badge>
              <span className="text-muted-foreground">
                {latest.upstreamContractNumber ?? t("boqWorkbook.tenderStage")}
              </span>
            </div>
            <p className="text-muted-foreground">
              {t("quotes.projectBoq.summary", {
                lines: latest.lineCount,
                total: `${formatVnd(latest.total)} ₫`,
              })}
            </p>
          </div>
          {editing && (
            <Button
              variant={applied ? "secondary" : "default"}
              disabled={applied}
              onClick={() => onApply(latest)}
              data-testid="quote-project-boq-apply"
            >
              {t(applied ? "quotes.projectBoq.applied" : "quotes.projectBoq.apply")}
            </Button>
          )}
        </div>
      )}
      {outdated && (
        <p className="flex items-start gap-2 rounded-md bg-amber-50 p-2 text-xs text-amber-900">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          {t("quotes.projectBoq.outdated", { revision: `R${latest!.revisionNumber}` })}
        </p>
      )}
    </section>
  );
}
