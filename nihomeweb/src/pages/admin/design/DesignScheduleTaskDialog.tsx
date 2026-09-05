import { useEffect, useMemo, useRef, useState } from "react";
import { Loader2, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  DESIGN_SCHEDULE_STATUSES,
  type DesignSchedulePhaseResponse,
  type DesignScheduleStatus,
  type DesignScheduleTaskResponse,
  type MasterDataOption,
  type OperationalProjectMemberResponse,
  type UpsertDesignScheduleTaskRequest,
} from "@/services/adminApi";

interface Props {
  operationalProjectId: number;
  phases: DesignSchedulePhaseResponse[];
  tasks: DesignScheduleTaskResponse[];
  departments: MasterDataOption[];
  members: OperationalProjectMemberResponse[];
  initialPhaseId?: number;
  task: DesignScheduleTaskResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<void> | void;
  onReload: () => Promise<DesignScheduleTaskResponse | undefined>;
}

const transitions: Record<DesignScheduleStatus, DesignScheduleStatus[]> = {
  NotStarted: ["NotStarted", "InProgress", "OnHold", "WaitingForDepartment"],
  InProgress: ["InProgress", "Completed", "OnHold", "WaitingForDepartment"],
  OnHold: ["OnHold", "InProgress", "WaitingForDepartment"],
  WaitingForDepartment: ["WaitingForDepartment", "InProgress", "OnHold"],
  Completed: ["Completed"],
};

interface TaskForm extends UpsertDesignScheduleTaskRequest { phaseId: number }

const emptyForm = (phaseId: number): TaskForm => ({
  phaseId,
  code: "",
  name: "",
  departmentCode: "",
  assigneeMemberId: 0,
  isMilestone: false,
  plannedStart: "",
  plannedEnd: "",
  actualStart: null,
  actualEnd: null,
  status: "NotStarted",
  progressPercent: 0,
  weight: 1,
  predecessorTaskIds: [],
});

const taskForm = (task: DesignScheduleTaskResponse): TaskForm => ({
  phaseId: task.phaseId,
  code: task.code,
  name: task.name,
  departmentCode: task.departmentCode,
  assigneeMemberId: task.assigneeMemberId,
  isMilestone: task.isMilestone,
  plannedStart: task.plannedStart.slice(0, 10),
  plannedEnd: task.plannedEnd.slice(0, 10),
  actualStart: task.actualStart?.slice(0, 10) ?? null,
  actualEnd: task.actualEnd?.slice(0, 10) ?? null,
  status: task.status,
  progressPercent: task.progressPercent,
  weight: task.weight,
  predecessorTaskIds: task.predecessorTaskIds,
  rowVersion: task.rowVersion,
});

export const DesignScheduleTaskDialog = ({ operationalProjectId, phases, tasks, departments, members, initialPhaseId, task, open, onOpenChange, onSaved, onReload }: Props) => {
  const { t } = useI18n();
  const [form, setForm] = useState<TaskForm>(emptyForm(initialPhaseId ?? phases[0]?.id ?? 0));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [baseStatus, setBaseStatus] = useState<DesignScheduleStatus>("NotStarted");
  const idempotencyKey = useRef("");
  const activeMembers = useMemo(() => members.filter((member) => member.isActive), [members]);
  const availablePredecessors = useMemo(() => tasks.filter((item) => item.id !== task?.id), [tasks, task]);

  useEffect(() => {
    if (!open) return;
    setForm(task ? taskForm(task) : emptyForm(initialPhaseId ?? phases[0]?.id ?? 0));
    setError(null);
    setConflict(false);
    setBaseStatus(task?.status ?? "NotStarted");
    idempotencyKey.current = newIdempotencyKey();
  }, [open, task, initialPhaseId, phases]);

  const createsCycle = () => {
    if (!task) return false;
    const graph = new Map(tasks.map((item) => [item.id, item.predecessorTaskIds]));
    graph.set(task.id, form.predecessorTaskIds);
    const visiting = new Set<number>();
    const visited = new Set<number>();
    const visit = (id: number): boolean => {
      if (visiting.has(id)) return true;
      if (visited.has(id)) return false;
      visiting.add(id);
      for (const predecessorId of graph.get(id) ?? []) if (visit(predecessorId)) return true;
      visiting.delete(id);
      visited.add(id);
      return false;
    };
    return visit(task.id);
  };

  const validate = () => {
    const code = form.code.trim(), name = form.name.trim();
    if (!code || code.length > 80) return t("designProjects.schedule.validation.code");
    if (!name || name.length > 300) return t("designProjects.schedule.validation.name");
    if (!departments.some((department) => department.code === form.departmentCode && department.isActive)) return t("designProjects.schedule.validation.department");
    if (!activeMembers.some((member) => member.id === form.assigneeMemberId)) return t("designProjects.schedule.validation.assignee");
    if (!form.plannedStart || !form.plannedEnd) return t("designProjects.schedule.validation.plannedRequired");
    if (form.plannedEnd < form.plannedStart) return t("designProjects.schedule.validation.plannedOrder");
    if (form.isMilestone && form.plannedStart !== form.plannedEnd) return t("designProjects.schedule.validation.milestoneDates");
    if (form.actualEnd && !form.actualStart) return t("designProjects.schedule.validation.actualStartRequired");
    if (form.actualStart && form.actualEnd && form.actualEnd < form.actualStart) return t("designProjects.schedule.validation.actualOrder");
    if (form.progressPercent < 0 || form.progressPercent > 100) return t("designProjects.schedule.validation.progressRange");
    if (form.weight < 1 || form.weight > 100) return t("designProjects.schedule.validation.weightRange");
    if (form.status === "NotStarted" && (form.actualStart || form.actualEnd || form.progressPercent !== 0)) return t("designProjects.schedule.validation.notStarted");
    if (form.status === "InProgress" && !form.actualStart) return t("designProjects.schedule.validation.inProgress");
    if (form.status === "Completed" && (!form.actualStart || !form.actualEnd || form.progressPercent !== 100)) return t("designProjects.schedule.validation.completed");
    if (form.status !== "Completed" && form.actualEnd) return t("designProjects.schedule.validation.actualEndCompleted");
    if (task && form.predecessorTaskIds.includes(task.id)) return t("designProjects.schedule.validation.selfDependency");
    if (createsCycle()) return t("designProjects.schedule.validation.dependencyCycle");
    return null;
  };

  const save = async () => {
    const validationError = validate();
    if (validationError) { setError(validationError); return; }
    setSaving(true);
    setError(null);
    setConflict(false);
    const { phaseId, ...values } = form;
    const request: UpsertDesignScheduleTaskRequest = { ...values, code: values.code.trim(), name: values.name.trim() };
    try {
      if (task) await adminApi.updateDesignScheduleTask(operationalProjectId, task.id, request, idempotencyKey.current);
      else await adminApi.createDesignScheduleTask(operationalProjectId, phaseId, request, idempotencyKey.current);
      idempotencyKey.current = newIdempotencyKey();
      onOpenChange(false);
      await onSaved();
    } catch (saveError) {
      const status = (saveError as { response?: { status?: number } }).response?.status;
      if (status === 409) {
        setConflict(true);
        setError(t("designProjects.schedule.conflict"));
      } else setError(extractApiError(saveError));
    } finally { setSaving(false); }
  };

  const reloadVersion = async () => {
    const latest = await onReload();
    if (latest) {
      setForm(taskForm(latest));
      setBaseStatus(latest.status);
      setConflict(false);
      setError(t("designProjects.schedule.conflictReloaded"));
    }
  };

  const statuses = task ? transitions[baseStatus] : DESIGN_SCHEDULE_STATUSES;
  const togglePredecessor = (id: number, checked: boolean) => setForm((current) => ({ ...current, predecessorTaskIds: checked ? [...current.predecessorTaskIds, id] : current.predecessorTaskIds.filter((item) => item !== id) }));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[94vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader><DialogTitle>{t(task ? "designProjects.schedule.taskDialog.editTitle" : "designProjects.schedule.taskDialog.createTitle")}</DialogTitle><DialogDescription>{t("designProjects.schedule.taskDialog.description")}</DialogDescription></DialogHeader>
        <div className="grid gap-4 sm:grid-cols-2">
          <div><Label>{t("designProjects.schedule.field.phase")}</Label><Select disabled={Boolean(task)} value={form.phaseId ? String(form.phaseId) : ""} onValueChange={(value) => setForm({ ...form, phaseId: Number(value) })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent>{phases.map((phase) => <SelectItem key={phase.id} value={String(phase.id)}>{t(`designProjects.stage.${phase.code}`)}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>{t("designProjects.schedule.field.type")}</Label><Select value={form.isMilestone ? "milestone" : "task"} onValueChange={(value) => setForm((current) => ({ ...current, isMilestone: value === "milestone", plannedEnd: value === "milestone" ? current.plannedStart : current.plannedEnd }))}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="task">{t("designProjects.schedule.type.task")}</SelectItem><SelectItem value="milestone">{t("designProjects.schedule.type.milestone")}</SelectItem></SelectContent></Select></div>
          <div><Label htmlFor="schedule-task-code">{t("designProjects.schedule.field.code")}</Label><Input id="schedule-task-code" className="mt-1 min-h-11" maxLength={80} value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value })} /></div>
          <div><Label htmlFor="schedule-task-name">{t("designProjects.schedule.field.name")}</Label><Input id="schedule-task-name" className="mt-1 min-h-11" maxLength={300} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></div>
          <div><Label>{t("designProjects.schedule.field.department")}</Label><Select value={form.departmentCode} onValueChange={(value) => setForm({ ...form, departmentCode: value })}><SelectTrigger className="mt-1 min-h-11"><SelectValue placeholder={t("designProjects.schedule.placeholder.department")} /></SelectTrigger><SelectContent>{departments.filter((item) => item.isActive).map((item) => <SelectItem key={item.id} value={item.code}>{item.labelKey ? t(item.labelKey) : item.name}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>{t("designProjects.schedule.field.assignee")}</Label><Select value={form.assigneeMemberId ? String(form.assigneeMemberId) : ""} onValueChange={(value) => setForm({ ...form, assigneeMemberId: Number(value) })}><SelectTrigger className="mt-1 min-h-11"><SelectValue placeholder={t("designProjects.schedule.placeholder.assignee")} /></SelectTrigger><SelectContent>{activeMembers.map((member) => <SelectItem key={member.id} value={String(member.id)}>{member.userName} · {member.position}</SelectItem>)}</SelectContent></Select></div>
          <div><Label htmlFor="task-planned-start">{t("designProjects.schedule.field.plannedStart")}</Label><Input id="task-planned-start" className="mt-1 min-h-11" type="date" value={form.plannedStart} onChange={(event) => setForm({ ...form, plannedStart: event.target.value, plannedEnd: form.isMilestone ? event.target.value : form.plannedEnd })} /></div>
          <div><Label htmlFor="task-planned-end">{t("designProjects.schedule.field.plannedEnd")}</Label><Input id="task-planned-end" className="mt-1 min-h-11" type="date" disabled={form.isMilestone} min={form.plannedStart} value={form.plannedEnd} onChange={(event) => setForm({ ...form, plannedEnd: event.target.value })} /></div>
          <div><Label htmlFor="task-actual-start">{t("designProjects.schedule.field.actualStart")}</Label><Input id="task-actual-start" className="mt-1 min-h-11" type="date" value={form.actualStart ?? ""} onChange={(event) => setForm({ ...form, actualStart: event.target.value || null })} /></div>
          <div><Label htmlFor="task-actual-end">{t("designProjects.schedule.field.actualEnd")}</Label><Input id="task-actual-end" className="mt-1 min-h-11" type="date" min={form.actualStart ?? undefined} value={form.actualEnd ?? ""} onChange={(event) => setForm({ ...form, actualEnd: event.target.value || null })} /></div>
          <div><Label>{t("designProjects.schedule.field.status")}</Label><Select value={form.status} onValueChange={(value) => setForm({ ...form, status: value as DesignScheduleStatus })}><SelectTrigger className="mt-1 min-h-11"><SelectValue /></SelectTrigger><SelectContent>{statuses.map((status) => <SelectItem key={status} value={status}>{t(`designProjects.schedule.status.${status}`)}</SelectItem>)}</SelectContent></Select></div>
          <div><Label htmlFor="task-progress">{t("designProjects.schedule.field.progress")}</Label><Input id="task-progress" className="mt-1 min-h-11" type="number" min={0} max={100} value={form.progressPercent} onChange={(event) => setForm({ ...form, progressPercent: Number(event.target.value) })} /></div>
          <div><Label htmlFor="task-weight">{t("designProjects.schedule.field.weight")}</Label><Input id="task-weight" className="mt-1 min-h-11" type="number" min={1} max={100} value={form.weight} onChange={(event) => setForm({ ...form, weight: Number(event.target.value) })} /></div>
          <fieldset className="space-y-2 sm:col-span-2"><legend className="text-sm font-medium text-slate-800">{t("designProjects.schedule.field.predecessors")}</legend>{availablePredecessors.length ? <div className="max-h-44 space-y-1 overflow-y-auto rounded-md border p-2">{availablePredecessors.map((item) => <label key={item.id} className="flex min-h-11 cursor-pointer items-center gap-3 rounded px-2 hover:bg-slate-50"><Checkbox checked={form.predecessorTaskIds.includes(item.id)} onCheckedChange={(checked) => togglePredecessor(item.id, checked === true)} /><span className="text-sm"><span className="font-mono text-xs text-slate-500">{item.code}</span> · {item.name}</span></label>)}</div> : <p className="text-sm text-slate-500">{t("designProjects.schedule.predecessors.empty")}</p>}</fieldset>
        </div>
        {error ? <div className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700" role="alert"><p>{error}</p>{conflict ? <Button className="mt-2 min-h-10" type="button" size="sm" variant="outline" onClick={() => void reloadVersion()}><RefreshCw className="mr-1.5 h-4 w-4" />{t("designProjects.schedule.action.reload")}</Button> : null}</div> : null}
        <DialogFooter><Button className="min-h-11" variant="outline" disabled={saving} onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button><Button className="min-h-11" disabled={saving} onClick={() => void save()}>{saving ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : null}{t("common.save")}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
