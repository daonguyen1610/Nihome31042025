import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CalendarClock, CheckCircle2, ChevronLeft, ChevronRight, Diamond, Filter, Loader2, Pencil, Plus, RefreshCw } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  DESIGN_SCHEDULE_PHASE_CODES,
  DESIGN_SCHEDULE_STATUSES,
  type DesignProjectResponse,
  type DesignSchedulePhaseResponse,
  type DesignScheduleQuery,
  type DesignScheduleResponse,
  type DesignScheduleTaskResponse,
  type MasterDataOption,
  type OperationalProjectTeamResponse,
} from "@/services/adminApi";
import { DesignSchedulePhaseDialog } from "./DesignSchedulePhaseDialog";
import { DesignScheduleTaskDialog } from "./DesignScheduleTaskDialog";
import { DesignScheduleTimeline } from "./DesignScheduleTimeline";

interface Props { project: DesignProjectResponse }

const PAGE_SIZE = 10;
const DEPENDENCY_PAGE_SIZE = 100;

const loadAllScheduleTasks = async (projectId: number) => {
  const first = await adminApi.getDesignSchedule(projectId, { page: 1, pageSize: DEPENDENCY_PAGE_SIZE });
  const pageCount = Math.ceil(first.data.tasks.totalCount / DEPENDENCY_PAGE_SIZE);
  if (pageCount <= 1) return first.data.tasks.items;
  const remaining = await Promise.all(Array.from({ length: pageCount - 1 }, (_, index) =>
    adminApi.getDesignSchedule(projectId, { page: index + 2, pageSize: DEPENDENCY_PAGE_SIZE })));
  return [first.data.tasks.items, ...remaining.map((response) => response.data.tasks.items)].flat();
};

const statusClass: Record<string, string> = {
  NotStarted: "border-slate-200 bg-slate-50 text-slate-700",
  InProgress: "border-sky-200 bg-sky-50 text-sky-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
  OnHold: "border-amber-200 bg-amber-50 text-amber-700",
  WaitingForDepartment: "border-violet-200 bg-violet-50 text-violet-700",
};

export const DesignScheduleTab = ({ project }: Props) => {
  const { t, lang } = useI18n();
  const operationalProjectId = project.operationalProjectId;
  const [schedule, setSchedule] = useState<DesignScheduleResponse | null>(null);
  const [dependencyTasks, setDependencyTasks] = useState<DesignScheduleTaskResponse[]>([]);
  const [team, setTeam] = useState<OperationalProjectTeamResponse | null>(null);
  const [departments, setDepartments] = useState<MasterDataOption[]>([]);
  const [query, setQuery] = useState<DesignScheduleQuery>({ page: 1, pageSize: PAGE_SIZE });
  const [loading, setLoading] = useState(Boolean(operationalProjectId));
  const [error, setError] = useState<string | null>(null);
  const [initializing, setInitializing] = useState(false);
  const [editingPhase, setEditingPhase] = useState<DesignSchedulePhaseResponse | null>(null);
  const [editingTask, setEditingTask] = useState<DesignScheduleTaskResponse | null>(null);
  const [taskDialogOpen, setTaskDialogOpen] = useState(false);
  const [initialPhaseId, setInitialPhaseId] = useState<number | undefined>();
  const initializeKey = useRef(newIdempotencyKey());

  const load = useCallback(async () => {
    if (!operationalProjectId) { setLoading(false); return; }
    setLoading(true);
    setError(null);
    try {
      const [scheduleResponse, allTasks, teamResponse, departmentResponse] = await Promise.all([
        adminApi.getDesignSchedule(operationalProjectId, query),
        loadAllScheduleTasks(operationalProjectId),
        adminApi.getOperationalProjectTeam(operationalProjectId),
        adminApi.getMasterDataOptions("project-department"),
      ]);
      setSchedule(scheduleResponse.data);
      setDependencyTasks(allTasks);
      setTeam(teamResponse.data);
      setDepartments(departmentResponse.data ?? []);
    } catch (loadError) { setError(extractApiError(loadError)); }
    finally { setLoading(false); }
  }, [operationalProjectId, query]);

  useEffect(() => { void load(); }, [load]);

  const formatDate = (value?: string | null) => {
    if (!value) return t("designProjects.schedule.value.none");
    return new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeZone: "UTC" }).format(new Date(`${value.slice(0, 10)}T00:00:00Z`));
  };
  const departmentLabel = (code: string) => {
    const option = departments.find((item) => item.code === code);
    return option?.labelKey ? t(option.labelKey) : option?.name ?? code;
  };
  const memberLabel = (id: number) => team?.members.find((member) => member.id === id)?.userName ?? t("designProjects.schedule.value.unknown");
  const totalPages = Math.max(1, Math.ceil((schedule?.tasks.totalCount ?? 0) / PAGE_SIZE));
  const hasFilters = Boolean(query.phase || query.assigneeMemberId || query.departmentCode || query.status || query.plannedFrom || query.plannedTo || query.overdueOnly);
  const activeMembers = useMemo(() => team?.members.filter((member) => member.isActive) ?? [], [team]);

  const initialize = async () => {
    if (!operationalProjectId) return;
    setInitializing(true);
    setError(null);
    try {
      await adminApi.initializeDesignSchedule(operationalProjectId, { phases: [
        { code: "Concept", weight: 34 },
        { code: "BasicDesign", weight: 33 },
        { code: "ShopDrawing", weight: 33 },
      ] }, initializeKey.current);
      initializeKey.current = newIdempotencyKey();
      await load();
    } catch (initializeError) { setError(extractApiError(initializeError)); }
    finally { setInitializing(false); }
  };

  const updateQuery = (patch: Partial<DesignScheduleQuery>) => setQuery((current) => ({ ...current, ...patch, page: patch.page ?? 1, pageSize: PAGE_SIZE }));
  const clearFilters = () => setQuery({ page: 1, pageSize: PAGE_SIZE });
  const openCreateTask = (phaseId?: number) => { setEditingTask(null); setInitialPhaseId(phaseId); setTaskDialogOpen(true); };
  const openEditTask = (task: DesignScheduleTaskResponse) => { setEditingTask(task); setInitialPhaseId(task.phaseId); setTaskDialogOpen(true); };
  const reloadPhaseVersion = async () => {
    if (!editingPhase) return undefined;
    const response = await adminApi.getDesignSchedule(operationalProjectId, { page: 1, pageSize: 1 });
    return response.data.phases.find((phase) => phase.id === editingPhase.id);
  };
  const reloadTaskVersion = async () => {
    if (!editingTask) return undefined;
    const tasks = await loadAllScheduleTasks(operationalProjectId);
    setDependencyTasks(tasks);
    return tasks.find((task) => task.id === editingTask.id);
  };

  if (!operationalProjectId) return <section className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center"><CalendarClock className="mx-auto h-9 w-9 text-slate-400" /><h2 className="mt-3 font-semibold text-slate-900">{t("designProjects.schedule.unlinkedTitle")}</h2><p className="mx-auto mt-1 max-w-xl text-sm text-slate-500">{t("designProjects.schedule.unlinkedDescription")}</p></section>;
  if (loading && !schedule) return <div className="flex flex-col items-center justify-center gap-3 rounded-lg border bg-white py-16 text-sm text-slate-500"><Loader2 className="h-7 w-7 animate-spin" /><span>{t("designProjects.schedule.loading")}</span></div>;
  if (error && !schedule) return <div className="rounded-lg border border-rose-200 bg-rose-50 p-5 text-sm text-rose-700"><p>{error}</p><Button variant="outline" className="mt-3 min-h-11" onClick={() => void load()}><RefreshCw className="mr-1.5 h-4 w-4" />{t("common.retry")}</Button></div>;
  if (!schedule) return null;

  if (!schedule.phases.length) return <section className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center"><CalendarClock className="mx-auto h-9 w-9 text-slate-400" /><h2 className="mt-3 font-semibold text-slate-900">{t("designProjects.schedule.emptyTitle")}</h2><p className="mx-auto mt-1 max-w-xl text-sm text-slate-500">{t("designProjects.schedule.emptyDescription")}</p>{schedule.canManage ? <Button className="mt-5 min-h-11" disabled={initializing} onClick={() => void initialize()}>{initializing ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Plus className="mr-1.5 h-4 w-4" />}{t("designProjects.schedule.action.initialize")}</Button> : <p className="mt-4 text-sm font-medium text-slate-600">{t("designProjects.schedule.readOnly")}</p>}{error ? <p className="mt-3 text-sm text-rose-600" role="alert">{error}</p> : null}</section>;

  return (
    <div className="space-y-4" data-testid="design-schedule-tab">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="flex items-center gap-2 text-base font-semibold text-slate-900"><CalendarClock className="h-5 w-5 text-slate-500" />{t("designProjects.schedule.title")}</h2><p className="mt-1 text-sm text-slate-500">{t("designProjects.schedule.description")}</p></div>{schedule.canManage ? <Button className="min-h-11" onClick={() => openCreateTask()}><Plus className="mr-1.5 h-4 w-4" />{t("designProjects.schedule.action.createTask")}</Button> : <Badge variant="outline">{t("designProjects.schedule.readOnly")}</Badge>}</div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2"><div className="rounded-lg bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">{t("designProjects.schedule.rollup.project")}</p><div className="mt-2 flex items-end justify-between gap-3"><strong className="text-2xl text-slate-900">{schedule.progressPercent == null ? "—" : `${schedule.progressPercent}%`}</strong><Badge variant="outline" className={schedule.baselineReady ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-amber-200 bg-amber-50 text-amber-700"}>{t(schedule.baselineReady ? "designProjects.schedule.baseline.ready" : "designProjects.schedule.baseline.incomplete")}</Badge></div><Progress className="mt-3 h-2" value={schedule.progressPercent ?? 0} /></div><div className="rounded-lg bg-slate-50 p-4"><p className="text-xs uppercase tracking-wide text-slate-500">{t("designProjects.schedule.rollup.policy")}</p><p className="mt-2 font-mono text-sm text-slate-700">{schedule.rollupPolicyVersion}</p><p className="mt-2 text-xs text-slate-500">{t("designProjects.schedule.rollup.explanation")}</p></div></div>
      </section>

      <section className="grid gap-3 lg:grid-cols-3">
        {schedule.phases.map((phase) => <article key={phase.id} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"><div className="flex items-start justify-between gap-2"><div><h3 className="font-semibold text-slate-900">{t(`designProjects.stage.${phase.code}`)}</h3><Badge variant="outline" className={`mt-2 ${statusClass[phase.status]}`}>{t(`designProjects.schedule.status.${phase.status}`)}</Badge>{phase.overdue ? <Badge variant="outline" className="ml-1 border-rose-200 bg-rose-50 text-rose-700">{t("designProjects.schedule.overdue")}</Badge> : null}</div>{schedule.canManage ? <Button variant="ghost" size="icon" className="min-h-11 min-w-11" aria-label={t("designProjects.schedule.action.editPhase")} onClick={() => setEditingPhase(phase)}><Pencil className="h-4 w-4" /></Button> : null}</div><dl className="mt-4 grid grid-cols-2 gap-3 text-sm"><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.plannedDates")}</dt><dd>{formatDate(phase.plannedStart)} – {formatDate(phase.plannedEnd)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.actualDates")}</dt><dd>{formatDate(phase.actualStart)} – {formatDate(phase.actualEnd)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.weight")}</dt><dd>{phase.weight}%</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.rollup.phase")}</dt><dd>{phase.rolledUpProgressPercent == null ? "—" : `${phase.rolledUpProgressPercent}%`}</dd></div></dl><Progress className="mt-3 h-2" value={phase.rolledUpProgressPercent ?? 0} /><div className="mt-3 flex items-center justify-between"><span className="text-xs text-slate-500">{t(phase.baselineReady ? "designProjects.schedule.baseline.ready" : "designProjects.schedule.baseline.incomplete")}</span>{schedule.canManage ? <Button size="sm" variant="outline" className="min-h-10" onClick={() => openCreateTask(phase.id)}><Plus className="mr-1 h-4 w-4" />{t("designProjects.schedule.action.add")}</Button> : null}</div></article>)}
      </section>

      <DesignScheduleTimeline tasks={dependencyTasks} members={team?.members ?? []} />

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2"><h3 className="flex items-center gap-2 font-semibold text-slate-900"><Filter className="h-4 w-4 text-slate-500" />{t("designProjects.schedule.filters.title")}</h3>{hasFilters ? <Button variant="ghost" size="sm" className="min-h-10" onClick={clearFilters}>{t("designProjects.schedule.action.clearFilters")}</Button> : null}</div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div><Label>{t("designProjects.schedule.field.phase")}</Label><Select value={query.phase ?? "all"} onValueChange={(value) => updateQuery({ phase: value === "all" ? undefined : value as DesignScheduleQuery["phase"] })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("designProjects.schedule.filter.allPhases")}</SelectItem>{DESIGN_SCHEDULE_PHASE_CODES.map((phase) => <SelectItem key={phase} value={phase}>{t(`designProjects.stage.${phase}`)}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>{t("designProjects.schedule.field.assignee")}</Label><Select value={query.assigneeMemberId ? String(query.assigneeMemberId) : "all"} onValueChange={(value) => updateQuery({ assigneeMemberId: value === "all" ? undefined : Number(value) })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("designProjects.schedule.filter.allAssignees")}</SelectItem>{activeMembers.map((member) => <SelectItem key={member.id} value={String(member.id)}>{member.userName}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>{t("designProjects.schedule.field.department")}</Label><Select value={query.departmentCode ?? "all"} onValueChange={(value) => updateQuery({ departmentCode: value === "all" ? undefined : value })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("designProjects.schedule.filter.allDepartments")}</SelectItem>{departments.filter((item) => item.isActive).map((item) => <SelectItem key={item.id} value={item.code}>{item.labelKey ? t(item.labelKey) : item.name}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>{t("designProjects.schedule.field.status")}</Label><Select value={query.status ?? "all"} onValueChange={(value) => updateQuery({ status: value === "all" ? undefined : value as DesignScheduleQuery["status"] })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("designProjects.schedule.filter.allStatuses")}</SelectItem>{DESIGN_SCHEDULE_STATUSES.map((status) => <SelectItem key={status} value={status}>{t(`designProjects.schedule.status.${status}`)}</SelectItem>)}</SelectContent></Select></div>
          <div><Label htmlFor="schedule-planned-from">{t("designProjects.schedule.filter.plannedFrom")}</Label><Input id="schedule-planned-from" className="mt-1 min-h-11" type="date" value={query.plannedFrom ?? ""} onChange={(event) => updateQuery({ plannedFrom: event.target.value || undefined })} /></div>
          <div><Label htmlFor="schedule-planned-to">{t("designProjects.schedule.filter.plannedTo")}</Label><Input id="schedule-planned-to" className="mt-1 min-h-11" type="date" min={query.plannedFrom} value={query.plannedTo ?? ""} onChange={(event) => updateQuery({ plannedTo: event.target.value || undefined })} /></div>
          <label className="flex min-h-11 items-center gap-3 self-end rounded-md border px-3 text-sm"><Checkbox checked={query.overdueOnly ?? false} onCheckedChange={(checked) => updateQuery({ overdueOnly: checked === true || undefined })} />{t("designProjects.schedule.filter.overdueOnly")}</label>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm"><div className="flex flex-wrap items-center justify-between gap-2 border-b p-4"><div><h3 className="font-semibold text-slate-900">{t("designProjects.schedule.tasks.title")}</h3><p className="text-sm text-slate-500">{t("designProjects.schedule.tasks.total", { count: schedule.tasks.totalCount })}</p></div>{loading ? <Loader2 className="h-5 w-5 animate-spin text-slate-400" /> : null}</div>
        {error ? <div className="m-4 rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700" role="alert"><p>{error}</p><Button size="sm" variant="outline" className="mt-2 min-h-10" onClick={() => void load()}><RefreshCw className="mr-1 h-4 w-4" />{t("common.retry")}</Button></div> : null}
        {!schedule.tasks.items.length ? <div className="p-10 text-center text-sm text-slate-500">{t(hasFilters ? "designProjects.schedule.tasks.emptyFiltered" : "designProjects.schedule.tasks.empty")}</div> : <div className="divide-y">{schedule.tasks.items.map((task) => <article key={task.id} className="p-4"><div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2">{task.isMilestone ? <Diamond className="h-4 w-4 fill-amber-400 text-amber-600" /> : null}<strong className="text-slate-900">{task.name}</strong><Badge variant="outline" className={statusClass[task.status]}>{t(`designProjects.schedule.status.${task.status}`)}</Badge>{task.overdue ? <Badge variant="outline" className="border-rose-200 bg-rose-50 text-rose-700">{t("designProjects.schedule.overdue")}</Badge> : null}</div><p className="mt-1 font-mono text-xs text-slate-500">{task.code} · {t(`designProjects.stage.${task.phaseCode}`)}</p></div>{schedule.canManage ? <Button variant="ghost" size="icon" className="min-h-11 min-w-11" aria-label={t("designProjects.schedule.action.editTask")} onClick={() => openEditTask(task)}><Pencil className="h-4 w-4" /></Button> : null}</div><dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-5"><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.assignee")}</dt><dd>{memberLabel(task.assigneeMemberId)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.department")}</dt><dd>{departmentLabel(task.departmentCode)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.plannedDates")}</dt><dd>{formatDate(task.plannedStart)} – {formatDate(task.plannedEnd)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.actualDates")}</dt><dd>{formatDate(task.actualStart)} – {formatDate(task.actualEnd)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.schedule.field.progressWeight")}</dt><dd>{task.progressPercent}% · {task.weight}%</dd></div></dl>{task.predecessorTaskIds.length ? <p className="mt-2 text-xs text-slate-500">{t("designProjects.schedule.field.predecessors")}: {task.predecessorTaskIds.map((id) => dependencyTasks.find((item) => item.id === id)?.code ?? `#${id}`).join(", ")}</p> : null}<Progress className="mt-3 h-1.5" value={task.progressPercent} /></article>)}</div>}
        <div className="flex items-center justify-between border-t p-4"><span className="text-sm text-slate-500">{t("designProjects.schedule.pagination", { page: schedule.tasks.page, totalPages })}</span><div className="flex gap-2"><Button variant="outline" size="icon" className="min-h-11 min-w-11" aria-label={t("designProjects.schedule.action.previous")} disabled={schedule.tasks.page <= 1 || loading} onClick={() => updateQuery({ page: schedule.tasks.page - 1 })}><ChevronLeft className="h-4 w-4" /></Button><Button variant="outline" size="icon" className="min-h-11 min-w-11" aria-label={t("common.next")} disabled={schedule.tasks.page >= totalPages || loading} onClick={() => updateQuery({ page: schedule.tasks.page + 1 })}><ChevronRight className="h-4 w-4" /></Button></div></div>
      </section>

      <div className="flex items-center gap-2 text-xs text-slate-500"><CheckCircle2 className="h-4 w-4" /><span>{t("designProjects.schedule.rollup.audit")}</span></div>
      <DesignSchedulePhaseDialog operationalProjectId={operationalProjectId} phase={editingPhase} open={Boolean(editingPhase)} onOpenChange={(open) => !open && setEditingPhase(null)} onSaved={load} onReload={reloadPhaseVersion} />
      <DesignScheduleTaskDialog operationalProjectId={operationalProjectId} phases={schedule.phases} tasks={dependencyTasks} departments={departments} members={team?.members ?? []} initialPhaseId={initialPhaseId} task={editingTask} open={taskDialogOpen} onOpenChange={setTaskDialogOpen} onSaved={load} onReload={reloadTaskVersion} />
    </div>
  );
};
