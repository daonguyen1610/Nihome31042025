import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { RefreshCw } from "lucide-react";
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip, Legend } from "recharts";
import {
  cmScheduleApi,
  type CmScheduleWorkspace as Workspace,
  type CmScheduleLine,
  type CmBaseline,
  type CmSnapshot,
  type CmActualInput,
  type CmMailPreview,
  type CmScheduleReport,
} from "@/services/admin/cmScheduleApi";
import { useAppSelector } from "@/store";
import { usePermissions } from "@/hooks/usePermissions";
import { useUiError } from "@/hooks/useUiError";
import { useI18n } from "@/lib/i18n";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { LocalizedDateInput } from "@/components/ui/localized-inputs";
import { MoneyInput } from "@/components/ui/money-input";
import { PageLoading, PageError } from "@/components/PageState";
import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
  DialogHeader,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Select,
  SelectTrigger,
  SelectContent,
  SelectItem,
  SelectValue,
} from "@/components/ui/select";

export function CmScheduleWorkspace({
  projectId,
  standalone = false,
}: {
  projectId: number;
  standalone?: boolean;
}) {
  const { t, lang } = useI18n();
  const uiError = useUiError();
  const { has } = usePermissions();
  const userId = useAppSelector((state) => state.auth.user?.userId);
  const manage = has(ADMIN_PERMS.constructionCmPreparationManage);
  const [data, setData] = useState<Workspace | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const [baselineId, setBaselineId] = useState<string>("");
  const [createOpen, setCreateOpen] = useState(false);
  const [lines, setLines] = useState<CmScheduleLine[]>([]);
  const [submission, setSubmission] = useState<{ row: CmBaseline; preview: CmMailPreview } | null>(
    null,
  );
  const [reason, setReason] = useState("");
  const [siteManager, setSiteManager] = useState("");
  const [decision, setDecision] = useState<CmBaseline | null>(null);
  const [note, setNote] = useState("");
  const [recovery, setRecovery] = useState<CmSnapshot | null>(null);
  const [explanation, setExplanation] = useState("");
  const [plan, setPlan] = useState("");
  const [period, setPeriod] = useState<"day" | "month" | "year">("month");
  const [periodDate, setPeriodDate] = useState("");
  const [report, setReport] = useState<CmScheduleReport | null>(null);
  const [reportLoading, setReportLoading] = useState(false);
  const [reportError, setReportError] = useState<string | null>(null);
  const [due, setDue] = useState("");
  const [actual, setActual] = useState<CmActualInput | null>(null);
  const [progress, setProgress] = useState(0);
  const [weightBasisNote, setWeightBasisNote] = useState("");
  const [workingDays, setWorkingDays] = useState<number[]>([1, 2, 3, 4, 5, 6]);
  const [holidayDate, setHolidayDate] = useState("");
  const [nonWorkingDates, setNonWorkingDates] = useState<string[]>([]);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void cmScheduleApi
      .workspace(projectId)
      .then(({ data }) => {
        if (active) setData(data);
      })
      .catch((cause) => {
        if (active) setError(uiError(cause));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [projectId, revision, uiError]);
  const refresh = useCallback(() => setRevision((x) => x + 1), []);
  async function act(work: () => Promise<unknown>, done?: () => void) {
    setBusy(true);
    setError(null);
    try {
      await work();
      done?.();
      refresh();
    } catch (cause) {
      setError(uiError(cause));
    } finally {
      setBusy(false);
    }
  }
  const selected =
    data?.baselines.find((x) => String(x.id) === baselineId) ??
    data?.baselines.find((x) => x.status === "Draft" || x.status === "PendingApproval") ??
    data?.baselines.find((x) => x.status === "Published") ??
    data?.baselines[0];
  const planReady = !!data?.crmRevisionId && data.proposed.length > 0 && data.unlinked.length === 0;
  const anchor = periodDate || data?.asOfDate || "";
  const snapshots = useMemo(
    () => data?.snapshots.filter((x) => x.baselineId === selected?.id) ?? [],
    [data, selected],
  );
  useEffect(() => {
    if (!selected || !anchor) {
      setReport(null);
      return;
    }
    let active = true;
    setReport(null);
    setReportLoading(true);
    setReportError(null);
    void cmScheduleApi
      .report(projectId, selected.id, period, anchor)
      .then(({ data }) => {
        if (active) setReport(data);
      })
      .catch((cause) => {
        if (active) setReportError(uiError(cause));
      })
      .finally(() => {
        if (active) setReportLoading(false);
      });
    return () => {
      active = false;
    };
  }, [projectId, selected, period, anchor, revision, uiError]);
  const chart = useMemo(
    () =>
      report?.points.map((x) => ({
        ...x,
        date: Date.parse(`${x.date}T00:00:00Z`),
      })) ?? [],
    [report],
  );
  const percent = (value: number) => `${value.toLocaleString(lang, { maximumFractionDigits: 8 })}%`;
  const displayDate = (value: string) =>
    new Date(`${value.slice(0, 10)}T00:00:00Z`).toLocaleDateString(lang, { timeZone: "UTC" });
  const setLine = (id: number, patch: Partial<CmScheduleLine>) =>
    setLines((current) => current.map((row) => (row.taskId === id ? { ...row, ...patch } : row)));
  const unlinkedTasks = data && data.unlinked.length > 0 && (
    <div className="space-y-3">
      <p className="font-medium">{t("cmSchedule.unlinked")}</p>
      <ul className="space-y-2">
        {data.unlinked.map((task) => (
          <li key={task.id}>
            <Link
              className="text-sm text-foreground underline decoration-border underline-offset-4 hover:decoration-current"
              to={`/admin/construction/cm/schedule?tab=tasks&designProjectId=${task.designProjectId}&taskId=${task.id}`}
            >
              {task.taskCode} · {task.name}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
  if (loading && !data) return <PageLoading />;
  return (
    <section className="space-y-5" data-testid="cm-schedule-workspace">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h2 className="min-w-0 flex-1 text-lg font-semibold">
          {t(
            standalone
              ? selected
                ? "cmSchedule.planSection"
                : "cmSchedule.setupTitle"
              : "cmSchedule.title",
          )}
        </h2>
        <Button
          variant="outline"
          className="h-11 w-11 shrink-0 gap-2 p-0 sm:w-auto sm:px-4"
          disabled={busy || loading}
          onClick={refresh}
          title={t("common.refresh")}
        >
          <RefreshCw className="h-4 w-4" aria-hidden="true" />
          <span className="sr-only sm:not-sr-only">{t("common.refresh")}</span>
        </Button>
      </div>
      {error && (
        <div role="alert">
          <PageError message={error} onRetry={refresh} />
        </div>
      )}
      {data && (
        <>
          {!selected && (
            <div
              className="space-y-5 rounded-xl border bg-card p-5 sm:p-6"
              data-testid="cm-schedule-setup"
            >
              <div className="space-y-2">
                <h3 className="font-semibold">{t("scheduleUx.setupHeading")}</h3>
                <p className="max-w-3xl text-sm text-muted-foreground">
                  {t("scheduleUx.setupDescription")}
                </p>
              </div>
              <ol className="list-decimal space-y-5 pl-5 marker:font-semibold">
                <li className="space-y-2 pl-1">
                  <p className="font-medium">{t("scheduleUx.crmStep")}</p>
                  <p className="max-w-3xl text-sm text-muted-foreground">
                    {t(data.crmRevisionId ? "scheduleUx.crmReady" : "scheduleUx.crmMissing")}
                  </p>
                  {has(ADMIN_PERMS.operationalProjects) && (
                    <Button asChild variant={data.crmRevisionId ? "outline" : "default"}>
                      <Link
                        to={`/admin/operational-projects/${projectId}?tab=schedule#project-master-schedule`}
                      >
                        {t("cmSchedule.openCrm")}
                      </Link>
                    </Button>
                  )}
                </li>
                <li className="space-y-2 pl-1">
                  <p className="font-medium">{t("scheduleUx.taskStep")}</p>
                  <p className="max-w-3xl text-sm text-muted-foreground">
                    {t("scheduleUx.taskStepHint")}
                  </p>
                  {unlinkedTasks}
                  {data.crmRevisionId &&
                    data.proposed.length === 0 &&
                    data.unlinked.length === 0 && (
                      <p className="text-sm">{t("cmSchedule.noTasks")}</p>
                    )}
                  {has(ADMIN_PERMS.constructionTasks) &&
                    data.unlinked.length === 0 &&
                    !data.proposed.length && (
                      <Button asChild variant="outline">
                        <Link to="/admin/construction/cm/schedule?tab=tasks">
                          {t("cmSchedule.openTasks")}
                        </Link>
                      </Button>
                    )}
                </li>
                <li className="space-y-2 pl-1">
                  <p className="font-medium">{t("scheduleUx.planStep")}</p>
                  <p className="max-w-3xl text-sm text-muted-foreground">
                    {t(planReady ? "scheduleUx.planReady" : "scheduleUx.planBlocked")}
                  </p>
                </li>
              </ol>
            </div>
          )}
          {selected && unlinkedTasks && (
            <div className="space-y-3 rounded-xl border bg-card p-5 sm:p-6">{unlinkedTasks}</div>
          )}
          <div className="flex flex-wrap gap-3 items-end">
            {data.baselines.length > 0 && (
              <div className="w-full space-y-1.5 sm:w-80">
                <Label htmlFor="cm-baseline-version">{t("cmSchedule.version")}</Label>
                <Select value={selected ? String(selected.id) : ""} onValueChange={setBaselineId}>
                  <SelectTrigger
                    id="cm-baseline-version"
                    className="w-full"
                    aria-label={t("cmSchedule.version")}
                  >
                    <SelectValue placeholder={t("cmSchedule.noBaseline")} />
                  </SelectTrigger>
                  <SelectContent>
                    {data.baselines.map((x) => (
                      <SelectItem key={x.id} value={String(x.id)}>
                        {t("cmSchedule.version")} {x.version} · {t(`cmSchedule.status.${x.status}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {manage &&
              (selected || planReady) &&
              !data.baselines.some(
                (x) => x.status === "Draft" || x.status === "PendingApproval",
              ) && (
                <Button
                  disabled={
                    busy ||
                    !data.crmRevisionId ||
                    data.proposed.length === 0 ||
                    data.unlinked.length > 0
                  }
                  onClick={() => {
                    setLines(data.proposed);
                    setReason("");
                    setSiteManager("");
                    setWeightBasisNote("");
                    setWorkingDays([1, 2, 3, 4, 5, 6]);
                    setNonWorkingDates([]);
                    setHolidayDate("");
                    setError(null);
                    setCreateOpen(true);
                  }}
                >
                  {t("cmSchedule.create")}
                </Button>
              )}
            {selected?.status === "Draft" && manage && selected.createdByUserId === userId && (
              <>
                <Button
                  disabled={busy}
                  onClick={() =>
                    void act(async () => {
                      const { data } = await cmScheduleApi.previewSubmit(projectId, selected);
                      setSubmission({ row: selected, preview: data });
                    })
                  }
                >
                  {t("cmSchedule.submit")}
                </Button>
              </>
            )}
            {selected &&
              ["Draft", "PendingApproval"].includes(selected.status) &&
              manage &&
              selected.createdByUserId === userId && (
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() => void act(() => cmScheduleApi.cancel(projectId, selected))}
                >
                  {t("cmSchedule.discard")}
                </Button>
              )}
            {selected?.status === "PendingApproval" &&
              selected.approverUserId === userId &&
              selected.createdByUserId !== userId && (
                <Button
                  disabled={busy}
                  onClick={() => {
                    setDecision(selected);
                    setNote("");
                  }}
                >
                  {t("cmSchedule.review")}
                </Button>
              )}
            {selected?.status === "Published" &&
              selected.id === data.baselines.find((x) => x.status === "Published")?.id && (
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() => void act(() => cmScheduleApi.capture(projectId))}
                >
                  {t("cmSchedule.capture")}
                </Button>
              )}
          </div>
          {selected && (
            <>
              <p role="status" className="max-w-3xl text-sm text-muted-foreground">
                {t(`scheduleUx.state.${selected.status}`)}
              </p>
              <details className="text-sm">
                <summary className="cursor-pointer py-2 font-medium">
                  {t("scheduleUx.planDetails")}
                </summary>
                <div className="mt-2 space-y-2">
                  <p className="text-sm">{selected.changeReason}</p>
                  <p className="text-sm text-muted-foreground">{selected.weightBasisNote}</p>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <p>
                      {t("cmSchedule.workingDays")}:{" "}
                      {[1, 2, 3, 4, 5, 6, 0]
                        .filter((day) => selected.workingDaysOfWeek.includes(day))
                        .map((day) => t(`cmSchedule.weekday.${day}`))
                        .join(", ")}
                    </p>
                    <p>
                      {t("cmSchedule.nonWorkingDate")}:{" "}
                      {selected.nonWorkingDates.length
                        ? selected.nonWorkingDates.join(", ")
                        : t("common.noData")}
                    </p>
                  </div>
                </div>
              </details>
              {selected.crmRevisionId !== data.crmRevisionId && (
                <p role="status" className="text-destructive">
                  {t("cmSchedule.crmChanged")}
                </p>
              )}
              {data.emailDeliveries
                .filter((x) => x.baselineId === selected.id)
                .map((email) => (
                  <div key={email.id} className="space-y-2 text-sm">
                    <p>
                      {t("cmSchedule.emailDelivery")}: {t(`cmSchedule.emailStatus.${email.status}`)}
                    </p>
                    {email.status === "NeedsReview" && data.projectManagerUserId === userId && (
                      <div className="flex flex-wrap gap-2">
                        <Button
                          variant="outline"
                          disabled={busy}
                          onClick={() =>
                            void act(() =>
                              cmScheduleApi.resolveEmail(projectId, email.id, "ConfirmReceived"),
                            )
                          }
                        >
                          {t("operationalProjects.schedule.email.confirmReceived")}
                        </Button>
                        <Button
                          variant="outline"
                          disabled={busy}
                          onClick={() =>
                            void act(() => cmScheduleApi.resolveEmail(projectId, email.id, "Retry"))
                          }
                        >
                          {t("common.retry")}
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
              {selected.reviewNote && <p>{selected.reviewNote}</p>}
              <div className="space-y-2">
                <h3 className="font-semibold">{t("scheduleUx.curveTitle")}</h3>
                <p className="max-w-3xl text-sm text-muted-foreground">
                  {t("scheduleUx.curveHint")}
                </p>
                <p className="max-w-3xl text-sm text-muted-foreground">
                  {t("scheduleUx.curveMissingHint")}
                </p>
              </div>
              <div className="flex flex-wrap items-center gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="cm-report-period">{t("cmSchedule.period")}</Label>
                  <Select
                    value={period}
                    onValueChange={(value) => setPeriod(value as "day" | "month" | "year")}
                  >
                    <SelectTrigger
                      id="cm-report-period"
                      className="w-48"
                      aria-label={t("cmSchedule.period")}
                    >
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {["day", "month", "year"].map((value) => (
                        <SelectItem key={value} value={value}>
                          {t(`cmSchedule.period.${value}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="cm-report-date">{t("cmSchedule.periodDate")}</Label>
                  <LocalizedDateInput
                    id="cm-report-date"
                    aria-label={t("cmSchedule.periodDate")}
                    value={anchor}
                    onChange={(e) => setPeriodDate(e.target.value)}
                  />
                </div>
              </div>
              {reportLoading && <p role="status">{t("common.loading")}</p>}
              {reportError && (
                <p role="alert" className="text-destructive">
                  {reportError}
                </p>
              )}
              {!reportLoading && !reportError && chart.length === 0 && (
                <p className="rounded-lg border p-4 text-sm">{t("scheduleUx.noPeriodData")}</p>
              )}
              {chart.length > 0 && (
                <div className="h-72 w-full min-w-0">
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={chart}>
                      <XAxis
                        dataKey="date"
                        type="number"
                        domain={["dataMin", "dataMax"]}
                        tickFormatter={(value) =>
                          new Date(value).toLocaleDateString(lang, { timeZone: "UTC" })
                        }
                      />
                      <YAxis domain={[0, 100]} tickFormatter={(value) => `${value}%`} width={48} />
                      <Tooltip
                        labelFormatter={(value) =>
                          new Date(Number(value)).toLocaleDateString(lang, { timeZone: "UTC" })
                        }
                        formatter={(value) => percent(Number(value))}
                      />
                      <Legend />
                      <Line
                        type="linear"
                        dataKey="crmPercent"
                        name={t("scheduleUx.seriesCrm")}
                        stroke="var(--chart-1, #2563eb)"
                        strokeWidth={2}
                        strokeDasharray="6 4"
                        dot={false}
                      />
                      <Line
                        type="linear"
                        dataKey="planPercent"
                        name={t("scheduleUx.seriesCm")}
                        stroke="var(--chart-2, #d97706)"
                        strokeWidth={2}
                        strokeDasharray="2 3"
                        dot={false}
                      />
                      <Line
                        type="linear"
                        dataKey="actualPercent"
                        name={t("scheduleUx.seriesActual")}
                        stroke="var(--chart-3, #16a34a)"
                        strokeWidth={3}
                        connectNulls={false}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              )}
              {chart.length > 0 && (
                <details className="text-sm">
                  <summary className="cursor-pointer py-2 font-medium">
                    {t("scheduleUx.viewValues")}
                  </summary>
                  <div className="mt-2 overflow-x-auto">
                    <table className="w-full text-sm">
                      <caption className="sr-only">{t("scheduleUx.curveTitle")}</caption>
                      <thead>
                        <tr>
                          {[
                            "cmSchedule.periodDate",
                            "scheduleUx.seriesCrm",
                            "scheduleUx.seriesCm",
                            "scheduleUx.seriesActual",
                          ].map((key) => (
                            <th key={key} scope="col" className="p-2 text-left">
                              {t(key)}
                            </th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {report?.points.map((point) => (
                          <tr key={point.date} className="border-t">
                            <th scope="row" className="whitespace-nowrap p-2 text-left font-normal">
                              {displayDate(point.date)}
                            </th>
                            <td className="p-2 tabular-nums">{percent(point.crmPercent)}</td>
                            <td className="p-2 tabular-nums">{percent(point.planPercent)}</td>
                            <td className="p-2 tabular-nums">
                              {point.actualPercent === null
                                ? t("cmSchedule.actualUnreported")
                                : percent(point.actualPercent)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </details>
              )}
              {chart.some((point) => !point.isDataComplete) && (
                <p role="status" className="text-sm text-muted-foreground">
                  {t("cmSchedule.reportIncomplete")}
                </p>
              )}
              <div className="space-y-2 pt-3">
                <h3 className="font-semibold">{t("scheduleUx.taskProgress")}</h3>
                <p className="max-w-3xl text-sm text-muted-foreground">
                  {t("scheduleUx.actualGuidance")}
                </p>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr>
                      <th className="p-2 text-left">{t("cmSchedule.task")}</th>
                      <th className="p-2 text-left">{t("scheduleUx.seriesCrm")}</th>
                      <th className="p-2 text-left">{t("scheduleUx.seriesCm")}</th>
                      <th className="p-2 text-right">{t("cmSchedule.weight")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selected.items.map((x) => (
                      <tr key={x.taskId} className="border-t">
                        <td className="p-2">
                          <span className={x.isLeaf ? "" : "font-semibold"}>
                            {x.wbs} · {x.name}
                          </span>
                          {x.isLeaf &&
                            data.actualInputs.find((input) => input.taskId === x.taskId) && (
                              <p className="text-xs text-muted-foreground">
                                {t("scheduleUx.seriesActual")}:{" "}
                                {data.actualInputs.find((input) => input.taskId === x.taskId)!
                                  .progressReportedAt
                                  ? percent(
                                      data.actualInputs.find((input) => input.taskId === x.taskId)!
                                        .progressPercent,
                                    )
                                  : t("cmSchedule.actualUnreported")}
                              </p>
                            )}
                          {x.isLeaf &&
                            data.actualInputs.find((input) => input.taskId === x.taskId)
                              ?.progressReportedAt && (
                              <p className="text-xs text-muted-foreground">
                                {t("cmSchedule.actualReported", {
                                  user:
                                    data.members.find(
                                      (member) =>
                                        member.userId ===
                                        data.actualInputs.find((input) => input.taskId === x.taskId)
                                          ?.progressReportedByUserId,
                                    )?.name ?? t("cmSchedule.siteManager"),
                                  at: new Date(
                                    data.actualInputs.find((input) => input.taskId === x.taskId)!
                                      .progressReportedAt!,
                                  ).toLocaleString(lang, { timeZone: "Asia/Ho_Chi_Minh" }),
                                })}
                              </p>
                            )}
                          {x.isLeaf &&
                            selected.id ===
                              data.baselines.find((b) => b.status === "Published")?.id &&
                            selected.siteManagerUserId === userId && (
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={busy}
                                onClick={() => {
                                  const input = data.actualInputs.find(
                                    (input) => input.taskId === x.taskId,
                                  );
                                  if (input) {
                                    setActual(input);
                                    setProgress(input.progressPercent);
                                    setError(null);
                                  }
                                }}
                              >
                                {t("cmSchedule.updateActual")}
                              </Button>
                            )}
                        </td>
                        <td className="p-2 whitespace-nowrap">
                          {displayDate(x.crmStart)} → {displayDate(x.crmEnd)}
                        </td>
                        <td className="p-2 whitespace-nowrap">
                          {displayDate(x.plannedStart)} → {displayDate(x.plannedEnd)}
                        </td>
                        <td className="p-2 text-right">
                          {x.isMilestone
                            ? t("cmSchedule.milestone")
                            : x.isLeaf
                              ? `${x.weight.toLocaleString(lang)}%`
                              : t("cmSchedule.summaryTask")}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <h3 className="font-semibold">{t("scheduleUx.history")}</h3>
              {snapshots.length === 0 && (
                <p className="text-sm text-muted-foreground">{t("scheduleUx.noSnapshots")}</p>
              )}
              <ul className="space-y-3">
                {snapshots.map((row) => (
                  <li key={row.id} className="space-y-2 rounded-md border p-3">
                    <div className="flex flex-wrap gap-3">
                      <span>{displayDate(row.asOfDate)}</span>
                      <span>CRM {percent(row.crmPercent)}</span>
                      <span>CM {percent(row.planPercent)}</span>
                      <span>BCH {percent(row.actualPercent)}</span>
                      <span className={row.isDelayed ? "font-semibold text-destructive" : ""}>
                        {t(row.isDelayed ? "cmSchedule.delayed" : "cmSchedule.variance", {
                          value: percent(row.delayPercent),
                        })}
                      </span>
                    </div>
                    {row.explanation && <p>{row.explanation}</p>}
                    {row.recoveryPlan && (
                      <p>
                        {row.recoveryPlan} · {row.recoveryDueDate}
                      </p>
                    )}
                    {row.isDelayed && !row.recoveryAt && selected.siteManagerUserId === userId && (
                      <Button
                        disabled={busy}
                        onClick={() => {
                          setRecovery(row);
                          setExplanation("");
                          setPlan("");
                          setDue("");
                        }}
                      >
                        {t("cmSchedule.recovery")}
                      </Button>
                    )}
                    {row.recoveryAt &&
                      !row.reviewedAt &&
                      manage &&
                      row.recoveryByUserId !== userId && (
                        <Button
                          disabled={busy}
                          onClick={() => void act(() => cmScheduleApi.review(projectId, row))}
                        >
                          {t("cmSchedule.reviewRecovery")}
                        </Button>
                      )}
                    {row.reviewedAt && <p>{t("cmSchedule.reviewed")}</p>}
                  </li>
                ))}
              </ul>
            </>
          )}
          <details className="text-sm">
            <summary className="w-fit cursor-pointer rounded py-2 font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
              {t("cmSchedule.howItWorks")}
            </summary>
            <div className="mt-2 max-w-3xl space-y-3 text-muted-foreground">
              <p>{t("cmSchedule.method")}</p>
              <p>
                {t("cmSchedule.policy", { threshold: data.thresholdPercent.toLocaleString(lang) })}
              </p>
            </div>
          </details>
        </>
      )}
      <Dialog open={submission !== null} onOpenChange={(open) => !open && setSubmission(null)}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("cmSchedule.submit")}</DialogTitle>
            <DialogDescription>{t("cmSchedule.emailPreviewHint")}</DialogDescription>
          </DialogHeader>
          {submission?.preview.emails.map((email) => (
            <div key={email.toEmail} className="space-y-2">
              <p>{email.toEmail}</p>
              <p className="font-medium">{email.subject}</p>
              <iframe
                className="h-80 w-full border"
                sandbox=""
                title={t("cmSchedule.emailTemplate")}
                srcDoc={email.body}
              />
            </div>
          ))}
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button
              disabled={busy}
              onClick={() =>
                submission &&
                void act(
                  () =>
                    cmScheduleApi.submit(
                      projectId,
                      submission.row,
                      submission.preview.previewToken,
                    ),
                  () => setSubmission(null),
                )
              }
            >
              {t("cmSchedule.submit")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={actual !== null} onOpenChange={(open) => !open && setActual(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("cmSchedule.updateActual")}</DialogTitle>
            <DialogDescription>{t("cmSchedule.actualHint")}</DialogDescription>
          </DialogHeader>
          <Label htmlFor="cm-actual-progress">{t("scheduleUx.seriesActual")}</Label>
          <MoneyInput
            showZero
            decimalScale={4}
            id="cm-actual-progress"
            value={progress}
            onChange={setProgress}
          />
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button
              disabled={busy || !Number.isInteger(progress) || progress < 0 || progress > 100}
              onClick={() =>
                actual &&
                void act(
                  () => cmScheduleApi.updateActual(projectId, actual, progress),
                  () => setActual(null),
                )
              }
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("cmSchedule.create")}</DialogTitle>
            <DialogDescription>{t("cmSchedule.method")}</DialogDescription>
          </DialogHeader>
          <Label htmlFor="cm-change-reason">{t("cmSchedule.reason")}</Label>
          <Textarea
            id="cm-change-reason"
            maxLength={2000}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
          <Label htmlFor="cm-site-manager">{t("cmSchedule.siteManager")}</Label>
          <Select value={siteManager} onValueChange={setSiteManager}>
            <SelectTrigger id="cm-site-manager" aria-label={t("cmSchedule.siteManager")}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {data?.members.map((x) => (
                <SelectItem key={x.userId} value={String(x.userId)}>
                  {x.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Label htmlFor="cm-weight-basis">{t("cmSchedule.weightBasisNote")}</Label>
          <Textarea
            id="cm-weight-basis"
            value={weightBasisNote}
            maxLength={1000}
            onChange={(e) => setWeightBasisNote(e.target.value)}
          />
          <p className="text-sm">
            {t("cmSchedule.weightTotal", {
              total: lines
                .filter((x) => x.isLeaf && !x.isMilestone)
                .reduce((sum, x) => sum + x.weight, 0)
                .toLocaleString(lang, { maximumFractionDigits: 4 }),
            })}
          </p>
          <fieldset className="space-y-2 rounded-md border p-3">
            <legend className="px-1 text-sm font-medium">{t("cmSchedule.workingCalendar")}</legend>
            <div className="flex flex-wrap gap-3">
              {[1, 2, 3, 4, 5, 6, 0].map((day) => (
                <label key={day} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={workingDays.includes(day)}
                    onChange={(e) =>
                      setWorkingDays((current) =>
                        e.target.checked
                          ? [...current, day].sort()
                          : current.filter((x) => x !== day),
                      )
                    }
                  />
                  {t(`cmSchedule.weekday.${day}`)}
                </label>
              ))}
            </div>
            <div className="flex flex-wrap items-end gap-2">
              <LocalizedDateInput
                aria-label={t("cmSchedule.nonWorkingDate")}
                value={holidayDate}
                onChange={(e) => setHolidayDate(e.target.value)}
              />
              <Button
                type="button"
                variant="outline"
                disabled={!holidayDate || nonWorkingDates.includes(holidayDate)}
                onClick={() => {
                  setNonWorkingDates((current) => [...current, holidayDate].sort());
                  setHolidayDate("");
                }}
              >
                {t("cmSchedule.addNonWorkingDate")}
              </Button>
            </div>
            {nonWorkingDates.map((date) => (
              <div key={date} className="flex items-center gap-2 text-sm">
                <span>{date}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => setNonWorkingDates((current) => current.filter((x) => x !== date))}
                >
                  {t("cmSchedule.removeNonWorkingDate")}
                </Button>
              </div>
            ))}
          </fieldset>
          <div className="space-y-4">
            {lines.map((row) => (
              <div key={row.taskId} className="grid gap-2 border-t pt-3 sm:grid-cols-4">
                <p className="sm:col-span-4">
                  {row.wbs} · {row.name}
                </p>
                <div className="space-y-1.5">
                  <Label htmlFor={`cm-start-${row.taskId}`}>{t("cmSchedule.start")}</Label>
                  <LocalizedDateInput
                    id={`cm-start-${row.taskId}`}
                    aria-label={`${t("cmSchedule.start")} ${row.wbs}`}
                    min={row.crmStart}
                    max={row.crmEnd}
                    value={row.plannedStart}
                    onChange={(e) => setLine(row.taskId, { plannedStart: e.target.value })}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor={`cm-end-${row.taskId}`}>{t("cmSchedule.end")}</Label>
                  <LocalizedDateInput
                    id={`cm-end-${row.taskId}`}
                    aria-label={`${t("cmSchedule.end")} ${row.wbs}`}
                    min={row.crmStart}
                    max={row.crmEnd}
                    value={row.plannedEnd}
                    onChange={(e) => setLine(row.taskId, { plannedEnd: e.target.value })}
                  />
                </div>
                {row.isLeaf ? (
                  <>
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="checkbox"
                        checked={row.isMilestone}
                        onChange={(e) =>
                          setLine(row.taskId, { isMilestone: e.target.checked, weight: 0 })
                        }
                      />
                      {t("cmSchedule.milestone")}
                    </label>
                    {!row.isMilestone && (
                      <div className="space-y-1.5">
                        <Label htmlFor={`cm-weight-${row.taskId}`}>{t("cmSchedule.weight")}</Label>
                        <MoneyInput
                          id={`cm-weight-${row.taskId}`}
                          aria-label={`${t("cmSchedule.weight")} ${row.wbs}`}
                          decimalScale={4}
                          value={row.weight}
                          onChange={(weight) => setLine(row.taskId, { weight })}
                        />
                      </div>
                    )}
                  </>
                ) : (
                  <p className="text-sm text-muted-foreground">{t("cmSchedule.summaryTask")}</p>
                )}
              </div>
            ))}
          </div>
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <p className="text-sm text-muted-foreground">{t("scheduleUx.createGuide")}</p>
          <DialogFooter>
            <Button
              disabled={
                busy ||
                reason.trim().length < 3 ||
                weightBasisNote.trim().length < 3 ||
                !siteManager ||
                workingDays.length === 0 ||
                Math.abs(
                  lines
                    .filter((x) => x.isLeaf && !x.isMilestone)
                    .reduce((sum, x) => sum + x.weight, 0) - 100,
                ) > 0.0001 ||
                lines.some(
                  (x) =>
                    !Number.isFinite(x.weight) ||
                    (x.isLeaf && !x.isMilestone && x.weight <= 0) ||
                    x.plannedStart > x.plannedEnd ||
                    x.plannedStart < x.crmStart ||
                    x.plannedEnd > x.crmEnd,
                )
              }
              onClick={() =>
                void act(
                  () =>
                    cmScheduleApi.create(projectId, {
                      changeReason: reason,
                      weightBasisNote,
                      siteManagerUserId: Number(siteManager),
                      workingDaysOfWeek: workingDays,
                      nonWorkingDates,
                      items: lines,
                    }),
                  () => setCreateOpen(false),
                )
              }
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("cmSchedule.review")}</DialogTitle>
            <DialogDescription>{t("cmSchedule.independent")}</DialogDescription>
          </DialogHeader>
          <Label htmlFor="cm-review-note">{t("cmSchedule.reason")}</Label>
          <Textarea
            id="cm-review-note"
            value={note}
            maxLength={2000}
            onChange={(e) => setNote(e.target.value)}
          />
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={busy || note.trim().length < 3}
              onClick={() =>
                decision &&
                void act(
                  () => cmScheduleApi.decide(projectId, decision, false, note),
                  () => setDecision(null),
                )
              }
            >
              {t("common.reject")}
            </Button>
            <Button
              disabled={busy}
              onClick={() =>
                decision &&
                void act(
                  () => cmScheduleApi.decide(projectId, decision, true, note),
                  () => setDecision(null),
                )
              }
            >
              {t("common.approve")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={recovery !== null} onOpenChange={(open) => !open && setRecovery(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("cmSchedule.recovery")}</DialogTitle>
            <DialogDescription>{t("cmSchedule.recoveryHint")}</DialogDescription>
          </DialogHeader>
          <Label htmlFor="cm-explanation">{t("cmSchedule.explanation")}</Label>
          <Textarea
            id="cm-explanation"
            value={explanation}
            maxLength={2000}
            onChange={(e) => setExplanation(e.target.value)}
          />
          <Label htmlFor="cm-recovery-plan">{t("cmSchedule.plan")}</Label>
          <Textarea
            id="cm-recovery-plan"
            value={plan}
            maxLength={4000}
            onChange={(e) => setPlan(e.target.value)}
          />
          <Label htmlFor="cm-recovery-date">{t("cmSchedule.due")}</Label>
          <LocalizedDateInput
            id="cm-recovery-date"
            value={due}
            onChange={(e) => setDue(e.target.value)}
          />
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button
              disabled={busy || explanation.trim().length < 3 || plan.trim().length < 3 || !due}
              onClick={() =>
                recovery &&
                void act(
                  () =>
                    cmScheduleApi.recover(projectId, recovery, {
                      explanation,
                      recoveryPlan: plan,
                      dueDate: due,
                    }),
                  () => setRecovery(null),
                )
              }
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}
