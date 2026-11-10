import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { useUiError } from "@/hooks/useUiError";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { collectLookupPages } from "@/lib/pagination";
import { contractsApi } from "@/services/admin/contractsApi";
import type { ContractResponse } from "@/services/adminApi";

export function CmContractsPanel({ projectId }: { projectId: number }) {
  const { t, lang } = useI18n();
  const uiError = useUiError();
  const [rows, setRows] = useState<ContractResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void collectLookupPages((page) =>
      contractsApi.listContracts({ operationalProjectId: projectId, pageSize: 200, page }),
    )
      .then(({ data }) => {
        if (active) setRows(data.items ?? []);
      })
      .catch((cause) => {
        if (active) setError(uiError(cause, t("construction.cm.contracts.loadError")));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [projectId, reload, t, uiError]);

  if (loading) return <PageLoading />;
  if (error) return <PageError message={error} onRetry={() => setReload((value) => value + 1)} />;

  return (
    <section className="space-y-3" data-testid="cm-contracts-panel">
      <div className="flex flex-wrap items-center justify-end gap-3">
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link to={`/admin/contracts?direction=Upstream&operationalProjectId=${projectId}`}>
              {t("construction.cm.contracts.openUpstreamLedger")}
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link to={`/admin/contracts?direction=Downstream&operationalProjectId=${projectId}`}>
              {t("construction.cm.contracts.openDownstreamLedger")}
            </Link>
          </Button>
        </div>
      </div>
      {rows.length === 0 ? (
        <PageEmpty message={t("construction.cm.contracts.empty")} />
      ) : (
        <div className="overflow-x-auto rounded-xl border bg-card">
          <table className="w-full min-w-[720px] text-sm">
            <thead>
              <tr className="border-b bg-muted/40 text-left">
                <th className="p-3 font-medium">{t("contracts.field.number")}</th>
                <th className="p-3 font-medium">{t("construction.cm.contracts.counterparty")}</th>
                <th className="p-3 font-medium">{t("contracts.field.direction")}</th>
                <th className="p-3 font-medium">{t("contracts.field.status")}</th>
                <th className="p-3 font-medium">{t("contracts.field.signedDate")}</th>
                <th className="p-3 text-right font-medium">{t("contracts.field.value")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${row.direction}-${row.id}`} className="border-b last:border-b-0">
                  <td className="p-3 font-medium">{row.contractNumber}</td>
                  <td className="p-3">
                    {row.direction === "Upstream"
                      ? (row.customerName ?? "—")
                      : (row.vendorName ?? "—")}
                  </td>
                  <td className="p-3">{t(`contracts.direction.${row.direction}`)}</td>
                  <td className="p-3">{t(`contracts.status.${row.status}`)}</td>
                  <td className="p-3 tabular-nums">
                    {row.signedDate
                      ? new Date(`${row.signedDate.slice(0, 10)}T00:00:00Z`).toLocaleDateString(
                          lang,
                          { timeZone: "UTC" },
                        )
                      : "—"}
                  </td>
                  <td className="p-3 text-right tabular-nums">{formatVnd(row.currentValue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
