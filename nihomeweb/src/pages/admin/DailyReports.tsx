import { useCallback, useEffect, useState } from "react";
import { Loader2, Plus, Trash2 } from "lucide-react";
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
import { Textarea } from "@/components/ui/textarea";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatDateTime } from "@/lib/displaySettings";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { operationalProjectsApi } from "@/services/admin/operationalProjectsApi";
import {
  myWorkApi,
  type DailyWorkReport,
  type DailyWorkReportItem,
  type DepartmentDailyReports,
} from "@/services/admin/myWorkApi";

function ReportBody({ report }: { report: DailyWorkReport }) {
  const { t, lang } = useI18n();
  return (
    <div className="space-y-3 text-sm">
      {report.items.length === 0 ? (
        <p className="text-muted-foreground">{t("dailyReport.noItems")}</p>
      ) : (
        <ul className="divide-y rounded-md border">
          {report.items.map((item, index) => (
            <li key={index} className="flex flex-wrap justify-between gap-2 px-3 py-2">
              <span>
                {item.title}
                {item.note && <span className="block text-muted-foreground">{item.note}</span>}
              </span>
              <span className="font-medium tabular-nums">{item.progressPercent}%</span>
            </li>
          ))}
        </ul>
      )}
      {report.proposals && (
        <p>
          <span className="font-medium">{t("dailyReport.field.proposals")}: </span>
          {report.proposals}
        </p>
      )}
      <p>
        <span className="font-medium">{t("dailyReport.field.tomorrowPlan")}: </span>
        {report.tomorrowPlan}
      </p>
      {report.reviewComment && (
        <div className="rounded-md bg-info-soft p-3 text-info-strong">
          <p className="font-medium">
            {t("dailyReport.reviewBy", {
              name: report.reviewedByName ?? "",
              time: report.reviewedAt ? formatDateTime(report.reviewedAt, lang) : "",
            })}
          </p>
          <p className="whitespace-pre-wrap">{report.reviewComment}</p>
        </div>
      )}
    </div>
  );
}

function TodayReport() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [report, setReport] = useState<DailyWorkReport | null>(null);
  const [items, setItems] = useState<DailyWorkReportItem[]>([]);
  const [proposals, setProposals] = useState("");
  const [tomorrowPlan, setTomorrowPlan] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const apply = (data: DailyWorkReport) => {
    setReport(data);
    setItems(data.items);
    setProposals(data.proposals ?? "");
    setTomorrowPlan(data.tomorrowPlan);
  };
  const load = useCallback(() => {
    setError(null);
    myWorkApi
      .todayReport()
      .then((response) => apply(response.data))
      .catch((err) => setError(extractApiError(err, t("dailyReport.loadError"), t)));
  }, [t]);
  useEffect(load, [load]);

  const tomorrowMissing = tomorrowPlan.trim().length < 2;
  const badLine = items.some((item) => item.title.trim().length < 2);
  const submit = async () => {
    if (tomorrowMissing || badLine) return;
    setSaving(true);
    setError(null);
    try {
      const response = await myWorkApi.submitToday({
        items,
        proposals: proposals.trim() || null,
        tomorrowPlan,
        rowVersion: report?.rowVersion || undefined,
      });
      apply(response.data);
      toast({ title: t("dailyReport.saved") });
    } catch (err) {
      // Keep what the person typed; only show why it did not save.
      setError(extractApiError(err, t("dailyReport.saveError"), t));
    } finally {
      setSaving(false);
    }
  };
  const update = (index: number, patch: Partial<DailyWorkReportItem>) =>
    setItems((current) => current.map((item, i) => (i === index ? { ...item, ...patch } : item)));

  if (!report)
    return error ? (
      <div role="alert" className="space-y-2">
        <p className="text-sm text-destructive">{error}</p>
        <Button type="button" variant="outline" onClick={load}>
          {t("common.retry")}
        </Button>
      </div>
    ) : (
      <p className="flex items-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        {t("dailyReport.loading")}
      </p>
    );

  return (
    <section className="space-y-4 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">
          {t("dailyReport.todayTitle", {
            date: formatDate(report.reportDate, lang, { timeZone: "UTC" }),
          })}
        </h2>
        <Badge variant={report.id ? "secondary" : "outline"}>
          {report.reviewedAt
            ? t("dailyReport.status.reviewed")
            : report.id
              ? t("dailyReport.status.submitted")
              : t("dailyReport.status.draft")}
        </Badge>
      </div>
      {!report.canEdit ? (
        <>
          <p className="text-sm text-muted-foreground">{t("dailyReport.lockedHelp")}</p>
          <ReportBody report={report} />
        </>
      ) : (
        <>
          <p className="max-w-prose text-sm text-muted-foreground">{t("dailyReport.formHelp")}</p>
          <div className="space-y-3">
            <h3 className="font-medium">{t("dailyReport.field.items")}</h3>
            {items.length === 0 && (
              <p className="text-sm text-muted-foreground">{t("dailyReport.noItems")}</p>
            )}
            {items.map((item, index) => (
              <div
                key={index}
                className="grid gap-2 rounded-md border p-3 sm:grid-cols-[minmax(0,1fr)_7rem_auto]"
              >
                <div className="space-y-1">
                  <Label htmlFor={`report-item-${index}`}>{t("dailyReport.field.work")}</Label>
                  <Input
                    id={`report-item-${index}`}
                    maxLength={300}
                    value={item.title}
                    onChange={(event) => update(index, { title: event.target.value })}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor={`report-progress-${index}`}>
                    {t("dailyReport.field.progress")}
                  </Label>
                  <Input
                    id={`report-progress-${index}`}
                    type="number"
                    min={0}
                    max={100}
                    value={item.progressPercent}
                    onChange={(event) =>
                      update(index, {
                        progressPercent: Math.min(
                          100,
                          Math.max(0, Number(event.target.value) || 0),
                        ),
                      })
                    }
                  />
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  className="self-end"
                  aria-label={t("dailyReport.removeLine")}
                  onClick={() => setItems((current) => current.filter((_, i) => i !== index))}
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                </Button>
                <div className="space-y-1 sm:col-span-3">
                  <Label htmlFor={`report-note-${index}`}>{t("dailyReport.field.note")}</Label>
                  <Input
                    id={`report-note-${index}`}
                    maxLength={1000}
                    value={item.note ?? ""}
                    onChange={(event) => update(index, { note: event.target.value })}
                  />
                </div>
              </div>
            ))}
            {badLine && <p className="text-sm text-destructive">{t("dailyReport.lineRequired")}</p>}
            <Button
              type="button"
              variant="outline"
              disabled={items.length >= 50}
              onClick={() => setItems((current) => [...current, { title: "", progressPercent: 0 }])}
            >
              <Plus className="h-4 w-4" aria-hidden="true" />
              {t("dailyReport.addLine")}
            </Button>
          </div>
          <div className="space-y-1">
            <Label htmlFor="report-proposals">{t("dailyReport.field.proposals")}</Label>
            <Textarea
              id="report-proposals"
              maxLength={4000}
              value={proposals}
              onChange={(event) => setProposals(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="report-tomorrow">{t("dailyReport.field.tomorrowPlanRequired")}</Label>
            <Textarea
              id="report-tomorrow"
              maxLength={4000}
              value={tomorrowPlan}
              aria-invalid={tomorrowMissing}
              onChange={(event) => setTomorrowPlan(event.target.value)}
            />
            {tomorrowMissing && (
              <p className="text-sm text-muted-foreground">{t("dailyReport.tomorrowRequired")}</p>
            )}
          </div>
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          <Button
            type="button"
            onClick={() => void submit()}
            disabled={saving || tomorrowMissing || badLine}
          >
            {saving && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
            {report.id ? t("dailyReport.update") : t("dailyReport.submit")}
          </Button>
        </>
      )}
    </section>
  );
}

function ReviewForm({ report, onSaved }: { report: DailyWorkReport; onSaved: () => void }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [comment, setComment] = useState(report.reviewComment ?? "");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await myWorkApi.review(report.id, comment, report.rowVersion);
      toast({ title: t("dailyReport.reviewSaved") });
      onSaved();
    } catch (err) {
      setError(extractApiError(err, t("dailyReport.reviewError"), t));
    } finally {
      setSaving(false);
    }
  };
  return (
    <div className="space-y-2">
      <Label htmlFor={`review-${report.id}`}>{t("dailyReport.field.comment")}</Label>
      <Textarea
        id={`review-${report.id}`}
        maxLength={4000}
        value={comment}
        onChange={(event) => setComment(event.target.value)}
      />
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <Button
        type="button"
        variant="outline"
        disabled={saving || comment.trim().length < 2}
        onClick={() => void save()}
      >
        {t("dailyReport.sendComment")}
      </Button>
    </div>
  );
}

function DepartmentReports({ canViewAll }: { canViewAll: boolean }) {
  const { t, lang } = useI18n();
  const [date, setDate] = useState("");
  const [departmentId, setDepartmentId] = useState<number | undefined>();
  const [departments, setDepartments] = useState<{ id: number; labelKey: string }[]>([]);
  const [data, setData] = useState<DepartmentDailyReports | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (canViewAll)
      void operationalProjectsApi
        .getWorkPackageDepartments()
        .then((response) => setDepartments(response.data));
  }, [canViewAll]);
  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    myWorkApi
      .departmentReports({ date: date || undefined, departmentId })
      .then((response) => setData(response.data))
      .catch((err) => {
        setData(null);
        setError(
          (err as { response?: { status?: number } })?.response?.status === 403
            ? t(canViewAll ? "dailyReport.pickDepartment" : "dailyReport.notHead")
            : extractApiError(err, t("dailyReport.loadError"), t),
        );
      })
      .finally(() => setLoading(false));
  }, [date, departmentId, canViewAll, t]);
  useEffect(load, [load]);

  return (
    <section className="space-y-4 rounded-lg border bg-card p-4">
      <h2 className="text-lg font-semibold">
        {t("dailyReport.departmentTitle")}
        {data && ` · ${t(data.departmentLabelKey)}`}
      </h2>
      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1">
          <Label htmlFor="department-report-date">{t("dailyReport.field.date")}</Label>
          <Input
            id="department-report-date"
            type="date"
            value={date || data?.reportDate || ""}
            onChange={(event) => setDate(event.target.value)}
          />
        </div>
        {canViewAll && (
          <div className="space-y-1">
            <Label htmlFor="department-report-group">{t("dailyReport.field.department")}</Label>
            <Select
              value={departmentId ? String(departmentId) : ""}
              onValueChange={(value) => setDepartmentId(Number(value))}
            >
              <SelectTrigger id="department-report-group" className="w-56">
                <SelectValue placeholder={t("dailyReport.field.departmentPlaceholder")} />
              </SelectTrigger>
              <SelectContent>
                {departments.map((department) => (
                  <SelectItem key={department.id} value={String(department.id)}>
                    {t(department.labelKey)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}
      </div>
      {loading ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          {t("dailyReport.loading")}
        </p>
      ) : error ? (
        <p className="text-sm text-muted-foreground">{error}</p>
      ) : (
        data && (
          <>
            {data.missingMembers.length > 0 && (
              <p className="rounded-md bg-warning-soft p-3 text-sm text-warning-strong">
                {t("dailyReport.missing", {
                  names: data.missingMembers.map((member) => member.name).join(", "),
                })}
              </p>
            )}
            {data.reports.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t("dailyReport.departmentEmpty")}</p>
            ) : (
              data.reports.map((report) => (
                <article key={report.id} className="space-y-3 rounded-md border p-3">
                  <h3 className="font-medium">
                    {report.userName}
                    <span className="ml-2 text-sm font-normal text-muted-foreground">
                      {report.updatedAt && formatDateTime(report.updatedAt, lang)}
                    </span>
                  </h3>
                  <ReportBody report={report} />
                  {report.canReview && <ReviewForm report={report} onSaved={load} />}
                </article>
              ))
            )}
          </>
        )
      )}
    </section>
  );
}

function MyHistory() {
  const { t, lang } = useI18n();
  const [reports, setReports] = useState<DailyWorkReport[] | null>(null);
  useEffect(() => {
    void myWorkApi
      .myReports()
      .then((response) => setReports(response.data))
      .catch(() => setReports([]));
  }, []);
  if (!reports || reports.length === 0) return null;
  return (
    <section className="space-y-3 rounded-lg border bg-card p-4">
      <h2 className="text-lg font-semibold">{t("dailyReport.historyTitle")}</h2>
      {reports.map((report) => (
        <details key={report.id} className="rounded-md border p-3">
          <summary className="cursor-pointer font-medium">
            {formatDate(report.reportDate, lang, { timeZone: "UTC" })}
            {" · "}
            {report.reviewedAt
              ? t("dailyReport.status.reviewed")
              : t("dailyReport.status.submitted")}
          </summary>
          <div className="mt-3">
            <ReportBody report={report} />
          </div>
        </details>
      ))}
    </section>
  );
}

export default function DailyReports() {
  const { t } = useI18n();
  const { has } = usePermissions();
  const canViewAll = has(ADMIN_PERMS.workPackagesViewAll);
  const isHead = has(ADMIN_PERMS.dailyReportsReview);
  return (
    <AdminLayout>
      <div className="space-y-5">
        <div>
          <h1 className="text-2xl font-semibold">{t("dailyReport.title")}</h1>
          <p className="mt-1 max-w-prose text-sm text-muted-foreground">
            {t("dailyReport.subtitle")}
          </p>
        </div>
        <TodayReport />
        {(isHead || canViewAll) && <DepartmentReports canViewAll={canViewAll} />}
        <MyHistory />
      </div>
    </AdminLayout>
  );
}
