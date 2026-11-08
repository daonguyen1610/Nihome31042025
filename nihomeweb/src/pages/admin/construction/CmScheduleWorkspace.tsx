import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip, Legend } from "recharts";
import {
  cmScheduleApi,
  type CmScheduleWorkspace as Workspace,
  type CmScheduleLine,
  type CmBaseline,
  type CmSnapshot,
  type CmActualInput,
  type CmMailPreview,
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

export function CmScheduleWorkspace({ projectId }: { projectId: number }) {
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
  const [period, setPeriod] = useState("all");
  const [periodDate, setPeriodDate] = useState("");
  const [due, setDue] = useState("");
  const [actual, setActual] = useState<CmActualInput | null>(null);
  const [progress, setProgress] = useState(0);
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
  const anchor = periodDate || data?.asOfDate || "";
  const snapshots = useMemo(
    () =>
      data?.snapshots.filter(
        (x) =>
          x.baselineId === selected?.id &&
          (period === "all" ||
            x.asOfDate.slice(0, period === "day" ? 10 : period === "month" ? 7 : 4) ===
              anchor.slice(0, period === "day" ? 10 : period === "month" ? 7 : 4)),
      ) ?? [],
    [data, selected, period, anchor],
  );
  const chart = useMemo(() => {
    const points = new Map<
      number,
      { date: number; crmPercent?: number; planPercent?: number; actualPercent?: number }
    >();
    selected?.curve.forEach((x) => {
      const date = Date.parse(x.day + "T00:00:00Z");
      points.set(date, { date, crmPercent: x.crmPercent, planPercent: x.planPercent });
    });
    [...snapshots].reverse().forEach((x) => {
      const date = Date.parse(x.asOfDate + "T00:00:00Z");
      points.set(date, {
        date,
        crmPercent: x.crmPercent,
        planPercent: x.planPercent,
        actualPercent: x.actualPercent,
      });
    });
    if (period !== "all" && anchor) {
      const start =
        period === "year"
          ? anchor.slice(0, 4) + "-01-01"
          : period === "month"
            ? anchor.slice(0, 7) + "-01"
            : anchor;
      const day = new Date(start + "T00:00:00Z");
      const end =
        period === "year"
          ? Date.UTC(day.getUTCFullYear() + 1, 0, 1) - 86400000
          : period === "month"
            ? Date.UTC(day.getUTCFullYear(), day.getUTCMonth() + 1, 1) - 86400000
            : day.getTime();
      const curve = [...points.values()].sort((a, b) => a.date - b.date);
      for (const date of [day.getTime(), end]) {
        if (points.has(date) || curve.length === 0) continue;
        const before = [...curve].reverse().find((x) => x.date <= date) ?? curve[0];
        const after = curve.find((x) => x.date >= date) ?? curve[curve.length - 1];
        const ratio =
          before.date === after.date ? 0 : (date - before.date) / (after.date - before.date);
        points.set(date, {
          date,
          crmPercent:
            (before.crmPercent ?? 0) + ratio * ((after.crmPercent ?? 0) - (before.crmPercent ?? 0)),
          planPercent:
            (before.planPercent ?? 0) +
            ratio * ((after.planPercent ?? 0) - (before.planPercent ?? 0)),
        });
      }
      return [...points.values()]
        .filter((x) => x.date >= day.getTime() && x.date <= end)
        .sort((a, b) => a.date - b.date);
    }
    return [...points.values()].sort((a, b) => a.date - b.date);
  }, [selected, snapshots, period, anchor]);
  const percent = (value: number) => `${value.toLocaleString(lang, { maximumFractionDigits: 8 })}%`;
  const setLine = (id: number, patch: Partial<CmScheduleLine>) =>
    setLines((current) => current.map((row) => (row.taskId === id ? { ...row, ...patch } : row)));
  if (loading && !data) return <PageLoading />;
  return (
    <section className="space-y-5" data-testid="cm-schedule-workspace">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t("cmSchedule.title")}</h2>
          <p className="max-w-3xl text-sm text-muted-foreground">{t("cmSchedule.method")}</p>
        </div>
        <Button variant="outline" disabled={busy || loading} onClick={refresh}>
          {t("common.refresh")}
        </Button>
      </div>
      {error && (
        <div role="alert">
          <PageError message={error} onRetry={refresh} />
        </div>
      )}
      {data && (
        <>
          <p className="text-sm">
            {t("cmSchedule.policy", { threshold: data.thresholdPercent.toLocaleString(lang) })}
          </p>
          {data.unlinked.length > 0 && (
            <div className="space-y-2 rounded-md border p-3">
              <p className="font-medium">{t("cmSchedule.unlinked")}</p>
              <ul>
                {data.unlinked.map((x) => (
                  <li key={x.id}>
                    <Link
                      className="text-primary underline"
                      to={`/admin/construction/tasks?taskId=${x.id}`}
                    >
                      {x.taskCode} · {x.name}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          )}
          <div className="flex flex-wrap gap-2 items-center">
            <Select value={selected ? String(selected.id) : ""} onValueChange={setBaselineId}>
              <SelectTrigger className="w-full sm:w-72" aria-label={t("cmSchedule.version")}>
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
            {manage &&
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
          {!data.crmRevisionId && <p>{t("cmSchedule.noCrm")}</p>}
          {!selected ? (
            <p>{t("cmSchedule.noBaseline")}</p>
          ) : (
            <>
              <p className="text-sm">{selected.changeReason}</p>
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
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr>
                      <th className="p-2 text-left">{t("cmSchedule.task")}</th>
                      <th className="p-2 text-left">CRM</th>
                      <th className="p-2 text-left">CM</th>
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
                                {t("cmSchedule.bch")}:{" "}
                                {percent(
                                  data.actualInputs.find((input) => input.taskId === x.taskId)!
                                    .progressPercent,
                                )}
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
                          {x.crmStart} → {x.crmEnd}
                        </td>
                        <td className="p-2 whitespace-nowrap">
                          {x.plannedStart} → {x.plannedEnd}
                        </td>
                        <td className="p-2 text-right">
                          {x.isLeaf ? x.weight.toLocaleString(lang) : "—"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <h3 className="font-semibold">{t("cmSchedule.comparison")}</h3>
              <div className="flex flex-wrap items-center gap-3">
                <Select value={period} onValueChange={setPeriod}>
                  <SelectTrigger className="w-48" aria-label={t("cmSchedule.period")}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {["all", "day", "month", "year"].map((value) => (
                      <SelectItem key={value} value={value}>
                        {t(`cmSchedule.period.${value}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {period !== "all" && (
                  <LocalizedDateInput
                    aria-label={t("cmSchedule.periodDate")}
                    value={anchor}
                    onChange={(e) => setPeriodDate(e.target.value)}
                  />
                )}
              </div>
              {snapshots.length === 0 && <p>{t("cmSchedule.noActual")}</p>}
              {chart.length === 0 ? (
                <p>{t("cmSchedule.noActual")}</p>
              ) : (
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
                      <YAxis domain={[0, 100]} />
                      <Tooltip
                        labelFormatter={(value) =>
                          new Date(Number(value)).toLocaleDateString(lang, { timeZone: "UTC" })
                        }
                        formatter={(value) => percent(Number(value))}
                      />
                      <Legend />
                      <Line
                        connectNulls
                        type="linear"
                        dataKey="crmPercent"
                        name={t("cmSchedule.crm")}
                        stroke="var(--chart-1, #2563eb)"
                      />
                      <Line
                        connectNulls
                        type="linear"
                        dataKey="planPercent"
                        name={t("cmSchedule.cm")}
                        stroke="var(--chart-2, #d97706)"
                      />
                      <Line
                        connectNulls
                        type="linear"
                        dataKey="actualPercent"
                        name={t("cmSchedule.bch")}
                        stroke="var(--chart-3, #16a34a)"
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              )}
              <ul className="space-y-3">
                {snapshots.map((row) => (
                  <li key={row.id} className="space-y-2 rounded-md border p-3">
                    <div className="flex flex-wrap gap-3">
                      <span>{row.asOfDate}</span>
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
          <Label htmlFor="cm-actual-progress">{t("cmSchedule.bch")}</Label>
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
          <Label>{t("cmSchedule.siteManager")}</Label>
          <Select value={siteManager} onValueChange={setSiteManager}>
            <SelectTrigger aria-label={t("cmSchedule.siteManager")}>
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
          <div className="space-y-4">
            {lines.map((row) => (
              <div key={row.taskId} className="grid gap-2 border-t pt-3 sm:grid-cols-3">
                <p className="sm:col-span-3">
                  {row.wbs} · {row.name}
                </p>
                <LocalizedDateInput
                  aria-label={`${t("cmSchedule.start")} ${row.wbs}`}
                  min={row.crmStart}
                  max={row.crmEnd}
                  value={row.plannedStart}
                  onChange={(e) => setLine(row.taskId, { plannedStart: e.target.value })}
                />
                <LocalizedDateInput
                  aria-label={`${t("cmSchedule.end")} ${row.wbs}`}
                  min={row.crmStart}
                  max={row.crmEnd}
                  value={row.plannedEnd}
                  onChange={(e) => setLine(row.taskId, { plannedEnd: e.target.value })}
                />
                <MoneyInput
                  aria-label={`${t("cmSchedule.weight")} ${row.wbs}`}
                  decimalScale={4}
                  value={row.weight}
                  onChange={(weight) => setLine(row.taskId, { weight })}
                />
              </div>
            ))}
          </div>
          {error && (
            <p role="alert" className="text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <Button
              disabled={
                busy ||
                reason.trim().length < 3 ||
                !siteManager ||
                lines.some(
                  (x) =>
                    !Number.isFinite(x.weight) ||
                    x.weight <= 0 ||
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
                      siteManagerUserId: Number(siteManager),
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
