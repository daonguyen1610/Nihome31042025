import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, BriefcaseBusiness, CheckCircle2, ClipboardList, Plus, Search, Users } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ContractResponse,
  type CreateDepartmentWorkPackageRequest,
  type DepartmentWorkPackageOptionsResponse,
  type DepartmentWorkPackageResponse,
  type DepartmentWorkTaskResponse,
  type OperationalProjectListItemResponse,
  type UpsertDepartmentWorkTaskRequest,
  type WorkPackageKind,
  type WorkPackageStatus,
} from "@/services/adminApi";

const dateValue = (value?: string | null) => value?.slice(0, 10) ?? "";
const apiDate = (value: string) => `${value}T00:00:00.000Z`;
const emptyOptions: DepartmentWorkPackageOptionsResponse = { departments: [], users: [], kpiDefinitions: [] };

const statusTone: Record<WorkPackageStatus, string> = {
  Planned: "border-sky-200 bg-sky-50 text-sky-700",
  Active: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Completed: "border-slate-200 bg-slate-50 text-slate-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

const DepartmentWorkPackages = () => {
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const packageId = id && /^\d+$/.test(id) ? Number(id) : null;
  const kind: WorkPackageKind = params.get("kind") === "Internal" ? "Internal" : "Project";
  const canManage = has(ADMIN_PERMS.workPackagesManage);
  const canAssign = has(ADMIN_PERMS.workPackagesAssign);
  const canCreate = canManage && (kind === "Project" || canAssign);

  const [rows, setRows] = useState<DepartmentWorkPackageResponse[]>([]);
  const [detail, setDetail] = useState<DepartmentWorkPackageResponse | null>(null);
  const [options, setOptions] = useState(emptyOptions);
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [packageOpen, setPackageOpen] = useState(false);
  const [taskOpen, setTaskOpen] = useState(false);
  const [progressOpen, setProgressOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [taskEditing, setTaskEditing] = useState<DepartmentWorkTaskResponse | null>(null);
  const [progressTaskEditing, setProgressTaskEditing] = useState<DepartmentWorkTaskResponse | null>(null);
  const [progressPercent, setProgressPercent] = useState(0);
  const [packageForm, setPackageForm] = useState<CreateDepartmentWorkPackageRequest>({
    kind,
    name: "",
    description: "",
    operationalProjectId: null,
    contractId: null,
    sourceOpportunityId: null,
    departmentRoleGroupId: 0,
    participantDepartmentRoleGroupIds: [],
    leadUserId: 0,
    plannedStart: null,
    plannedEnd: null,
  });
  const [taskForm, setTaskForm] = useState<UpsertDepartmentWorkTaskRequest>({
    wbsCode: "",
    title: "",
    description: "",
    resources: "",
    departmentRoleGroupId: null,
    assigneeUserId: 0,
    approverUserId: null,
    kpiDefinitionId: null,
    plannedStart: "",
    plannedEnd: "",
    ccUserIds: [],
    predecessorTaskIds: [],
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      if (packageId) {
        const response = await adminApi.getWorkPackage(packageId);
        setDetail(response.data);
      } else {
        const projectFilter = Number(params.get("projectId"));
        const response = await adminApi.listWorkPackages({
          kind,
          search: search || undefined,
          operationalProjectId: Number.isFinite(projectFilter) && projectFilter > 0 ? projectFilter : undefined,
          pageSize: 100,
        });
        setRows(response.data.items);
      }
    } catch (cause) {
      setError(extractApiError(cause, t("workPackages.error.load")));
    } finally {
      setLoading(false);
    }
  }, [kind, packageId, params, search, t]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (!detail) return;
    const taskId = params.get("taskId");
    if (taskId) document.getElementById(`task-${taskId}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
  }, [detail, params]);
  useEffect(() => {
    Promise.all([
      adminApi.getWorkPackageOptions(),
      adminApi.listOperationalProjects({ pageSize: 100 }),
    ]).then(([optionResponse, projectResponse]) => {
      setOptions(optionResponse.data);
      setProjects(projectResponse.data.items);
    }).catch(() => undefined);
  }, []);
  useEffect(() => {
    const projectIdValue = packageForm.operationalProjectId;
    if (!projectIdValue) { setContracts([]); return; }
    adminApi.listContracts({ operationalProjectId: projectIdValue, pageSize: 100 })
      .then(response => setContracts(response.data.items)).catch(() => setContracts([]));
  }, [packageForm.operationalProjectId]);
  useEffect(() => {
    if (!packageId && params.get("create") === "1" && canManage) {
      const project = Number(params.get("projectId"));
      const opportunity = Number(params.get("opportunityId"));
      setPackageForm(current => ({
        ...current,
        kind: "Project",
        operationalProjectId: Number.isFinite(project) && project > 0 ? project : null,
        sourceOpportunityId: Number.isFinite(opportunity) && opportunity > 0 ? opportunity : null,
      }));
      setPackageOpen(true);
    }
  }, [canManage, packageId, params]);

  const departmentUsers = useMemo(() => options.users.filter(user =>
    user.roleGroupId === packageForm.departmentRoleGroupId && user.canLead), [options.users, packageForm.departmentRoleGroupId]);
  const taskUsers = useMemo(() => options.users.filter(user =>
    user.roleGroupId === (taskForm.departmentRoleGroupId || detail?.departmentRoleGroupId)),
  [detail?.departmentRoleGroupId, options.users, taskForm.departmentRoleGroupId]);
  const availableDepartments = useMemo(() => packageForm.kind === "Project"
    ? options.departments.filter(group => group.canReceiveProjectPackage)
    : options.departments, [options.departments, packageForm.kind]);

  const openCreatePackage = () => {
    setPackageForm({
      kind,
      name: "",
      description: "",
      operationalProjectId: null,
      contractId: null,
      sourceOpportunityId: null,
      departmentRoleGroupId: 0,
      participantDepartmentRoleGroupIds: [],
      leadUserId: 0,
      plannedStart: null,
      plannedEnd: null,
    });
    setPackageOpen(true);
  };

  const savePackage = async () => {
    if (packageForm.name.trim().length < 2 || packageForm.name.length > 300 ||
      !packageForm.departmentRoleGroupId || !packageForm.leadUserId ||
      (packageForm.kind === "Project" && !packageForm.operationalProjectId) ||
      (packageForm.plannedStart && packageForm.plannedEnd && packageForm.plannedEnd < packageForm.plannedStart)) {
      toast({ variant: "destructive", title: t("workPackages.validation.package") });
      return;
    }
    setSaving(true);
    try {
      const response = await adminApi.createWorkPackage(packageForm, newIdempotencyKey());
      setPackageOpen(false);
      toast({ title: t("workPackages.toast.created") });
      navigate(`/admin/work-packages/${response.data.id}`);
    } catch (cause) {
      toast({ variant: "destructive", title: extractApiError(cause, t("workPackages.error.save")) });
    } finally { setSaving(false); }
  };

  const openTask = (task?: DepartmentWorkTaskResponse) => {
    setTaskEditing(task ?? null);
    setTaskForm(task ? {
      wbsCode: task.wbsCode,
      title: task.title,
      description: task.description ?? "",
      resources: task.resources ?? "",
      departmentRoleGroupId: task.departmentRoleGroupId,
      assigneeUserId: task.assigneeUserId,
      approverUserId: task.approverUserId ?? null,
      kpiDefinitionId: task.kpiDefinitionId ?? null,
      plannedStart: dateValue(task.plannedStart),
      plannedEnd: dateValue(task.plannedEnd),
      ccUserIds: task.ccUserIds,
      predecessorTaskIds: task.predecessorTaskIds,
      rowVersion: task.rowVersion,
    } : {
      wbsCode: "",
      title: "",
      description: "",
      resources: "",
      departmentRoleGroupId: detail?.kind === "Internal" ? null : detail?.departmentRoleGroupId,
      assigneeUserId: 0,
      approverUserId: null,
      kpiDefinitionId: null,
      plannedStart: dateValue(detail?.plannedStart),
      plannedEnd: dateValue(detail?.plannedEnd),
      ccUserIds: [],
      predecessorTaskIds: [],
    });
    setTaskOpen(true);
  };

  const saveTask = async () => {
    if (!detail || !taskForm.wbsCode.trim() || taskForm.title.trim().length < 2 || !taskForm.assigneeUserId ||
      !taskForm.plannedStart || !taskForm.plannedEnd || taskForm.plannedEnd < taskForm.plannedStart ||
      taskForm.assigneeUserId === taskForm.approverUserId) {
      toast({ variant: "destructive", title: t("workPackages.validation.task") });
      return;
    }
    setSaving(true);
    const body = { ...taskForm, plannedStart: apiDate(taskForm.plannedStart), plannedEnd: apiDate(taskForm.plannedEnd) };
    try {
      if (taskEditing) await adminApi.updateWorkTask(detail.id, taskEditing.id, body, newIdempotencyKey());
      else await adminApi.addWorkTask(detail.id, body, newIdempotencyKey());
      setTaskOpen(false);
      toast({ title: t(taskEditing ? "workPackages.toast.taskUpdated" : "workPackages.toast.taskCreated") });
      await load();
    } catch (cause) {
      toast({ variant: "destructive", title: extractApiError(cause, t("workPackages.error.save")) });
    } finally { setSaving(false); }
  };

  const transitionPackage = async (status: WorkPackageStatus) => {
    if (!detail) return;
    try {
      await adminApi.updateWorkPackage(detail.id, {
        name: detail.name,
        description: detail.description,
        contractId: detail.contractId,
        status,
        plannedStart: detail.plannedStart,
        plannedEnd: detail.plannedEnd,
        rowVersion: detail.rowVersion,
      }, newIdempotencyKey());
      await load();
    } catch (cause) { toast({ variant: "destructive", title: extractApiError(cause, t("workPackages.error.save")) }); }
  };

  const progressTask = async (task: DepartmentWorkTaskResponse, complete = false, percent?: number) => {
    if (!detail) return;
    const nextProgress = complete ? 100 : Math.min(99, Math.max(1, percent ?? task.progressPercent));
    try {
      await adminApi.updateWorkTaskProgress(detail.id, task.id, {
        progressPercent: nextProgress,
        status: complete ? "Completed" : "InProgress",
        rowVersion: task.rowVersion,
      }, newIdempotencyKey());
      setProgressOpen(false);
      await load();
    } catch (cause) { toast({ variant: "destructive", title: extractApiError(cause, t("workPackages.error.save")) }); }
  };

  const openProgress = (task: DepartmentWorkTaskResponse) => {
    setProgressTaskEditing(task);
    setProgressPercent(Math.max(1, Math.min(99, task.progressPercent || 1)));
    setProgressOpen(true);
  };

  const verifyTask = async (task: DepartmentWorkTaskResponse) => {
    if (!detail) return;
    try {
      await adminApi.verifyWorkTask(detail.id, task.id, task.rowVersion, newIdempotencyKey());
      toast({ title: t("workPackages.toast.verified") });
      await load();
    } catch (cause) { toast({ variant: "destructive", title: extractApiError(cause, t("workPackages.error.save")) }); }
  };

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={() => void load()} /></AdminLayout>;

  return <AdminLayout>
    <div className="space-y-6" data-testid="work-packages-page">
      {detail ? <>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-2">
            <Button variant="ghost" asChild className="-ml-3"><Link to="/admin/work-packages"><ArrowLeft className="mr-2 h-4 w-4" />{t("workPackages.back")}</Link></Button>
            <div className="flex flex-wrap items-center gap-2"><h1 className="text-2xl font-bold">{detail.code} · {detail.name}</h1><Badge className={statusTone[detail.status]} variant="outline">{t(`workPackages.status.${detail.status}`)}</Badge></div>
            <p className="text-sm text-muted-foreground">{detail.description || t("workPackages.noDescription")}</p>
          </div>
          <div className="flex gap-2">
            {detail.canManagePackage && detail.status === "Planned" && <Button onClick={() => void transitionPackage("Active")}>{t("workPackages.action.start")}</Button>}
            {detail.canManagePackage && detail.status === "Active" && <Button onClick={() => void transitionPackage("Completed")}>{t("workPackages.action.completePackage")}</Button>}
            {canAssign && detail.canAssignTasks && !["Completed", "Cancelled"].includes(detail.status) && <Button onClick={() => openTask()}><Plus className="mr-2 h-4 w-4" />{t("workPackages.action.addTask")}</Button>}
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <Summary icon={<BriefcaseBusiness />} label={t("workPackages.field.scope")} value={detail.kind === "Project" ? `${detail.operationalProjectCode} · ${detail.operationalProjectName}` : t("workPackages.kind.Internal")} href={detail.operationalProjectId ? `/admin/operational-projects/${detail.operationalProjectId}` : undefined} />
          <Summary icon={<Users />} label={t("workPackages.field.department")} value={t(detail.departmentLabelKey)} />
          <Summary icon={<Users />} label={t("workPackages.field.lead")} value={detail.leadUserName} />
          <Summary icon={<ClipboardList />} label={t("workPackages.field.contract")} value={detail.contractNumber || "—"} href={detail.contractId ? `/admin/contracts/${detail.contractId}` : undefined} />
        </div>

        <section className="rounded-xl border bg-card">
          <div className="flex items-center justify-between border-b p-4"><div><h2 className="font-semibold">{t("workPackages.tasks.title")}</h2><p className="text-sm text-muted-foreground">{t("workPackages.tasks.help")}</p></div><span className="text-sm font-medium">{detail.progressPercent}%</span></div>
          <div className="p-4"><Progress value={detail.progressPercent} /></div>
          <div className="divide-y">
            {detail.tasks.map(task => <article key={task.id} id={`task-${task.id}`} data-testid="work-task-row" className={params.get("taskId") === String(task.id) ? "bg-amber-50 p-4" : "p-4"}>
              <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                <div className="min-w-0 space-y-1"><div className="flex flex-wrap items-center gap-2"><Badge variant="outline">{task.wbsCode}</Badge><h3 className="font-medium">{task.title}</h3><Badge variant="secondary">{t(`workPackages.taskStatus.${task.status}`)}</Badge>{task.kpiDefinitionCode && <Badge>{t("workPackages.tasks.kpi")}</Badge>}</div><p className="text-sm text-muted-foreground">{task.assigneeName} · {dateValue(task.plannedStart)} → {dateValue(task.plannedEnd)}</p></div>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="w-12 text-right text-sm font-medium">{task.progressPercent}%</span>
                  {canAssign && detail.canAssignTasks && ["Planned", "InProgress"].includes(task.status) && <Button variant="outline" size="sm" onClick={() => openTask(task)}>{t("common.edit")}</Button>}
                  {detail.status === "Active" && task.canUpdateProgress && ["Planned", "InProgress"].includes(task.status) && <Button variant="outline" size="sm" onClick={() => openProgress(task)}>{t("workPackages.action.progress")}</Button>}
                  {detail.status === "Active" && task.canUpdateProgress && ["Planned", "InProgress"].includes(task.status) && <Button size="sm" onClick={() => void progressTask(task, true)}>{t("workPackages.action.completeTask")}</Button>}
                  {detail.status === "Active" && task.canVerify && <Button size="sm" onClick={() => void verifyTask(task)}><CheckCircle2 className="mr-2 h-4 w-4" />{t("workPackages.action.verify")}</Button>}
                </div>
              </div>
            </article>)}
            {detail.tasks.length === 0 && <div className="p-10 text-center text-sm text-muted-foreground">{t("workPackages.tasks.empty")}</div>}
          </div>
        </section>
      </> : <>
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div><h1 className="text-3xl font-bold">{t("workPackages.title")}</h1><p className="mt-1 text-muted-foreground">{t("workPackages.subtitle")}</p></div>
          {canCreate && <Button onClick={openCreatePackage}><Plus className="mr-2 h-4 w-4" />{t("workPackages.action.create")}</Button>}
        </div>
        <Tabs value={kind} onValueChange={value => { const next = new URLSearchParams(); next.set("kind", value); setParams(next); }}>
          <TabsList><TabsTrigger value="Project">{t("workPackages.tab.project")}</TabsTrigger><TabsTrigger value="Internal">{t("workPackages.tab.internal")}</TabsTrigger></TabsList>
        </Tabs>
        <div className="relative max-w-xl"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={search} onChange={event => setSearch(event.target.value)} placeholder={t("workPackages.search")} className="pl-9" /></div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {rows.map(item => <button key={item.id} type="button" className="rounded-xl border bg-card p-5 text-left transition hover:border-primary/50 hover:shadow-sm" onClick={() => navigate(`/admin/work-packages/${item.id}`)}>
            <div className="flex items-start justify-between gap-2"><Badge variant="outline">{item.code}</Badge><Badge className={statusTone[item.status]} variant="outline">{t(`workPackages.status.${item.status}`)}</Badge></div>
            <h2 className="mt-4 font-semibold">{item.name}</h2><p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{item.kind === "Project" ? `${item.operationalProjectCode} · ${item.operationalProjectName}` : t("workPackages.kind.Internal")}</p>
            <div className="mt-4 flex items-center justify-between text-sm"><span>{t(item.departmentLabelKey)} · {item.leadUserName}</span><strong>{item.progressPercent}%</strong></div><Progress value={item.progressPercent} className="mt-2" />
          </button>)}
          {rows.length === 0 && <div className="col-span-full rounded-xl border border-dashed p-12 text-center text-muted-foreground">{t("workPackages.empty")}</div>}
        </div>
      </>}
    </div>

    <Dialog open={packageOpen} onOpenChange={setPackageOpen}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{t("workPackages.create.title")}</DialogTitle><DialogDescription>{t("workPackages.create.help")}</DialogDescription></DialogHeader><div className="grid gap-4 sm:grid-cols-2">
      <Field label={t("workPackages.field.type")}><Select value={packageForm.kind} onValueChange={(value: WorkPackageKind) => setPackageForm(current => ({ ...current, kind: value, operationalProjectId: null, contractId: null, sourceOpportunityId: null, departmentRoleGroupId: 0, participantDepartmentRoleGroupIds: [], leadUserId: 0 }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Project">{t("workPackages.kind.Project")}</SelectItem>{canAssign && <SelectItem value="Internal">{t("workPackages.kind.Internal")}</SelectItem>}</SelectContent></Select></Field>
      {packageForm.kind === "Project" && <Field label={t("workPackages.field.project")}><Select value={packageForm.operationalProjectId?.toString() ?? ""} onValueChange={value => setPackageForm(current => ({ ...current, operationalProjectId: Number(value), contractId: null }))}><SelectTrigger><SelectValue placeholder={t("workPackages.placeholder.project")} /></SelectTrigger><SelectContent>{projects.map(project => <SelectItem key={project.id} value={String(project.id)}>{project.code} · {project.name}</SelectItem>)}</SelectContent></Select></Field>}
      {packageForm.kind === "Project" && <Field label={t("workPackages.field.contract")}><Select value={packageForm.contractId?.toString() ?? "none"} onValueChange={value => setPackageForm(current => ({ ...current, contractId: value === "none" ? null : Number(value) }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("workPackages.contract.none")}</SelectItem>{contracts.map(contract => <SelectItem key={contract.id} value={String(contract.id)}>{contract.contractNumber}</SelectItem>)}</SelectContent></Select></Field>}
      <Field label={t("workPackages.field.name")} className="sm:col-span-2"><Input maxLength={300} value={packageForm.name} onChange={event => setPackageForm(current => ({ ...current, name: event.target.value }))} /></Field>
      <Field label={t("workPackages.field.department")}><Select value={packageForm.departmentRoleGroupId ? String(packageForm.departmentRoleGroupId) : ""} onValueChange={value => setPackageForm(current => ({ ...current, departmentRoleGroupId: Number(value), participantDepartmentRoleGroupIds: current.participantDepartmentRoleGroupIds.filter(id => id !== Number(value)), leadUserId: 0 }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{availableDepartments.map(group => <SelectItem key={group.id} value={String(group.id)}>{t(group.labelKey)}</SelectItem>)}</SelectContent></Select></Field>
      <Field label={t("workPackages.field.lead")}><Select value={packageForm.leadUserId ? String(packageForm.leadUserId) : ""} onValueChange={value => setPackageForm(current => ({ ...current, leadUserId: Number(value) }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{departmentUsers.map(user => <SelectItem key={user.userId} value={String(user.userId)}>{user.name}</SelectItem>)}</SelectContent></Select></Field>
      {packageForm.kind === "Internal" && <Field label={t("workPackages.field.participants")} className="sm:col-span-2"><ChoiceGrid emptyLabel={t("workPackages.participants.none")} items={options.departments.filter(group => group.id !== packageForm.departmentRoleGroupId).map(group => ({ id: group.id, label: t(group.labelKey) }))} selected={packageForm.participantDepartmentRoleGroupIds} onChange={participantDepartmentRoleGroupIds => setPackageForm(current => ({ ...current, participantDepartmentRoleGroupIds }))} /></Field>}
      <Field label={t("workPackages.field.start")}><Input type="date" value={dateValue(packageForm.plannedStart)} onChange={event => setPackageForm(current => ({ ...current, plannedStart: event.target.value ? apiDate(event.target.value) : null }))} /></Field>
      <Field label={t("workPackages.field.end")}><Input type="date" value={dateValue(packageForm.plannedEnd)} onChange={event => setPackageForm(current => ({ ...current, plannedEnd: event.target.value ? apiDate(event.target.value) : null }))} /></Field>
      <Field label={t("workPackages.field.description")} className="sm:col-span-2"><Textarea maxLength={4000} value={packageForm.description ?? ""} onChange={event => setPackageForm(current => ({ ...current, description: event.target.value }))} /></Field>
    </div><DialogFooter><Button variant="outline" onClick={() => setPackageOpen(false)}>{t("common.cancel")}</Button><Button disabled={saving} onClick={() => void savePackage()}>{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={taskOpen} onOpenChange={setTaskOpen}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{t(taskEditing ? "workPackages.task.edit" : "workPackages.task.create")}</DialogTitle><DialogDescription>{t("workPackages.task.help")}</DialogDescription></DialogHeader><div className="grid gap-4 sm:grid-cols-2">
      <Field label={t("workPackages.field.wbs")}><Input maxLength={120} value={taskForm.wbsCode} onChange={event => setTaskForm(current => ({ ...current, wbsCode: event.target.value }))} /></Field>
      <Field label={t("workPackages.field.taskName")}><Input maxLength={300} value={taskForm.title} onChange={event => setTaskForm(current => ({ ...current, title: event.target.value }))} /></Field>
      {detail?.kind === "Internal" && <Field label={t("workPackages.field.department")}><Select value={taskForm.departmentRoleGroupId?.toString() ?? ""} onValueChange={value => setTaskForm(current => ({ ...current, departmentRoleGroupId: Number(value), assigneeUserId: 0, approverUserId: null, ccUserIds: [] }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{detail.participantDepartments.map(group => <SelectItem key={group.id} value={String(group.id)}>{t(group.labelKey)}</SelectItem>)}</SelectContent></Select></Field>}
      <Field label={t("workPackages.field.assignee")}><Select value={taskForm.assigneeUserId ? String(taskForm.assigneeUserId) : ""} onValueChange={value => setTaskForm(current => ({ ...current, assigneeUserId: Number(value), ccUserIds: current.ccUserIds.filter(id => id !== Number(value)), approverUserId: current.approverUserId === Number(value) ? null : current.approverUserId }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{taskUsers.map(user => <SelectItem key={user.userId} value={String(user.userId)}>{user.name}</SelectItem>)}</SelectContent></Select></Field>
      <Field label={t("workPackages.field.approver")}><Select value={taskForm.approverUserId?.toString() ?? "lead"} onValueChange={value => setTaskForm(current => ({ ...current, approverUserId: value === "lead" ? null : Number(value) }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="lead">{t("workPackages.approver.lead")}</SelectItem>{taskUsers.filter(user => user.userId !== taskForm.assigneeUserId).map(user => <SelectItem key={user.userId} value={String(user.userId)}>{user.name}</SelectItem>)}</SelectContent></Select></Field>
      <Field label={t("workPackages.field.start")}><Input type="date" value={taskForm.plannedStart} onChange={event => setTaskForm(current => ({ ...current, plannedStart: event.target.value }))} /></Field>
      <Field label={t("workPackages.field.end")}><Input type="date" value={taskForm.plannedEnd} onChange={event => setTaskForm(current => ({ ...current, plannedEnd: event.target.value }))} /></Field>
      <Field label={t("workPackages.field.kpi")} className="sm:col-span-2"><Select value={taskForm.kpiDefinitionId?.toString() ?? "none"} onValueChange={value => setTaskForm(current => ({ ...current, kpiDefinitionId: value === "none" ? null : Number(value) }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("workPackages.kpi.none")}</SelectItem>{options.kpiDefinitions.map(definition => <SelectItem key={definition.id} value={String(definition.id)}>{t(definition.nameKey)}</SelectItem>)}</SelectContent></Select></Field>
      <Field label={t("workPackages.field.predecessors")} className="sm:col-span-2"><ChoiceGrid emptyLabel={t("workPackages.predecessors.none")} items={(detail?.tasks ?? []).filter(task => task.id !== taskEditing?.id).map(task => ({ id: task.id, label: `${task.wbsCode} · ${task.title}` }))} selected={taskForm.predecessorTaskIds} onChange={predecessorTaskIds => setTaskForm(current => ({ ...current, predecessorTaskIds }))} /></Field>
      <Field label={t("workPackages.field.cc")} className="sm:col-span-2"><ChoiceGrid emptyLabel={t("workPackages.cc.none")} items={taskUsers.filter(user => user.userId !== taskForm.assigneeUserId).map(user => ({ id: user.userId, label: user.name }))} selected={taskForm.ccUserIds} onChange={ccUserIds => setTaskForm(current => ({ ...current, ccUserIds }))} /></Field>
      <Field label={t("workPackages.field.description")} className="sm:col-span-2"><Textarea maxLength={4000} value={taskForm.description ?? ""} onChange={event => setTaskForm(current => ({ ...current, description: event.target.value }))} /></Field>
      <Field label={t("workPackages.field.resources")} className="sm:col-span-2"><Textarea maxLength={2000} value={taskForm.resources ?? ""} onChange={event => setTaskForm(current => ({ ...current, resources: event.target.value }))} /></Field>
    </div><DialogFooter><Button variant="outline" onClick={() => setTaskOpen(false)}>{t("common.cancel")}</Button><Button disabled={saving} onClick={() => void saveTask()}>{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={progressOpen} onOpenChange={setProgressOpen}><DialogContent className="sm:max-w-md"><DialogHeader><DialogTitle>{t("workPackages.progress.title")}</DialogTitle><DialogDescription>{progressTaskEditing?.wbsCode} · {progressTaskEditing?.title}</DialogDescription></DialogHeader><Field label={t("workPackages.progress.percent")}><Input type="number" inputMode="numeric" min={1} max={99} value={progressPercent} onChange={event => setProgressPercent(Number(event.target.value))} /><p className="text-xs text-muted-foreground">{t("workPackages.progress.help")}</p></Field><DialogFooter><Button variant="outline" onClick={() => setProgressOpen(false)}>{t("common.cancel")}</Button><Button disabled={!progressTaskEditing || progressPercent < 1 || progressPercent > 99} onClick={() => progressTaskEditing && void progressTask(progressTaskEditing, false, progressPercent)}>{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>
  </AdminLayout>;
};

const Field = ({ label, children, className = "" }: { label: string; children: React.ReactNode; className?: string }) => <div className={`space-y-2 ${className}`}><Label>{label}</Label>{children}</div>;
const ChoiceGrid = ({ items, selected, onChange, emptyLabel }: { items: { id: number; label: string }[]; selected: number[]; onChange: (ids: number[]) => void; emptyLabel: string }) => items.length === 0 ? <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">{emptyLabel}</p> : <div className="grid max-h-36 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">{items.map(item => <label key={item.id} className="flex cursor-pointer items-start gap-2 text-sm"><Checkbox checked={selected.includes(item.id)} onCheckedChange={checked => onChange(checked ? [...selected, item.id] : selected.filter(id => id !== item.id))} /><span>{item.label}</span></label>)}</div>;
const Summary = ({ icon, label, value, href }: { icon: React.ReactNode; label: string; value: string; href?: string }) => <div className="rounded-xl border bg-card p-4"><div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground"><span className="[&>svg]:h-4 [&>svg]:w-4">{icon}</span>{label}</div>{href ? <Link className="font-medium text-primary hover:underline" to={href}>{value}</Link> : <p className="font-medium">{value}</p>}</div>;

export default DepartmentWorkPackages;
