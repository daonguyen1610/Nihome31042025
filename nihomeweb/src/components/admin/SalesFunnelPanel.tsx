import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { adminApi, type SalesFunnelResponse } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { formatDate, formatNumber, todayIsoDate } from "@/lib/displaySettings";
import { InlineWarning } from "@/components/PageState";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

type RangeKey = "30" | "90" | "year";

const shiftIsoDate = (iso: string, days: number) => {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
};

const rangeFor = (key: RangeKey) => {
  const to = todayIsoDate();
  if (key === "year") return { from: `${to.slice(0, 4)}-01-01`, to };
  return { from: shiftIsoDate(to, -(Number(key) - 1)), to };
};

/** Sales funnel on the dashboard: how leads turn into opportunities, wins and signed contracts. */
// One hue that deepens down the funnel: the steps are one series, not five categories.
const FUNNEL_SHADES = [
  "hsl(243 80% 70%)",
  "hsl(244 74% 65%)",
  "hsl(245 68% 60%)",
  "hsl(246 62% 54%)",
  "hsl(247 60% 48%)",
];

export function SalesFunnelPanel() {
  const { t } = useI18n();
  const uiError = useUiError();
  const [range, setRange] = useState<RangeKey>("90");
  const [data, setData] = useState<SalesFunnelResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      setData((await adminApi.getSalesFunnel(rangeFor(range))).data);
    } catch (reason) {
      setError(uiError(reason));
    }
  }, [range, uiError]);

  useEffect(() => {
    void load();
  }, [load]);

  const steps = data
    ? [
        { key: "leads", value: data.leadsCreated, to: "/admin/leads" },
        { key: "converted", value: data.leadsConverted, to: "/admin/leads" },
        { key: "opportunities", value: data.opportunitiesCreated, to: "/admin/opportunities" },
        { key: "won", value: data.opportunitiesWon, to: "/admin/opportunities" },
        { key: "contracts", value: data.contractsSigned, to: "/admin/contracts" },
      ]
    : [];
  const max = Math.max(1, ...steps.map((step) => step.value));
  const rate = (value: number | null) =>
    value === null ? "—" : `${formatNumber(value, undefined, { maximumFractionDigits: 1 })}%`;

  return (
    <section className="rounded-lg border bg-card p-6" aria-labelledby="sales-funnel-title">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="sales-funnel-title" className="text-lg font-semibold">
            {t("dash.funnel.title")}
          </h2>
          {data && (
            <p className="text-sm text-muted-foreground">
              {formatDate(data.from)} – {formatDate(data.to)} ·{" "}
              {t(data.includesAllOwners ? "dash.funnel.scopeAll" : "dash.funnel.scopeOwn")}
            </p>
          )}
        </div>
        <Select value={range} onValueChange={(value) => setRange(value as RangeKey)}>
          <SelectTrigger className="h-9 w-40" aria-label={t("dash.funnel.range")}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="30">{t("dash.funnel.range30")}</SelectItem>
            <SelectItem value="90">{t("dash.funnel.range90")}</SelectItem>
            <SelectItem value="year">{t("dash.funnel.rangeYear")}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {error ? (
        <InlineWarning message={error} onRetry={() => void load()} />
      ) : !data ? (
        <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : (
        <>
          <dl className="mb-5 grid grid-cols-2 gap-3 sm:grid-cols-3">
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">{t("dash.funnel.conversionRate")}</dt>
              <dd className="text-2xl font-semibold tabular-nums">
                {rate(data.leadConversionRate)}
              </dd>
            </div>
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">{t("dash.funnel.winRate")}</dt>
              <dd className="text-2xl font-semibold tabular-nums">{rate(data.winRate)}</dd>
            </div>
            <div className="rounded-md border p-3">
              <dt className="text-xs text-muted-foreground">{t("dash.funnel.open")}</dt>
              <dd className="text-2xl font-semibold tabular-nums">
                {formatNumber(data.opportunitiesOpen)}
              </dd>
            </div>
          </dl>
          <ol className="space-y-2" aria-label={t("dash.funnel.title")}>
            {steps.map((step, index) => (
              <li key={step.key}>
                <Link
                  to={step.to}
                  className="group grid grid-cols-[minmax(0,9rem)_1fr_auto] items-center gap-3 rounded-md py-1 text-sm hover:bg-muted/50 sm:grid-cols-[minmax(0,12rem)_1fr_auto]"
                  title={`${t(`dash.funnel.step.${step.key}`)}: ${formatNumber(step.value)}`}
                >
                  <span className="min-w-0 break-words leading-tight text-muted-foreground">
                    {t(`dash.funnel.step.${step.key}`)}
                  </span>
                  <span className="h-4 overflow-hidden rounded-r bg-muted" aria-hidden>
                    <span
                      className="block h-full rounded-r transition-[width] group-hover:opacity-80"
                      style={{
                        width: `${(step.value / max) * 100}%`,
                        backgroundColor: FUNNEL_SHADES[index % FUNNEL_SHADES.length],
                      }}
                    />
                  </span>
                  <span className="min-w-[3ch] text-right font-medium tabular-nums">
                    {formatNumber(step.value)}
                  </span>
                </Link>
              </li>
            ))}
          </ol>
          <p className="mt-3 text-xs text-muted-foreground">{t("dash.funnel.help")}</p>
        </>
      )}
    </section>
  );
}
