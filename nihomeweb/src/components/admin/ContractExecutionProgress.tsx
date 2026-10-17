import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { RefreshCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { formatVnd, formatVndWithSymbol } from "@/lib/numberFormat";
import { extractApiError } from "@/lib/apiError";
import {
  adminApi,
  type ContractResponse,
  type ContractExecutionProgressResponse,
  type ContractProgressSource,
} from "@/services/adminApi";

export default function ContractExecutionProgress({
  contract,
  onPayment,
}: {
  contract: ContractResponse;
  onPayment: () => void;
}) {
  const { t, lang } = useI18n();
  const [data, setData] = useState<ContractExecutionProgressResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const key = (suffix: string) => t(`contracts.progress.${suffix}`);
  useEffect(() => {
    const controller = new AbortController();
    setData(null);
    setError(null);
    setLoading(true);
    adminApi
      .getContractExecutionProgress(contract.id, controller.signal)
      .then(
        ({ data: result }) => {
          if (!controller.signal.aborted) setData(result);
        },
        (cause: unknown) => {
          if (!controller.signal.aborted) setError(extractApiError(cause));
        },
      )
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [contract, revision]);
  const metric = (label: string, value: number, money = false) => (
    <div key={label} className="flex items-start justify-between gap-3 py-1.5">
      <dt className="text-sm text-muted-foreground">{key(label)}</dt>
      <dd className="shrink-0 text-sm font-semibold tabular-nums">
        {money ? formatVndWithSymbol(value) : formatVnd(value)}
      </dd>
    </div>
  );
  const sources = (items: ContractProgressSource[], type: "work" | "qs", total: number) => (
    <>
      <p className="mt-4 text-xs text-muted-foreground">
        {key("latestSources")} ({formatVnd(items.length)}/{formatVnd(total)})
      </p>
      <ul className="mt-2 space-y-2">
        {items.map((item) => (
          <li key={item.id}>
            <Link
              className="block rounded-md border p-2 text-sm text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              to={
                type === "work"
                  ? `/admin/work-packages/${item.id}`
                  : `/admin/construction/bch/qs?projectId=${contract.operationalProjectId}&contractId=${contract.id}#qs-${item.id}`
              }
            >
              <span className="font-medium">{item.code}</span>
              <span className="mt-0.5 block break-words text-xs text-muted-foreground">
                {item.name}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
  return (
    <section
      className="mt-4 rounded-xl border bg-card p-4 shadow-sm sm:p-5"
      data-testid="contract-execution-progress"
      aria-labelledby="contract-progress-heading"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="contract-progress-heading" className="font-semibold">
            {key("title")}
          </h2>
          <p className="mt-1 max-w-3xl text-sm text-muted-foreground">{key("description")}</p>
        </div>
        <Button
          variant="outline"
          size="sm"
          aria-label={key("refresh")}
          disabled={loading}
          onClick={() => setRevision((value) => value + 1)}
        >
          <RefreshCcw className="mr-2 h-4 w-4" />
          {t("common.refresh")}
        </Button>
      </div>
      <div className="mt-4" aria-live="polite" aria-busy={loading}>
        {loading ? (
          <p className="py-5 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
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
        ) : (
          data && (
            <>
              <div className="grid items-start gap-4 lg:grid-cols-3">
                <article className="min-w-0 rounded-lg border p-4">
                  <h3 className="font-medium">{key("work")}</h3>
                  <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                    {key("workHint")}
                  </p>
                  {data.work == null ? (
                    <p className="mt-3 text-sm text-muted-foreground">{key("restricted")}</p>
                  ) : (
                    <>
                      <dl className="mt-3 divide-y">
                        {metric("packages", data.work.packageCount)}
                        {metric("cancelledPackages", data.work.cancelledPackageCount)}
                        {metric("tasks", data.work.taskCount)}
                        {metric("completed", data.work.completedTaskCount)}
                        {metric("verified", data.work.verifiedTaskCount)}
                        {metric("cancelledTasks", data.work.cancelledTaskCount)}
                      </dl>
                      {data.work.packageCount === 0 ? (
                        <p className="mt-3 text-sm text-muted-foreground">{key("emptyWork")}</p>
                      ) : (
                        sources(data.work.sources, "work", data.work.packageCount)
                      )}
                    </>
                  )}
                </article>
                <article className="min-w-0 rounded-lg border p-4">
                  <h3 className="font-medium">{key("qs")}</h3>
                  <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                    {key("qsHint")}
                  </p>
                  {data.qs == null ? (
                    <p className="mt-3 text-sm text-muted-foreground">{key("restricted")}</p>
                  ) : (
                    <>
                      <dl className="mt-3 divide-y">
                        {metric("dossiers", data.qs.dossierCount)}
                        {metric("activeApproved", data.qs.activeApprovedCount)}
                        {metric("pending", data.qs.pendingReviewCount)}
                      </dl>
                      {data.qs.dossierCount === 0 ? (
                        <p className="mt-3 text-sm text-muted-foreground">{key("emptyQs")}</p>
                      ) : (
                        sources(data.qs.sources, "qs", data.qs.dossierCount)
                      )}
                    </>
                  )}
                </article>
                <article className="min-w-0 rounded-lg border p-4">
                  <h3 className="font-medium">{key("payment")}</h3>
                  <p className="mt-1 text-xs leading-relaxed text-muted-foreground">
                    {key("paymentHint")}
                  </p>
                  <dl className="mt-3 divide-y">
                    {metric("milestones", data.payment.milestoneCount)}
                    {metric("paid", data.payment.paidCount)}
                    {metric("requested", data.payment.requestedCount)}
                    {metric("scheduled", data.payment.scheduledAmount, true)}
                    {metric("paidScheduled", data.payment.paidScheduledAmount, true)}
                  </dl>
                  <Button variant="outline" size="sm" className="mt-4" onClick={onPayment}>
                    {key("viewSchedule")}
                  </Button>
                </article>
              </div>
              <p className="mt-4 border-t pt-3 text-xs text-muted-foreground">
                {key("updated")}{" "}
                {new Intl.DateTimeFormat(lang, {
                  dateStyle: "short",
                  timeStyle: "short",
                  timeZone: "Asia/Ho_Chi_Minh",
                }).format(new Date(data.calculatedAt))}{" "}
                (UTC+7)
              </p>
            </>
          )
        )}
      </div>
    </section>
  );
}
