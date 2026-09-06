import { useDeferredValue, useEffect, useState } from "react";
import {
  AlertTriangle,
  Check,
  ClipboardCheck,
  FilePenLine,
  MapPin,
  Plus,
  Search,
  ShieldAlert,
  Wrench,
  X,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
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
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type CorrectHseViolationRequest,
  type HseViolationFieldsRequest,
  type HseViolationResponse,
  type HseViolationSeverity,
  type HseViolationStatus,
  type OperationalProjectListItemResponse,
  type UpdateHseViolationRequest,
} from "@/services/adminApi";

const STATUSES: HseViolationStatus[] = [
  "Draft",
  "Reported",
  "Confirmed",
  "Remediated",
  "Closed",
  "Rejected",
  "Cancelled",
];
const SEVERITIES: HseViolationSeverity[] = ["Low", "Medium", "High", "Critical"];

const statusStyle: Record<HseViolationStatus, string> = {
  Draft: "border-zinc-200 bg-zinc-100 text-zinc-700",
  Reported: "border-sky-200 bg-sky-50 text-sky-800",
  Confirmed: "border-amber-200 bg-amber-50 text-amber-900",
  Remediated: "border-teal-200 bg-teal-50 text-teal-800",
  Closed: "border-emerald-200 bg-emerald-50 text-emerald-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-800",
  Cancelled: "border-zinc-300 bg-zinc-100 text-zinc-600",
};

const severityStyle: Record<HseViolationSeverity, string> = {
  Low: "border-emerald-200 bg-emerald-50 text-emerald-800",
  Medium: "border-sky-200 bg-sky-50 text-sky-800",
  High: "border-orange-200 bg-orange-50 text-orange-900",
  Critical: "border-rose-300 bg-rose-100 text-rose-900",
};

interface UserOption {
  id: number;
  name: string;
}

interface HseForm {
  occurredAt: string;
  location: string;
  category: string;
  severity: HseViolationSeverity;
  description: string;
  regulatoryReference: string;
  responsibleSiteUserId: string;
  remediationOwnerUserId: string;
  remediationDeadline: string;
  remediationNote: string;
  penaltyReference: string;
  penaltyAmount: string;
  reason: string;
}

type FormMode = "create" | "edit" | "correct";
type DecisionAction = "reject" | "cancel" | "remediate" | null;

const emptyForm = (): HseForm => ({
  occurredAt: new Date().toISOString().slice(0, 16),
  location: "",
  category: "",
  severity: "Low",
  description: "",
  regulatoryReference: "",
  responsibleSiteUserId: "",
  remediationOwnerUserId: "",
  remediationDeadline: "",
  remediationNote: "",
  penaltyReference: "",
  penaltyAmount: "",
  reason: "",
});

const fromRecord = (row: HseViolationResponse): HseForm => ({
  occurredAt: new Date(row.occurredAt).toISOString().slice(0, 16),
  location: row.location,
  category: row.category,
  severity: row.severity,
  description: row.description,
  regulatoryReference: row.regulatoryReference ?? "",
  responsibleSiteUserId: String(row.responsibleSiteUserId),
  remediationOwnerUserId: row.remediationOwnerUserId ? String(row.remediationOwnerUserId) : "",
  remediationDeadline: row.remediationDeadline
    ? new Date(row.remediationDeadline).toISOString().slice(0, 16)
    : "",
  remediationNote: row.remediationNote ?? "",
  penaltyReference: row.penaltyReference ?? "",
  penaltyAmount: row.penaltyAmount == null ? "" : String(row.penaltyAmount),
  reason: "",
});

const dateTime = (value: string | null | undefined, lang: string) =>
  value ? new Date(value).toLocaleString(lang) : "-";

const HseViolationsPage = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionHseManage);
  const canConfirm = has(ADMIN_PERMS.constructionHseConfirm);
  const canClose = has(ADMIN_PERMS.constructionHseClose);

  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [projectId, setProjectId] = useState<number | null>(null);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [rows, setRows] = useState<HseViolationResponse[]>([]);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<HseViolationStatus, number>>>({});
  const [status, setStatus] = useState<HseViolationStatus | "all">("all");
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search.trim());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [detail, setDetail] = useState<HseViolationResponse | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>("create");
  const [formRecord, setFormRecord] = useState<HseViolationResponse | null>(null);
  const [form, setForm] = useState<HseForm>(() => emptyForm());
  const [saving, setSaving] = useState(false);

  const [decisionAction, setDecisionAction] = useState<DecisionAction>(null);
  const [decisionRecord, setDecisionRecord] = useState<HseViolationResponse | null>(null);
  const [decisionReason, setDecisionReason] = useState("");
  const [remediationOwnerId, setRemediationOwnerId] = useState("");
  const [remediationNote, setRemediationNote] = useState("");
  const [acting, setActing] = useState(false);

  useEffect(() => {
    let active = true;
    void adminApi.listOperationalProjects({ pageSize: 200 }).then(({ data }) => {
      if (!active) return;
      const available = data.items ?? [];
      setProjects(available);
      setProjectId((current) => current ?? available[0]?.id ?? null);
    }).catch((requestError) => {
      if (active) setError(extractApiError(requestError));
    });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!projectId) {
      setRows([]);
      setLoading(false);
      return;
    }
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      adminApi.listHseViolations(projectId, {
        status: status === "all" ? undefined : status,
        search: deferredSearch || undefined,
        pageSize: 200,
      }),
      adminApi.getOperationalProjectTeam(projectId),
    ]).then(([listResponse, teamResponse]) => {
      if (!active) return;
      setRows(listResponse.data.items ?? []);
      setStatusCounts(listResponse.data.statusCounts ?? {});
      const project = projects.find((item) => item.id === projectId);
      const options = new Map<number, string>();
      if (project?.projectManagerUserId) {
        options.set(project.projectManagerUserId, project.projectManagerName ?? `#${project.projectManagerUserId}`);
      }
      for (const member of teamResponse.data.members.filter((item) => item.isActive)) {
        options.set(member.userId, member.userName);
      }
      setUsers(Array.from(options, ([id, name]) => ({ id, name })));
    }).catch((requestError) => {
      if (active) setError(extractApiError(requestError));
    }).finally(() => {
      if (active) setLoading(false);
    });
    return () => { active = false; };
  }, [deferredSearch, projectId, projects, status]);

  const refresh = async (focusId?: number) => {
    if (!projectId) return;
    const { data } = await adminApi.listHseViolations(projectId, {
      status: status === "all" ? undefined : status,
      search: deferredSearch || undefined,
      pageSize: 200,
    });
    setRows(data.items ?? []);
    setStatusCounts(data.statusCounts ?? {});
    if (focusId) {
      const focused = await adminApi.getHseViolation(projectId, focusId);
      setDetail(focused.data);
    }
  };

  const openCreate = () => {
    const next = emptyForm();
    const project = projects.find((item) => item.id === projectId);
    if (project?.projectManagerUserId) next.responsibleSiteUserId = String(project.projectManagerUserId);
    setForm(next);
    setFormRecord(null);
    setFormMode("create");
    setFormOpen(true);
  };

  const openEdit = (row: HseViolationResponse, mode: FormMode) => {
    setForm(fromRecord(row));
    setFormRecord(row);
    setFormMode(mode);
    setFormOpen(true);
  };

  const buildFields = (): HseViolationFieldsRequest | null => {
    if (!form.occurredAt || !form.location.trim() || !form.category.trim() ||
        !form.description.trim() || !form.responsibleSiteUserId) return null;
    return {
      offlineClientId: formRecord?.offlineClientId ?? crypto.randomUUID(),
      occurredAt: new Date(form.occurredAt).toISOString(),
      location: form.location.trim(),
      category: form.category.trim(),
      severity: form.severity,
      description: form.description.trim(),
      regulatoryReference: form.regulatoryReference.trim() || null,
      evidenceDocuments: formRecord?.evidenceDocuments ?? [],
      responsibleSiteUserId: Number(form.responsibleSiteUserId),
      remediationOwnerUserId: form.remediationOwnerUserId ? Number(form.remediationOwnerUserId) : null,
      remediationDeadline: form.remediationDeadline ? new Date(form.remediationDeadline).toISOString() : null,
      remediationNote: form.remediationNote.trim() || null,
      penaltyReference: form.penaltyReference.trim() || null,
      penaltyAmount: form.penaltyAmount ? Number(form.penaltyAmount) : null,
    };
  };

  const saveForm = async () => {
    if (!projectId) return;
    const fields = buildFields();
    if (!fields || formMode === "correct" && form.reason.trim().length < 3) {
      toast({ title: t("hse.required"), variant: "destructive" });
      return;
    }
    setSaving(true);
    try {
      let saved: HseViolationResponse;
      if (formMode === "create") {
        saved = (await adminApi.createHseViolation(projectId, fields, crypto.randomUUID())).data;
      } else if (formMode === "edit" && formRecord) {
        const body: UpdateHseViolationRequest = { ...fields, rowVersion: formRecord.rowVersion };
        saved = (await adminApi.updateHseViolation(projectId, formRecord.id, body, crypto.randomUUID())).data;
      } else if (formRecord) {
        const body: CorrectHseViolationRequest = {
          ...fields,
          rowVersion: formRecord.rowVersion,
          reason: form.reason.trim(),
        };
        saved = (await adminApi.correctHseViolation(projectId, formRecord.id, body, crypto.randomUUID())).data;
      } else return;
      setFormOpen(false);
      setDetail(saved);
      await refresh(saved.id);
      toast({ title: t("hse.saved") });
    } catch (requestError) {
      toast({ title: extractApiError(requestError), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  };

  const transition = async (
    row: HseViolationResponse,
    action: "report" | "confirm" | "reject" | "remediate" | "cancel" | "close",
    extra: { reason?: string; remediationOwnerUserId?: number; remediationNote?: string } = {},
  ) => {
    if (!projectId) return;
    setActing(true);
    try {
      const { data } = await adminApi.transitionHseViolation(
        projectId,
        row.id,
        action,
        { ...extra, rowVersion: row.rowVersion },
        crypto.randomUUID(),
      );
      setDetail(data);
      setDecisionAction(null);
      await refresh(data.id);
      toast({ title: t("hse.transitioned") });
    } catch (requestError) {
      toast({ title: extractApiError(requestError), variant: "destructive" });
    } finally {
      setActing(false);
    }
  };

  const openDecision = (row: HseViolationResponse, action: DecisionAction) => {
    setDecisionRecord(row);
    setDecisionAction(action);
    setDecisionReason("");
    setRemediationOwnerId(row.remediationOwnerUserId ? String(row.remediationOwnerUserId) : String(row.responsibleSiteUserId));
    setRemediationNote(row.remediationNote ?? "");
  };

  const submitDecision = async () => {
    if (!decisionRecord || !decisionAction) return;
    if (decisionAction === "remediate") {
      if (!remediationOwnerId || !remediationNote.trim()) return;
      await transition(decisionRecord, "remediate", {
        remediationOwnerUserId: Number(remediationOwnerId),
        remediationNote: remediationNote.trim(),
      });
      return;
    }
    if (!decisionReason.trim()) return;
    await transition(decisionRecord, decisionAction, { reason: decisionReason.trim() });
  };

  const actionButtons = (row: HseViolationResponse) => (
    <div className="flex flex-wrap gap-2">
      {canManage && row.status === "Draft" && <>
        <Button size="sm" variant="outline" onClick={() => openEdit(row, "edit")}><FilePenLine className="mr-2 h-4 w-4" />{t("hse.edit")}</Button>
        <Button size="sm" onClick={() => void transition(row, "report")}><ShieldAlert className="mr-2 h-4 w-4" />{t("hse.action.report")}</Button>
      </>}
      {canConfirm && row.status === "Reported" && <>
        <Button size="sm" onClick={() => void transition(row, "confirm")}><Check className="mr-2 h-4 w-4" />{t("hse.action.confirm")}</Button>
        <Button size="sm" variant="destructive" onClick={() => openDecision(row, "reject")}><X className="mr-2 h-4 w-4" />{t("hse.action.reject")}</Button>
      </>}
      {canManage && row.status === "Confirmed" &&
        <Button size="sm" onClick={() => openDecision(row, "remediate")}><Wrench className="mr-2 h-4 w-4" />{t("hse.action.remediate")}</Button>}
      {canConfirm && row.status === "Confirmed" &&
        <Button size="sm" variant="outline" onClick={() => openDecision(row, "cancel")}><X className="mr-2 h-4 w-4" />{t("hse.action.cancel")}</Button>}
      {canClose && row.status === "Remediated" &&
        <Button size="sm" onClick={() => void transition(row, "close")}><ClipboardCheck className="mr-2 h-4 w-4" />{t("hse.action.close")}</Button>}
      {canConfirm && ["Confirmed", "Remediated", "Closed"].includes(row.status) &&
        <Button size="sm" variant="outline" onClick={() => openEdit(row, "correct")}><FilePenLine className="mr-2 h-4 w-4" />{t("hse.correct")}</Button>}
    </div>
  );

  return (
    <AdminLayout>
      <div className="mx-auto w-full max-w-7xl space-y-6 px-3 py-4 sm:px-6 sm:py-6">
        <header className="flex flex-col gap-4 border-b border-zinc-200 pb-5 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <div className="mb-2 flex items-center gap-2 text-rose-700"><ShieldAlert className="h-5 w-5" /><span className="text-sm font-semibold">HSE</span></div>
            <h1 className="text-2xl font-bold text-zinc-950 sm:text-3xl">{t("hse.title")}</h1>
            <p className="mt-1 max-w-2xl text-sm text-zinc-600">{t("hse.subtitle")}</p>
          </div>
          {canManage && <Button disabled={!projectId} onClick={openCreate}><Plus className="mr-2 h-4 w-4" />{t("hse.new")}</Button>}
        </header>

        <section className="grid gap-3 lg:grid-cols-[minmax(260px,1fr)_minmax(260px,1fr)_220px]">
          <div className="space-y-1.5">
            <Label>{t("hse.project")}</Label>
            <Select value={projectId ? String(projectId) : ""} onValueChange={(value) => setProjectId(Number(value))}>
              <SelectTrigger><SelectValue placeholder={t("hse.selectProject")} /></SelectTrigger>
              <SelectContent>{projects.map((project) => <SelectItem key={project.id} value={String(project.id)}>{project.code} - {project.name}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="hse-search">{t("hse.search")}</Label>
            <div className="relative"><Search className="absolute left-3 top-2.5 h-4 w-4 text-zinc-400" /><Input id="hse-search" value={search} onChange={(event) => setSearch(event.target.value)} className="pl-9" /></div>
          </div>
          <div className="space-y-1.5">
            <Label>{t("hse.allStatuses")}</Label>
            <Select value={status} onValueChange={(value) => setStatus(value as HseViolationStatus | "all")}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="all">{t("hse.allStatuses")}</SelectItem>{STATUSES.map((item) => <SelectItem key={item} value={item}>{t(`hse.status.${item}`)}</SelectItem>)}</SelectContent>
            </Select>
          </div>
        </section>

        <div className="flex gap-2 overflow-x-auto pb-1">
          {STATUSES.map((item) => <button key={item} type="button" onClick={() => setStatus(item)} className={cn("shrink-0 border px-3 py-2 text-xs font-semibold", statusStyle[item])}>{t(`hse.status.${item}`)} <span className="ml-1 tabular-nums">{statusCounts[item] ?? 0}</span></button>)}
        </div>

        {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => setProjectId((value) => value)} /> : !projectId ? (
          <div className="border-y border-dashed border-zinc-300 py-16 text-center text-sm text-zinc-600">{t("hse.selectProject")}</div>
        ) : rows.length === 0 ? (
          <div className="border-y border-dashed border-zinc-300 py-16 text-center text-sm text-zinc-600">{t("hse.empty")}</div>
        ) : <>
          <div className="space-y-3 md:hidden">
            {rows.map((row) => <button key={row.id} type="button" onClick={() => setDetail(row)} className="w-full border border-zinc-200 bg-white p-4 text-left shadow-sm">
              <div className="flex items-start justify-between gap-3"><div><p className="font-bold text-zinc-950">{row.code}</p><p className="mt-1 line-clamp-2 text-sm text-zinc-700">{row.description}</p></div><Badge variant="outline" className={statusStyle[row.status]}>{t(`hse.status.${row.status}`)}</Badge></div>
              <div className="mt-4 flex flex-wrap items-center gap-2 text-xs text-zinc-600"><Badge variant="outline" className={severityStyle[row.severity]}>{t(`hse.severity.${row.severity}`)}</Badge><span className="flex items-center gap-1"><MapPin className="h-3.5 w-3.5" />{row.location}</span><span>{dateTime(row.occurredAt, lang)}</span></div>
            </button>)}
          </div>
          <div className="hidden overflow-hidden border border-zinc-200 md:block">
            <table className="w-full text-left text-sm"><thead className="bg-zinc-100 text-xs font-semibold uppercase text-zinc-600"><tr><th className="px-4 py-3">{t("hse.field.description")}</th><th className="px-4 py-3">{t("hse.field.location")}</th><th className="px-4 py-3">{t("hse.field.responsible")}</th><th className="px-4 py-3">{t("hse.field.severity")}</th><th className="px-4 py-3">{t("hse.allStatuses")}</th></tr></thead>
              <tbody className="divide-y divide-zinc-200">{rows.map((row) => <tr key={row.id} onClick={() => setDetail(row)} className="cursor-pointer bg-white hover:bg-zinc-50"><td className="max-w-md px-4 py-3"><p className="font-semibold text-zinc-950">{row.code}</p><p className="truncate text-zinc-600">{row.description}</p></td><td className="px-4 py-3 text-zinc-700">{row.location}</td><td className="px-4 py-3 text-zinc-700">{row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}</td><td className="px-4 py-3"><Badge variant="outline" className={severityStyle[row.severity]}>{t(`hse.severity.${row.severity}`)}</Badge></td><td className="px-4 py-3"><Badge variant="outline" className={statusStyle[row.status]}>{t(`hse.status.${row.status}`)}</Badge></td></tr>)}</tbody>
            </table>
          </div>
        </>}
      </div>

      <Sheet open={Boolean(detail)} onOpenChange={(open) => { if (!open) setDetail(null); }}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-xl">
          {detail && <div className="space-y-6">
            <SheetHeader><SheetTitle className="flex items-center justify-between gap-3 pr-7"><span>{detail.code}</span><Badge variant="outline" className={statusStyle[detail.status]}>{t(`hse.status.${detail.status}`)}</Badge></SheetTitle></SheetHeader>
            <div className="border-l-4 border-rose-600 pl-4"><p className="text-sm font-semibold text-zinc-950">{detail.description}</p><p className="mt-2 text-sm text-zinc-600">{detail.category} - {detail.location}</p></div>
            <dl className="grid grid-cols-2 gap-x-4 gap-y-4 text-sm"><div><dt className="text-zinc-500">{t("hse.field.occurredAt")}</dt><dd className="mt-1 font-medium">{dateTime(detail.occurredAt, lang)}</dd></div><div><dt className="text-zinc-500">{t("hse.field.severity")}</dt><dd className="mt-1"><Badge variant="outline" className={severityStyle[detail.severity]}>{t(`hse.severity.${detail.severity}`)}</Badge></dd></div><div><dt className="text-zinc-500">{t("hse.field.responsible")}</dt><dd className="mt-1 font-medium">{detail.responsibleSiteUserName ?? `#${detail.responsibleSiteUserId}`}</dd></div><div><dt className="text-zinc-500">{t("hse.field.remediationOwner")}</dt><dd className="mt-1 font-medium">{detail.remediationOwnerUserName ?? "-"}</dd></div></dl>
            {detail.remediationNote && <div className="border border-teal-200 bg-teal-50 p-3 text-sm text-teal-950"><p className="font-semibold">{t("hse.field.remediationNote")}</p><p className="mt-1">{detail.remediationNote}</p></div>}
            {actionButtons(detail)}
            <section><h2 className="mb-3 text-sm font-bold text-zinc-950">{t("hse.history")}</h2><ol className="space-y-3 border-l border-zinc-300 pl-4">{detail.events.map((event) => <li key={event.id} className="text-sm"><p className="font-semibold text-zinc-900">{event.type === "Correction" ? t("hse.correct") : t(`hse.status.${event.toStatus}`)}</p><p className="text-xs text-zinc-500">{dateTime(event.createdAt, lang)} - {event.createdByName ?? `#${event.createdByUserId}`}</p>{event.reason && <p className="mt-1 text-zinc-700">{event.reason}</p>}</li>)}</ol></section>
          </div>}
        </SheetContent>
      </Sheet>

      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-h-[92vh] max-w-2xl overflow-y-auto">
          <DialogHeader><DialogTitle>{formMode === "create" ? t("hse.new") : formMode === "edit" ? t("hse.edit") : t("hse.correct")}</DialogTitle></DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t("hse.field.occurredAt")}><Input type="datetime-local" value={form.occurredAt} onChange={(event) => setForm({ ...form, occurredAt: event.target.value })} /></Field>
            <Field label={t("hse.field.location")}><Input value={form.location} maxLength={300} onChange={(event) => setForm({ ...form, location: event.target.value })} /></Field>
            <Field label={t("hse.field.category")}><Input value={form.category} maxLength={100} onChange={(event) => setForm({ ...form, category: event.target.value })} /></Field>
            <Field label={t("hse.field.severity")}><Select value={form.severity} onValueChange={(value) => setForm({ ...form, severity: value as HseViolationSeverity })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{SEVERITIES.map((item) => <SelectItem key={item} value={item}>{t(`hse.severity.${item}`)}</SelectItem>)}</SelectContent></Select></Field>
            <div className="sm:col-span-2"><Field label={t("hse.field.description")}><Textarea value={form.description} maxLength={4000} rows={4} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field></div>
            <Field label={t("hse.field.responsible")}><UserSelect value={form.responsibleSiteUserId} users={users} onChange={(value) => setForm({ ...form, responsibleSiteUserId: value })} /></Field>
            <Field label={t("hse.field.regulatoryReference")}><Input value={form.regulatoryReference} maxLength={500} onChange={(event) => setForm({ ...form, regulatoryReference: event.target.value })} /></Field>
            <Field label={t("hse.field.remediationOwner")}><UserSelect value={form.remediationOwnerUserId} users={users} optional onChange={(value) => setForm({ ...form, remediationOwnerUserId: value })} /></Field>
            <Field label={t("hse.field.remediationDeadline")}><Input type="datetime-local" value={form.remediationDeadline} onChange={(event) => setForm({ ...form, remediationDeadline: event.target.value })} /></Field>
            <div className="sm:col-span-2"><Field label={t("hse.field.remediationNote")}><Textarea value={form.remediationNote} maxLength={4000} onChange={(event) => setForm({ ...form, remediationNote: event.target.value })} /></Field></div>
            {canConfirm && <><Field label={t("hse.field.penaltyReference")}><Input value={form.penaltyReference} maxLength={200} onChange={(event) => setForm({ ...form, penaltyReference: event.target.value })} /></Field><Field label={t("hse.field.penaltyAmount")}><Input type="number" min="0" step="0.01" value={form.penaltyAmount} onChange={(event) => setForm({ ...form, penaltyAmount: event.target.value })} /></Field></>}
            {formMode === "correct" && <div className="sm:col-span-2"><Field label={t("hse.field.reason")}><Textarea value={form.reason} minLength={3} maxLength={2000} onChange={(event) => setForm({ ...form, reason: event.target.value })} /></Field></div>}
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setFormOpen(false)}>{t("hse.cancel")}</Button><Button disabled={saving} onClick={() => void saveForm()}>{t("hse.save")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(decisionAction)} onOpenChange={(open) => { if (!open) setDecisionAction(null); }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{decisionAction ? t(`hse.action.${decisionAction}`) : ""}</DialogTitle></DialogHeader>
          {decisionAction === "remediate" ? <div className="space-y-4"><Field label={t("hse.field.remediationOwner")}><UserSelect value={remediationOwnerId} users={users} onChange={setRemediationOwnerId} /></Field><Field label={t("hse.field.remediationNote")}><Textarea value={remediationNote} maxLength={4000} onChange={(event) => setRemediationNote(event.target.value)} /></Field></div> : <Field label={t("hse.field.reason")}><Textarea value={decisionReason} maxLength={2000} onChange={(event) => setDecisionReason(event.target.value)} /></Field>}
          <DialogFooter><Button variant="outline" onClick={() => setDecisionAction(null)}>{t("hse.cancel")}</Button><Button disabled={acting} onClick={() => void submitDecision()}>{t("hse.save")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const Field = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="space-y-1.5"><Label>{label}</Label>{children}</div>
);

const UserSelect = ({
  value,
  users,
  optional = false,
  onChange,
}: {
  value: string;
  users: UserOption[];
  optional?: boolean;
  onChange: (value: string) => void;
}) => (
  <Select value={value || (optional ? "none" : "")} onValueChange={(next) => onChange(next === "none" ? "" : next)}>
    <SelectTrigger><SelectValue /></SelectTrigger>
    <SelectContent>{optional && <SelectItem value="none">-</SelectItem>}{users.map((user) => <SelectItem key={user.id} value={String(user.id)}>{user.name}</SelectItem>)}</SelectContent>
  </Select>
);

export default HseViolationsPage;