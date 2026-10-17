import { useEffect, useState } from "react";
import { Cell, Pie, PieChart, ResponsiveContainer } from "recharts";
import { AlertCircle, ArrowDown, RefreshCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { extractApiError } from "@/lib/apiError";
import {
  adminApi,
  type TenderStatisticsParams,
  type TenderStatisticsResponse,
} from "@/services/adminApi";

const currentYear = () =>
  new Intl.DateTimeFormat("en", { timeZone: "Asia/Ho_Chi_Minh", year: "numeric" }).format(
    new Date(),
  );

interface Props {
  onInspect: (
    params: TenderStatisticsParams | null,
    customer?: { id: number; name: string },
  ) => void;
  refreshToken: number;
}

export default function TenderStatistics({ onInspect, refreshToken }: Props) {
  const { t, lang } = useI18n();
  const [draft, setDraft] = useState({
    fromDate: `${currentYear()}-01-01`,
    toDate: `${currentYear()}-12-31`,
    customerId: "",
    preparerUserId: "",
  });
  const [filters, setFilters] = useState<TenderStatisticsParams>({
    fromDate: draft.fromDate,
    toDate: draft.toDate,
  });
  const [data, setData] = useState<TenderStatisticsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const [preparers, setPreparers] = useState<{ id: number; fullName: string }[]>([]);
  const [customers, setCustomers] = useState<{ id: number; name: string }[]>([]);
  const [optionsError, setOptionsError] = useState(false);

  useEffect(() => {
    let active = true;
    setOptionsError(false);
    Promise.all([adminApi.getTenderReportPreparers(), adminApi.getTenderReportCustomers()]).then(
      ([people, clients]) => {
        if (active) {
          setPreparers(people.data);
          setCustomers(clients.data);
        }
      },
      () => {
        if (active) setOptionsError(true);
      },
    );
    return () => {
      active = false;
    };
  }, [revision, refreshToken]);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setData(null);
    setError(null);
    adminApi
      .getTenderStatistics(filters, controller.signal)
      .then(
        ({ data: result }) => {
          if (!controller.signal.aborted) setData(result);
        },
        (err: unknown) => {
          if (!controller.signal.aborted) setError(extractApiError(err));
        },
      )
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [filters, revision, refreshToken]);

  const invalidDates = !draft.fromDate || !draft.toDate || draft.fromDate > draft.toDate;
  const dirty =
    draft.fromDate !== filters.fromDate ||
    draft.toDate !== filters.toDate ||
    draft.customerId !== String(filters.customerId ?? "") ||
    draft.preparerUserId !== String(filters.preparerUserId ?? "");
  const segments = data
    ? [
        { key: "Won", value: data.wonCount, color: "#059669" },
        { key: "Lost", value: data.lostCount, color: "#e11d48" },
        { key: "Submitted", value: data.pendingCount, color: "#0284c7" },
        { key: "Cancelled", value: data.cancelledCount, color: "#71717a" },
      ]
    : [];
  const rate =
    data?.winRate == null
      ? "—"
      : `${new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }).format(data.winRate)}%`;

  return (
    <section
      className="rounded-xl border bg-card p-4 shadow-sm sm:p-6"
      aria-labelledby="tender-statistics-title"
      data-testid="tender-statistics"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="tender-statistics-title" className="text-lg font-semibold">
            {t("tenders.stats.title")}
          </h2>
          <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
            {t("tenders.stats.explanation")}
          </p>
        </div>
        <Button
          variant="ghost"
          size="sm"
          aria-label={t("tenders.stats.refresh")}
          disabled={loading}
          onClick={() => setRevision((v) => v + 1)}
        >
          <RefreshCcw className="mr-2 h-4 w-4" />
          {t("common.refresh")}
        </Button>
      </div>
      <form
        className="mt-5 grid items-end gap-3 sm:grid-cols-2 xl:grid-cols-[1fr_1fr_1.3fr_1.3fr_auto]"
        onSubmit={(event) => {
          event.preventDefault();
          if (invalidDates) return;
          onInspect(null);
          setFilters({
            fromDate: draft.fromDate,
            toDate: draft.toDate,
            customerId: draft.customerId ? Number(draft.customerId) : undefined,
            preparerUserId: draft.preparerUserId ? Number(draft.preparerUserId) : undefined,
          });
        }}
      >
        <div className="space-y-2">
          <Label htmlFor="tender-report-from">{t("tenders.stats.from")}</Label>
          <Input
            id="tender-report-from"
            type="date"
            required
            min="0001-01-02"
            max={draft.toDate || "9999-12-31"}
            value={draft.fromDate}
            onChange={(e) => setDraft({ ...draft, fromDate: e.target.value })}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="tender-report-to">{t("tenders.stats.to")}</Label>
          <Input
            id="tender-report-to"
            type="date"
            required
            min={draft.fromDate || "0001-01-02"}
            max="9999-12-31"
            value={draft.toDate}
            onChange={(e) => setDraft({ ...draft, toDate: e.target.value })}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("tenders.field.customer")}</Label>
          <SearchableSelect
            ariaLabel={t("tenders.field.customer")}
            value={draft.customerId}
            onChange={(customerId) => setDraft({ ...draft, customerId })}
            options={[
              { value: "", label: t("tenders.filter.allCustomers") },
              ...customers.map((c) => ({ value: String(c.id), label: c.name })),
            ]}
            placeholder={t("tenders.filter.allCustomers")}
          />
        </div>
        <div className="space-y-2">
          <Label>{t("tenders.field.preparer")}</Label>
          <SearchableSelect
            ariaLabel={t("tenders.field.preparer")}
            value={draft.preparerUserId}
            onChange={(preparerUserId) => setDraft({ ...draft, preparerUserId })}
            options={[
              { value: "", label: t("tenders.stats.allPreparers") },
              ...preparers.map((p) => ({ value: String(p.id), label: p.fullName })),
            ]}
            placeholder={t("tenders.stats.allPreparers")}
          />
        </div>
        <Button type="submit" disabled={invalidDates || loading}>
          {t("tenders.stats.apply")}
        </Button>
      </form>
      {invalidDates && (
        <p className="mt-2 text-sm text-destructive" role="alert">
          {t("tenders.stats.invalidDates")}
        </p>
      )}
      {dirty && !invalidDates && (
        <p className="mt-2 text-sm text-muted-foreground">{t("tenders.stats.unapplied")}</p>
      )}
      {optionsError && (
        <p className="mt-2 text-sm text-destructive" role="alert">
          {t("tenders.stats.optionsError")}
        </p>
      )}
      <div aria-live="polite" aria-busy={loading} className="mt-5">
        {loading ? (
          <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
        ) : error ? (
          <div role="alert" className="rounded-lg border border-destructive/30 p-4">
            <p className="text-sm text-destructive">{error}</p>
            <Button
              className="mt-3"
              variant="outline"
              size="sm"
              onClick={() => setRevision((v) => v + 1)}
            >
              {t("common.retry")}
            </Button>
          </div>
        ) : (
          data && (
            <>
              {data.submittedCount === 0 ? (
                <p className="rounded-lg bg-muted/40 p-6 text-center text-sm text-muted-foreground">
                  {t("tenders.stats.empty")}
                </p>
              ) : (
                <div className="grid items-center gap-6 md:grid-cols-[220px_1fr]">
                  <div className="relative mx-auto h-48 w-48">
                    <div aria-hidden="true" className="h-full w-full">
                      <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                          <Pie
                            data={segments.filter((s) => s.value > 0)}
                            dataKey="value"
                            innerRadius={68}
                            outerRadius={88}
                            strokeWidth={2}
                            isAnimationActive={false}
                          >
                            {segments
                              .filter((s) => s.value > 0)
                              .map((s) => (
                                <Cell key={s.key} fill={s.color} />
                              ))}
                          </Pie>
                        </PieChart>
                      </ResponsiveContainer>
                    </div>
                    <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
                      <strong className="text-3xl tracking-tight" data-testid="tender-win-rate">
                        {rate}
                      </strong>
                      <span className="mt-1 max-w-28 text-xs text-muted-foreground">
                        {t("tenders.stats.rate")}
                      </span>
                    </div>
                  </div>
                  <div>
                    <div className="flex flex-wrap items-baseline justify-between gap-2 border-b pb-3">
                      <h3 className="font-medium">{t("tenders.stats.total")}</h3>
                      <strong
                        className="text-2xl tabular-nums"
                        data-testid="tender-submitted-count"
                      >
                        {formatVnd(data.submittedCount)}
                      </strong>
                    </div>
                    <dl className="mt-3 grid grid-cols-2 gap-x-5 gap-y-4">
                      {segments.map((s) => (
                        <div key={s.key}>
                          <dt className="flex items-center gap-2 text-sm text-muted-foreground">
                            <span
                              aria-hidden="true"
                              className="h-2.5 w-2.5 shrink-0 rounded-full"
                              style={{ backgroundColor: s.color }}
                            />
                            {t(`tenders.status.${s.key}`)}
                          </dt>
                          <dd className="mt-1 pl-4 text-xl font-semibold tabular-nums">
                            {formatVnd(s.value)}
                          </dd>
                        </div>
                      ))}
                    </dl>
                    <Button
                      variant="outline"
                      size="sm"
                      className="mt-4"
                      disabled={dirty}
                      onClick={() =>
                        onInspect(
                          filters,
                          customers.find((item) => item.id === filters.customerId),
                        )
                      }
                    >
                      <ArrowDown className="mr-2 h-4 w-4" />
                      {t("tenders.stats.inspect")}
                    </Button>
                  </div>
                </div>
              )}
              {data.missingSubmissionCount > 0 && (
                <p
                  className="mt-4 flex items-start gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-950"
                  role="status"
                >
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>
                    {t("tenders.stats.missing")}{" "}
                    <strong>{formatVnd(data.missingSubmissionCount)}</strong>
                  </span>
                </p>
              )}
              <p className="mt-4 border-t pt-3 text-xs leading-relaxed text-muted-foreground">
                {t("tenders.stats.currentResults")} {t("tenders.stats.updated")}{" "}
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
