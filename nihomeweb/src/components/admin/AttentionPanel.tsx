import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ArrowRight } from "lucide-react";
import { ListSkeleton, PageError } from "@/components/PageState";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/displaySettings";
import { cn } from "@/lib/utils";
import { myWorkApi, type MyWorkItem } from "@/services/admin/myWorkApi";

const SHOWN = 5;
const urgency = (item: MyWorkItem) =>
  (item.isOverdue ? 1000 + (item.overdueDays ?? 0) : 0) +
  (item.isPriority ? 500 : 0) +
  (item.isDueSoon ? 100 : 0);

/** What is waiting for this person, most urgent first: the first thing the dashboard says. */
export function AttentionPanel() {
  const { t, lang } = useI18n();
  const query = useQuery({
    queryKey: ["dashboard-my-work"],
    queryFn: async () => (await myWorkApi.get()).data,
  });
  const open = (query.data?.items ?? []).filter((item) => item.isOpen);
  const urgent = [...open].sort((a, b) => urgency(b) - urgency(a));
  const counts = [
    {
      label: t("dash.attention.overdue"),
      value: open.filter((item) => item.isOverdue).length,
      tone: "text-danger-strong",
      surface: "border-danger/30 bg-danger-soft",
    },
    {
      label: t("dash.attention.dueSoon"),
      value: open.filter((item) => item.isDueSoon && !item.isOverdue).length,
      tone: "text-warning-strong",
      surface: "border-warning/40 bg-warning-soft",
    },
    { label: t("dash.attention.open"), value: open.length, tone: "text-foreground", surface: "" },
  ];

  return (
    <section className="rounded-lg border bg-card" aria-labelledby="dash-attention-title">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
        <h2 id="dash-attention-title" className="text-lg font-semibold">
          {t("dash.attention.title")}
        </h2>
        <Link
          to="/admin/my-work"
          className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline pointer-coarse:min-h-11"
        >
          {t("dash.attention.all")} <ArrowRight className="h-4 w-4" aria-hidden />
        </Link>
      </div>
      {query.isLoading ? (
        <div className="p-4">
          <ListSkeleton />
        </div>
      ) : query.isError ? (
        <div className="p-4">
          <PageError message={t("myWork.loadError")} onRetry={() => void query.refetch()} />
        </div>
      ) : (
        <>
          <dl className="grid grid-cols-3 gap-2 border-b p-3 sm:gap-3 sm:p-4">
            {counts.map((count) => (
              // Colour only when something waits, so a quiet day stays quiet.
              <div
                key={count.label}
                className={cn(
                  "flex flex-col-reverse justify-end rounded-md border p-3 sm:p-4",
                  count.value > 0 && count.surface,
                )}
              >
                <dt className="text-sm text-foreground/75">{count.label}</dt>
                <dd
                  className={cn("text-3xl font-bold tabular-nums", count.value > 0 && count.tone)}
                >
                  {count.value}
                </dd>
              </div>
            ))}
          </dl>
          {urgent.length === 0 ? (
            <p className="p-4 text-sm text-muted-foreground">{t("dash.attention.none")}</p>
          ) : (
            <ul className="divide-y">
              {urgent.slice(0, SHOWN).map((item) => (
                <li key={`${item.source}-${item.id}`}>
                  <Link
                    to={item.linkUrl}
                    className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 transition-colors hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                  >
                    <span className="min-w-0 flex-1 basis-60">
                      <span className="block text-sm font-medium">{item.title}</span>
                      <span className="block text-xs text-muted-foreground">
                        {[item.code, item.containerCode, item.containerName]
                          .filter(Boolean)
                          .join(" · ")}
                      </span>
                    </span>
                    {item.isOverdue ? (
                      <span className="status-pill chip-danger">
                        {t("myWork.overdueDays", { days: item.overdueDays ?? 0 })}
                      </span>
                    ) : item.plannedEnd ? (
                      <span
                        className={cn(
                          "status-pill",
                          item.isDueSoon ? "chip-warning" : "chip-muted",
                        )}
                      >
                        {t("myWork.due", {
                          date: formatDate(item.plannedEnd, lang, { timeZone: "UTC" }),
                        })}
                      </span>
                    ) : null}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}
