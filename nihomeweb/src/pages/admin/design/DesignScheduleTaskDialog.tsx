import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { FieldScope } from "@/components/ui/field-scope";
import { projectMemberOption, projectMemberRole } from "@/lib/people";
import { PeopleChecklist } from "@/components/admin/PeopleChecklist";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useEffect, useMemo, useRef, useState } from "react";
import { Loader2, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { focusFormField } from "@/lib/formFocus";
import { PHASE_TASK_WEIGHT_TOTAL, suggestedTaskWeight } from "@/lib/designScheduleWeights";
import {
  adminApi,
  DESIGN_SCHEDULE_STATUSES,
  type DesignScheduleDepartmentResponse,
  type DesignSchedulePhaseResponse,
  type DesignScheduleStatus,
  type DesignScheduleTaskResponse,
  type MasterDataOption,
  type OperationalProjectMemberResponse,
  type ProjectScheduleItemResponse,
  type DesignScheduleOutsourceOptionResponse,
  type UpsertDesignScheduleTaskRequest,
} from "@/services/adminApi";

interface Props {
  operationalProjectId: number;
  phases: DesignSchedulePhaseResponse[];
  masterScheduleItems: ProjectScheduleItemResponse[];
  tasks: DesignScheduleTaskResponse[];
  departments: DesignScheduleDepartmentResponse[];
  disciplines: MasterDataOption[];
  members: OperationalProjectMemberResponse[];
  initialPhaseId?: number;
  /** Sum of task weights already used in each phase (whole schedule). */
  taskWeightTotals: Map<number, number>;
  task: DesignScheduleTaskResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<void> | void;
  onReload: () => Promise<DesignScheduleTaskResponse | undefined>;
}

/** Form fields a validation message can point at (each wrapper carries data-form-field). */
type TaskField =
  | "code"
  | "name"
  | "department"
  | "discipline"
  | "assignee"
  | "approver"
  | "cc"
  | "plannedStart"
  | "plannedEnd"
  | "masterSchedule"
  | "actualStart"
  | "actualEnd"
  | "progress"
  | "weight"
  | "status"
  | "predecessors"
  | "vendor";

const DESIGN_DEPARTMENT = "DESIGN";

const transitions: Record<DesignScheduleStatus, DesignScheduleStatus[]> = {
  NotStarted: ["NotStarted", "InProgress", "OnHold", "WaitingForDepartment"],
  InProgress: ["InProgress", "OnHold", "WaitingForDepartment"],
  OnHold: ["OnHold", "InProgress", "WaitingForDepartment"],
  WaitingForDepartment: ["WaitingForDepartment", "InProgress", "OnHold"],
  Completed: ["Completed"],
};

interface TaskForm extends UpsertDesignScheduleTaskRequest {
  phaseId: number;
}

const emptyForm = (phaseId: number): TaskForm => ({
  phaseId,
  code: "",
  name: "",
  description: null,
  departmentCode: "",
  disciplineCode: null,
  assigneeMemberId: 0,
  approverMemberId: 0,
  parentTaskId: null,
  projectScheduleNodeId: null,
  resourceSummary: null,
  vendorId: null,
  downstreamContractId: null,
  isCritical: false,
  isMilestone: false,
  plannedStart: "",
  plannedEnd: "",
  actualStart: null,
  actualEnd: null,
  status: "NotStarted",
  progressPercent: 0,
  weight: 1,
  predecessorTaskIds: [],
  ccMemberIds: [],
});

const taskForm = (task: DesignScheduleTaskResponse): TaskForm => ({
  phaseId: task.phaseId,
  code: task.code,
  name: task.name,
  description: task.description ?? null,
  departmentCode: task.departmentCode,
  disciplineCode: task.disciplineCode ?? null,
  assigneeMemberId: task.assigneeMemberId,
  approverMemberId: task.approverMemberId,
  parentTaskId: task.parentTaskId ?? null,
  projectScheduleNodeId: task.projectScheduleNodeId ?? null,
  resourceSummary: task.resourceSummary ?? null,
  vendorId: task.vendorId ?? null,
  downstreamContractId: task.downstreamContractId ?? null,
  isCritical: task.isCritical,
  isMilestone: task.isMilestone,
  plannedStart: task.plannedStart.slice(0, 10),
  plannedEnd: task.plannedEnd.slice(0, 10),
  actualStart: task.actualStart?.slice(0, 10) ?? null,
  actualEnd: task.actualEnd?.slice(0, 10) ?? null,
  status: task.status,
  progressPercent: task.progressPercent,
  weight: task.weight,
  predecessorTaskIds: task.predecessorTaskIds,
  ccMemberIds: task.ccMemberIds,
  rowVersion: task.rowVersion,
});

export const DesignScheduleTaskDialog = ({
  operationalProjectId,
  phases,
  masterScheduleItems,
  tasks,
  departments,
  disciplines,
  members,
  initialPhaseId,
  taskWeightTotals,
  task,
  open,
  onOpenChange,
  onSaved,
  onReload,
}: Props) => {
  const uiError = useUiError();
  const { t } = useI18n();
  const [form, setForm] = useState<TaskForm>(emptyForm(initialPhaseId ?? phases[0]?.id ?? 0));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  // Field messages appear after the first save attempt and follow the form as it is fixed.
  const [attempted, setAttempted] = useState(false);
  const [baseStatus, setBaseStatus] = useState<DesignScheduleStatus>("NotStarted");
  const [outsourceOptions, setOutsourceOptions] = useState<DesignScheduleOutsourceOptionResponse[]>(
    [],
  );
  const idempotencyKey = useRef("");
  const activeMembers = useMemo(
    () => members.filter((member) => member.isActive && member.isUserActive !== false),
    [members],
  );
  const availablePredecessors = useMemo(
    () => tasks.filter((item) => item.id !== task?.id),
    [tasks, task],
  );

  useEffect(() => {
    if (!open) return;
    const blank = emptyForm(initialPhaseId ?? phases[0]?.id ?? 0);
    // A new task starts with the weight that completes its phase to 100%.
    blank.weight = suggestedTaskWeight(taskWeightTotals.get(blank.phaseId) ?? 0);
    // Most tasks in a design schedule belong to the design department.
    if (departments.some((department) => department.code === DESIGN_DEPARTMENT))
      blank.departmentCode = DESIGN_DEPARTMENT;
    setForm(task ? taskForm(task) : blank);
    setError(null);
    setConflict(false);
    setAttempted(false);
    setBaseStatus(task?.status ?? "NotStarted");
    idempotencyKey.current = newIdempotencyKey();
  }, [open, task, initialPhaseId, phases, departments, taskWeightTotals]);

  useEffect(() => {
    if (!open) return;
    void adminApi
      .getDesignScheduleOutsourceOptions(operationalProjectId)
      .then((response) => setOutsourceOptions(response.data ?? []))
      .catch((reason) => setError(uiError(reason)));
  }, [open, operationalProjectId, uiError]);

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

  /** First rule the form breaks, with the field it belongs to so the message can sit beside it. */
  const validate = (): { field: TaskField; message: string } | null => {
    const fail = (field: TaskField, key: string, values?: Record<string, string | number>) => ({
      field,
      message: t(key, values),
    });
    const code = form.code.trim(),
      name = form.name.trim();
    if (code.length > 80) return fail("code", "designProjects.schedule.validation.codeLength");
    if (!name || name.length > 300) return fail("name", "designProjects.schedule.validation.name");
    if (
      !departments.some(
        (department) => department.code.toLowerCase() === form.departmentCode.toLowerCase(),
      )
    )
      return fail("department", "designProjects.schedule.validation.department");
    if (
      form.disciplineCode &&
      !disciplines.some(
        (discipline) => discipline.code === form.disciplineCode && discipline.isActive,
      )
    )
      return fail("discipline", "designProjects.schedule.validation.discipline");
    if (!activeMembers.some((member) => member.id === form.assigneeMemberId))
      return fail("assignee", "designProjects.schedule.validation.assignee");
    if (
      !activeMembers.some((member) => member.id === form.approverMemberId) ||
      form.approverMemberId === form.assigneeMemberId
    )
      return fail("approver", "designProjects.schedule.validation.approver");
    if (
      form.ccMemberIds.some((id) => id === form.assigneeMemberId || id === form.approverMemberId) ||
      new Set(form.ccMemberIds).size !== form.ccMemberIds.length
    )
      return fail("cc", "designProjects.schedule.validation.cc");
    if (!form.plannedStart || !form.plannedEnd)
      return fail(
        form.plannedStart ? "plannedEnd" : "plannedStart",
        "designProjects.schedule.validation.plannedRequired",
      );
    if (form.plannedEnd < form.plannedStart)
      return fail("plannedEnd", "designProjects.schedule.validation.plannedOrder");
    if (masterScheduleItems.length > 0 && !form.projectScheduleNodeId)
      return fail("masterSchedule", "designProjects.schedule.validation.masterScheduleRequired");
    const milestone = masterScheduleItems.find(
      (item) => item.nodeId === form.projectScheduleNodeId,
    );
    if (
      milestone &&
      (form.plannedStart < milestone.plannedStart || form.plannedEnd > milestone.plannedEnd)
    )
      return fail("plannedStart", "designProjects.schedule.validation.masterScheduleDates", {
        wbs: milestone.wbsCode,
      });
    if (form.isMilestone && form.plannedStart !== form.plannedEnd)
      return fail("plannedEnd", "designProjects.schedule.validation.milestoneDates");
    if (form.actualEnd && !form.actualStart)
      return fail("actualStart", "designProjects.schedule.validation.actualStartRequired");
    if (form.actualStart && form.actualEnd && form.actualEnd < form.actualStart)
      return fail("actualEnd", "designProjects.schedule.validation.actualOrder");
    if (form.progressPercent < 0 || form.progressPercent > 100)
      return fail("progress", "designProjects.schedule.validation.progressRange");
    if (form.weight < 1 || form.weight > 100)
      return fail("weight", "designProjects.schedule.validation.weightRange");
    if (
      form.status === "NotStarted" &&
      (form.actualStart || form.actualEnd || form.progressPercent !== 0)
    )
      return fail("status", "designProjects.schedule.validation.notStarted");
    if (form.status === "InProgress" && !form.actualStart)
      return fail("actualStart", "designProjects.schedule.validation.inProgress");
    if (form.status === "Completed")
      return fail("status", "designProjects.schedule.validation.completeAction");
    if (form.actualEnd)
      return fail("actualEnd", "designProjects.schedule.validation.actualEndCompleted");
    if (task && form.predecessorTaskIds.includes(task.id))
      return fail("predecessors", "designProjects.schedule.validation.selfDependency");
    if (createsCycle())
      return fail("predecessors", "designProjects.schedule.validation.dependencyCycle");
    if (Boolean(form.vendorId) !== Boolean(form.downstreamContractId))
      return fail("vendor", "designProjects.schedule.validation.outsourcePair");
    return null;
  };

  const fieldError = attempted ? validate() : null;
  const invalid = (field: TaskField) =>
    fieldError?.field === field
      ? { "aria-invalid": true as const, "aria-describedby": `task-${field}-error` }
      : {};
  const fieldMessage = (field: TaskField) =>
    fieldError?.field === field ? (
      <p id={`task-${field}-error`} className="mt-1 text-xs text-destructive">
        {fieldError.message}
      </p>
    ) : null;

  const save = async () => {
    setAttempted(true);
    const validationError = validate();
    if (validationError) {
      focusFormField(validationError.field);
      return;
    }
    setSaving(true);
    setError(null);
    setConflict(false);
    const { phaseId, ...values } = form;
    const request: UpsertDesignScheduleTaskRequest = {
      ...values,
      code: values.code.trim(),
      name: values.name.trim(),
    };
    try {
      if (task)
        await adminApi.updateDesignScheduleTask(
          operationalProjectId,
          task.id,
          request,
          idempotencyKey.current,
        );
      else
        await adminApi.createDesignScheduleTask(
          operationalProjectId,
          phaseId,
          request,
          idempotencyKey.current,
        );
      idempotencyKey.current = newIdempotencyKey();
      onOpenChange(false);
      await onSaved();
    } catch (saveError) {
      const status = (saveError as { response?: { status?: number } }).response?.status;
      if (status === 409) {
        setConflict(true);
        setError(t("designProjects.schedule.conflict"));
      } else setError(uiError(saveError));
    } finally {
      setSaving(false);
    }
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

  const statuses = (task ? transitions[baseStatus] : DESIGN_SCHEDULE_STATUSES).filter(
    (status) => status !== "Completed",
  );
  const togglePredecessor = (id: number, checked: boolean) =>
    setForm((current) => ({
      ...current,
      predecessorTaskIds: checked
        ? [...current.predecessorTaskIds, id]
        : current.predecessorTaskIds.filter((item) => item !== id),
    }));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[94vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>
            {t(
              task
                ? "designProjects.schedule.taskDialog.editTitle"
                : "designProjects.schedule.taskDialog.createTitle",
            )}
          </DialogTitle>
          <DialogDescription>
            {t("designProjects.schedule.taskDialog.description")}
          </DialogDescription>
        </DialogHeader>
        <div className="grid min-w-0 grid-cols-1 gap-4 sm:grid-cols-2 [&>div]:min-w-0">
          <FieldScope>
            <Label>{t("designProjects.schedule.field.phase")}</Label>
            <Select
              disabled={Boolean(task)}
              value={form.phaseId ? String(form.phaseId) : ""}
              onValueChange={(value) =>
                setForm({
                  ...form,
                  phaseId: Number(value),
                  // Moving a new task to another phase suggests that phase's remaining weight.
                  weight: task
                    ? form.weight
                    : suggestedTaskWeight(taskWeightTotals.get(Number(value)) ?? 0),
                })
              }
            >
              <SelectTrigger className="mt-1 min-h-11">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {phases.map((phase) => (
                  <SelectItem key={phase.id} value={String(phase.id)}>
                    {t(`designProjects.stage.${phase.code}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FieldScope>
          <FieldScope>
            <Label>{t("designProjects.schedule.field.type")}</Label>
            <Select
              value={form.isMilestone ? "milestone" : "task"}
              onValueChange={(value) =>
                setForm((current) => ({
                  ...current,
                  isMilestone: value === "milestone",
                  plannedEnd: value === "milestone" ? current.plannedStart : current.plannedEnd,
                }))
              }
            >
              <SelectTrigger className="mt-1 min-h-11">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="task">{t("designProjects.schedule.type.task")}</SelectItem>
                <SelectItem value="milestone">
                  {t("designProjects.schedule.type.milestone")}
                </SelectItem>
              </SelectContent>
            </Select>
          </FieldScope>
          <div data-form-field="code">
            <Label htmlFor="schedule-task-code">{t("designProjects.schedule.field.code")}</Label>
            <Input
              id="schedule-task-code"
              className="mt-1 min-h-11"
              maxLength={80}
              value={form.code}
              onChange={(event) => setForm({ ...form, code: event.target.value })}
              placeholder={t("designProjects.schedule.placeholder.codeAuto")}
              {...invalid("code")}
            />
            {fieldMessage("code") ?? (
              <p className="mt-1 text-xs text-muted-foreground">
                {t("designProjects.schedule.hint.codeAuto")}
              </p>
            )}
          </div>
          <div data-form-field="name">
            <Label htmlFor="schedule-task-name">{t("designProjects.schedule.field.name")} *</Label>
            <Input
              id="schedule-task-name"
              className="mt-1 min-h-11"
              maxLength={300}
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
              {...invalid("name")}
            />
            {fieldMessage("name")}
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="schedule-task-description">
              {t("designProjects.schedule.field.description")}
            </Label>
            <Textarea
              id="schedule-task-description"
              className="mt-1"
              maxLength={4000}
              value={form.description ?? ""}
              onChange={(event) => setForm({ ...form, description: event.target.value || null })}
            />
          </div>
          <FieldScope data-form-field="vendor">
            <Label>{t("designProjects.schedule.field.vendor")}</Label>
            <Select
              value={form.vendorId ? String(form.vendorId) : "none"}
              onValueChange={(value) =>
                setForm((current) => ({
                  ...current,
                  vendorId: value === "none" ? null : Number(value),
                  downstreamContractId: null,
                }))
              }
            >
              <SelectTrigger className="mt-1 min-h-11" {...invalid("vendor")}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">{t("designProjects.schedule.value.none")}</SelectItem>
                {Array.from(
                  new Map(
                    outsourceOptions.map((option) => [option.vendorId, option] as const),
                  ).values(),
                ).map((option) => (
                  <SelectItem key={option.vendorId} value={String(option.vendorId)}>
                    {option.vendorCode} · {option.vendorName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {fieldMessage("vendor")}
          </FieldScope>
          <FieldScope>
            <Label>{t("designProjects.schedule.field.downstreamContract")}</Label>
            <Select
              disabled={!form.vendorId}
              value={form.downstreamContractId ? String(form.downstreamContractId) : "none"}
              onValueChange={(value) =>
                setForm({
                  ...form,
                  downstreamContractId: value === "none" ? null : Number(value),
                })
              }
            >
              <SelectTrigger className="mt-1 min-h-11">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">{t("designProjects.schedule.value.none")}</SelectItem>
                {outsourceOptions
                  .filter((option) => option.vendorId === form.vendorId)
                  .map((option) => (
                    <SelectItem key={option.contractId} value={String(option.contractId)}>
                      {option.contractNumber}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
          </FieldScope>
          <FieldScope data-form-field="department">
            <Label>{t("designProjects.schedule.field.department")} *</Label>
            <Select
              value={form.departmentCode}
              onValueChange={(value) => setForm({ ...form, departmentCode: value })}
            >
              <SelectTrigger className="mt-1 min-h-11" {...invalid("department")}>
                <SelectValue placeholder={t("designProjects.schedule.placeholder.department")} />
              </SelectTrigger>
              <SelectContent>
                {departments.map((item) => (
                  <SelectItem key={item.code} value={item.code}>
                    {t(item.labelKey)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {fieldMessage("department")}
          </FieldScope>
          <FieldScope data-form-field="discipline">
            <Label>{t("designProjects.schedule.field.discipline")}</Label>
            <Select
              value={form.disciplineCode ?? "none"}
              onValueChange={(value) =>
                setForm({ ...form, disciplineCode: value === "none" ? null : value })
              }
            >
              <SelectTrigger className="mt-1 min-h-11" {...invalid("discipline")}>
                <SelectValue placeholder={t("designProjects.schedule.placeholder.discipline")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">{t("designProjects.schedule.value.none")}</SelectItem>
                {disciplines
                  .filter((item) => item.isActive)
                  .map((item) => (
                    <SelectItem key={item.id} value={item.code}>
                      {item.labelKey ? t(item.labelKey) : item.name}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
            {fieldMessage("discipline")}
          </FieldScope>
          <FieldScope data-form-field="assignee">
            <Label>{t("designProjects.schedule.field.assignee")} *</Label>
            <SearchableSelect
              value={form.assigneeMemberId ? String(form.assigneeMemberId) : ""}
              onChange={(value) => {
                const memberId = Number(value);
                setForm((current) => ({
                  ...current,
                  assigneeMemberId: memberId,
                  approverMemberId:
                    current.approverMemberId === memberId ? 0 : current.approverMemberId,
                  ccMemberIds: current.ccMemberIds.filter((id) => id !== memberId),
                }));
              }}
              ariaLabel={t("designProjects.schedule.field.assignee")}
              placeholder={t("designProjects.schedule.field.assignee")}
              options={[...activeMembers.map((member) => projectMemberOption(member, t))]}
              {...invalid("assignee")}
            />
            {fieldMessage("assignee")}
          </FieldScope>
          <FieldScope data-form-field="approver">
            <Label>{t("designProjects.schedule.field.approver")} *</Label>
            <SearchableSelect
              value={form.approverMemberId ? String(form.approverMemberId) : ""}
              onChange={(value) => {
                const memberId = Number(value);
                setForm((current) => ({
                  ...current,
                  approverMemberId: memberId,
                  ccMemberIds: current.ccMemberIds.filter((id) => id !== memberId),
                }));
              }}
              ariaLabel={t("designProjects.schedule.field.approver")}
              placeholder={t("designProjects.schedule.field.approver")}
              options={[
                ...activeMembers
                  .filter((member) => member.id !== form.assigneeMemberId)
                  .map((member) => projectMemberOption(member, t)),
              ]}
              {...invalid("approver")}
            />
            {fieldMessage("approver")}
          </FieldScope>
          <FieldScope>
            <Label>{t("designProjects.schedule.field.parentTask")}</Label>
            <Select
              value={form.parentTaskId ? String(form.parentTaskId) : "none"}
              onValueChange={(value) =>
                setForm({ ...form, parentTaskId: value === "none" ? null : Number(value) })
              }
            >
              <SelectTrigger className="mt-1 min-h-11">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">{t("designProjects.schedule.value.none")}</SelectItem>
                {availablePredecessors.map((item) => (
                  <SelectItem key={item.id} value={String(item.id)}>
                    {item.code} / {item.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FieldScope>
          <div className="sm:col-span-2">
            <Label htmlFor="schedule-task-resources">
              {t("designProjects.schedule.field.resources")}
            </Label>
            <Textarea
              id="schedule-task-resources"
              className="mt-1"
              maxLength={2000}
              value={form.resourceSummary ?? ""}
              onChange={(event) =>
                setForm({ ...form, resourceSummary: event.target.value || null })
              }
            />
          </div>
          <div data-form-field="masterSchedule">
            <Label htmlFor="task-master-schedule-node">
              {t("operationalProjects.schedule.milestone")}
              {masterScheduleItems.length > 0 ? " *" : ""}
            </Label>
            <Select
              value={form.projectScheduleNodeId ?? "none"}
              onValueChange={(value) =>
                setForm({ ...form, projectScheduleNodeId: value === "none" ? null : value })
              }
              disabled={masterScheduleItems.length === 0}
            >
              <SelectTrigger
                id="task-master-schedule-node"
                className="mt-1 min-h-11"
                {...invalid("masterSchedule")}
              >
                <SelectValue placeholder={t("operationalProjects.schedule.chooseMilestone")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">
                  {t("operationalProjects.schedule.noMilestone")}
                </SelectItem>
                {masterScheduleItems.map((item) => (
                  <SelectItem key={item.nodeId} value={item.nodeId}>
                    {item.wbsCode} · {item.title} ({item.plannedStart} – {item.plannedEnd})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {fieldMessage("masterSchedule") ??
              (masterScheduleItems.length > 0 && !form.projectScheduleNodeId && (
                <p className="mt-1 text-xs text-muted-foreground">
                  {t("operationalProjects.schedule.milestoneRequired")}
                </p>
              ))}
          </div>
          <div data-form-field="plannedStart">
            <Label htmlFor="task-planned-start">
              {t("designProjects.schedule.field.plannedStart")} *
            </Label>
            <Input
              id="task-planned-start"
              {...invalid("plannedStart")}
              className="mt-1 min-h-11"
              type="date"
              value={form.plannedStart}
              onChange={(event) =>
                setForm({
                  ...form,
                  plannedStart: event.target.value,
                  plannedEnd: form.isMilestone ? event.target.value : form.plannedEnd,
                })
              }
            />
            {fieldMessage("plannedStart")}
          </div>
          <div data-form-field="plannedEnd">
            <Label htmlFor="task-planned-end">
              {t("designProjects.schedule.field.plannedEnd")} *
            </Label>
            <Input
              id="task-planned-end"
              {...invalid("plannedEnd")}
              className="mt-1 min-h-11"
              type="date"
              disabled={form.isMilestone}
              min={form.plannedStart}
              value={form.plannedEnd}
              onChange={(event) => setForm({ ...form, plannedEnd: event.target.value })}
            />
            {fieldMessage("plannedEnd")}
          </div>
          <div data-form-field="actualStart">
            <Label htmlFor="task-actual-start">
              {t("designProjects.schedule.field.actualStart")}
            </Label>
            <Input
              id="task-actual-start"
              {...invalid("actualStart")}
              className="mt-1 min-h-11"
              type="date"
              value={form.actualStart ?? ""}
              onChange={(event) => setForm({ ...form, actualStart: event.target.value || null })}
            />
            {fieldMessage("actualStart")}
          </div>
          <div data-form-field="actualEnd">
            <Label htmlFor="task-actual-end">{t("designProjects.schedule.field.actualEnd")}</Label>
            <Input
              id="task-actual-end"
              {...invalid("actualEnd")}
              className="mt-1 min-h-11"
              type="date"
              min={form.actualStart ?? undefined}
              value={form.actualEnd ?? ""}
              onChange={(event) => setForm({ ...form, actualEnd: event.target.value || null })}
            />
            {fieldMessage("actualEnd")}
          </div>
          <div data-form-field="status">
            <Label htmlFor="task-status">{t("designProjects.schedule.field.status")}</Label>
            <Select
              value={form.status}
              onValueChange={(value) => setForm({ ...form, status: value as DesignScheduleStatus })}
            >
              <SelectTrigger id="task-status" className="mt-1 min-h-11" {...invalid("status")}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {statuses.map((status) => (
                  <SelectItem key={status} value={status}>
                    {t(`designProjects.schedule.status.${status}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {fieldMessage("status")}
          </div>
          <div data-form-field="progress">
            <Label htmlFor="task-progress">{t("designProjects.schedule.field.progress")}</Label>
            <Input
              id="task-progress"
              {...invalid("progress")}
              className="mt-1 min-h-11"
              type="number"
              min={0}
              max={100}
              value={form.progressPercent}
              onChange={(event) =>
                setForm({ ...form, progressPercent: Number(event.target.value) })
              }
            />
            {fieldMessage("progress")}
          </div>
          <div data-form-field="weight">
            <Label htmlFor="task-weight">{t("designProjects.schedule.field.weight")}</Label>
            <Input
              id="task-weight"
              aria-describedby="task-weight-hint"
              {...invalid("weight")}
              className="mt-1 min-h-11"
              type="number"
              min={1}
              max={100}
              value={form.weight}
              onChange={(event) => setForm({ ...form, weight: Number(event.target.value) })}
            />
            {fieldMessage("weight") ??
              (() => {
                const others = (taskWeightTotals.get(form.phaseId) ?? 0) - (task?.weight ?? 0);
                const afterSave = others + (form.weight || 0);
                const exact = afterSave === PHASE_TASK_WEIGHT_TOTAL;
                return (
                  <p
                    id="task-weight-hint"
                    className={`mt-1 text-xs ${exact ? "text-muted-foreground" : "text-amber-700 dark:text-amber-400"}`}
                  >
                    {t("designProjects.schedule.hint.weightAfterSave", {
                      others,
                      after: afterSave,
                      total: PHASE_TASK_WEIGHT_TOTAL,
                    })}
                    {exact
                      ? null
                      : ` ${t("designProjects.schedule.hint.weightNotExact", { total: PHASE_TASK_WEIGHT_TOTAL })}`}
                  </p>
                );
              })()}
          </div>
          <label className="flex min-h-11 items-center gap-3 rounded-md border px-3 text-sm">
            <Checkbox
              checked={form.isCritical}
              onCheckedChange={(checked) => setForm({ ...form, isCritical: checked === true })}
            />
            {t("designProjects.schedule.field.critical")}
          </label>
          <fieldset className="min-w-0 space-y-2 sm:col-span-2" data-form-field="predecessors">
            <legend className="text-sm font-medium text-foreground">
              {t("designProjects.schedule.field.predecessors")}
            </legend>
            {fieldMessage("predecessors")}
            {availablePredecessors.length ? (
              <div className="max-h-44 space-y-1 overflow-y-auto rounded-md border p-2">
                {availablePredecessors.map((item) => (
                  <label
                    key={item.id}
                    className="flex min-h-11 cursor-pointer items-center gap-3 rounded px-2 hover:bg-muted/30"
                  >
                    <Checkbox
                      checked={form.predecessorTaskIds.includes(item.id)}
                      onCheckedChange={(checked) => togglePredecessor(item.id, checked === true)}
                    />
                    <span className="text-sm">
                      <span className="font-mono text-xs text-muted-foreground">{item.code}</span> ·{" "}
                      {item.name}
                    </span>
                  </label>
                ))}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                {t("designProjects.schedule.predecessors.empty")}
              </p>
            )}
          </fieldset>
          <fieldset className="min-w-0 space-y-2 sm:col-span-2" data-form-field="cc">
            <legend className="text-sm font-medium text-foreground">
              {t("designProjects.schedule.field.cc")}
            </legend>
            {fieldMessage("cc")}
            <PeopleChecklist
              people={members
                .filter(
                  (member) =>
                    member.id !== form.assigneeMemberId && member.id !== form.approverMemberId,
                )
                .map((member) => ({
                  ...member,
                  userId: member.id,
                  name: member.userName,
                  roleName: projectMemberRole(member, t),
                  roleLabelKey: null,
                  selectable: member.isActive && member.isUserActive !== false,
                }))}
              selected={form.ccMemberIds}
              onChange={(ccMemberIds) => setForm((current) => ({ ...current, ccMemberIds }))}
            />
          </fieldset>
        </div>
        {error ? (
          <div
            className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700"
            role="alert"
          >
            <p>{error}</p>
            {conflict ? (
              <Button
                className="mt-2 min-h-10"
                type="button"
                size="sm"
                variant="outline"
                onClick={() => void reloadVersion()}
              >
                <RefreshCw className="mr-1.5 h-4 w-4" />
                {t("designProjects.schedule.action.reload")}
              </Button>
            ) : null}
          </div>
        ) : null}
        <DialogFooter>
          <Button
            className="min-h-11"
            variant="outline"
            disabled={saving}
            onClick={() => onOpenChange(false)}
          >
            {t("common.cancel")}
          </Button>
          <Button className="min-h-11" disabled={saving} onClick={() => void save()}>
            {saving ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : null}
            {t(task ? "common.save" : "designProjects.schedule.taskDialog.create")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
