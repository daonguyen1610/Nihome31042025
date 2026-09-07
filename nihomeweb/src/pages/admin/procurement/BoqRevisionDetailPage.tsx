import { useEffect, useState } from "react";
import { ArrowLeft, Check, Edit3, Plus, Send, Trash2, X } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type OperationalProjectResponse,
  type ProjectBoqRevisionRequest,
  type ProjectBoqRevisionResponse,
} from "@/services/adminApi";

type BoqLineDraft = ProjectBoqRevisionRequest["lines"][number] & { draftId: string };

const emptyLine = (): BoqLineDraft => ({
  draftId: crypto.randomUUID(),
  itemCode: "",
  description: "",
  unit: "",
  approvedQuantity: 1,
  budgetUnitPrice: 0,
});

const statusClass: Record<string, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
};

const BoqRevisionDetailPage = () => {
  const { projectId: projectIdParam, boqId: boqIdParam } = useParams();
  const projectId = Number(projectIdParam);
  const boqId = Number(boqIdParam);
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const canManage = has(ADMIN_PERMS.procurementManage);
  const canApprove = has(ADMIN_PERMS.procurementApprove);
  const [project, setProject] = useState<OperationalProjectResponse | null>(null);
  const [boq, setBoq] = useState<ProjectBoqRevisionResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [decision, setDecision] = useState<boolean | null>(null);
  const [decisionReason, setDecisionReason] = useState("");
  const [currency, setCurrency] = useState("VND");
  const [lines, setLines] = useState<BoqLineDraft[]>([emptyLine()]);

  const load = async () => {
    if (!Number.isInteger(projectId) || projectId < 1 || !Number.isInteger(boqId) || boqId < 1) {
      setError(t("procurement.boq.detail.invalidRoute"));
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [projectResponse, boqResponse] = await Promise.all([
        adminApi.getOperationalProject(projectId),
        adminApi.getProjectBoqRevision(projectId, boqId),
      ]);
      setProject(projectResponse.data);
      setBoq(boqResponse.data);
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // Route params are the only load identity; translations do not change the resource.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId, boqId]);

  const openEdit = () => {
    if (!boq) return;
    setCurrency(boq.currency);
    setLines(boq.lines.map((line) => ({
      draftId: String(line.id),
      itemCode: line.itemCode,
      description: line.description,
      unit: line.unit,
      approvedQuantity: line.approvedQuantity,
      budgetUnitPrice: line.budgetUnitPrice,
    })));
    setFormError(null);
    setEditOpen(true);
  };

  const validateForm = () => {
    const normalizedCodes = lines.map((line) => line.itemCode.trim().toUpperCase());
    if (normalizedCodes.length !== new Set(normalizedCodes).size) return t("procurement.validation.duplicateItemCode");
    const valid = /^[A-Z]{3}$/.test(currency)
      && lines.length > 0
      && lines.every((line) => /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(line.itemCode.trim())
        && line.description.trim().length > 0
        && line.unit.trim().length > 0
        && Number.isFinite(line.approvedQuantity)
        && line.approvedQuantity >= 0
        && Number.isFinite(line.budgetUnitPrice)
        && line.budgetUnitPrice >= 0);
    return valid ? null : t("procurement.validation.boq");
  };

  const save = async () => {
    if (!boq) return;
    const validationError = validateForm();
    if (validationError) {
      setFormError(validationError);
      return;
    }
    setBusy(true);
    setFormError(null);
    try {
      const response = await adminApi.updateProjectBoqRevision(projectId, boq.id, {
        currency,
        sourceTenderEstimateRevisionId: boq.sourceTenderEstimateRevisionId,
        sourceContractAppendixId: boq.sourceContractAppendixId,
        rowVersion: boq.rowVersion,
        lines: lines.map(({ draftId: _, ...line }) => ({
          ...line,
          itemCode: line.itemCode.trim(),
          description: line.description.trim(),
          unit: line.unit.trim(),
        })),
      });
      setBoq(response.data);
      setEditOpen(false);
      toast({ title: t("procurement.success.boqUpdated") });
    } catch (reason) {
      const message = extractApiError(reason);
      setFormError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const submit = async () => {
    if (!boq) return;
    setBusy(true);
    try {
      const response = await adminApi.submitProjectBoqRevision(projectId, boq.id, boq.rowVersion);
      setBoq(response.data);
      toast({ title: t("procurement.success.boqSubmitted") });
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const decide = async () => {
    if (!boq || decision == null) return;
    if (!decision && decisionReason.trim().length < 3) {
      setFormError(t("procurement.validation.rejectionReason"));
      return;
    }
    setBusy(true);
    setFormError(null);
    try {
      const response = await adminApi.decideProjectBoqRevision(
        projectId,
        boq.id,
        decision,
        boq.rowVersion,
        decisionReason.trim() || undefined,
      );
      setBoq(response.data);
      setDecision(null);
      setDecisionReason("");
      toast({ title: t(decision ? "procurement.success.approved" : "procurement.success.rejected") });
    } catch (reason) {
      const message = extractApiError(reason);
      setFormError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const dateTime = (value?: string | null) => value
    ? new Intl.DateTimeFormat(lang, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
    : t("procurement.boq.detail.notAvailable");
  const money = (value: number, valueCurrency: string) => new Intl.NumberFormat(lang, {
    style: "currency",
    currency: valueCurrency,
    maximumFractionDigits: 2,
  }).format(value);

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error || !project || !boq) return <AdminLayout><div className="p-4 sm:p-6"><PageError message={error ?? t("procurement.boq.detail.notFound")} onRetry={() => void load()} /></div></AdminLayout>;

  const editable = canManage && (boq.status === "Draft" || boq.status === "Rejected");
  const lifecycle = [
    { label: t("procurement.boq.history.created"), value: boq.createdAt },
    { label: t("procurement.boq.history.submitted"), value: boq.submittedAt },
    { label: boq.status === "Rejected" ? t("procurement.boq.history.rejected") : t("procurement.boq.history.approved"), value: boq.rejectedAt ?? boq.approvedAt },
    { label: t("procurement.boq.history.updated"), value: boq.updatedAt },
  ].filter((item) => item.value);

  return (
    <AdminLayout>
      <div className="space-y-6 p-4 sm:p-6">
        <nav aria-label={t("procurement.boq.detail.context")} className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span>{project.customerName}</span><span aria-hidden="true">/</span>
          <span>{project.code} · {project.name}</span><span aria-hidden="true">/</span>
          <span className="font-medium text-foreground">R{boq.revisionNumber}</span>
        </nav>

        <header className="flex flex-col gap-4 border-b pb-5 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <Button asChild variant="ghost" size="sm" className="mb-2 -ml-3">
              <Link to="/admin/procurement-control"><ArrowLeft className="mr-2 h-4 w-4" />{t("common.back")}</Link>
            </Button>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-semibold">{t("procurement.boq.detail.title", { revision: boq.revisionNumber })}</h1>
              <Badge variant="outline" className={statusClass[boq.status] ?? ""}>{t(`procurement.status.${boq.status}`)}</Badge>
              {boq.isFinal && <Badge variant="secondary">{t("procurement.boq.final")}</Badge>}
            </div>
            <p className="mt-1 text-sm text-muted-foreground">{t("procurement.boq.detail.subtitle")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            {editable && <Button variant="outline" onClick={openEdit}><Edit3 className="mr-2 h-4 w-4" />{t("common.edit")}</Button>}
            {canManage && (boq.status === "Draft" || boq.status === "Rejected") && <Button onClick={() => void submit()} disabled={busy}><Send className="mr-2 h-4 w-4" />{t("procurement.action.submit")}</Button>}
            {canApprove && boq.status === "Submitted" && <>
              <Button onClick={() => { setFormError(null); setDecisionReason(""); setDecision(true); }} disabled={busy}><Check className="mr-2 h-4 w-4" />{t("procurement.action.approve")}</Button>
              <Button variant="destructive" onClick={() => { setFormError(null); setDecisionReason(""); setDecision(false); }} disabled={busy}><X className="mr-2 h-4 w-4" />{t("procurement.action.reject")}</Button>
            </>}
          </div>
        </header>

        <section aria-labelledby="boq-overview-heading" className="grid gap-5 border-b pb-6 sm:grid-cols-2 lg:grid-cols-4">
          <h2 id="boq-overview-heading" className="sr-only">{t("procurement.boq.detail.overview")}</h2>
          <Datum label={t("procurement.field.preparedBy")} value={boq.preparedByName ?? `#${boq.preparedByUserId}`} />
          <Datum label={t("procurement.field.total")} value={money(boq.costTotal, boq.currency)} />
          <Datum label={t("procurement.field.lines")} value={String(boq.lines.length)} />
          <Datum label={t("procurement.boq.detail.lastUpdated")} value={dateTime(boq.updatedAt)} />
        </section>

        <section aria-labelledby="boq-lines-heading" className="space-y-3">
          <div><h2 id="boq-lines-heading" className="text-lg font-semibold">{t("procurement.boq.detail.linesTitle")}</h2><p className="text-sm text-muted-foreground">{t("procurement.boq.detail.linesDescription")}</p></div>
          <div className="overflow-x-auto border-y">
            <table className="w-full min-w-[760px] text-sm">
              <thead><tr className="border-b text-left text-xs uppercase text-muted-foreground"><th className="px-3 py-3 font-medium">{t("procurement.field.itemCode")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.description")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.unit")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.quantity")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.budgetPrice")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.field.total")}</th></tr></thead>
              <tbody>{boq.lines.map((line) => <tr key={line.id} className="border-b"><td className="px-3 py-3 font-medium">{line.itemCode}</td><td className="px-3 py-3">{line.description}</td><td className="px-3 py-3">{line.unit}</td><td className="px-3 py-3 text-right">{line.approvedQuantity}</td><td className="px-3 py-3 text-right">{money(line.budgetUnitPrice, boq.currency)}</td><td className="px-3 py-3 text-right font-medium">{money(line.amount, boq.currency)}</td></tr>)}</tbody>
            </table>
          </div>
        </section>

        <div className="grid gap-7 lg:grid-cols-2">
          <section aria-labelledby="boq-context-heading" className="space-y-3 border-t pt-5">
            <h2 id="boq-context-heading" className="text-lg font-semibold">{t("procurement.boq.detail.businessContext")}</h2>
            <dl className="grid gap-3 sm:grid-cols-2"><Datum label={t("procurement.boq.detail.customer")} value={project.customerName} /><Datum label={t("procurement.project.label")} value={`${project.code} · ${project.name}`} /></dl>
            <h3 className="text-sm font-semibold">{t("procurement.boq.detail.contracts", { count: project.contracts.length })}</h3>
            {project.contracts.length === 0 ? <p className="text-sm text-muted-foreground">{t("procurement.boq.detail.noContracts")}</p> : <ul className="divide-y border-y text-sm">{project.contracts.map((contract) => <li key={contract.id} className="flex flex-wrap items-center justify-between gap-2 py-3"><Link className="font-medium text-primary hover:underline" to={`/admin/contracts/${contract.id}`}>{contract.contractNumber}</Link><span className="text-muted-foreground">{contract.vendorName ?? project.customerName} · {t(`contracts.status.${contract.status}`)}</span></li>)}</ul>}
          </section>

          <section aria-labelledby="boq-history-heading" className="space-y-3 border-t pt-5">
            <h2 id="boq-history-heading" className="text-lg font-semibold">{t("procurement.boq.detail.history")}</h2>
            <ol className="space-y-3">{lifecycle.map((item) => <li key={item.label} className="grid grid-cols-[8px_1fr] gap-3"><span className="mt-1.5 h-2 w-2 rounded-full bg-primary" /><div><p className="text-sm font-medium">{item.label}</p><p className="text-sm text-muted-foreground">{dateTime(item.value)}</p></div></li>)}</ol>
            {boq.decisionReason && <div className="border-l-2 border-rose-400 pl-3"><p className="text-xs font-medium text-muted-foreground">{t("procurement.field.decisionReason")}</p><p className="text-sm">{boq.decisionReason}</p></div>}
          </section>
        </div>

        <section aria-labelledby="boq-source-heading" className="space-y-3 border-t pt-5">
          <h2 id="boq-source-heading" className="text-lg font-semibold">{t("procurement.boq.detail.sources")}</h2>
          {!boq.sourceTenderEstimateRevisionId && !boq.sourceContractAppendixId ? <p className="text-sm text-muted-foreground">{t("procurement.boq.detail.noSources")}</p> : <dl className="grid gap-3 sm:grid-cols-2">{boq.sourceTenderEstimateRevisionId && <Datum label={t("procurement.boq.detail.tenderEstimate")} value={`#${boq.sourceTenderEstimateRevisionId}`} />}{boq.sourceContractAppendixId && <Datum label={t("procurement.boq.detail.contractAppendix")} value={`#${boq.sourceContractAppendixId}`} />}</dl>}
        </section>
      </div>

      <Dialog open={editOpen} onOpenChange={(open) => { if (!busy) setEditOpen(open); }}>
        <DialogContent className="max-h-[92vh] w-[95vw] max-w-5xl overflow-y-auto">
          <DialogHeader><DialogTitle>{t("procurement.boq.edit.title", { revision: boq.revisionNumber })}</DialogTitle><DialogDescription>{t("procurement.boq.edit.description")}</DialogDescription></DialogHeader>
          <BoqForm currency={currency} setCurrency={setCurrency} lines={lines} setLines={setLines} t={t} />
          {formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}
          <DialogFooter><Button variant="outline" onClick={() => setEditOpen(false)} disabled={busy}>{t("common.cancel")}</Button><Button onClick={() => void save()} disabled={busy}>{busy ? t("common.saving") : t("common.save")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={decision != null} onOpenChange={(open) => { if (!open && !busy) setDecision(null); }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{t(decision ? "procurement.decision.approveTitle" : "procurement.decision.rejectTitle")}</DialogTitle><DialogDescription>{t("procurement.decision.description")}</DialogDescription></DialogHeader>
          <div className="space-y-2"><Label htmlFor="boq-decision-reason">{t("procurement.field.decisionReason")}</Label><Textarea id="boq-decision-reason" maxLength={2000} value={decisionReason} onChange={(event) => setDecisionReason(event.target.value)} placeholder={t("procurement.decision.reasonPlaceholder")} /></div>
          {formError && <p className="text-sm text-destructive" role="alert">{formError}</p>}
          <DialogFooter><Button variant="outline" onClick={() => setDecision(null)} disabled={busy}>{t("common.cancel")}</Button><Button variant={decision ? "default" : "destructive"} onClick={() => void decide()} disabled={busy}>{t(decision ? "procurement.action.approve" : "procurement.action.reject")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const Datum = ({ label, value }: { label: string; value: string }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium">{value}</dd></div>;

type Translate = (key: string, params?: Record<string, string | number>) => string;

const BoqForm = ({ currency, setCurrency, lines, setLines, t }: { currency: string; setCurrency: (value: string) => void; lines: BoqLineDraft[]; setLines: React.Dispatch<React.SetStateAction<BoqLineDraft[]>>; t: Translate }) => (
  <div className="space-y-4">
    <div className="max-w-xs space-y-2"><Label htmlFor="boq-edit-currency">{t("procurement.field.currency")}</Label><Input id="boq-edit-currency" maxLength={3} value={currency} onChange={(event) => setCurrency(event.target.value.toUpperCase())} /></div>
    <div className="space-y-3">
      <div className="flex items-center justify-between"><Label>{t("procurement.field.lines")}</Label><Button type="button" variant="outline" size="sm" onClick={() => setLines((current) => [...current, emptyLine()])}><Plus className="mr-2 h-4 w-4" />{t("procurement.action.addLine")}</Button></div>
      {lines.map((line, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-2 lg:grid-cols-[1fr_2fr_0.8fr_1fr_1fr_auto]" key={line.draftId}>
        <div><Label htmlFor={`boq-edit-code-${index}`}>{t("procurement.field.itemCode")}</Label><Input id={`boq-edit-code-${index}`} maxLength={80} value={line.itemCode} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, itemCode: event.target.value } : item))} /></div>
        <div><Label htmlFor={`boq-edit-description-${index}`}>{t("procurement.field.description")}</Label><Input id={`boq-edit-description-${index}`} maxLength={500} value={line.description} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, description: event.target.value } : item))} /></div>
        <div><Label htmlFor={`boq-edit-unit-${index}`}>{t("procurement.field.unit")}</Label><Input id={`boq-edit-unit-${index}`} maxLength={50} value={line.unit} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, unit: event.target.value } : item))} /></div>
        <div><Label htmlFor={`boq-edit-quantity-${index}`}>{t("procurement.field.quantity")}</Label><Input id={`boq-edit-quantity-${index}`} type="number" min={0} step="any" value={line.approvedQuantity} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, approvedQuantity: Number(event.target.value) } : item))} /></div>
        <div><Label htmlFor={`boq-edit-price-${index}`}>{t("procurement.field.budgetPrice")}</Label><Input id={`boq-edit-price-${index}`} type="number" min={0} step="any" value={line.budgetUnitPrice} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, budgetUnitPrice: Number(event.target.value) } : item))} /></div>
        <div className="self-end"><Button type="button" variant="ghost" size="icon" aria-label={t("procurement.action.removeLine")} title={t("procurement.action.removeLine")} disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="h-4 w-4 text-destructive" /></Button></div>
      </div>)}
    </div>
  </div>
);

export default BoqRevisionDetailPage;
