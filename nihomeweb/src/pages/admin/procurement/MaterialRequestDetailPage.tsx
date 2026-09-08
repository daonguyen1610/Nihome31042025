import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowLeft, Ban, Check, Edit3, Plus, Send, Trash2, X } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type KpiUserOptionResponse,
  type MaterialRequestBoqLineOptionResponse,
  type MaterialRequestDetailResponse,
  type MaterialRequestResponse,
  type MaterialRequestUpsertRequest,
} from "@/services/adminApi";
import { useAppSelector } from "@/store";

type UserOption = { userId: number; userName: string };
type RequestLineDraft = MaterialRequestUpsertRequest["lines"][number] & { draftId: string };
type Decision = { approved: boolean } | null;

const statusClass: Record<string, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  PartiallyFulfilled: "border-amber-200 bg-amber-50 text-amber-800",
  Fulfilled: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-slate-200 bg-slate-50 text-slate-500",
};

const toLocalDateTime = (value: string) => {
  const date = new Date(value);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
};

const MaterialRequestDetailPage = () => {
  const { projectId: projectIdParam, requestId: requestIdParam } = useParams();
  const projectId = Number(projectIdParam);
  const requestId = Number(requestIdParam);
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUser = useAppSelector((state) => state.auth.user);
  const canManage = has(ADMIN_PERMS.procurementMaterialRequestsManage);
  const canApprove = has(ADMIN_PERMS.procurementMaterialRequestsApprove);
  const canViewContracts = has(ADMIN_PERMS.contracts);

  const [request, setRequest] = useState<MaterialRequestDetailResponse | null>(null);
  const [boqLines, setBoqLines] = useState<MaterialRequestBoqLineOptionResponse[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [procurementUsers, setProcurementUsers] = useState<UserOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [decision, setDecision] = useState<Decision>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [form, setForm] = useState({ responsibleSiteUserId: 0, assignedProcurementUserId: 0, requiredAt: "", note: "" });
  const [lines, setLines] = useState<RequestLineDraft[]>([]);

  const load = useCallback(async () => {
    if (!Number.isInteger(projectId) || projectId < 1 || !Number.isInteger(requestId) || requestId < 1) {
      setError(t("procurement.request.detail.invalidRoute"));
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [requestResult, contextResult, teamResult, kpiUsersResult] = await Promise.all([
        adminApi.getMaterialRequest(projectId, requestId),
        adminApi.listMaterialRequests(projectId, { page: 1, pageSize: 1 }),
        adminApi.getOperationalProjectTeam(projectId).catch(() => ({ data: null })),
        adminApi.listKpiEligibleUsers().catch(() => ({ data: [] as KpiUserOptionResponse[] })),
      ]);
      const detail = requestResult.data;
      setRequest(detail);
      setBoqLines(contextResult.data.currentApprovedBoq?.lines ?? []);
      const activeMembers = (teamResult.data?.members ?? []).filter((member) => member.isActive);
      const teamUsers: UserOption[] = activeMembers
        .map((member) => ({ userId: member.userId, userName: member.userName }));
      const fallbackUsers = kpiUsersResult.data.map((user) => ({ userId: user.userId, userName: user.userName }));
      const relatedUsers: UserOption[] = [
        { userId: detail.responsibleSiteUserId, userName: detail.responsibleSiteUserName ?? `#${detail.responsibleSiteUserId}` },
        { userId: detail.assignedProcurementUserId, userName: detail.assignedProcurementUserName ?? `#${detail.assignedProcurementUserId}` },
      ];
      if (currentUser) relatedUsers.push({ userId: currentUser.userId, userName: currentUser.fullName });
      setUsers(Array.from(new Map([...teamUsers, ...fallbackUsers, ...relatedUsers].map((user) => [user.userId, user])).values()));
      const procurementCandidates: UserOption[] = [
        ...activeMembers
          .filter((member) => member.position.toUpperCase() === "PROCUREMENT" || member.roles.some((role) => role.roleCode === "PROCUREMENT" && !role.endedAt))
          .map((member) => ({ userId: member.userId, userName: member.userName })),
        ...kpiUsersResult.data
          .filter((user) => user.positionCode === "PROCUREMENT")
          .map((user) => ({ userId: user.userId, userName: user.userName })),
        { userId: detail.assignedProcurementUserId, userName: detail.assignedProcurementUserName ?? `#${detail.assignedProcurementUserId}` },
      ];
      if (currentUser?.role === "PROCUREMENT") procurementCandidates.push({ userId: currentUser.userId, userName: currentUser.fullName });
      setProcurementUsers(Array.from(new Map(procurementCandidates.map((user) => [user.userId, user])).values()));
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [currentUser, projectId, requestId, t]);

  useEffect(() => { void load(); }, [load]);

  const number = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }), [lang]);
  const dateTime = (value?: string | null) => value
    ? new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
    : t("procurement.request.detail.notAvailable");

  const openEdit = () => {
    if (!request) return;
    setForm({
      responsibleSiteUserId: request.responsibleSiteUserId,
      assignedProcurementUserId: request.assignedProcurementUserId,
      requiredAt: toLocalDateTime(request.requiredAt),
      note: request.note ?? "",
    });
    setLines(request.lines.map((line) => ({
      draftId: String(line.id),
      projectBoqLineId: line.projectBoqLineId,
      requestedQuantity: line.requestedQuantity,
    })));
    setFormError(null);
    setEditOpen(true);
  };

  const lineOptions = useMemo(() => {
    const current = new Map(boqLines.map((line) => [line.id, line]));
    for (const line of request?.lines ?? []) {
      if (!current.has(line.projectBoqLineId)) {
        current.set(line.projectBoqLineId, {
          id: line.projectBoqLineId,
          itemCode: line.itemCode,
          description: line.description,
          unit: line.unit,
          approvedQuantity: line.boqApprovedQuantity,
          remainingQuantity: line.boqRemainingQuantity,
        });
      }
    }
    return Array.from(current.values());
  }, [boqLines, request?.lines]);

  const validateEdit = () => {
    if (form.responsibleSiteUserId < 1 || form.assignedProcurementUserId < 1 || !form.requiredAt || new Date(form.requiredAt) <= new Date()) {
      return t("procurement.validation.request");
    }
    const ids = lines.map((line) => line.projectBoqLineId);
    if (lines.length === 0 || ids.some((id) => id < 1) || ids.length !== new Set(ids).size) {
      return t("procurement.validation.requestLines");
    }
    for (const line of lines) {
      const option = lineOptions.find((item) => item.id === line.projectBoqLineId);
      if (!option || line.requestedQuantity <= 0 || line.requestedQuantity > option.remainingQuantity) {
        return t("procurement.validation.requestAllowance");
      }
    }
    return null;
  };

  const runAction = async (operation: () => Promise<{ data: MaterialRequestResponse }>, successKey: string) => {
    setBusy(true);
    setFormError(null);
    try {
      const response = await operation();
      setRequest((current) => current ? { ...current, ...response.data } : null);
      setEditOpen(false);
      setDecision(null);
      setCancelOpen(false);
      setReason("");
      toast({ title: t(successKey) });
    } catch (actionError) {
      const message = extractApiError(actionError);
      setFormError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const save = () => {
    if (!request) return;
    const validationError = validateEdit();
    if (validationError) {
      setFormError(validationError);
      return;
    }
    void runAction(() => adminApi.updateMaterialRequest(projectId, request.id, {
      responsibleSiteUserId: form.responsibleSiteUserId,
      assignedProcurementUserId: form.assignedProcurementUserId,
      requiredAt: new Date(form.requiredAt).toISOString(),
      note: form.note.trim() || null,
      rowVersion: request.rowVersion,
      lines: lines.map(({ projectBoqLineId, requestedQuantity }) => ({ projectBoqLineId, requestedQuantity })),
    }), "procurement.success.requestUpdated");
  };

  const submit = () => request && void runAction(
    () => adminApi.submitMaterialRequest(projectId, request.id, request.rowVersion),
    "procurement.success.requestSubmitted",
  );

  const decide = () => {
    if (!request || decision == null) return;
    if (!decision.approved && reason.trim().length < 3) {
      setFormError(t("procurement.validation.rejectionReason"));
      return;
    }
    void runAction(
      () => adminApi.decideMaterialRequest(projectId, request.id, decision.approved, request.rowVersion, reason.trim() || undefined),
      decision.approved ? "procurement.success.approved" : "procurement.success.rejected",
    );
  };

  const cancel = () => {
    if (!request) return;
    if (reason.trim().length < 3) {
      setFormError(t("procurement.validation.cancellationReason"));
      return;
    }
    void runAction(
      () => adminApi.cancelMaterialRequest(projectId, request.id, request.rowVersion, reason.trim()),
      "procurement.success.requestCancelled",
    );
  };

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error || !request) return <AdminLayout><div className="p-4 sm:p-6"><PageError message={error ?? t("procurement.request.detail.notFound")} onRetry={() => void load()} /></div></AdminLayout>;

  const editable = canManage && request.status === "Draft" && currentUser?.userId === request.siteRequesterUserId;
  const submittable = editable;
  const cancellable = canManage && !["Fulfilled", "Rejected", "Cancelled"].includes(request.status);
  const lifecycle = [
    { label: t("procurement.request.history.created"), value: request.createdAt, actor: request.siteRequesterName },
    { label: t("procurement.request.history.submitted"), value: request.submittedAt, actor: request.submittedByName },
    { label: t("procurement.request.history.approved"), value: request.approvedAt, actor: request.approvedByName },
    { label: t("procurement.request.history.rejected"), value: request.rejectedAt, actor: request.rejectedByName },
    { label: t("procurement.request.history.fulfilled"), value: request.fulfilledAt, actor: null },
    { label: t("procurement.status.Cancelled"), value: request.cancelledAt, actor: null },
    { label: t("procurement.request.history.updated"), value: request.updatedAt, actor: null },
  ].filter((item) => item.value);

  return (
    <AdminLayout>
      <div className="space-y-6 p-4 sm:p-6">
        <nav aria-label={t("procurement.request.detail.context")} className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span>{request.customerName}</span><span aria-hidden="true">/</span>
          <span>{request.operationalProjectCode} · {request.operationalProjectName}</span><span aria-hidden="true">/</span>
          <span className="font-medium text-foreground">{request.code}</span>
        </nav>

        <header className="flex flex-col gap-4 border-b pb-5 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <Button asChild variant="ghost" size="sm" className="mb-2 -ml-3"><Link to={`/admin/procurement-control?projectId=${projectId}&tab=requests`}><ArrowLeft className="mr-2 h-4 w-4" />{t("common.back")}</Link></Button>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-semibold">{request.code}</h1>
              <Badge variant="outline" className={statusClass[request.status] ?? ""}>{t(`procurement.status.${request.status}`)}</Badge>
            </div>
            <p className="mt-1 text-sm text-muted-foreground">{t("procurement.request.detail.subtitle")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            {editable && <Button variant="outline" onClick={openEdit}><Edit3 className="mr-2 h-4 w-4" />{t("common.edit")}</Button>}
            {submittable && <Button onClick={submit} disabled={busy}><Send className="mr-2 h-4 w-4" />{t("procurement.action.submit")}</Button>}
            {canApprove && request.status === "Submitted" && <>
              <Button onClick={() => { setReason(""); setFormError(null); setDecision({ approved: true }); }} disabled={busy}><Check className="mr-2 h-4 w-4" />{t("procurement.action.approve")}</Button>
              <Button variant="destructive" onClick={() => { setReason(""); setFormError(null); setDecision({ approved: false }); }} disabled={busy}><X className="mr-2 h-4 w-4" />{t("procurement.action.reject")}</Button>
            </>}
            {cancellable && <Button variant="outline" onClick={() => { setReason(""); setFormError(null); setCancelOpen(true); }} disabled={busy}><Ban className="mr-2 h-4 w-4" />{t("procurement.action.cancelRequest")}</Button>}
          </div>
        </header>

        <section aria-labelledby="request-overview-heading" className="grid gap-5 border-b pb-6 sm:grid-cols-2 lg:grid-cols-4">
          <h2 id="request-overview-heading" className="sr-only">{t("procurement.request.detail.overview")}</h2>
          <Datum label={t("procurement.field.requester")} value={request.siteRequesterName ?? `#${request.siteRequesterUserId}`} />
          <Datum label={t("procurement.field.responsibleSite")} value={request.responsibleSiteUserName ?? `#${request.responsibleSiteUserId}`} />
          <Datum label={t("procurement.field.procurementOwner")} value={request.assignedProcurementUserName ?? `#${request.assignedProcurementUserId}`} />
          <Datum label={t("procurement.field.requiredAt")} value={dateTime(request.requiredAt)} />
        </section>

        <section aria-labelledby="request-lines-heading" className="space-y-3">
          <div><h2 id="request-lines-heading" className="text-lg font-semibold">{t("procurement.request.detail.linesTitle")}</h2><p className="text-sm text-muted-foreground">{t("procurement.request.detail.linesDescription")}</p></div>
          <div className="grid gap-3 xl:hidden">
            {request.lines.map((line) => <article className="rounded-md border p-4" data-testid={`material-request-line-card-${line.id}`} key={line.id}>
              <div className="flex items-start justify-between gap-3"><div className="min-w-0"><h3 className="break-words font-semibold">{line.itemCode}</h3><p className="mt-1 break-words text-sm text-muted-foreground">{line.description}</p></div><Badge variant="outline" className="shrink-0">{line.unit}</Badge></div>
              <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3"><Datum label={t("procurement.field.requestedQuantity")} value={number.format(line.requestedQuantity)} /><Datum label={t("procurement.field.receivedQuantity")} value={number.format(line.receivedQuantity)} /><Datum label={t("procurement.field.boqApprovedQuantity")} value={number.format(line.boqApprovedQuantity)} /><Datum label={t("procurement.field.boqRemainingQuantity")} value={number.format(line.boqRemainingQuantity)} /></dl>
            </article>)}
          </div>
          <div className="hidden overflow-x-auto border-y xl:block"><table className="w-full min-w-[800px] text-sm"><thead><tr className="border-b text-left text-xs uppercase text-muted-foreground"><th className="px-3 py-3 font-medium">{t("procurement.field.itemCode")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.description")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.unit")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.requestedQuantity")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.receivedQuantity")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.boqRemainingQuantity")}</th></tr></thead><tbody>{request.lines.map((line) => <tr className="border-b" key={line.id}><td className="px-3 py-3 font-medium">{line.itemCode}</td><td className="px-3 py-3">{line.description}</td><td className="px-3 py-3">{line.unit}</td><td className="px-3 py-3 text-right">{number.format(line.requestedQuantity)}</td><td className="px-3 py-3 text-right">{number.format(line.receivedQuantity)}</td><td className="px-3 py-3 text-right">{number.format(line.boqRemainingQuantity)}</td></tr>)}</tbody></table></div>
        </section>

        <div className="grid gap-7 lg:grid-cols-2">
          <section aria-labelledby="request-context-heading" className="space-y-3 border-t pt-5">
            <h2 id="request-context-heading" className="text-lg font-semibold">{t("procurement.request.detail.businessContext")}</h2>
            <dl className="grid gap-3 sm:grid-cols-2"><Datum label={t("procurement.request.detail.customer")} value={request.customerName} /><Datum label={t("procurement.project.label")} value={`${request.operationalProjectCode} · ${request.operationalProjectName}`} /></dl>
            <h3 className="text-sm font-semibold">{t("procurement.request.detail.contracts", { count: request.contracts.length })}</h3>
            {request.contracts.length === 0 ? <p className="text-sm text-muted-foreground">{t("procurement.request.detail.noContracts")}</p> : <ul className="divide-y border-y text-sm">{request.contracts.map((contract) => <li key={contract.id} className="flex flex-wrap items-center justify-between gap-2 py-3">{canViewContracts ? <Link className="font-medium text-primary hover:underline" to={`/admin/contracts/${contract.id}`}>{contract.contractNumber}</Link> : <span className="font-medium">{contract.contractNumber}</span>}<span className="text-muted-foreground">{contract.vendorName ?? request.customerName} · {t(`contracts.status.${contract.status}`)}</span></li>)}</ul>}
          </section>

          <section aria-labelledby="request-history-heading" className="space-y-3 border-t pt-5">
            <h2 id="request-history-heading" className="text-lg font-semibold">{t("procurement.request.detail.history")}</h2>
            <ol className="space-y-3">{lifecycle.map((item) => <li key={item.label} className="grid grid-cols-[8px_1fr] gap-3"><span className="mt-1.5 h-2 w-2 rounded-full bg-primary" /><div><p className="text-sm font-medium">{item.label}</p><p className="text-sm text-muted-foreground">{dateTime(item.value)}{item.actor ? ` · ${item.actor}` : ""}</p></div></li>)}</ol>
            {request.decisionReason && <div className="border-l-2 border-rose-400 pl-3"><p className="text-xs font-medium text-muted-foreground">{t("procurement.field.decisionReason")}</p><p className="text-sm">{request.decisionReason}</p></div>}
          </section>
        </div>

        <section aria-labelledby="request-note-heading" className="space-y-2 border-t pt-5"><h2 id="request-note-heading" className="text-lg font-semibold">{t("procurement.field.note")}</h2><p className="whitespace-pre-wrap text-sm text-muted-foreground">{request.note || t("procurement.request.detail.noNote")}</p></section>
      </div>

      <Dialog open={editOpen} onOpenChange={(open) => { if (!busy) setEditOpen(open); }}><DialogContent className="max-h-[92vh] w-[95vw] max-w-4xl overflow-y-auto"><DialogHeader><DialogTitle>{t("procurement.request.edit.title", { code: request.code })}</DialogTitle><DialogDescription>{t("procurement.request.edit.description")}</DialogDescription></DialogHeader><RequestEditForm form={form} setForm={setForm} lines={lines} setLines={setLines} users={users} procurementUsers={procurementUsers} boqLines={lineOptions} t={t} />{formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setEditOpen(false)} disabled={busy}>{t("common.cancel")}</Button><Button onClick={save} disabled={busy}>{busy ? t("common.saving") : t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={decision != null} onOpenChange={(open) => { if (!open && !busy) setDecision(null); }}><DialogContent><DialogHeader><DialogTitle>{t(decision?.approved ? "procurement.decision.approveTitle" : "procurement.decision.rejectTitle")}</DialogTitle><DialogDescription>{t("procurement.decision.description")}</DialogDescription></DialogHeader><ReasonField value={reason} setValue={setReason} t={t} />{formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setDecision(null)} disabled={busy}>{t("common.cancel")}</Button><Button variant={decision?.approved ? "default" : "destructive"} onClick={decide} disabled={busy}>{busy ? t("common.saving") : t(decision?.approved ? "procurement.action.approve" : "procurement.action.reject")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={cancelOpen} onOpenChange={(open) => { if (!open && !busy) setCancelOpen(false); }}><DialogContent><DialogHeader><DialogTitle>{t("procurement.request.cancel.title")}</DialogTitle><DialogDescription>{t("procurement.request.cancel.description")}</DialogDescription></DialogHeader><ReasonField value={reason} setValue={setReason} placeholder={t("procurement.request.cancel.reasonPlaceholder")} t={t} />{formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setCancelOpen(false)} disabled={busy}>{t("common.back")}</Button><Button variant="destructive" onClick={cancel} disabled={busy}>{busy ? t("common.saving") : t("procurement.action.cancelRequest")}</Button></DialogFooter></DialogContent></Dialog>
    </AdminLayout>
  );
};

const Datum = ({ label, value }: { label: string; value: string }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium">{value}</dd></div>;
type Translate = (key: string, params?: Record<string, string | number>) => string;

const UserField = ({ id, label, value, onChange, users, t }: { id: string; label: string; value: number; onChange: (value: number) => void; users: UserOption[]; t: Translate }) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label><Select value={value ? String(value) : ""} onValueChange={(next) => onChange(Number(next))}><SelectTrigger id={id}><SelectValue placeholder={t("procurement.lookup.userPlaceholder")} /></SelectTrigger><SelectContent>{users.map((user) => <SelectItem value={String(user.userId)} key={user.userId}>{user.userName}</SelectItem>)}</SelectContent></Select></div>;

const RequestEditForm = ({ form, setForm, lines, setLines, users, procurementUsers, boqLines, t }: { form: { responsibleSiteUserId: number; assignedProcurementUserId: number; requiredAt: string; note: string }; setForm: React.Dispatch<React.SetStateAction<typeof form>>; lines: RequestLineDraft[]; setLines: React.Dispatch<React.SetStateAction<RequestLineDraft[]>>; users: UserOption[]; procurementUsers: UserOption[]; boqLines: MaterialRequestBoqLineOptionResponse[]; t: Translate }) => <div className="space-y-4"><div className="grid gap-4 sm:grid-cols-2"><UserField id="request-detail-site-user" label={t("procurement.field.responsibleSite")} value={form.responsibleSiteUserId} onChange={(value) => setForm((current) => ({ ...current, responsibleSiteUserId: value }))} users={users} t={t} /><UserField id="request-detail-procurement-user" label={t("procurement.field.procurementOwner")} value={form.assignedProcurementUserId} onChange={(value) => setForm((current) => ({ ...current, assignedProcurementUserId: value }))} users={procurementUsers} t={t} /><div className="space-y-2"><Label htmlFor="request-detail-required-at">{t("procurement.field.requiredAt")}</Label><Input id="request-detail-required-at" type="datetime-local" value={form.requiredAt} onChange={(event) => setForm((current) => ({ ...current, requiredAt: event.target.value }))} /></div><div className="space-y-2"><Label htmlFor="request-detail-note">{t("procurement.field.note")}</Label><Input id="request-detail-note" maxLength={2000} value={form.note} onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))} /></div></div><div className="space-y-3"><div className="flex items-center justify-between"><Label>{t("procurement.field.lines")}</Label><Button type="button" variant="outline" size="sm" onClick={() => setLines((current) => [...current, { draftId: crypto.randomUUID(), projectBoqLineId: 0, requestedQuantity: 1 }])}><Plus className="mr-2 h-4 w-4" />{t("procurement.action.addLine")}</Button></div>{lines.map((line, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_1fr_auto]" key={line.draftId}><div className="space-y-2"><Label>{t("procurement.field.item")}</Label><Select value={line.projectBoqLineId ? String(line.projectBoqLineId) : ""} onValueChange={(value) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, projectBoqLineId: Number(value) } : item))}><SelectTrigger aria-label={t("procurement.field.item")}><SelectValue placeholder={t("procurement.lookup.boqLinePlaceholder")} /></SelectTrigger><SelectContent>{boqLines.map((option) => <SelectItem value={String(option.id)} key={option.id}>{option.itemCode} · {option.description} · {t("procurement.request.remaining", { quantity: option.remainingQuantity })}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label htmlFor={`request-detail-quantity-${index}`}>{t("procurement.field.requestedQuantity")}</Label><Input id={`request-detail-quantity-${index}`} type="number" min={0.000001} step="any" value={line.requestedQuantity} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, requestedQuantity: Number(event.target.value) } : item))} /></div><div className="self-end"><Button type="button" variant="ghost" size="icon" title={t("procurement.action.removeLine")} aria-label={t("procurement.action.removeLine")} disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></div>)}</div></div>;

const ReasonField = ({ value, setValue, placeholder, t }: { value: string; setValue: (value: string) => void; placeholder?: string; t: Translate }) => <div className="space-y-2"><Label htmlFor="material-request-reason">{t("procurement.field.decisionReason")}</Label><Textarea id="material-request-reason" maxLength={2000} value={value} onChange={(event) => setValue(event.target.value)} placeholder={placeholder ?? t("procurement.decision.reasonPlaceholder")} /></div>;

export default MaterialRequestDetailPage;
