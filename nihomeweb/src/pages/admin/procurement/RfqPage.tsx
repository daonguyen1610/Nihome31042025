import { useEffect, useRef, useState, type MouseEvent } from "react";
import { MoveHorizontal } from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { usePermissions } from "@/hooks/usePermissions";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { adminApi, type OperationalProjectListItemResponse } from "@/services/adminApi";
import { rfqApi, saveRfqDownload, type RfqDetail, type RfqFilter, type RfqHeader, type RfqReferences, type RfqStatus } from "@/services/rfqApi";
import { RfqBatchAwardForm, RfqBidForm, RfqDraftForm, RfqEvaluationForm, RfqField, rfqSelectClass } from "./RfqForms";

const statuses: RfqStatus[] = ["Draft", "Issued", "UnderEvaluation", "Awarded", "Closed", "Cancelled"];
const emptyReferences: RfqReferences = { revisions: [], vendors: [], owners: [], documents: [] };

export default function RfqPage() {
  const { t } = useI18n();
  const [params, setParams] = useSearchParams();
  const projectId = Number(params.get("projectId")) || 0;
  const rfqId = Number(params.get("rfqId")) || 0;
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError("");
    (async () => {
      const first = (await adminApi.listOperationalProjects({ page: 1, pageSize: 100 })).data;
      const rows = [...first.items];
      for (let page = 2; page <= Math.ceil(first.total / 100); page++)
        rows.push(...(await adminApi.listOperationalProjects({ page, pageSize: 100 })).data.items);
      if (!cancelled) setProjects(rows);
    })().catch(e => { if (!cancelled) setError(extractApiError(e)); }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [retry]);
  const project = projects.find(x => x.id === projectId);
  return <AdminLayout>
    <div className="space-y-6">
      <header><h1 className="text-2xl font-semibold">{t("rfq.title")}</h1><p className="mt-2 text-sm text-muted-foreground">{t("rfq.subtitle")}</p></header>
      {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => setRetry(x => x + 1)} /> : <>
        <div className="max-w-xl space-y-1.5"><p className="text-sm font-medium">{t("procurement.project.label")}</p><SearchableSelect
          ariaLabel={t("procurement.project.label")}
          value={projectId ? String(projectId) : null}
          onChange={value => setParams(value ? { projectId: value } : {})}
          options={projects.map(projectOption => ({ value: String(projectOption.id), label: `${projectOption.code} · ${projectOption.name}`, hint: projectOption.customerName }))}
          placeholder={t("procurement.project.placeholder")}
          searchPlaceholder={t("rfq.projectSearch")}
          emptyText={t("procurement.project.empty")}
        /></div>
        {!projectId ? <PageEmpty message={t("procurement.project.emptySelection")} /> : !project ? <PageError message={t("rfq.projectUnavailable")} /> :
          <RfqWorkspace key={projectId} project={project} rfqId={rfqId} onSelect={id => setParams(id ? { projectId: String(projectId), rfqId: String(id) } : { projectId: String(projectId) })} />}
      </>}
    </div>
  </AdminLayout>;
}

function RfqWorkspace({ project, rfqId, onSelect }: { project: OperationalProjectListItemResponse; rfqId: number; onSelect: (id: number) => void }) {
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const projectId = project.id;
  const mounted = useRef(false);
  const dialogTriggerNameRef = useRef<"create" | "edit" | "bid" | "evaluate" | "award" | null>(null);
  const dialogClosedRef = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const mutable = project.status !== "Completed" && project.status !== "Cancelled";
  const canManage = has("proc.rfqs.manage") && mutable;
  const canAward = has("proc.rfqs.award") && mutable;
  const canExport = has("proc.rfqs.export");
  const [filter, setFilter] = useState<RfqFilter>({ page: 1, pageSize: 20, sortBy: "updatedAt", sortDirection: "desc" });
  const [rows, setRows] = useState<RfqHeader[]>([]);
  const [total, setTotal] = useState(0);
  const [references, setReferences] = useState<RfqReferences>(emptyReferences);
  const [detail, setDetail] = useState<RfqDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionError, setActionError] = useState("");
  const [success, setSuccess] = useState("");
  const [refresh, setRefresh] = useState(0);
  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<"create" | "edit" | "bid" | "evaluate" | "award" | null>(null);
  const [reason, setReason] = useState("");
  const [documentId, setDocumentId] = useState(0);
  const filterJson = JSON.stringify(filter);
  const number = new Intl.NumberFormat(lang, { maximumFractionDigits: 4 });
  const quantity = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
  const date = (value: string | null) => value ? new Date(value).toLocaleString(lang) : "—";
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError("");
    setDetail(null);
    const query = JSON.parse(filterJson) as RfqFilter;
    Promise.all([rfqApi.list(projectId, query), rfqApi.references(projectId), rfqId ? rfqApi.detail(projectId, rfqId) : Promise.resolve(null)])
      .then(([list, refs, item]) => {
        if (cancelled) return;
        setRows(list.data.items); setTotal(list.data.total); setReferences(refs.data); setDetail(item?.data ?? null);
      }).catch(e => { if (!cancelled) setError(extractApiError(e)); }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [projectId, rfqId, filterJson, refresh]);
  useEffect(() => { setReason(""); setDocumentId(0); setDialog(null); setActionError(""); }, [rfqId]);
  useEffect(() => {
    if (dialog !== null || loading || !dialogClosedRef.current || !dialogTriggerNameRef.current) return;
    const frame = requestAnimationFrame(() => {
      const trigger = document.querySelector<HTMLButtonElement>(
        `[data-rfq-dialog-trigger="${dialogTriggerNameRef.current}"]`);
      if (trigger) {
        trigger.focus();
        dialogTriggerNameRef.current = null;
      }
    });
    return () => cancelAnimationFrame(frame);
  }, [dialog, loading]);
  function changeFilter(value: Partial<RfqFilter>) { setFilter(previous => ({ ...previous, ...value, page: 1 })); }
  async function mutate(operation: () => Promise<{ data: RfqDetail }>) {
    if (busy) return;
    setBusy(true); setActionError(""); setSuccess("");
    try {
      const result = await operation();
      if (!mounted.current) return;
      setDetail(result.data); setDialog(null); setReason(""); setDocumentId(0);
      setSuccess(t("rfq.saved")); onSelect(result.data.header.id); setRefresh(x => x + 1);
    } catch (e) { if (mounted.current) setActionError(extractApiError(e)); }
    finally { if (mounted.current) setBusy(false); }
  }
  async function upload(file: File) {
    if (busy) return;
    setBusy(true); setActionError("");
    try {
      await rfqApi.upload(projectId, file);
      const refs = (await rfqApi.references(projectId)).data;
      if (mounted.current) setReferences(refs);
    } catch (e) { if (mounted.current) setActionError(extractApiError(e)); }
    finally { if (mounted.current) setBusy(false); }
  }
  async function openAward(event: MouseEvent<HTMLButtonElement>) {
    if (!detail || busy) return;
    dialogTriggerNameRef.current = "award";
    dialogClosedRef.current = false;
    setBusy(true); setActionError("");
    try {
      const fresh = (await rfqApi.detail(projectId, detail.header.id)).data;
      if (!mounted.current) return;
      setDetail(fresh);
      setDialog("award");
    } catch (reason) { if (mounted.current) setActionError(extractApiError(reason)); }
    finally { if (mounted.current) setBusy(false); }
  }
  async function download(operation: () => Promise<{ data: Blob }>, filename: string) {
    setBusy(true); setActionError("");
    try { saveRfqDownload((await operation()).data, filename); }
    catch (e) { setActionError(extractApiError(e)); }
    finally { setBusy(false); }
  }
  const transition = (action: string) => {
    if (!detail) return;
    if (action === "cancel" && reason.trim().length < 3) { setActionError(t("rfq.validation.reason")); return; }
    void mutate(() => rfqApi.transition(projectId, detail.header.id, action, detail.header.rowVersion, reason.trim()));
  };
  const statusBadge = (row: RfqHeader) => <span className="flex flex-wrap gap-2"><Badge variant="outline">{t(`rfq.status.${row.status}`)}</Badge>{row.overdue && <Badge variant="destructive">{t("rfq.overdue")}</Badge>}</span>;
  const openDialog = (event: MouseEvent<HTMLButtonElement>, value: "create" | "edit" | "bid" | "evaluate") => {
    dialogTriggerNameRef.current = value;
    dialogClosedRef.current = false;
    setActionError("");
    setDialog(value);
  };

  return <div className="space-y-5">
    <nav aria-label={t("rfq.context")} className="flex flex-wrap gap-2 text-sm text-muted-foreground">
      <span>{project.customerName}</span><span>→</span><Link to={`/admin/operational-projects/${projectId}`}>{project.code} · {project.name}</Link>
      {detail && <><span>→</span><span className="break-all">{detail.header.code}</span></>}
      {detail?.contractId && <><span>→</span><Link to={`/admin/contracts/${detail.contractId}`}>{detail.contractNumber}</Link></>}
    </nav>
    <div className="flex flex-wrap gap-2">
      {rfqId ? <Button variant="outline" disabled={busy} onClick={() => onSelect(0)}>{t("rfq.back")}</Button> : canManage && <Button data-rfq-dialog-trigger="create" disabled={loading || busy} onClick={event => openDialog(event, "create")}>{t("rfq.create")}</Button>}
      <Button variant="outline" disabled={busy} onClick={() => setRefresh(x => x + 1)}>{t("rfq.refresh")}</Button>
      {canExport && <Button variant="outline" disabled={busy || loading || !!error} onClick={() => void download(
        () => rfqId ? rfqApi.exportDetail(projectId, rfqId) : rfqApi.exportList(projectId, filter), rfqId ? `${detail?.header.code}-comparison.json` : `rfqs-${project.code}.csv`)}>{t("rfq.export")}</Button>}
    </div>
    {success && <p role="status" className="text-sm text-emerald-700">{success}</p>}
    {actionError && !dialog && <PageError message={actionError} />}
    {!rfqId && <div className="grid gap-3 rounded-lg border bg-muted/20 p-4 sm:grid-cols-2 lg:grid-cols-4">
      <RfqField label={t("rfq.search")}><Input maxLength={200} value={filter.search ?? ""} onChange={e => changeFilter({ search: e.target.value })} /></RfqField>
      <RfqField label={t("rfq.field.status")}><select className={rfqSelectClass} value={filter.status ?? ""} onChange={e => changeFilter({ status: e.target.value || undefined })}>
        <option value="">{t("rfq.all")}</option>{statuses.map(s => <option key={s} value={s}>{t(`rfq.status.${s}`)}</option>)}
      </select></RfqField>
      <RfqField label={t("rfq.field.owner")}><select className={rfqSelectClass} value={filter.ownerUserId ?? ""} onChange={e => changeFilter({ ownerUserId: Number(e.target.value) || undefined })}>
        <option value="">{t("rfq.all")}</option>{references.owners.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
      </select></RfqField>
      <RfqField label={t("rfq.sort")}><select className={rfqSelectClass} value={`${filter.sortBy}:${filter.sortDirection}`} onChange={e => { const [sortBy, sortDirection] = e.target.value.split(":"); changeFilter({ sortBy, sortDirection }); }}>
        <option value="updatedAt:desc">{t("rfq.sort.updated")}</option><option value="dueAt:asc">{t("rfq.sort.due")}</option><option value="code:asc">{t("rfq.sort.code")}</option>
      </select></RfqField>
      <RfqField label={t("rfq.dueFrom")}><Input type="date" value={filter.dueFrom ? new Date(Date.parse(filter.dueFrom) - new Date(filter.dueFrom).getTimezoneOffset() * 60000).toISOString().slice(0, 10) : ""} onChange={e => changeFilter({ dueFrom: e.target.value ? new Date(`${e.target.value}T00:00:00`).toISOString() : undefined })} /></RfqField>
      <RfqField label={t("rfq.dueTo")}><Input type="date" value={filter.dueTo ? new Date(Date.parse(filter.dueTo) - new Date(filter.dueTo).getTimezoneOffset() * 60000).toISOString().slice(0, 10) : ""} onChange={e => changeFilter({ dueTo: e.target.value ? new Date(`${e.target.value}T23:59:59.999`).toISOString() : undefined })} /></RfqField>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={filter.overdue ?? false} onChange={e => changeFilter({ overdue: e.target.checked })} />{t("rfq.overdue")}</label>
    </div>}
    {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => setRefresh(x => x + 1)} /> : !rfqId ? <>
      {!rows.length ? <PageEmpty message={t("rfq.empty")} /> : <>
        <div className="hidden overflow-x-auto rounded-lg border md:block"><table className="w-full text-left text-sm"><thead className="bg-muted"><tr>
          {["title", "boq", "status", "owner", "bids", "issued", "due", "updated"].map(key => <th key={key} className="p-3">{t(`rfq.field.${key}`)}</th>)}
        </tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t">
          <td className="max-w-64 p-3"><button className="text-left font-medium text-primary underline" onClick={() => onSelect(row.id)}>{row.title}</button><div className="mt-1 break-all text-xs text-muted-foreground">{row.code}</div></td>
          <td className="p-3">#{row.boqRevision}</td><td className="p-3">{statusBadge(row)}</td><td className="p-3">{row.ownerName}</td><td className="p-3">{row.receivedCount}/{row.invitedCount}</td>
          <td className="p-3">{date(row.issuedAt)}</td><td className="p-3">{date(row.dueAt)}</td><td className="p-3">{date(row.updatedAt)}</td>
        </tr>)}</tbody></table></div>
        <div className="grid gap-3 md:hidden">{rows.map(row => <article className="space-y-3 rounded-lg border p-4" key={row.id}>
          <button className="text-left font-semibold text-primary underline" onClick={() => onSelect(row.id)}>{row.title}</button><p className="break-all text-xs text-muted-foreground">{row.code}</p>
          {statusBadge(row)}<p className="text-sm">{row.ownerName} · BOQ #{row.boqRevision}</p><p className="text-sm">{t("rfq.field.due")}: {date(row.dueAt)}</p>
          <p className="text-sm">{t("rfq.field.bids")}: {row.receivedCount}/{row.invitedCount}</p>
        </article>)}</div>
      </>}
      <div className="flex flex-wrap items-center justify-between gap-3 text-sm"><span>{t("rfq.totalRecords")}: {total}</span><div className="flex items-center gap-3">
        <Button variant="outline" disabled={(filter.page ?? 1) === 1} onClick={() => setFilter(x => ({ ...x, page: (x.page ?? 1) - 1 }))}>{t("rfq.previous")}</Button>
        <span>{filter.page}/{Math.max(1, Math.ceil(total / 20))}</span><Button variant="outline" disabled={(filter.page ?? 1) * 20 >= total} onClick={() => setFilter(x => ({ ...x, page: (x.page ?? 1) + 1 }))}>{t("rfq.next")}</Button>
      </div></div>
    </> : detail && <>
      <div className="space-y-3 rounded-lg border p-5"><h2 className="text-xl font-semibold">{detail.header.title}</h2>{statusBadge(detail.header)}
        <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <span>{t("rfq.field.owner")}: {detail.header.ownerName}</span><span>BOQ #{detail.header.boqRevision} · {detail.currency}</span>
          <span>{t("rfq.field.issued")}: {date(detail.header.issuedAt)}</span><span>{t("rfq.field.due")}: {date(detail.header.dueAt)}</span>
          <span>{t("rfq.field.bids")}: {detail.header.receivedCount}/{detail.header.invitedCount}</span>
        </div>{detail.note && <p className="whitespace-pre-wrap text-sm">{detail.note}</p>}
        {canManage && <div className="flex flex-wrap gap-2">
          {detail.header.status === "Draft" && <><Button data-rfq-dialog-trigger="edit" disabled={busy} variant="outline" onClick={event => openDialog(event, "edit")}>{t("rfq.edit")}</Button><Button disabled={busy} onClick={() => transition("issue")}>{t("rfq.issue")}</Button></>}
          {detail.header.status === "Issued" && <><Button data-rfq-dialog-trigger="bid" disabled={busy || Date.parse(detail.header.dueAt) < Date.now()} onClick={event => openDialog(event, "bid")}>{t("rfq.submitBid")}</Button>
            <Button variant="outline" disabled={busy || !detail.bids.some(b => b.isCurrent && !b.withdrawnAt)} onClick={() => transition("evaluate")}>{t("rfq.evaluate")}</Button>
            <Button variant="outline" disabled={busy} onClick={() => void mutate(() => rfqApi.resendInvitations(projectId, rfqId, detail.header.rowVersion))}>{t("rfq.portal.resend")}</Button></>}
          {detail.header.status === "UnderEvaluation" && <Button data-rfq-dialog-trigger="evaluate" variant="outline" disabled={busy} onClick={event => openDialog(event, "evaluate")}>{t("rfq.scoring.action")}</Button>}
          {detail.header.status === "Awarded" && <Button disabled={busy} onClick={() => transition("close")}>{t("rfq.close")}</Button>}
        </div>}
        {canAward && detail.header.status === "UnderEvaluation" && <Button data-rfq-dialog-trigger="award" disabled={busy} onClick={event => void openAward(event)}>{t("rfq.award")}</Button>}
      </div>
      <section className="space-y-3"><h2 className="text-lg font-semibold">{t("rfq.matrix")}</h2><p className="text-sm text-muted-foreground">{t("rfq.matrixHelp")}</p><p className="flex items-center gap-2 text-xs text-muted-foreground md:hidden"><MoveHorizontal className="h-4 w-4" />{t("rfq.matrixScrollHint")}</p>
        <div className="max-w-full overflow-x-auto rounded-lg border" role="region" aria-label={t("rfq.matrix")} tabIndex={0}>
          <table className="w-full min-w-[640px] text-left text-sm"><thead className="bg-muted"><tr><th className="min-w-52 p-3">{t("rfq.field.boq")}</th>{detail.vendors.map(v => <th key={v.id} className="min-w-52 p-3">{v.name}<small className="mt-1 block font-normal text-muted-foreground">{v.invitationDeliveryError ? t("rfq.portal.deliveryFailed") : v.invitationSentAt ? t("rfq.portal.sent") : ""}</small></th>)}</tr></thead>
            <tbody>{detail.lines.map(line => <tr key={line.id} className="border-t"><th className="p-3 font-medium">{line.itemCode}<div className="text-xs font-normal">{line.description}<br />{quantity.format(line.quantity)} {line.unit}</div></th>
              {detail.vendors.map(v => { const bid = detail.bids.find(b => b.vendorId === v.id && b.isCurrent && !b.withdrawnAt); const cell = bid?.lines.find(l => l.rfqLineId === line.id);
                return <td key={v.id} className={`p-3 ${v.isActive && cell && Date.parse(bid!.validUntil) >= Date.now() && cell.unitPrice === line.lowestUnitPrice ? "bg-emerald-50 text-emerald-900" : ""}`}>
                  {cell ? <><div>{number.format(cell.unitPrice)} {bid!.currency}</div><div className="text-xs">{number.format(cell.unitPriceVnd)} VND</div><div className="text-xs">{t("rfq.field.total")}: {number.format(cell.amountVnd)} VND</div></> : t("rfq.missing")}
                </td>; })}</tr>)}
              <tr className="border-t bg-muted/30"><th className="p-3">{t("rfq.field.total")}</th>{detail.vendors.map(v => { const bid = detail.bids.find(b => b.vendorId === v.id && b.isCurrent && !b.withdrawnAt); return <td key={v.id} className={`p-3 ${bid?.isLowest ? "bg-emerald-50" : ""}`}>
                {bid ? <><strong>{number.format(bid.totalOriginal)} {bid.currency}</strong><p className="text-xs">{number.format(bid.total)} VND</p><p>{!bid.isComplete && t("rfq.partial")}</p><p>{!bid.isEligible && t("rfq.ineligible")}</p>
                  <p className="mt-2">{t("rfq.field.leadTime")}: {bid.leadTimeDays}</p><p>{t("rfq.scoring.total")}: {bid.weightedScore ?? "—"}</p><p className="whitespace-pre-wrap">{bid.paymentTerms}</p><p>{t("rfq.field.validity")}: {date(bid.validUntil)}</p>{bid.note && <p className="whitespace-pre-wrap">{bid.note}</p>}</> : t("rfq.missing")}
              </td>; })}</tr>
            </tbody></table>
        </div>
      </section>
      {(detail.awards.length > 0 || detail.contractId) && <section className="space-y-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4"><h2 className="font-semibold">{t("rfq.awardResult")}</h2>
        {detail.awards.length ? detail.awards.map(award => <div key={award.id}><Link className="underline" to={`/admin/contracts/${award.contractId}`}>{award.contractNumber}</Link><span> · {award.vendorName} · {number.format(award.originalValue)} {award.currency} / {number.format(award.valueVnd)} VND</span></div>) :
          <Link className="underline" to={`/admin/contracts/${detail.contractId}`}>{detail.contractNumber}</Link>}
        <p>{detail.awardedBy} · {date(detail.awardedAt)}</p><p className="whitespace-pre-wrap">{detail.awardReason}</p>
      </section>}
      {canManage && ["Draft", "Issued", "UnderEvaluation"].includes(detail.header.status) &&
        <section className="space-y-3 rounded-lg border p-4"><RfqField label={t("rfq.field.reason")}><Textarea maxLength={2000} disabled={busy} value={reason} onChange={e => setReason(e.target.value)} /></RfqField>
          {canManage && <Button variant="destructive" disabled={busy} onClick={() => transition("cancel")}>{t("rfq.cancelRfq")}</Button>}
        </section>}
      <section className="space-y-3"><h2 className="text-lg font-semibold">{t("rfq.documents")}</h2>
        {!detail.documents.length && <p className="text-sm text-muted-foreground">{t("rfq.noDocuments")}</p>}
        {detail.documents.map(file => <div key={file.id} className="flex flex-wrap items-center gap-2 rounded border p-2"><Button disabled={busy} variant="link" className="h-auto max-w-full whitespace-normal break-all text-left" onClick={() => void download(() => rfqApi.download(projectId, rfqId, file.id), file.name)}>{file.name}</Button>
          {file.bidId && <span className="text-xs">{t("rfq.field.bid")}: #{file.bidId}</span>}</div>)}
        {canManage && detail.header.status === "Draft" && <div className="space-y-3 rounded-lg border p-4">
          <RfqField label={t("rfq.attach")}><select className={rfqSelectClass} value={documentId || ""} onChange={e => setDocumentId(Number(e.target.value))}><option value="">{t("rfq.choose")}</option>{references.documents.map(file => <option key={file.id} value={file.id}>{file.name}</option>)}</select></RfqField>
          <Button variant="outline" disabled={busy || !documentId} onClick={() => void mutate(() => rfqApi.attach(projectId, rfqId, documentId, detail.header.rowVersion))}>{t("rfq.attach")}</Button>
          <RfqField label={t("rfq.upload")}><Input disabled={busy} type="file" onChange={e => { const file = e.target.files?.[0]; if (file) void upload(file); e.target.value = ""; }} /></RfqField><p className="text-xs text-muted-foreground">{t("rfq.uploadHelp")}</p>
        </div>}
      </section>
      <section className="space-y-3"><h2 className="text-lg font-semibold">{t("rfq.bidHistory")}</h2>{detail.bids.map(bid => <details key={bid.id} className="rounded-lg border p-3">
        <summary className="cursor-pointer text-sm font-medium">{detail.vendors.find(v => v.id === bid.vendorId)?.name} · #{bid.revision} · {number.format(bid.total)} {detail.currency} · {bid.withdrawnAt ? t("rfq.withdrawn") : bid.isCurrent ? t("rfq.current") : t("rfq.superseded")}</summary>
        <div className="mt-3 space-y-2 text-sm"><p>{bid.submittedBy} · {date(bid.submittedAt)}</p><p>{bid.paymentTerms}</p><p>{t("rfq.field.leadTime")}: {bid.leadTimeDays}</p><p>{t("rfq.field.validity")}: {date(bid.validUntil)}</p><p className="whitespace-pre-wrap">{bid.note}</p>
          {bid.lines.map(line => <p key={line.rfqLineId}>{detail.lines.find(l => l.id === line.rfqLineId)?.itemCode}: {number.format(line.unitPrice)} · {number.format(line.amount)} {detail.currency}</p>)}
          {canManage && detail.header.status === "Issued" && bid.isCurrent && !bid.withdrawnAt && <Button variant="outline" disabled={busy} onClick={() => {
            if (reason.trim().length < 3) { setActionError(t("rfq.validation.reason")); return; }
            void mutate(() => rfqApi.withdraw(projectId, rfqId, bid.id, detail.header.rowVersion, reason.trim())); }}>{t("rfq.withdraw")}</Button>}
        </div>
      </details>)}</section>
      <section className="space-y-3"><h2 className="text-lg font-semibold">{t("rfq.history")}</h2>{detail.events.map((event, index) => <div key={index} className="border-l-2 pl-3 text-sm"><p className="font-medium">{t(`rfq.event.${event.action}`)} · {event.actor}</p><p className="text-muted-foreground">{date(event.at)}</p>{event.reason && <p className="whitespace-pre-wrap">{event.reason}</p>}</div>)}</section>
    </>}
    <Dialog open={dialog !== null} onOpenChange={open => { if (!open && !busy) setDialog(null); }}><DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto" onCloseAutoFocus={event => {
      event.preventDefault();
      dialogClosedRef.current = true;
      const trigger = document.querySelector<HTMLButtonElement>(
        `[data-rfq-dialog-trigger="${dialogTriggerNameRef.current}"]`);
      if (trigger) {
        trigger.focus();
        dialogTriggerNameRef.current = null;
      }
    }}>
      <DialogHeader><DialogTitle>{t(dialog === "bid" ? "rfq.submitBid" : dialog === "edit" ? "rfq.edit" : dialog === "evaluate" ? "rfq.scoring.action" : dialog === "award" ? "rfq.award" : "rfq.create")}</DialogTitle><DialogDescription>{project.customerName} → {project.code} · {project.name}{dialog === "create" && <span className="mt-2 block">{t("rfq.createHelp")}</span>}</DialogDescription></DialogHeader>
      {actionError && <p role="alert" className="text-sm text-destructive">{actionError}</p>}
      {dialog === "bid" && detail ? <RfqBidForm detail={detail} references={references} busy={busy} onSave={draft => void mutate(() => rfqApi.bid(projectId, rfqId, draft))} onUpload={upload} /> :
        dialog === "evaluate" && detail ? <RfqEvaluationForm detail={detail} busy={busy} onSave={(bidId, score, note) => void mutate(() => rfqApi.evaluateBid(projectId, rfqId, bidId, score, note, detail.header.rowVersion))} /> :
        dialog === "award" && detail ? <RfqBatchAwardForm detail={detail} busy={busy} onSave={draft => void mutate(() => rfqApi.batchAward(projectId, rfqId, draft))} /> :
        dialog && <RfqDraftForm detail={dialog === "edit" ? detail : null} references={references} busy={busy} onSave={draft => void mutate(() => rfqApi.save(projectId, dialog === "edit" ? rfqId : null, draft))} />}
    </DialogContent></Dialog>
  </div>;
}
