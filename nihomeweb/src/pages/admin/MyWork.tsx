import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Loader2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/displaySettings";
import { extractApiError } from "@/lib/apiError";
import { myWorkApi, type MyWorkItem, type MyWorkResponse } from "@/services/admin/myWorkApi";

type Period = "day" | "week" | "month" | "year";
type Relation = "all" | "Assigned" | "Created";

const iso = (date: Date) => date.toISOString().slice(0, 10);

/** The calendar day, ISO week (Monday first), month or year that contains `day`. */
function periodRange(period: Period, day: string): { from: string; to: string } {
  const date = new Date(`${day}T00:00:00Z`);
  const y = date.getUTCFullYear();
  const m = date.getUTCMonth();
  if (period === "year") return { from: `${y}-01-01`, to: `${y}-12-31` };
  if (period === "month")
    return { from: iso(new Date(Date.UTC(y, m, 1))), to: iso(new Date(Date.UTC(y, m + 1, 0))) };
  if (period === "week") {
    const monday = new Date(Date.UTC(y, m, date.getUTCDate() - ((date.getUTCDay() + 6) % 7)));
    const sunday = new Date(monday);
    sunday.setUTCDate(monday.getUTCDate() + 6);
    return { from: iso(monday), to: iso(sunday) };
  }
  return { from: day, to: day };
}

function useMyWork(range?: { from: string; to: string }) {
  const { t } = useI18n();
  const [data, setData] = useState<MyWorkResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const from = range?.from;
  const to = range?.to;
  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    myWorkApi
      .get(from && to ? { from, to } : {})
      .then((response) => setData(response.data))
      .catch((err) => setError(extractApiError(err, t("myWork.loadError"), t)))
      .finally(() => setLoading(false));
  }, [from, to, t]);
  useEffect(load, [load]);
  return { data, error, loading, reload: load };
}

function WorkRow({ item }: { item: MyWorkItem }) {
  const { t, lang } = useI18n();
  const statusKey =
    item.source === "WorkTask"
      ? `workPackages.taskStatus.${item.status}`
      : `designProjects.schedule.status.${item.status}`;
  return (
    <li>
      <Link
        to={item.linkUrl}
        className="block rounded-md px-3 py-3 transition hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline">{item.code}</Badge>
          <span className="font-medium">{item.title}</span>
          {item.isPriority && <Badge variant="destructive">{t("myWork.priorityBadge")}</Badge>}
          {item.isOverdue && (
            <Badge className="border-transparent bg-warning-soft text-warning-strong">
              {t("myWork.overdueDays", { days: item.overdueDays ?? 0 })}
            </Badge>
          )}
        </div>
        <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
          <span>
            {t(item.source === "WorkTask" ? "myWork.source.WorkTask" : "myWork.source.DesignTask")}
            {": "}
            {item.isInternal
              ? `${t("workPackages.kind.Internal")} · ${item.containerName ?? ""}`
              : [item.containerCode, item.containerName].filter(Boolean).join(" · ")}
          </span>
          <span>
            {t(statusKey)} · {item.progressPercent}%
          </span>
          {item.plannedEnd && (
            <span>
              {t("myWork.due", { date: formatDate(item.plannedEnd, lang, { timeZone: "UTC" }) })}
            </span>
          )}
          {item.relation === "Created" && (
            <span>{t("myWork.assignedTo", { name: item.assigneeName ?? "" })}</span>
          )}
        </div>
      </Link>
    </li>
  );
}

function WorkSection({ title, help, items }: { title: string; help: string; items: MyWorkItem[] }) {
  const { t } = useI18n();
  return (
    <section className="rounded-lg border bg-card p-4">
      <h2 className="text-lg font-semibold">
        {title} <span className="text-sm font-normal text-muted-foreground">({items.length})</span>
      </h2>
      <p className="text-sm text-muted-foreground">{help}</p>
      {items.length === 0 ? (
        <p className="mt-3 text-sm text-muted-foreground">{t("myWork.sectionEmpty")}</p>
      ) : (
        <ul className="mt-2 divide-y">
          {items.map((item) => (
            <WorkRow key={`${item.source}-${item.id}`} item={item} />
          ))}
        </ul>
      )}
    </section>
  );
}

function LoadState({
  loading,
  error,
  reload,
}: {
  loading: boolean;
  error: string | null;
  reload: () => void;
}) {
  const { t } = useI18n();
  if (loading)
    return (
      <p className="flex items-center gap-2 py-10 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        {t("myWork.loading")}
      </p>
    );
  return (
    <div role="alert" className="space-y-2 py-10">
      <p className="text-sm text-destructive">{error}</p>
      <Button type="button" variant="outline" onClick={reload}>
        {t("common.retry")}
      </Button>
    </div>
  );
}

export default function MyWork() {
  const { t } = useI18n();
  const [relation, setRelation] = useState<Relation>("all");
  const [period, setPeriod] = useState<Period>("week");
  const [day, setDay] = useState(() => iso(new Date()));
  const attention = useMyWork();
  const plan = useMyWork(periodRange(period, day));
  const pick = (items: MyWorkItem[] = []) =>
    items.filter((item) => relation === "all" || item.relation === relation);
  const open = pick(attention.data?.items);

  return (
    <AdminLayout>
      <div className="space-y-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("myWork.title")}</h1>
            <p className="mt-1 max-w-prose text-sm text-muted-foreground">{t("myWork.subtitle")}</p>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <div className="space-y-1">
              <Label htmlFor="my-work-relation">{t("myWork.relation.label")}</Label>
              <Select value={relation} onValueChange={(value) => setRelation(value as Relation)}>
                <SelectTrigger id="my-work-relation" className="w-52">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("myWork.relation.all")}</SelectItem>
                  <SelectItem value="Assigned">{t("myWork.relation.Assigned")}</SelectItem>
                  <SelectItem value="Created">{t("myWork.relation.Created")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <Button asChild variant="outline">
              <Link to="/admin/daily-reports">{t("myWork.openDailyReport")}</Link>
            </Button>
          </div>
        </div>

        <Tabs defaultValue="attention">
          <TabsList>
            <TabsTrigger value="attention">{t("myWork.tab.attention")}</TabsTrigger>
            <TabsTrigger value="plan">{t("myWork.tab.plan")}</TabsTrigger>
          </TabsList>

          <TabsContent value="attention" className="space-y-4">
            {!attention.data ? (
              <LoadState {...attention} />
            ) : (
              <div className="grid gap-4 lg:grid-cols-2">
                <WorkSection
                  title={t("myWork.group.overdue")}
                  help={t("myWork.group.overdueHelp")}
                  items={open.filter((item) => item.isOverdue)}
                />
                <WorkSection
                  title={t("myWork.group.today")}
                  help={t("myWork.group.todayHelp")}
                  items={open.filter((item) => item.isToday)}
                />
                <WorkSection
                  title={t("myWork.group.dueSoon")}
                  help={t("myWork.group.dueSoonHelp", { days: attention.data.dueSoonDays })}
                  items={open.filter((item) => item.isDueSoon)}
                />
                <WorkSection
                  title={t("myWork.group.priority")}
                  help={t("myWork.group.priorityHelp")}
                  items={open.filter((item) => item.isPriority)}
                />
                <div className="lg:col-span-2">
                  <WorkSection
                    title={t("myWork.group.open")}
                    help={t("myWork.group.openHelp")}
                    items={open}
                  />
                </div>
              </div>
            )}
          </TabsContent>

          <TabsContent value="plan" className="space-y-4">
            <div className="flex flex-wrap items-end gap-3">
              <div className="space-y-1">
                <Label htmlFor="my-work-period">{t("myWork.plan.period")}</Label>
                <Select value={period} onValueChange={(value) => setPeriod(value as Period)}>
                  <SelectTrigger id="my-work-period" className="w-40">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(["day", "week", "month", "year"] as const).map((value) => (
                      <SelectItem key={value} value={value}>
                        {t(`myWork.plan.${value}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="my-work-day">{t("myWork.plan.date")}</Label>
                <Input
                  id="my-work-day"
                  type="date"
                  value={day}
                  onChange={(event) => event.target.value && setDay(event.target.value)}
                />
              </div>
            </div>
            {!plan.data || plan.loading ? (
              <LoadState {...plan} />
            ) : (
              <WorkSection
                title={t("myWork.plan.title")}
                help={t("myWork.plan.help")}
                items={pick(plan.data.items)}
              />
            )}
          </TabsContent>
        </Tabs>
      </div>
    </AdminLayout>
  );
}
