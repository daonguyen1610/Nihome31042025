import { useCallback, useEffect, useMemo, useState } from "react";
import { AlertTriangle, BookOpen, Calculator, CheckCircle2, Download, LockKeyhole, RefreshCw, Save, ShieldAlert, SlidersHorizontal, Target, UserRoundSearch } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { useAppSelector } from "@/store";
import {
  adminApi,
  type KpiDashboardResponse,
  type KpiDefinitionResponse,
  type KpiScoreStatus,
  type KpiUserOptionResponse,
} from "@/services/adminApi";

const statusStyle: Record<KpiScoreStatus, string> = {
  Available: "border-emerald-200 bg-emerald-50 text-emerald-700",
  MissingData: "border-amber-200 bg-amber-50 text-amber-800",
  MissingConfiguration: "border-rose-200 bg-rose-50 text-rose-700",
};

export interface KpiDashboardProps {
  mode?: "evaluation" | "configuration";
}

const KpiDashboard = ({ mode = "evaluation" }: KpiDashboardProps) => {
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUser = useAppSelector((state) => state.auth.user);
  const [searchParams] = useSearchParams();
  const canManage = has(ADMIN_PERMS.kpiManage);
  const canViewAll = has(ADMIN_PERMS.kpiViewAll);
  const canExport = has(ADMIN_PERMS.kpiExport);
  const now = new Date();
  const queryYear = Number(searchParams.get("year"));
  const queryMonth = Number(searchParams.get("month"));
  const [year, setYear] = useState(Number.isInteger(queryYear) && queryYear >= 2020 && queryYear <= 2100 ? queryYear : now.getFullYear());
  const [month, setMonth] = useState(Number.isInteger(queryMonth) && queryMonth >= 1 && queryMonth <= 12 ? queryMonth : now.getMonth() + 1);
  const [userId, setUserId] = useState(currentUser?.userId ?? 0);
  const [users, setUsers] = useState<KpiUserOptionResponse[]>([]);
  const [definitions, setDefinitions] = useState<KpiDefinitionResponse[]>([]);
  const [dashboard, setDashboard] = useState<KpiDashboardResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lockNote, setLockNote] = useState("");

  const number = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }), [lang]);
  const definitionReadiness = useMemo(() => {
    const groups = new Map<string, KpiDefinitionResponse[]>();
    definitions.forEach((definition) => {
      groups.set(definition.roleCode, [...(groups.get(definition.roleCode) ?? []), definition]);
    });
    return Array.from(groups.entries()).map(([roleCode, roleDefinitions]) => {
      const active = roleDefinitions.filter((definition) => definition.isActive);
      const weight = active.reduce((sum, definition) => sum + definition.weight, 0);
      const missingTargets = active.filter((definition) =>
        definition.requiresTarget && definition.targetValue == null).length;
      return {
        roleCode,
        weight,
        missingTargets,
        ready: Math.abs(weight - 1) <= 0.0001 && missingTargets === 0,
      };
    });
  }, [definitions]);

  const load = useCallback(async () => {
    if (mode === "configuration") {
      setLoading(true);
      setError(null);
      try {
        const response = await adminApi.listKpiDefinitions();
        setDefinitions(response.data);
      } catch (reason) {
        setError(extractApiError(reason));
      } finally {
        setLoading(false);
      }
      return;
    }
    if (!userId) return;
    setLoading(true);
    setError(null);
    try {
      const requests = [
        adminApi.getKpiDashboard({ year, month, userId }).catch(() => ({ data: null })),
        canViewAll ? adminApi.listKpiEligibleUsers() : Promise.resolve({ data: [] as KpiUserOptionResponse[] }),
      ] as const;
      const [dashboardResponse, usersResponse] = await Promise.all(requests);
      setDashboard(dashboardResponse.data);
      setUsers(usersResponse.data ?? []);
      if (canViewAll && !usersResponse.data.some((user) => user.userId === userId) && usersResponse.data[0]) {
        setUserId(usersResponse.data[0].userId);
      }
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [canViewAll, mode, month, userId, year]);

  useEffect(() => { void load(); }, [load]);

  const calculate = async () => {
    setBusy(true);
    try {
      const response = await adminApi.calculateKpi({ year, month, userId }, newIdempotencyKey());
      setDashboard(response.data);
      toast({ title: t("kpi.calculate.success") });
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    } finally { setBusy(false); }
  };

  const lock = async () => {
    if (!dashboard || lockNote.trim().length < 3) {
      toast({ title: t("kpi.lock.validation"), variant: "destructive" });
      return;
    }
    setBusy(true);
    try {
      const response = await adminApi.lockKpiPeriod(year, month, {
        userId,
        note: lockNote.trim(),
        rowVersion: dashboard.periodRowVersion,
      });
      setDashboard(response.data);
      setLockNote("");
      toast({ title: t("kpi.lock.success") });
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    } finally { setBusy(false); }
  };

  const saveDefinition = async (definition: KpiDefinitionResponse) => {
    setBusy(true);
    try {
      const response = await adminApi.updateKpiDefinition(definition.id, {
        weight: definition.weight,
        targetValue: definition.targetValue,
        minimumAcceptableScore: definition.minimumAcceptableScore,
        targetDirection: definition.targetDirection,
        isActive: definition.isActive,
        rowVersion: definition.rowVersion,
      });
      setDefinitions((current) => current.map((item) => item.id === response.data.id ? response.data : item));
      toast({ title: t("kpi.definition.saved") });
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    } finally { setBusy(false); }
  };

  const exportCsv = async () => {
    const response = await adminApi.exportKpi({ year, month, userId });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `kpi-${year}-${String(month).padStart(2, "0")}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={() => void load()} /></AdminLayout>;

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold">{t(mode === "configuration" ? "kpi.definition.title" : "kpi.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t(mode === "configuration" ? "kpi.definition.description" : "kpi.subtitle")}</p>
          </div>
          {mode === "evaluation" && <div className="flex flex-wrap gap-2">
            {canExport && dashboard && <Button variant="outline" onClick={() => void exportCsv()}><Download className="mr-2 h-4 w-4" />{t("kpi.export")}</Button>}
            {dashboard?.periodStatus !== "Locked" && <Button onClick={() => void calculate()} disabled={busy}><Calculator className="mr-2 h-4 w-4" />{t("kpi.calculate.action")}</Button>}
          </div>}
        </header>

        <KpiUsageGuide mode={mode} />

        {mode === "evaluation" && <>
        <section className="grid gap-3 border-y py-4 sm:grid-cols-2 lg:grid-cols-4">
          <div><Label>{t("kpi.period.year")}</Label><Input type="number" min={2020} max={2100} value={year} onChange={(event) => setYear(Number(event.target.value))} /></div>
          <div><Label>{t("kpi.period.month")}</Label><Select value={String(month)} onValueChange={(value) => setMonth(Number(value))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{Array.from({ length: 12 }, (_, index) => index + 1).map((value) => <SelectItem key={value} value={String(value)}>{value}</SelectItem>)}</SelectContent></Select></div>
          {canViewAll && <div className="sm:col-span-2"><Label>{t("kpi.user")}</Label><Select value={String(userId)} onValueChange={(value) => setUserId(Number(value))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent className="max-h-72">{users.map((user) => <SelectItem key={user.userId} value={String(user.userId)}>{user.userName} · {user.positionCode}</SelectItem>)}</SelectContent></Select></div>}
        </section>

        {!dashboard ? (
          <section className="border border-dashed p-8 text-center">
            <ShieldAlert className="mx-auto h-7 w-7 text-muted-foreground" />
            <p className="mt-3 text-sm text-muted-foreground">{t("kpi.empty")}</p>
            <Button className="mt-4" onClick={() => void calculate()} disabled={busy}>{t("kpi.calculate.action")}</Button>
          </section>
        ) : (
          <>
            <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Metric label={t("kpi.totalScore")} value={dashboard.isComplete && dashboard.totalScore != null ? number.format(dashboard.totalScore) : t("kpi.incomplete")} />
              <Metric label={t("kpi.availableWeight")} value={`${number.format(dashboard.availableWeight * 100)}%`} />
              <Metric label={t("kpi.period.status")} value={t(`kpi.periodStatus.${dashboard.periodStatus}`)} />
              <Metric label={t("kpi.timezone")} value={dashboard.timeZoneId} />
            </section>

            {dashboard.periodStatus === "Locked" && (
              <section className="border-l-2 border-slate-400 px-4 py-2 text-sm">
                <p className="font-medium">{t("kpi.lock.lockedBy", { name: dashboard.lockedByName ?? "—" })}</p>
                <p className="text-muted-foreground">{dashboard.lockNote ?? "—"}</p>
              </section>
            )}

            <section className="overflow-x-auto border-y">
              <table className="w-full min-w-[900px] text-sm">
                <thead><tr className="border-b text-left text-xs uppercase text-muted-foreground"><th className="px-3 py-3">{t("kpi.metric")}</th><th className="px-3 py-3">{t("kpi.source")}</th><th className="px-3 py-3">{t("kpi.weight")}</th><th className="px-3 py-3">{t("kpi.raw")}</th><th className="px-3 py-3">{t("kpi.score")}</th><th className="px-3 py-3">{t("kpi.status")}</th><th className="px-3 py-3">{t("kpi.version")}</th></tr></thead>
                <tbody>{dashboard.scores.map((score) => <tr key={score.id} className="border-b"><td className="px-3 py-3 font-medium">{t(score.nameKey)}<details className="mt-1 font-normal text-xs text-muted-foreground"><summary className="cursor-pointer">{t("kpi.evidence")}</summary><pre className="mt-1 max-w-sm whitespace-pre-wrap break-all">{score.evidenceJson}</pre></details></td><td className="px-3 py-3">{score.sourceModule}</td><td className="px-3 py-3">{number.format(score.weight * 100)}%</td><td className="px-3 py-3">{score.rawValue == null ? "—" : number.format(score.rawValue)}</td><td className="px-3 py-3">{score.score == null ? "—" : number.format(score.score)}</td><td className="px-3 py-3"><Badge variant="outline" className={statusStyle[score.status]}>{t(`kpi.scoreStatus.${score.status}`)}</Badge></td><td className="px-3 py-3">v{score.definitionVersion}</td></tr>)}</tbody>
              </table>
            </section>

            {canManage && dashboard.periodStatus === "Open" && <section className="flex flex-col gap-3 border-t pt-4 sm:flex-row sm:items-end"><div className="flex-1"><Label htmlFor="kpi-lock-note">{t("kpi.lock.note")}</Label><Input id="kpi-lock-note" maxLength={1000} value={lockNote} onChange={(event) => setLockNote(event.target.value)} /></div><Button variant="outline" onClick={() => void lock()} disabled={busy}><LockKeyhole className="mr-2 h-4 w-4" />{t("kpi.lock.action")}</Button></section>}
          </>
        )}
        </>}

        {mode === "configuration" && canManage && definitions.length > 0 && (
          <section className="space-y-3 border-t pt-5">
            <div><h2 className="text-lg font-semibold">{t("kpi.definition.listTitle")}</h2><p className="text-sm text-muted-foreground">{t("kpi.definition.description")}</p></div>
            <div className="grid gap-3 border-y border-slate-200 py-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
              {definitionReadiness.map((item) => (
                <div
                  key={item.roleCode}
                  className={`flex min-h-40 flex-col rounded-md border p-3 shadow-sm ${item.ready
                    ? "border-emerald-200 bg-emerald-50/40"
                    : "border-amber-200 bg-amber-50/40"
                  }`}
                >
                  <div>
                    <span className={`flex h-8 w-8 items-center justify-center rounded-md ${item.ready
                      ? "bg-emerald-100 text-emerald-700"
                      : "bg-amber-100 text-amber-700"
                    }`}>
                      {item.ready ? <CheckCircle2 className="h-4 w-4" /> : <AlertTriangle className="h-4 w-4" />}
                    </span>
                  </div>
                  <p className="mt-3 min-h-8 text-xs font-semibold leading-4">{item.roleCode.replace(/_/g, " ")}</p>
                  <p className="mt-1 text-xs leading-5 text-muted-foreground">
                    {t("kpi.guide.config.readiness", {
                      weight: number.format(item.weight * 100),
                      missing: item.missingTargets,
                    })}
                  </p>
                  <div className="mb-3 mt-2 h-1.5 overflow-hidden rounded-full bg-white/80">
                    <div
                      className={`h-full rounded-full ${item.ready ? "bg-emerald-500" : "bg-amber-500"}`}
                      style={{ width: `${Math.min(Math.max(item.weight * 100, 0), 100)}%` }}
                    />
                  </div>
                  <Badge variant="outline" className={item.ready ? "mt-auto w-fit border-emerald-200 bg-white text-emerald-700" : "mt-auto w-fit border-amber-200 bg-white text-amber-800"}>
                    {t(item.ready ? "kpi.guide.config.ready" : "kpi.guide.config.needsAttention")}
                  </Badge>
                </div>
              ))}
            </div>
            <div className="overflow-x-auto"><table className="w-full min-w-[1200px] text-sm"><thead><tr className="border-b text-left text-xs uppercase text-muted-foreground"><th className="px-3 py-3">{t("kpi.metric")}</th><th className="px-3 py-3">{t("kpi.role")}</th><th className="px-3 py-3">{t("kpi.weight")}</th><th className="px-3 py-3">{t("kpi.target")}</th><th className="px-3 py-3">{t("kpi.direction")}</th><th className="px-3 py-3">{t("kpi.threshold")}</th><th className="px-3 py-3">{t("kpi.active")}</th><th className="px-3 py-3">{t("kpi.version")}</th><th /></tr></thead><tbody>{definitions.map((definition) => <tr key={definition.id} className="border-b"><td className="px-3 py-3">{t(definition.nameKey)}</td><td className="px-3 py-3">{definition.roleCode}</td><td className="px-3 py-3"><Input className="w-24" type="number" min={0.01} max={1} step={0.01} value={definition.weight} onChange={(event) => setDefinitions((current) => current.map((item) => item.id === definition.id ? { ...item, weight: Number(event.target.value) } : item))} /></td><td className="px-3 py-3"><Input className="w-36" type="number" min={0} value={definition.targetValue ?? ""} onChange={(event) => setDefinitions((current) => current.map((item) => item.id === definition.id ? { ...item, targetValue: event.target.value === "" ? null : Number(event.target.value) } : item))} /></td><td className="px-3 py-3"><Select value={definition.targetDirection} onValueChange={(value) => setDefinitions((current) => current.map((item) => item.id === definition.id ? { ...item, targetDirection: value as KpiDefinitionResponse["targetDirection"] } : item))}><SelectTrigger className="w-44"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="HigherIsBetter">{t("kpi.direction.HigherIsBetter")}</SelectItem><SelectItem value="LowerIsBetter">{t("kpi.direction.LowerIsBetter")}</SelectItem></SelectContent></Select></td><td className="px-3 py-3"><Input className="w-28" type="number" min={0} max={100} value={definition.minimumAcceptableScore ?? ""} onChange={(event) => setDefinitions((current) => current.map((item) => item.id === definition.id ? { ...item, minimumAcceptableScore: event.target.value === "" ? null : Number(event.target.value) } : item))} /></td><td className="px-3 py-3"><Checkbox checked={definition.isActive} onCheckedChange={(value) => setDefinitions((current) => current.map((item) => item.id === definition.id ? { ...item, isActive: value === true } : item))} /></td><td className="px-3 py-3">v{definition.version}</td><td className="px-3 py-3 text-right"><Button size="sm" variant="ghost" onClick={() => void saveDefinition(definition)} disabled={busy}><Save className="mr-1 h-4 w-4" />{t("common.save")}</Button></td></tr>)}</tbody></table></div>
          </section>
        )}
      </div>
    </AdminLayout>
  );
};

const Metric = ({ label, value }: { label: string; value: string }) => <div className="border-l-2 border-primary px-4 py-2"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 text-xl font-semibold">{value}</p></div>;

const KpiUsageGuide = ({ mode }: { mode: "evaluation" | "configuration" }) => {
  const { t } = useI18n();
  const isEvaluation = mode === "evaluation";
  const steps = isEvaluation
    ? [
        { key: "select", icon: UserRoundSearch },
        { key: "calculate", icon: Calculator },
        { key: "review", icon: CheckCircle2 },
        { key: "lock", icon: LockKeyhole },
      ]
    : [
        { key: "weight", icon: SlidersHorizontal },
        { key: "target", icon: Target },
        { key: "direction", icon: RefreshCw },
        { key: "threshold", icon: ShieldAlert },
        { key: "active", icon: CheckCircle2 },
      ];
  const statusIcons = {
    Available: CheckCircle2,
    MissingData: AlertTriangle,
    MissingConfiguration: ShieldAlert,
  } satisfies Record<KpiScoreStatus, typeof CheckCircle2>;

  return (
    <Accordion type="single" collapsible defaultValue="usage" className="border-y border-slate-200 bg-slate-50/70 px-4 sm:px-5">
      <AccordionItem value="usage" className="border-0">
        <AccordionTrigger className="py-4 text-left hover:no-underline">
          <span className="flex items-center gap-3">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-primary/20 bg-primary/10 text-primary">
              <BookOpen className="h-4 w-4" />
            </span>
            <span>
              <span className="block text-sm font-semibold">{t("kpi.guide.title")}</span>
              <span className="mt-0.5 block text-xs font-normal leading-5 text-muted-foreground">
                {t(isEvaluation ? "kpi.guide.evaluation.summary" : "kpi.guide.config.summary")}
              </span>
            </span>
          </span>
        </AccordionTrigger>
        <AccordionContent className="pb-5">
          <div className={isEvaluation ? "grid gap-3 md:grid-cols-2 xl:grid-cols-4" : "grid gap-3 md:grid-cols-2 xl:grid-cols-5"}>
            {steps.map(({ key, icon: StepIcon }, index) => (
              <div key={key} className="min-h-36 rounded-md border border-slate-200 bg-background p-4 shadow-sm">
                <div className="flex items-center justify-between gap-3">
                  <span className="flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary">
                    <StepIcon className="h-4 w-4" />
                  </span>
                  <span className="text-xs font-semibold text-slate-400">{String(index + 1).padStart(2, "0")}</span>
                </div>
                <p className="mt-3 text-sm font-semibold leading-5">{t(`kpi.guide.${isEvaluation ? "evaluation" : "config"}.${key}.title`)}</p>
                <p className="mt-1.5 text-xs leading-5 text-muted-foreground">{t(`kpi.guide.${isEvaluation ? "evaluation" : "config"}.${key}.body`)}</p>
              </div>
            ))}
          </div>
          {isEvaluation && (
            <div className="mt-4 border-t border-slate-200 pt-4">
              <div className="grid gap-3 md:grid-cols-3">
              {(["Available", "MissingData", "MissingConfiguration"] as KpiScoreStatus[]).map((status) => {
                const StatusIcon = statusIcons[status];
                return (
                  <div key={status} className="flex min-w-0 items-start gap-3 rounded-md border border-slate-200 bg-background p-3">
                    <StatusIcon className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" />
                    <div className="min-w-0">
                      <Badge variant="outline" className={statusStyle[status]}>{t(`kpi.scoreStatus.${status}`)}</Badge>
                      <p className="mt-1.5 text-xs leading-5 text-muted-foreground">{t(`kpi.guide.status.${status}`)}</p>
                    </div>
                  </div>
                );
              })}
              </div>
              <div className="mt-3 flex items-start gap-3 border-l-2 border-slate-400 bg-slate-100/80 px-3 py-2.5 text-xs leading-5 text-muted-foreground">
                <LockKeyhole className="mt-0.5 h-4 w-4 shrink-0 text-slate-600" />
                <p><strong className="text-foreground">{t("kpi.guide.lock.title")}:</strong> {t("kpi.guide.lock.body")}</p>
              </div>
            </div>
          )}
        </AccordionContent>
      </AccordionItem>
    </Accordion>
  );
};

export default KpiDashboard;
