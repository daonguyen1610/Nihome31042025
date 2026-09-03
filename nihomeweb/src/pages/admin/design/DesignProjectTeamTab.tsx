import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CheckCircle2, ClipboardList, History, Loader2, Pencil, Plus, RefreshCw, UserRound, UserRoundX, Users, X, XCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type DesignProjectResponse,
  type OperationalProjectAssignmentResponse,
  type OperationalProjectMemberResponse,
  type OperationalProjectTeamHistoryResponse,
  type OperationalProjectTeamResponse,
  type ProjectAssignmentStatus,
  type ProjectMemberCandidateResponse,
  type ProjectMemberRoleRequest,
  type UpsertOperationalProjectAssignmentRequest,
  type UpsertOperationalProjectMemberRequest,
} from "@/services/adminApi";

interface Props { project: DesignProjectResponse }
type MemberDialogMode = "add" | "edit" | "end";
type AssignmentDialogMode = "add" | "edit";
type TerminalAssignmentStatus = "Completed" | "Cancelled";
interface MemberForm extends UpsertOperationalProjectMemberRequest { startedAt: string; endedAt: string | null }
interface AssignmentForm extends UpsertOperationalProjectAssignmentRequest { plannedStart: string | null; plannedEnd: string | null }

const today = () => new Date().toISOString().slice(0, 10);
const toDateValue = (value?: string | null) => value?.slice(0, 10) ?? "";
const toUtcDate = (value?: string | null) => value ? `${value.slice(0, 10)}T00:00:00.000Z` : null;
const optional = (value?: string | null) => value?.trim() || null;
const emptyMemberForm = (): MemberForm => ({ userId: 0, position: "", reportsToMemberId: null, startedAt: today(), endedAt: null, roles: [{ roleCode: "", scope: "", scopeValue: null }] });
const emptyAssignmentForm = (): AssignmentForm => ({ workKey: "", title: "", module: "", discipline: null, parallelGroup: null, assigneeMemberId: 0, managerMemberId: null, status: "Planned", plannedStart: null, plannedEnd: null, note: null });
const assignmentBadge: Record<ProjectAssignmentStatus, string> = {
  Planned: "border-slate-200 bg-slate-50 text-slate-700",
  InProgress: "border-sky-200 bg-sky-50 text-sky-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

export const DesignProjectTeamTab = ({ project }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [team, setTeam] = useState<OperationalProjectTeamResponse | null>(null);
  const [history, setHistory] = useState<OperationalProjectTeamHistoryResponse[]>([]);
  const [candidates, setCandidates] = useState<ProjectMemberCandidateResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [memberDialog, setMemberDialog] = useState<MemberDialogMode | null>(null);
  const [editingMember, setEditingMember] = useState<OperationalProjectMemberResponse | null>(null);
  const [memberForm, setMemberForm] = useState<MemberForm>(emptyMemberForm());
  const [assignmentDialog, setAssignmentDialog] = useState<AssignmentDialogMode | null>(null);
  const [editingAssignment, setEditingAssignment] = useState<OperationalProjectAssignmentResponse | null>(null);
  const [assignmentForm, setAssignmentForm] = useState<AssignmentForm>(emptyAssignmentForm());
  const [terminalAction, setTerminalAction] = useState<{ assignment: OperationalProjectAssignmentResponse; status: TerminalAssignmentStatus } | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const memberIdempotencyKey = useRef("");
  const assignmentIdempotencyKey = useRef("");
  const operationalProjectId = project.operationalProjectId;

  const load = useCallback(async () => {
    if (!operationalProjectId) { setLoading(false); setTeam(null); return; }
    setLoading(true);
    setError(null);
    try {
      const [teamResponse, historyResponse] = await Promise.all([
        adminApi.getOperationalProjectTeam(operationalProjectId),
        adminApi.getOperationalProjectTeamHistory(operationalProjectId),
      ]);
      setTeam(teamResponse.data);
      setHistory(historyResponse.data ?? []);
      if (teamResponse.data.canManage) {
        const candidateResponse = await adminApi.getOperationalProjectTeamCandidates(operationalProjectId);
        setCandidates(candidateResponse.data ?? []);
      } else setCandidates([]);
    } catch (loadError) { setError(extractApiError(loadError)); }
    finally { setLoading(false); }
  }, [operationalProjectId]);

  useEffect(() => { void load(); }, [load]);
  const activeMembers = useMemo(() => team?.members.filter((member) => member.isActive) ?? [], [team]);
  const endedMembers = useMemo(() => team?.members.filter((member) => !member.isActive) ?? [], [team]);
  const availableCandidates = useMemo(() => {
    const activeUserIds = new Set(activeMembers.map((member) => member.userId));
    return candidates.filter((candidate) => !activeUserIds.has(candidate.userId));
  }, [activeMembers, candidates]);
  const formatDate = (value?: string | null, withTime = false) => {
    if (!value) return t("designProjects.team.value.none");
    return new Intl.DateTimeFormat(lang, withTime ? { dateStyle: "medium", timeStyle: "short" } : { dateStyle: "medium" }).format(new Date(value));
  };
  const roleLabel = (code: string) => t(`designProjects.team.role.${code}`);
  const scopeLabel = (scope: string) => t(`designProjects.team.scope.${scope}`);

  const createsReportingCycle = (memberId: number, managerId: number | null | undefined) => {
    let current = managerId ?? null;
    const visited = new Set<number>();
    while (current && !visited.has(current)) {
      if (current === memberId) return true;
      visited.add(current);
      current = team?.members.find((member) => member.id === current)?.reportsToMemberId ?? null;
    }
    return false;
  };

  const openAddMember = () => {
    const definition = team?.roleDefinitions[0];
    const form = emptyMemberForm();
    if (definition) form.roles = [{ roleCode: definition.code, scope: definition.allowedScopes[0] ?? "", scopeValue: null }];
    setEditingMember(null); setMemberForm(form); setFormError(null);
    memberIdempotencyKey.current = newIdempotencyKey(); setMemberDialog("add");
  };
  const openMember = (member: OperationalProjectMemberResponse, mode: Exclude<MemberDialogMode, "add">) => {
    setEditingMember(member);
    setMemberForm({ userId: member.userId, position: member.position, reportsToMemberId: member.reportsToMemberId ?? null, startedAt: toDateValue(member.startedAt), endedAt: mode === "end" ? today() : toDateValue(member.endedAt) || null, roles: member.roles.map((role) => ({ roleCode: role.roleCode, scope: role.scope, scopeValue: role.scopeValue ?? null })), rowVersion: member.rowVersion });
    setFormError(null); memberIdempotencyKey.current = newIdempotencyKey(); setMemberDialog(mode);
  };
  const updateRole = (index: number, patch: Partial<ProjectMemberRoleRequest>) => setMemberForm((current) => ({ ...current, roles: current.roles.map((role, roleIndex) => roleIndex === index ? { ...role, ...patch } : role) }));

  const validateMember = () => {
    const position = memberForm.position.trim();
    if (!memberForm.userId) return t("designProjects.team.validation.userRequired");
    if (position.length < 2 || position.length > 150) return t("designProjects.team.validation.positionLength");
    if (!memberForm.startedAt) return t("designProjects.team.validation.startRequired");
    if (memberDialog === "end" && !memberForm.endedAt) return t("designProjects.team.validation.endRequired");
    if (memberForm.endedAt && memberForm.endedAt < memberForm.startedAt) return t("designProjects.team.validation.memberDates");
    if (!editingMember && activeMembers.some((member) => member.userId === memberForm.userId)) return t("designProjects.team.validation.userAlreadyActive");
    if (!memberForm.roles.length) return t("designProjects.team.validation.roleRequired");
    const roleKeys = new Set<string>();
    for (const role of memberForm.roles) {
      const definition = team?.roleDefinitions.find((item) => item.code === role.roleCode);
      if (!definition || !definition.allowedScopes.includes(role.scope)) return t("designProjects.team.validation.roleInvalid");
      const scopeValue = role.scopeValue?.trim() ?? "";
      if (role.scope === "Project" && scopeValue) return t("designProjects.team.validation.projectScopeValue");
      if (role.scope !== "Project" && (!scopeValue || scopeValue.length > 80)) return t("designProjects.team.validation.scopeValue");
      const key = `${role.roleCode}|${role.scope}|${scopeValue}`.toLowerCase();
      if (roleKeys.has(key)) return t("designProjects.team.validation.duplicateRole");
      roleKeys.add(key);
    }
    if (editingMember && memberForm.reportsToMemberId === editingMember.id) return t("designProjects.team.validation.selfManager");
    if (editingMember && createsReportingCycle(editingMember.id, memberForm.reportsToMemberId)) return t("designProjects.team.validation.managerCycle");
    if (memberForm.reportsToMemberId && !activeMembers.some((member) => member.id === memberForm.reportsToMemberId)) return t("designProjects.team.validation.managerActive");
    return null;
  };
  const saveMember = async () => {
    if (!operationalProjectId) return;
    const validationError = validateMember();
    if (validationError) { setFormError(validationError); return; }
    setSaving(true); setFormError(null);
    const request: UpsertOperationalProjectMemberRequest = { ...memberForm, position: memberForm.position.trim(), startedAt: toUtcDate(memberForm.startedAt)!, endedAt: toUtcDate(memberForm.endedAt), roles: memberForm.roles.map((role) => ({ ...role, scopeValue: role.scope === "Project" ? null : optional(role.scopeValue) })) };
    try {
      if (editingMember) await adminApi.updateOperationalProjectMember(operationalProjectId, editingMember.id, request, memberIdempotencyKey.current);
      else await adminApi.addOperationalProjectMember(operationalProjectId, request, memberIdempotencyKey.current);
      toast({ title: t(memberDialog === "end" ? "designProjects.team.toast.memberEnded" : "designProjects.team.toast.memberSaved") });
      setMemberDialog(null); await load();
    } catch (saveError) { setFormError(extractApiError(saveError)); }
    finally { setSaving(false); }
  };

  const openAddAssignment = () => { setEditingAssignment(null); setAssignmentForm(emptyAssignmentForm()); setFormError(null); assignmentIdempotencyKey.current = newIdempotencyKey(); setAssignmentDialog("add"); };
  const openEditAssignment = (assignment: OperationalProjectAssignmentResponse) => {
    setEditingAssignment(assignment);
    setAssignmentForm({ workKey: assignment.workKey, title: assignment.title, module: assignment.module, discipline: assignment.discipline ?? null, parallelGroup: assignment.parallelGroup ?? null, assigneeMemberId: assignment.assigneeMemberId, managerMemberId: assignment.managerMemberId ?? null, status: assignment.status, plannedStart: toDateValue(assignment.plannedStart) || null, plannedEnd: toDateValue(assignment.plannedEnd) || null, note: assignment.note ?? null, rowVersion: assignment.rowVersion });
    setFormError(null); assignmentIdempotencyKey.current = newIdempotencyKey(); setAssignmentDialog("edit");
  };
  const validateAssignment = (form: AssignmentForm) => {
    const workKey = form.workKey.trim(), title = form.title.trim(), module = form.module.trim();
    if (workKey.length < 2 || workKey.length > 120) return t("designProjects.team.validation.workKeyLength");
    if (title.length < 2 || title.length > 300) return t("designProjects.team.validation.titleLength");
    if (module.length < 2 || module.length > 50) return t("designProjects.team.validation.moduleLength");
    if ((form.discipline?.trim().length ?? 0) > 50) return t("designProjects.team.validation.disciplineLength");
    if ((form.parallelGroup?.trim().length ?? 0) > 80) return t("designProjects.team.validation.parallelGroupLength");
    if ((form.note?.trim().length ?? 0) > 2000) return t("designProjects.team.validation.noteLength");
    if (!activeMembers.some((member) => member.id === form.assigneeMemberId)) return t("designProjects.team.validation.assigneeActive");
    if (form.managerMemberId === form.assigneeMemberId) return t("designProjects.team.validation.assignmentSelfManager");
    if (form.managerMemberId && !activeMembers.some((member) => member.id === form.managerMemberId)) return t("designProjects.team.validation.assignmentManagerActive");
    if (form.plannedStart && form.plannedEnd && form.plannedEnd < form.plannedStart) return t("designProjects.team.validation.assignmentDates");
    if (team?.assignments.some((assignment) => assignment.id !== editingAssignment?.id && assignment.workKey.trim() === workKey && assignment.assigneeMemberId === form.assigneeMemberId)) return t("designProjects.team.validation.duplicateAssignment");
    return null;
  };
  const assignmentRequest = (form: AssignmentForm): UpsertOperationalProjectAssignmentRequest => ({ ...form, workKey: form.workKey.trim(), title: form.title.trim(), module: form.module.trim(), discipline: optional(form.discipline), parallelGroup: optional(form.parallelGroup), plannedStart: toUtcDate(form.plannedStart), plannedEnd: toUtcDate(form.plannedEnd), note: optional(form.note) });
  const saveAssignment = async () => {
    if (!operationalProjectId) return;
    const validationError = validateAssignment(assignmentForm);
    if (validationError) { setFormError(validationError); return; }
    setSaving(true); setFormError(null);
    try {
      const request = assignmentRequest(assignmentForm);
      if (editingAssignment) await adminApi.updateOperationalProjectAssignment(operationalProjectId, editingAssignment.id, request, assignmentIdempotencyKey.current);
      else await adminApi.addOperationalProjectAssignment(operationalProjectId, request, assignmentIdempotencyKey.current);
      toast({ title: t("designProjects.team.toast.assignmentSaved") }); setAssignmentDialog(null); await load();
    } catch (saveError) { setFormError(extractApiError(saveError)); }
    finally { setSaving(false); }
  };
  const runTerminalAction = async () => {
    if (!operationalProjectId || !terminalAction) return;
    const { assignment, status } = terminalAction;
    setSaving(true); setFormError(null);
    try {
      const request = assignmentRequest({ workKey: assignment.workKey, title: assignment.title, module: assignment.module, discipline: assignment.discipline ?? null, parallelGroup: assignment.parallelGroup ?? null, assigneeMemberId: assignment.assigneeMemberId, managerMemberId: assignment.managerMemberId ?? null, status, plannedStart: toDateValue(assignment.plannedStart) || null, plannedEnd: toDateValue(assignment.plannedEnd) || null, note: assignment.note ?? null, rowVersion: assignment.rowVersion });
      await adminApi.updateOperationalProjectAssignment(operationalProjectId, assignment.id, request, assignmentIdempotencyKey.current);
      toast({ title: t(status === "Completed" ? "designProjects.team.toast.assignmentCompleted" : "designProjects.team.toast.assignmentCancelled") }); setTerminalAction(null); await load();
    } catch (actionError) { setFormError(extractApiError(actionError)); }
    finally { setSaving(false); }
  };
  const requestTerminalAction = (assignment: OperationalProjectAssignmentResponse, status: TerminalAssignmentStatus) => { assignmentIdempotencyKey.current = newIdempotencyKey(); setFormError(null); setTerminalAction({ assignment, status }); };

  if (loading) return <div className="flex flex-col items-center justify-center gap-3 rounded-lg border bg-white py-16 text-sm text-slate-500"><Loader2 className="h-7 w-7 animate-spin text-slate-400" /><span>{t("designProjects.team.loading")}</span></div>;
  if (!operationalProjectId) return <section className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center"><Users className="mx-auto h-8 w-8 text-slate-400" /><h2 className="mt-3 font-semibold text-slate-900">{t("designProjects.team.unlinkedTitle")}</h2><p className="mt-1 text-sm text-slate-500">{t("designProjects.team.unlinkedDescription")}</p></section>;
  if (error || !team) return <div className="rounded-lg border border-rose-200 bg-rose-50 p-5 text-sm text-rose-700"><p>{error ?? t("common.error")}</p><Button variant="outline" size="sm" className="mt-3" onClick={() => void load()}><RefreshCw className="mr-1.5 h-3.5 w-3.5" />{t("common.retry")}</Button></div>;

  return (
    <div className="space-y-4" data-testid="design-project-team-tab">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="flex items-center gap-2 text-base font-semibold text-slate-900"><Users className="h-4 w-4 text-slate-500" />{t("designProjects.team.title")}</h2><p className="mt-1 text-sm text-slate-500">{t("designProjects.team.description")}</p></div><div className="flex items-center gap-2"><Badge variant="outline">{t("designProjects.team.memberTotal", { count: team.members.length })}</Badge>{team.canManage ? <Button size="sm" onClick={openAddMember}><Plus className="mr-1 h-4 w-4" />{t("designProjects.team.action.addMember")}</Button> : null}</div></div>
        {activeMembers.length === 0 ? <div className="mt-4 rounded-md border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500">{t("designProjects.team.empty")}</div> : <>
          <ul className="mt-4 grid grid-cols-1 gap-3 lg:hidden">{activeMembers.map((member) => (
            <li key={member.id} className="rounded-lg border border-slate-200 p-4"><div className="flex items-start justify-between gap-3"><div className="flex min-w-0 items-start gap-3"><div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-600"><UserRound className="h-4 w-4" /></div><div className="min-w-0"><p className="truncate font-semibold text-slate-900">{member.userName}</p><p className="truncate text-sm text-slate-500">{member.position} · {member.email}</p></div></div>{team.canManage ? <div className="flex shrink-0 gap-1"><Button variant="ghost" size="icon" aria-label={t("designProjects.team.action.editMember")} onClick={() => openMember(member, "edit")}><Pencil className="h-4 w-4" /></Button><Button variant="ghost" size="icon" aria-label={t("designProjects.team.action.endMember")} onClick={() => openMember(member, "end")}><UserRoundX className="h-4 w-4 text-rose-600" /></Button></div> : null}</div>
              <div className="mt-3 flex flex-wrap gap-2">{member.roles.map((role, index) => { const definition = team.roleDefinitions.find((item) => item.code === role.roleCode); return <Badge key={`${role.roleCode}-${role.scope}-${role.scopeValue ?? index}`} variant="outline" className="whitespace-normal bg-slate-50">{roleLabel(role.roleCode)} · {definition?.raci ?? t("designProjects.team.value.none")} · {scopeLabel(role.scope)}{role.scopeValue ? `: ${role.scopeValue}` : ""}</Badge>; })}</div>
              <dl className="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2"><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.reportsTo")}</dt><dd>{member.reportsToName ?? t("designProjects.team.value.none")}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.memberDates")}</dt><dd>{formatDate(member.startedAt)}</dd></div></dl>
            </li>))}</ul>
          <div className="mt-4 hidden overflow-x-auto rounded-lg border border-slate-200 lg:block"><table className="min-w-full text-sm"><thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-3 py-2">{t("designProjects.team.field.user")}</th><th className="px-3 py-2">{t("designProjects.team.field.position")}</th><th className="px-3 py-2">{t("designProjects.team.field.roles")}</th><th className="px-3 py-2">{t("designProjects.team.field.reportsTo")}</th><th className="px-3 py-2">{t("designProjects.team.field.memberDates")}</th>{team.canManage ? <th className="px-3 py-2 text-right">{t("common.actions")}</th> : null}</tr></thead><tbody className="divide-y divide-slate-100">{activeMembers.map((member) => <tr key={member.id}><td className="px-3 py-3"><div className="font-medium text-slate-900">{member.userName}</div><div className="text-xs text-slate-500">{member.email}</div></td><td className="px-3 py-3 text-slate-700">{member.position}</td><td className="max-w-md px-3 py-3"><div className="flex flex-wrap gap-1.5">{member.roles.map((role, index) => { const definition = team.roleDefinitions.find((item) => item.code === role.roleCode); return <Badge key={`${role.roleCode}-${role.scope}-${role.scopeValue ?? index}`} variant="outline" className="whitespace-normal bg-slate-50">{roleLabel(role.roleCode)} · {definition?.raci ?? t("designProjects.team.value.none")} · {scopeLabel(role.scope)}{role.scopeValue ? `: ${role.scopeValue}` : ""}</Badge>; })}</div></td><td className="px-3 py-3 text-slate-700">{member.reportsToName ?? t("designProjects.team.value.none")}</td><td className="whitespace-nowrap px-3 py-3 text-slate-700">{formatDate(member.startedAt)}</td>{team.canManage ? <td className="px-3 py-3"><div className="flex justify-end gap-1"><Button variant="ghost" size="icon" aria-label={t("designProjects.team.action.editMember")} onClick={() => openMember(member, "edit")}><Pencil className="h-4 w-4" /></Button><Button variant="ghost" size="icon" aria-label={t("designProjects.team.action.endMember")} onClick={() => openMember(member, "end")}><UserRoundX className="h-4 w-4 text-rose-600" /></Button></div></td> : null}</tr>)}</tbody></table></div>
        </>}
        {endedMembers.length ? <div className="mt-5 border-t pt-4"><h3 className="text-sm font-semibold text-slate-700">{t("designProjects.team.endedMembers")}</h3><div className="mt-2 grid gap-2 md:grid-cols-2">{endedMembers.map((member) => <div key={member.id} className="flex items-center justify-between rounded-md bg-slate-50 p-3 text-sm text-slate-600"><span><strong className="text-slate-800">{member.userName}</strong> · {member.position}</span><span>{formatDate(member.endedAt)}</span></div>)}</div></div> : null}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="flex items-center gap-2 font-semibold text-slate-900"><ClipboardList className="h-4 w-4 text-slate-500" />{t("designProjects.team.assignments.title")}</h2><p className="mt-1 text-sm text-slate-500">{t("designProjects.team.assignments.description")}</p></div>{team.canManage ? <Button size="sm" disabled={!activeMembers.length} onClick={openAddAssignment}><Plus className="mr-1 h-4 w-4" />{t("designProjects.team.action.addAssignment")}</Button> : null}</div>
        {team.assignments.length === 0 ? <div className="mt-4 rounded-md border border-dashed p-8 text-center text-sm text-slate-500">{t("designProjects.team.assignments.empty")}</div> : <div className="mt-4 grid gap-3 xl:grid-cols-2">{team.assignments.map((assignment) => { const mutable = assignment.status !== "Completed" && assignment.status !== "Cancelled"; return (
          <article key={assignment.id} className="rounded-lg border border-slate-200 p-4"><div className="flex items-start justify-between gap-3"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><strong className="text-slate-900">{assignment.title}</strong><Badge variant="outline" className={assignmentBadge[assignment.status]}>{t(`designProjects.team.assignmentStatus.${assignment.status}`)}</Badge></div><p className="mt-1 break-all font-mono text-xs text-slate-500">{assignment.workKey}</p></div>{team.canManage && mutable ? <Button variant="ghost" size="icon" aria-label={t("designProjects.team.action.editAssignment")} onClick={() => openEditAssignment(assignment)}><Pencil className="h-4 w-4" /></Button> : null}</div>
            <dl className="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2"><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.assignee")}</dt><dd>{assignment.assigneeName}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.assignmentManager")}</dt><dd>{assignment.managerName ?? t("designProjects.team.value.none")}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.moduleDiscipline")}</dt><dd>{assignment.module}{assignment.discipline ? ` · ${assignment.discipline}` : ""}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.plannedDates")}</dt><dd>{formatDate(assignment.plannedStart)} – {formatDate(assignment.plannedEnd)}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.parallelGroup")}</dt><dd>{assignment.parallelGroup ?? t("designProjects.team.value.none")}</dd></div><div><dt className="text-xs text-slate-500">{t("designProjects.team.field.kpiIdentity")}</dt><dd className="break-all font-mono text-xs">{assignment.kpiIdentity}</dd></div></dl>
            {assignment.note ? <p className="mt-3 whitespace-pre-wrap rounded-md bg-slate-50 p-2 text-sm text-slate-600">{assignment.note}</p> : null}{team.canManage && mutable ? <div className="mt-3 flex flex-wrap gap-2 border-t pt-3"><Button size="sm" variant="outline" onClick={() => requestTerminalAction(assignment, "Completed")}><CheckCircle2 className="mr-1 h-4 w-4" />{t("designProjects.team.action.completeAssignment")}</Button><Button size="sm" variant="outline" className="text-rose-600" onClick={() => requestTerminalAction(assignment, "Cancelled")}><XCircle className="mr-1 h-4 w-4" />{t("designProjects.team.action.cancelAssignment")}</Button></div> : null}
          </article>); })}</div>}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"><h2 className="flex items-center gap-2 font-semibold text-slate-900"><History className="h-4 w-4 text-slate-500" />{t("designProjects.team.history.title")}</h2>{history.length === 0 ? <p className="mt-4 text-sm text-slate-500">{t("designProjects.team.history.empty")}</p> : <ol className="mt-4 space-y-2">{history.map((item) => <li key={item.id} className="flex flex-col justify-between gap-1 rounded-md border border-slate-100 bg-slate-50 p-3 text-sm sm:flex-row sm:items-center"><span><strong>{item.changedByName}</strong> · {t(`designProjects.team.history.entity.${item.entityType}`)} · {t(`designProjects.team.history.action.${item.action}`)}</span><span className="shrink-0 text-xs text-slate-500">{formatDate(item.changedAt, true)}</span></li>)}</ol>}</section>

      <Dialog open={memberDialog !== null} onOpenChange={(open) => !open && setMemberDialog(null)}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{t(`designProjects.team.dialog.member.${memberDialog ?? "add"}.title`)}</DialogTitle><DialogDescription>{t(`designProjects.team.dialog.member.${memberDialog ?? "add"}.description`)}</DialogDescription></DialogHeader><div className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2"><Label>{t("designProjects.team.field.user")} *</Label><Select disabled={memberDialog !== "add"} value={memberForm.userId ? String(memberForm.userId) : ""} onValueChange={(value) => setMemberForm((current) => ({ ...current, userId: Number(value) }))}><SelectTrigger className="mt-1"><SelectValue placeholder={t("designProjects.team.placeholder.user")} /></SelectTrigger><SelectContent>{availableCandidates.map((candidate) => <SelectItem key={candidate.userId} value={String(candidate.userId)}>{candidate.name} · {candidate.email}</SelectItem>)}</SelectContent></Select></div>
        <div className="sm:col-span-2"><Label>{t("designProjects.team.field.position")} *</Label><Input className="mt-1" maxLength={150} disabled={memberDialog === "end"} value={memberForm.position} onChange={(event) => setMemberForm((current) => ({ ...current, position: event.target.value }))} /></div>
        <div><Label>{t("designProjects.team.field.startedAt")} *</Label><Input className="mt-1" type="date" disabled={memberDialog === "end"} value={memberForm.startedAt} onChange={(event) => setMemberForm((current) => ({ ...current, startedAt: event.target.value }))} /></div>
        <div><Label>{t("designProjects.team.field.endedAt")}{memberDialog === "end" ? " *" : ""}</Label><Input className="mt-1" type="date" min={memberForm.startedAt} disabled={memberDialog !== "end"} value={memberForm.endedAt ?? ""} onChange={(event) => setMemberForm((current) => ({ ...current, endedAt: event.target.value || null }))} /></div>
        <div className="sm:col-span-2"><Label>{t("designProjects.team.field.reportsTo")}</Label><Select disabled={memberDialog === "end"} value={memberForm.reportsToMemberId ? String(memberForm.reportsToMemberId) : "none"} onValueChange={(value) => setMemberForm((current) => ({ ...current, reportsToMemberId: value === "none" ? null : Number(value) }))}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("designProjects.team.value.none")}</SelectItem>{activeMembers.filter((member) => member.id !== editingMember?.id).map((member) => <SelectItem key={member.id} value={String(member.id)}>{member.userName} · {member.position}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-3 sm:col-span-2"><div className="flex items-center justify-between"><Label>{t("designProjects.team.field.roles")} *</Label>{memberDialog !== "end" ? <Button type="button" size="sm" variant="outline" onClick={() => { const definition = team.roleDefinitions[0]; if (definition) setMemberForm((current) => ({ ...current, roles: [...current.roles, { roleCode: definition.code, scope: definition.allowedScopes[0] ?? "", scopeValue: null }] })); }}><Plus className="mr-1 h-4 w-4" />{t("designProjects.team.action.addRole")}</Button> : null}</div>
          {memberForm.roles.map((role, index) => { const definition = team.roleDefinitions.find((item) => item.code === role.roleCode); const scopeOptions = role.scope === "Module" ? team.moduleOptions : team.disciplineOptions; return <div key={index} className="grid gap-2 rounded-md border p-3 sm:grid-cols-[1fr_1fr_1fr_auto]"><Select disabled={memberDialog === "end"} value={role.roleCode} onValueChange={(value) => { const next = team.roleDefinitions.find((item) => item.code === value); updateRole(index, { roleCode: value, scope: next?.allowedScopes[0] ?? "", scopeValue: null }); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{team.roleDefinitions.map((item) => <SelectItem key={item.code} value={item.code}>{roleLabel(item.code)} · {item.raci}</SelectItem>)}</SelectContent></Select><Select disabled={memberDialog === "end"} value={role.scope} onValueChange={(value) => updateRole(index, { scope: value, scopeValue: null })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{definition?.allowedScopes.map((scope) => <SelectItem key={scope} value={scope}>{scopeLabel(scope)}</SelectItem>)}</SelectContent></Select><Select disabled={memberDialog === "end" || role.scope === "Project"} value={role.scopeValue ?? ""} onValueChange={(value) => updateRole(index, { scopeValue: value })}><SelectTrigger><SelectValue placeholder={t("designProjects.team.placeholder.scopeValue")} /></SelectTrigger><SelectContent>{scopeOptions.map((option) => <SelectItem key={option} value={option}>{option}</SelectItem>)}</SelectContent></Select>{memberDialog !== "end" ? <Button type="button" size="icon" variant="ghost" aria-label={t("designProjects.team.action.removeRole")} onClick={() => setMemberForm((current) => ({ ...current, roles: current.roles.filter((_, roleIndex) => roleIndex !== index) }))}><X className="h-4 w-4" /></Button> : null}</div>; })}
        </div></div>{formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}<DialogFooter><Button variant="outline" disabled={saving} onClick={() => setMemberDialog(null)}>{t("common.cancel")}</Button><Button variant={memberDialog === "end" ? "destructive" : "default"} disabled={saving} onClick={() => void saveMember()}>{saving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t(memberDialog === "end" ? "designProjects.team.action.confirmEndMember" : "common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={assignmentDialog !== null} onOpenChange={(open) => !open && setAssignmentDialog(null)}><DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{t(`designProjects.team.dialog.assignment.${assignmentDialog ?? "add"}.title`)}</DialogTitle><DialogDescription>{t("designProjects.team.dialog.assignment.description")}</DialogDescription></DialogHeader><div className="grid gap-4 sm:grid-cols-2">
        <div><Label>{t("designProjects.team.field.workKey")} *</Label><Input className="mt-1" maxLength={120} disabled={assignmentDialog === "edit"} value={assignmentForm.workKey} onChange={(event) => setAssignmentForm((current) => ({ ...current, workKey: event.target.value }))} /></div><div><Label>{t("designProjects.team.field.assignmentStatus")} *</Label><Select value={assignmentForm.status} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, status: value as ProjectAssignmentStatus }))}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent>{(["Planned", "InProgress"] as const).map((status) => <SelectItem key={status} value={status}>{t(`designProjects.team.assignmentStatus.${status}`)}</SelectItem>)}</SelectContent></Select></div>
        <div className="sm:col-span-2"><Label>{t("designProjects.team.field.assignmentTitle")} *</Label><Input className="mt-1" maxLength={300} value={assignmentForm.title} onChange={(event) => setAssignmentForm((current) => ({ ...current, title: event.target.value }))} /></div><div><Label>{t("designProjects.team.field.module")} *</Label><Select value={assignmentForm.module} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, module: value }))}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent>{team.moduleOptions.map((option) => <SelectItem key={option} value={option}>{option}</SelectItem>)}</SelectContent></Select></div><div><Label>{t("designProjects.team.field.discipline")}</Label><Select value={assignmentForm.discipline ?? "none"} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, discipline: value === "none" ? null : value }))}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("designProjects.team.value.none")}</SelectItem>{team.disciplineOptions.map((option) => <SelectItem key={option} value={option}>{option}</SelectItem>)}</SelectContent></Select></div>
        <div><Label>{t("designProjects.team.field.assignee")} *</Label><Select disabled={assignmentDialog === "edit"} value={assignmentForm.assigneeMemberId ? String(assignmentForm.assigneeMemberId) : ""} onValueChange={(value) => setAssignmentForm((current) => { const assigneeMemberId = Number(value); return { ...current, assigneeMemberId, managerMemberId: current.managerMemberId === assigneeMemberId ? null : current.managerMemberId }; })}><SelectTrigger className="mt-1"><SelectValue placeholder={t("designProjects.team.placeholder.member")} /></SelectTrigger><SelectContent>{activeMembers.map((member) => <SelectItem key={member.id} value={String(member.id)}>{member.userName} · {member.position}</SelectItem>)}</SelectContent></Select></div><div><Label>{t("designProjects.team.field.assignmentManager")}</Label><Select value={assignmentForm.managerMemberId ? String(assignmentForm.managerMemberId) : "none"} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, managerMemberId: value === "none" ? null : Number(value) }))}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("designProjects.team.value.none")}</SelectItem>{activeMembers.filter((member) => member.id !== assignmentForm.assigneeMemberId).map((member) => <SelectItem key={member.id} value={String(member.id)}>{member.userName} · {member.position}</SelectItem>)}</SelectContent></Select></div>
        <div><Label>{t("designProjects.team.field.plannedStart")}</Label><Input className="mt-1" type="date" value={assignmentForm.plannedStart ?? ""} onChange={(event) => setAssignmentForm((current) => ({ ...current, plannedStart: event.target.value || null }))} /></div><div><Label>{t("designProjects.team.field.plannedEnd")}</Label><Input className="mt-1" type="date" min={assignmentForm.plannedStart ?? undefined} value={assignmentForm.plannedEnd ?? ""} onChange={(event) => setAssignmentForm((current) => ({ ...current, plannedEnd: event.target.value || null }))} /></div><div className="sm:col-span-2"><Label>{t("designProjects.team.field.parallelGroup")}</Label><Input className="mt-1" maxLength={80} value={assignmentForm.parallelGroup ?? ""} onChange={(event) => setAssignmentForm((current) => ({ ...current, parallelGroup: event.target.value }))} /></div><div className="sm:col-span-2"><Label>{t("designProjects.team.field.note")}</Label><Textarea className="mt-1" maxLength={2000} value={assignmentForm.note ?? ""} onChange={(event) => setAssignmentForm((current) => ({ ...current, note: event.target.value }))} /></div>
      </div>{formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}<DialogFooter><Button variant="outline" disabled={saving} onClick={() => setAssignmentDialog(null)}>{t("common.cancel")}</Button><Button disabled={saving} onClick={() => void saveAssignment()}>{saving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={terminalAction !== null} onOpenChange={(open) => !open && setTerminalAction(null)}><DialogContent className="sm:max-w-md"><DialogHeader><DialogTitle>{t(terminalAction?.status === "Completed" ? "designProjects.team.dialog.complete.title" : "designProjects.team.dialog.cancel.title")}</DialogTitle><DialogDescription>{t(terminalAction?.status === "Completed" ? "designProjects.team.dialog.complete.description" : "designProjects.team.dialog.cancel.description", { title: terminalAction?.assignment.title ?? "" })}</DialogDescription></DialogHeader>{formError ? <p className="text-sm font-medium text-rose-600">{formError}</p> : null}<DialogFooter><Button variant="outline" disabled={saving} onClick={() => setTerminalAction(null)}>{t("common.cancel")}</Button><Button variant={terminalAction?.status === "Cancelled" ? "destructive" : "default"} disabled={saving} onClick={() => void runTerminalAction()}>{saving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.confirm")}</Button></DialogFooter></DialogContent></Dialog>
    </div>
  );
};