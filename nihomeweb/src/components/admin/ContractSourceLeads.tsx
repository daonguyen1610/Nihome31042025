import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import {
  adminApi,
  type ContractResponse,
  type ContractSourceLeadResponse,
} from "@/services/adminApi";

export default function ContractSourceLeads({ contract }: { contract: ContractResponse }) {
  const { t, lang } = useI18n();
  const [leads, setLeads] = useState<ContractSourceLeadResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLeads([]);
    setError(null);
    if (contract.opportunityId == null) {
      setLoading(false);
      return () => controller.abort();
    }
    setLoading(true);
    adminApi
      .getContractSourceLeads(contract.id, controller.signal)
      .then(
        ({ data }) => {
          if (!controller.signal.aborted) setLeads(data);
        },
        (err: unknown) => {
          if (!controller.signal.aborted) setError(extractApiError(err));
        },
      )
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [contract, revision]);

  return (
    <section
      className="mt-4 border-t pt-4"
      aria-labelledby="contract-source-leads-title"
      data-testid="contract-source-leads"
    >
      <h3 id="contract-source-leads-title" className="text-sm font-semibold">
        {t("contracts.sourceLeads.title")}
      </h3>
      <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
        {t("contracts.sourceLeads.description")}
      </p>
      <div className="mt-3" aria-live="polite" aria-busy={loading}>
        {contract.opportunityId == null ? (
          <p className="text-sm text-muted-foreground">
            {t("contracts.sourceLeads.noOpportunity")}
          </p>
        ) : loading ? (
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            {t("common.loading")}
          </p>
        ) : error ? (
          <div role="alert">
            <p className="text-sm text-destructive">{error}</p>
            <Button
              variant="outline"
              size="sm"
              className="mt-2"
              onClick={() => setRevision((value) => value + 1)}
            >
              {t("common.retry")}
            </Button>
          </div>
        ) : leads.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t("contracts.sourceLeads.empty")}</p>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2">
            {leads.map((lead) => (
              <li key={lead.id}>
                <Link
                  to={`/admin/leads/${lead.id}`}
                  className="flex h-full items-start justify-between gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <span className="min-w-0">
                    <span className="block break-words text-sm font-medium text-primary">
                      {lead.name}
                    </span>
                    <span className="mt-1 block text-xs text-muted-foreground">
                      {t("contracts.sourceLeads.converted")}{" "}
                      {new Intl.DateTimeFormat(lang, {
                        dateStyle: "medium",
                        timeZone: "Asia/Ho_Chi_Minh",
                      }).format(new Date(lead.convertedAt))}
                    </span>
                  </span>
                  <ArrowUpRight
                    className="h-4 w-4 shrink-0 text-muted-foreground"
                    aria-hidden="true"
                  />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
