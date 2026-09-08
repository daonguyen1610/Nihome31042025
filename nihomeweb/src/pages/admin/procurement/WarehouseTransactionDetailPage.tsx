import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowLeft, Edit3, PackageCheck, RotateCcw } from "lucide-react";
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
  type WarehouseIssueDetailResponse,
  type WarehouseIssueUpsertRequest,
  type WarehouseReceiptDetailResponse,
  type WarehouseReceiptUpsertRequest,
  type WarehouseStockItemResponse,
} from "@/services/adminApi";
import { useAppSelector } from "@/store";

type WarehouseKind = "receipt" | "issue";
type WarehouseDetail = WarehouseReceiptDetailResponse | WarehouseIssueDetailResponse;
type UserOption = { userId: number; userName: string };
type ReceiptLineDraft = WarehouseReceiptUpsertRequest["lines"][number];
type IssueLineDraft = WarehouseIssueUpsertRequest["lines"][number];

const statusClass: Record<string, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Posted: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Reversed: "border-rose-200 bg-rose-50 text-rose-700",
};

const toLocalDateTime = (value: string) => {
  const date = new Date(value);
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
  return date.toISOString().slice(0, 16);
};

const WarehouseTransactionDetailPage = () => {
  const { projectId: projectIdParam, transactionType, transactionId: transactionIdParam } = useParams();
  const projectId = Number(projectIdParam);
  const transactionId = Number(transactionIdParam);
  const kind = transactionType as WarehouseKind;
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUser = useAppSelector((state) => state.auth.user);
  const canPost = has(ADMIN_PERMS.procurementWarehousePost);
  const canViewContracts = has(ADMIN_PERMS.contracts);
  const canReadKpiUsers = has(ADMIN_PERMS.kpi);

  const [record, setRecord] = useState<WarehouseDetail | null>(null);
  const [stock, setStock] = useState<WarehouseStockItemResponse[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [reverseOpen, setReverseOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [occurredAt, setOccurredAt] = useState("");
  const [responsibleSiteUserId, setResponsibleSiteUserId] = useState(0);
  const [workItemCode, setWorkItemCode] = useState("");
  const [receiptLines, setReceiptLines] = useState<ReceiptLineDraft[]>([]);
  const [issueLines, setIssueLines] = useState<IssueLineDraft[]>([]);

  const load = useCallback(async () => {
    if (!Number.isInteger(projectId) || projectId < 1 || !Number.isInteger(transactionId) || transactionId < 1 || !["receipt", "issue"].includes(kind)) {
      setError(t("procurement.warehouse.detail.invalidRoute"));
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [detailResult, listResult, teamResult, kpiUsersResult] = await Promise.all([
        kind === "receipt" ? adminApi.getWarehouseReceipt(projectId, transactionId) : adminApi.getWarehouseIssue(projectId, transactionId),
        adminApi.listWarehouseTransactions(projectId, { page: 1, pageSize: 1 }),
        adminApi.getOperationalProjectTeam(projectId).catch(() => ({ data: null })),
        canReadKpiUsers
          ? adminApi.listKpiEligibleUsers().catch(() => ({ data: [] as KpiUserOptionResponse[] }))
          : Promise.resolve({ data: [] as KpiUserOptionResponse[] }),
      ]);
      setRecord(detailResult.data);
      setStock(listResult.data.stock);
      const teamUsers = (teamResult.data?.members ?? []).filter((member) => member.isActive)
        .map((member) => ({ userId: member.userId, userName: member.userName }));
      const fallbackUsers = kpiUsersResult.data.map((user) => ({ userId: user.userId, userName: user.userName }));
      const detailUsers: UserOption[] = kind === "issue"
        ? [{ userId: (detailResult.data as WarehouseIssueDetailResponse).responsibleSiteUserId, userName: (detailResult.data as WarehouseIssueDetailResponse).responsibleSiteUserName ?? `#${(detailResult.data as WarehouseIssueDetailResponse).responsibleSiteUserId}` }]
        : [];
      setUsers(Array.from(new Map([...teamUsers, ...fallbackUsers, ...detailUsers].map((user) => [user.userId, user])).values()));
    } catch (loadError) {
      setError(extractApiError(loadError));
    } finally {
      setLoading(false);
    }
  }, [canReadKpiUsers, kind, projectId, transactionId, t]);

  useEffect(() => { void load(); }, [load]);

  const number = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }), [lang]);
  const dateTime = (value?: string | null) => value
    ? new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
    : t("procurement.request.detail.notAvailable");
  const isReceipt = kind === "receipt";
  const receipt = isReceipt ? record as WarehouseReceiptDetailResponse | null : null;
  const issue = !isReceipt ? record as WarehouseIssueDetailResponse | null : null;
  const creatorUserId = receipt?.receivedByUserId ?? issue?.issuedByUserId;
  const reversalOfId = receipt?.reversalOfReceiptId ?? issue?.reversalOfIssueId;
  const editable = canPost && record?.status === "Draft" && creatorUserId === currentUser?.userId;
  const postable = canPost && record?.status === "Draft";
  const reversible = canPost && record?.status === "Posted" && !(receipt?.reversalOfReceiptId ?? issue?.reversalOfIssueId);

  const openEdit = () => {
    if (!record) return;
    setFormError(null);
    if (receipt) {
      setOccurredAt(toLocalDateTime(receipt.inspectedAt));
      setReceiptLines(receipt.lines.map((line) => ({ materialRequestLineId: line.materialRequestLineId, contractLineId: line.contractLineId, receivedQuantity: line.receivedQuantity })));
    } else if (issue) {
      setOccurredAt(toLocalDateTime(issue.issuedAt));
      setResponsibleSiteUserId(issue.responsibleSiteUserId);
      setWorkItemCode(issue.workItemCode ?? "");
      setIssueLines(issue.lines.map((line) => ({ projectBoqLineId: line.projectBoqLineId, issuedQuantity: line.issuedQuantity })));
    }
    setEditOpen(true);
  };

  const save = async () => {
    if (!record || !currentUser?.userId || !occurredAt || new Date(occurredAt) > new Date()) {
      setFormError(t("procurement.warehouse.validation.edit"));
      return;
    }
    if (receipt && (receiptLines.length === 0 || receiptLines.some((line) => line.receivedQuantity <= 0))) {
      setFormError(t("procurement.validation.receipt"));
      return;
    }
    if (issue && (responsibleSiteUserId < 1 || issueLines.length === 0 || issueLines.some((line) => {
      const available = stock.find((item) => item.projectBoqLineId === line.projectBoqLineId)?.onHandQuantity ?? 0;
      return line.issuedQuantity <= 0 || line.issuedQuantity > available;
    }))) {
      setFormError(t("procurement.warehouse.validation.issueStock"));
      return;
    }
    setBusy(true);
    setFormError(null);
    try {
      if (receipt) {
        await adminApi.updateWarehouseReceipt(projectId, record.id, {
          inspectedAt: new Date(occurredAt).toISOString(),
          receivedByUserId: currentUser.userId,
          lines: receiptLines,
          rowVersion: record.rowVersion,
        });
      } else {
        await adminApi.updateWarehouseIssue(projectId, record.id, {
          issuedAt: new Date(occurredAt).toISOString(),
          responsibleSiteUserId,
          issuedByUserId: currentUser.userId,
          workItemCode: workItemCode.trim() || null,
          lines: issueLines,
          rowVersion: record.rowVersion,
        });
      }
      setEditOpen(false);
      toast({ title: t("procurement.warehouse.success.updated") });
      await load();
    } catch (saveError) {
      setFormError(extractApiError(saveError));
    } finally {
      setBusy(false);
    }
  };

  const post = async () => {
    if (!record) return;
    setBusy(true);
    setFormError(null);
    try {
      if (receipt) await adminApi.postWarehouseReceipt(projectId, record.id, record.rowVersion);
      else await adminApi.postWarehouseIssue(projectId, record.id, record.rowVersion);
      toast({ title: t("procurement.warehouse.success.posted") });
      await load();
    } catch (postError) {
      setFormError(extractApiError(postError));
    } finally {
      setBusy(false);
    }
  };

  const reverse = async () => {
    if (!record || reason.trim().length < 3) {
      setFormError(t("procurement.warehouse.validation.reversalReason"));
      return;
    }
    setBusy(true);
    setFormError(null);
    try {
      if (receipt) await adminApi.reverseWarehouseReceipt(projectId, record.id, record.rowVersion, reason.trim());
      else await adminApi.reverseWarehouseIssue(projectId, record.id, record.rowVersion, reason.trim());
      setReverseOpen(false);
      setReason("");
      toast({ title: t("procurement.warehouse.success.reversed") });
      await load();
    } catch (reverseError) {
      setFormError(extractApiError(reverseError));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error || !record) return <AdminLayout><div className="p-4 sm:p-6"><PageError message={error ?? t("procurement.warehouse.detail.notFound")} onRetry={() => void load()} /></div></AdminLayout>;

  const occurredValue = receipt?.inspectedAt ?? issue!.issuedAt;
  const actorName = receipt?.receivedByName ?? issue?.issuedByName ?? t("procurement.warehouse.userUnavailable");

  return (
    <AdminLayout>
      <div className="space-y-6 p-4 sm:p-6">
        <nav aria-label={t("procurement.warehouse.detail.context")} className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span>{record.customerName}</span><span aria-hidden="true">/</span><span>{record.operationalProjectCode} · {record.operationalProjectName}</span><span aria-hidden="true">/</span><span className="font-medium text-foreground">{record.code}</span>
        </nav>
        <header className="flex flex-col gap-4 border-b pb-5 lg:flex-row lg:items-end lg:justify-between">
          <div><Button asChild variant="ghost" size="sm" className="mb-2 -ml-3"><Link to={`/admin/procurement-control?projectId=${projectId}&tab=warehouse`}><ArrowLeft className="mr-2 h-4 w-4" />{t("common.back")}</Link></Button><div className="flex flex-wrap items-center gap-2"><h1 className="text-2xl font-semibold">{record.code}</h1><Badge variant="outline" className={statusClass[record.status] ?? ""}>{t(`procurement.status.${record.status}`)}</Badge><Badge variant="secondary">{t(`procurement.warehouse.${kind}`)}</Badge></div><p className="mt-1 text-sm text-muted-foreground">{t("procurement.warehouse.detail.subtitle")}</p></div>
          <div className="flex flex-wrap gap-2">{editable && <Button variant="outline" onClick={openEdit}><Edit3 className="mr-2 h-4 w-4" />{t("common.edit")}</Button>}{postable && <Button onClick={() => void post()} disabled={busy}><PackageCheck className="mr-2 h-4 w-4" />{t("procurement.action.post")}</Button>}{reversible && <Button variant="destructive" onClick={() => { setReason(""); setFormError(null); setReverseOpen(true); }} disabled={busy}><RotateCcw className="mr-2 h-4 w-4" />{t("procurement.warehouse.reverse")}</Button>}</div>
        </header>

        {formError && !editOpen && !reverseOpen && <p role="alert" className="border-l-2 border-destructive pl-3 text-sm text-destructive">{formError}</p>}

        <section aria-labelledby="warehouse-overview-heading" className="grid gap-5 border-b pb-6 sm:grid-cols-2 lg:grid-cols-4"><h2 id="warehouse-overview-heading" className="sr-only">{t("procurement.warehouse.detail.overview")}</h2><Datum label={t("procurement.warehouse.occurredAt")} value={dateTime(occurredValue)} /><Datum label={t("procurement.warehouse.actor")} value={actorName} /><Datum label={t("procurement.warehouse.postedAt")} value={dateTime(record.postedAt)} /><Datum label={t("procurement.field.workItem")} value={issue?.workItemCode || t("procurement.request.detail.notAvailable")} /></section>

        <section aria-labelledby="warehouse-lines-heading" className="space-y-3"><div><h2 id="warehouse-lines-heading" className="text-lg font-semibold">{t("procurement.warehouse.detail.lines")}</h2><p className="text-sm text-muted-foreground">{t("procurement.warehouse.detail.linesDescription")}</p></div>
          <div className="grid gap-3 xl:hidden">{record.lines.map((line) => <article className="rounded-md border p-4" key={line.id}><div className="flex items-start justify-between gap-3"><div><h3 className="font-semibold">{line.itemCode}</h3><p className="mt-1 text-sm text-muted-foreground">{line.description}</p></div><Badge variant="outline">{line.unit}</Badge></div>{receipt && "receivedQuantity" in line ? <dl className="mt-4 grid gap-3 sm:grid-cols-2"><Datum label={t("procurement.field.receivedQuantity")} value={number.format(line.receivedQuantity)} /><Datum label={t("procurement.warehouse.netReceived")} value={number.format(line.netReceivedQuantity)} /><Datum label={t("procurement.warehouse.requestCode")} value={line.materialRequestCode} /><Datum label={t("procurement.warehouse.contract")} value={line.contractNumber ?? t("procurement.request.detail.notAvailable")} /></dl> : "issuedQuantity" in line && <dl className="mt-4 grid gap-3 sm:grid-cols-2"><Datum label={t("procurement.warehouse.issued")} value={number.format(line.issuedQuantity)} /><Datum label={t("procurement.warehouse.onHand")} value={number.format(line.stockOnHand)} /><Datum label={t("procurement.field.boqApprovedQuantity")} value={number.format(line.boqApprovedQuantity)} /><Datum label={t("procurement.field.workItem")} value={issue?.workItemCode || t("procurement.request.detail.notAvailable")} /></dl>}</article>)}</div>
          <div className="hidden overflow-x-auto border-y xl:block"><table className="w-full min-w-[820px] text-sm"><thead><tr className="border-b text-left text-xs uppercase text-muted-foreground"><th className="px-3 py-3 font-medium">{t("procurement.field.itemCode")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.description")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.unit")}</th><th className="px-3 py-3 font-medium">{receipt ? t("procurement.warehouse.requestCode") : t("procurement.field.workItem")}</th><th className="px-3 py-3 text-right font-medium">{receipt ? t("procurement.field.receivedQuantity") : t("procurement.warehouse.issued")}</th><th className="px-3 py-3 text-right font-medium">{receipt ? t("procurement.warehouse.netReceived") : t("procurement.warehouse.onHand")}</th></tr></thead><tbody>{record.lines.map((line) => <tr className="border-b" key={line.id}><td className="px-3 py-3 font-medium">{line.itemCode}</td><td className="px-3 py-3">{line.description}</td><td className="px-3 py-3">{line.unit}</td><td className="px-3 py-3">{"materialRequestCode" in line ? line.materialRequestCode : issue?.workItemCode || "—"}</td><td className="px-3 py-3 text-right">{number.format("receivedQuantity" in line ? line.receivedQuantity : line.issuedQuantity)}</td><td className="px-3 py-3 text-right">{number.format("netReceivedQuantity" in line ? line.netReceivedQuantity : line.stockOnHand)}</td></tr>)}</tbody></table></div>
        </section>

        <div className="grid gap-7 lg:grid-cols-2"><section aria-labelledby="warehouse-context-heading" className="space-y-3 border-t pt-5"><h2 id="warehouse-context-heading" className="text-lg font-semibold">{t("procurement.request.detail.businessContext")}</h2><dl className="grid gap-3 sm:grid-cols-2"><Datum label={t("procurement.request.detail.customer")} value={record.customerName} /><Datum label={t("procurement.project.label")} value={`${record.operationalProjectCode} · ${record.operationalProjectName}`} /></dl><h3 className="text-sm font-semibold">{t("procurement.request.detail.contracts", { count: record.contracts.length })}</h3>{record.contracts.length === 0 ? <p className="text-sm text-muted-foreground">{t("procurement.request.detail.noContracts")}</p> : <ul className="divide-y border-y text-sm">{record.contracts.map((contract) => <li className="flex flex-wrap items-center justify-between gap-2 py-3" key={contract.id}>{canViewContracts ? <Link className="font-medium text-primary hover:underline" to={`/admin/contracts/${contract.id}`}>{contract.contractNumber}</Link> : <span className="font-medium">{contract.contractNumber}</span>}<span className="text-muted-foreground">{contract.vendorName ?? record.customerName} · {t(`contracts.status.${contract.status}`)}</span></li>)}</ul>}</section>
          <section aria-labelledby="warehouse-history-heading" className="space-y-3 border-t pt-5"><h2 id="warehouse-history-heading" className="text-lg font-semibold">{t("procurement.request.detail.history")}</h2><ol className="space-y-3"><History label={t("procurement.request.history.created")} value={dateTime(record.createdAt)} actor={actorName} /><History label={t("procurement.status.Posted")} value={dateTime(record.postedAt)} actor={record.postedByName} />{record.status === "Reversed" && <History label={t("procurement.status.Reversed")} value={dateTime(record.updatedAt)} />}</ol>{record.reversalReason && <div className="border-l-2 border-rose-400 pl-3"><p className="text-xs font-medium text-muted-foreground">{t("procurement.warehouse.reversalReason")}</p><p className="text-sm">{record.reversalReason}</p></div>}</section>
        </div>
        {(reversalOfId || record.reversalTransactionId) && <section aria-labelledby="warehouse-correction-heading" className="space-y-2 border-t pt-5"><h2 id="warehouse-correction-heading" className="text-lg font-semibold">{t("procurement.warehouse.correction")}</h2><p className="text-sm text-muted-foreground">{t(reversalOfId ? "procurement.warehouse.correctionIsReversal" : "procurement.warehouse.correctionHasReversal")}</p><Button asChild variant="outline" size="sm"><Link to={`/admin/procurement-control/projects/${projectId}/warehouse/${kind}/${reversalOfId ?? record.reversalTransactionId}`}><RotateCcw className="mr-2 h-4 w-4" />{t(reversalOfId ? "procurement.warehouse.viewOriginal" : "procurement.warehouse.viewReversal")}</Link></Button></section>}
      </div>

      <Dialog open={editOpen} onOpenChange={(open) => { if (!busy) setEditOpen(open); }}><DialogContent className="max-h-[92vh] w-[95vw] max-w-3xl overflow-y-auto"><DialogHeader><DialogTitle>{t("procurement.warehouse.editTitle", { code: record.code })}</DialogTitle><DialogDescription>{t("procurement.warehouse.editDescription")}</DialogDescription></DialogHeader><div className="space-y-4"><div className="space-y-2"><Label htmlFor="warehouse-occurred-at">{t("procurement.warehouse.occurredAt")}</Label><Input className="h-11 sm:h-10" id="warehouse-occurred-at" type="datetime-local" value={occurredAt} onChange={(event) => setOccurredAt(event.target.value)} /></div>{issue && <><div className="space-y-2"><Label htmlFor="warehouse-responsible-user">{t("procurement.field.responsibleSite")}</Label><Select value={responsibleSiteUserId ? String(responsibleSiteUserId) : ""} onValueChange={(value) => setResponsibleSiteUserId(Number(value))}><SelectTrigger className="h-11 sm:h-10" id="warehouse-responsible-user"><SelectValue /></SelectTrigger><SelectContent>{users.map((user) => <SelectItem key={user.userId} value={String(user.userId)}>{user.userName}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label htmlFor="warehouse-work-item">{t("procurement.field.workItem")}</Label><Input className="h-11 sm:h-10" id="warehouse-work-item" value={workItemCode} onChange={(event) => setWorkItemCode(event.target.value)} /></div></>}
        <div className="space-y-3">{receipt ? receipt.lines.map((line, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_1fr]" key={line.id}><div><p className="font-medium">{line.itemCode}</p><p className="text-sm text-muted-foreground">{line.materialRequestCode} · {line.description}</p></div><div className="space-y-2"><Label htmlFor={`warehouse-receipt-quantity-${index}`}>{t("procurement.field.receivedQuantity")}</Label><Input className="h-11 sm:h-10" id={`warehouse-receipt-quantity-${index}`} type="number" min={0.000001} step="any" value={receiptLines[index]?.receivedQuantity ?? 0} onChange={(event) => setReceiptLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, receivedQuantity: Number(event.target.value) } : item))} /></div></div>) : issue!.lines.map((line, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_1fr]" key={line.id}><div><p className="font-medium">{line.itemCode}</p><p className="text-sm text-muted-foreground">{line.description} · {t("procurement.warehouse.onHand")}: {number.format(line.stockOnHand)}</p></div><div className="space-y-2"><Label htmlFor={`warehouse-issue-quantity-${index}`}>{t("procurement.warehouse.issued")}</Label><Input className="h-11 sm:h-10" id={`warehouse-issue-quantity-${index}`} type="number" min={0.000001} step="any" value={issueLines[index]?.issuedQuantity ?? 0} onChange={(event) => setIssueLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, issuedQuantity: Number(event.target.value) } : item))} /></div></div>)}</div></div>{formError && <p role="alert" className="text-sm text-destructive">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setEditOpen(false)} disabled={busy}>{t("common.cancel")}</Button><Button onClick={() => void save()} disabled={busy}>{busy ? t("common.saving") : t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={reverseOpen} onOpenChange={(open) => { if (!open && !busy) setReverseOpen(false); }}><DialogContent><DialogHeader><DialogTitle>{t("procurement.warehouse.reverseTitle")}</DialogTitle><DialogDescription>{t("procurement.warehouse.reverseDescription")}</DialogDescription></DialogHeader><div className="space-y-2"><Label htmlFor="warehouse-reversal-reason">{t("procurement.warehouse.reversalReason")}</Label><Textarea id="warehouse-reversal-reason" value={reason} onChange={(event) => setReason(event.target.value)} placeholder={t("procurement.warehouse.reversalPlaceholder")} /></div>{formError && <p role="alert" className="text-sm text-destructive">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setReverseOpen(false)} disabled={busy}>{t("common.cancel")}</Button><Button variant="destructive" onClick={() => void reverse()} disabled={busy}>{busy ? t("common.saving") : t("procurement.warehouse.reverse")}</Button></DialogFooter></DialogContent></Dialog>
    </AdminLayout>
  );
};

const Datum = ({ label, value }: { label: string; value: string }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium">{value}</dd></div>;
const History = ({ label, value, actor }: { label: string; value: string; actor?: string | null }) => <li className="grid grid-cols-[8px_1fr] gap-3"><span className="mt-1.5 h-2 w-2 rounded-full bg-primary" /><div><p className="text-sm font-medium">{label}</p><p className="text-sm text-muted-foreground">{value}{actor ? ` · ${actor}` : ""}</p></div></li>;

export default WarehouseTransactionDetailPage;
