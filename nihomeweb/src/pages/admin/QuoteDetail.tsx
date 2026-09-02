import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  Ban,
  Calendar,
  CheckCheck,
  Clipboard,
  Download,
  FileText,
  History,
  ListChecks,
  Loader2,
  Pencil,
  Plus,
  Save,
  Send,
  ThumbsDown,
  ThumbsUp,
  Trash2,
  Upload,
  User,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import BoqPasteDialog from "@/components/admin/BoqPasteDialog";
import QuoteRateFields from "@/components/admin/QuoteRateFields";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError, isConcurrencyConflict } from "@/lib/apiError";
import { formatFileSize, formatVnd, parseVnd } from "@/lib/numberFormat";
import { calculateQuoteTotals, validateQuoteValues } from "@/lib/quoteTotals";
import { isValidVietnameseOverrideReason } from "@/lib/quoteRate";
import { normalizeBoqSortOrder } from "@/lib/boqPaste";
import { PageLoading, PageError } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  adminApi,
  type QuoteDocumentResponse,
  type QuoteItemInput,
  type QuoteResponse,
  type QuoteStatus,
  type QuoteVersionResponse,
  type QuoteVersionsResponse,
  type UpdateQuoteRequest,
  type MaterialRateRevisionResponse,
} from "@/services/adminApi";

const STATUS_STYLES: Record<QuoteStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  PendingApproval: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  SentToCustomer: "border-sky-200 bg-sky-50 text-sky-700",
  CustomerApproved: "border-green-300 bg-green-100 text-green-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Expired: "border-orange-200 bg-orange-50 text-orange-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

type WorkflowKind =
  | "submit"
  | "approve"
  | "rejectInternal"
  | "send"
  | "customerApprove"
  | "customerReject"
  | "cancel";

/**
 * Set of workflow actions to expose given the current status. Delete lives
 * outside because it is destructive and only makes sense for Draft.
 */
const WORKFLOW_BY_STATUS: Record<QuoteStatus, WorkflowKind[]> = {
  Draft: ["submit"],
  PendingApproval: ["approve", "rejectInternal", "cancel"],
  Approved: ["send", "cancel"],
  SentToCustomer: ["customerApprove", "customerReject", "cancel"],
  CustomerApproved: [],
  Rejected: [],
  Expired: ["cancel"],
  Cancelled: [],
};

/**
 * Whether a Draft item edit should hit the API. When status is one of these
 * the update route spawns a new version server-side (spec NIH-84 AC #4).
 */
function toFormState(q: QuoteResponse): UpdateQuoteRequest {
  return {
    rowVersion: q.rowVersion,
    ownerUserId: q.ownerUserId ?? null,
    areaSqm: q.areaSqm ?? null,
    unitPricePerSqm: q.unitPricePerSqm ?? null,
    materialRateCatalogId: q.materialRateCatalogId ?? null,
    pricingEffectiveDate: q.pricingEffectiveDate ?? null,
    rateOverrideReason: q.rateOverrideReason ?? null,
    packageDescription: q.packageDescription ?? "",
    items: q.items.map((i) => ({
      itemCode: i.itemCode ?? null,
      name: i.name,
      unit: i.unit,
      quantity: i.quantity,
      unitPrice: i.unitPrice,
      sortOrder: i.sortOrder,
    })),
    discountPercent: q.discountPercent,
    vatPercent: q.vatPercent,
    validUntil: q.validUntil,
    note: q.note ?? "",
  };
}

/** Server-side rounding: match the QuoteService implementation. */
function computePreview(
  method: "UnitCost" | "Boq",
  form: UpdateQuoteRequest,
): { subtotal: number; grandTotal: number } {
  return calculateQuoteTotals(method, form);
}

// -------- Component --------

const AdminQuoteDetail = () => {
  const { id } = useParams();
  const quoteId = Number(id);
  const navigate = useNavigate();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();

  const canManage = has(ADMIN_PERMS.quotesManage);
  const canApprove = has(ADMIN_PERMS.quotesApprove);
  const canSend = has(ADMIN_PERMS.quotesSend);
  const canOverrideRate = has(ADMIN_PERMS.quotesRateOverride);

  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<UpdateQuoteRequest | null>(null);
  const [saving, setSaving] = useState(false);
  const [effectiveRevision, setEffectiveRevision] = useState<MaterialRateRevisionResponse | null>(null);
  const [exportingPdf, setExportingPdf] = useState(false);
  const [boqPasteOpen, setBoqPasteOpen] = useState(false);

  const [versions, setVersions] = useState<QuoteVersionsResponse | null>(null);
  const [versionsLoading, setVersionsLoading] = useState(false);
  const [selectedVersion, setSelectedVersion] = useState<QuoteVersionResponse | null>(null);

  const [documents, setDocuments] = useState<QuoteDocumentResponse[] | null>(null);
  const [documentsLoading, setDocumentsLoading] = useState(false);
  const [documentsError, setDocumentsError] = useState<string | null>(null);
  const [documentFile, setDocumentFile] = useState<File | null>(null);
  const [documentLabel, setDocumentLabel] = useState("");
  const [documentInputKey, setDocumentInputKey] = useState(0);
  const [uploadingDocument, setUploadingDocument] = useState(false);
  const [deletingDocumentId, setDeletingDocumentId] = useState<number | null>(null);

  const [workflow, setWorkflow] = useState<WorkflowKind | null>(null);
  const [workflowNote, setWorkflowNote] = useState("");
  const [workflowBusy, setWorkflowBusy] = useState(false);

  // ---------- data load ----------

  const load = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getQuote(quoteId);
      setQuote(data);
      setForm(toFormState(data));
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [quoteId]);

  useEffect(() => {
    void load();
  }, [load]);

  const loadVersions = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setVersionsLoading(true);
    try {
      const { data } = await adminApi.getQuoteVersions(quoteId);
      setVersions(data);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setVersionsLoading(false);
    }
  }, [quoteId, toast, t]);

  const loadDocuments = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setDocumentsLoading(true);
    setDocumentsError(null);
    try {
      const { data } = await adminApi.listQuoteDocuments(quoteId);
      setDocuments(data);
    } catch (err) {
      setDocumentsError(extractApiError(err));
    } finally {
      setDocumentsLoading(false);
    }
  }, [quoteId]);

  // ---------- BOQ helpers ----------

  const addBoqRow = () => {
    if (!form) return;
    const items = [...(form.items ?? [])];
    items.push({
      itemCode: null,
      name: "",
      unit: "",
      quantity: 0,
      unitPrice: 0,
      sortOrder: items.length + 1,
    });
    setForm({ ...form, items });
  };
  const removeBoqRow = (idx: number) => {
    if (!form) return;
    const items = [...(form.items ?? [])];
    items.splice(idx, 1);
    setForm({ ...form, items: normalizeBoqSortOrder(items) });
  };
  const updateBoqRow = (idx: number, patch: Partial<QuoteItemInput>) => {
    if (!form) return;
    const items = [...(form.items ?? [])];
    items[idx] = { ...items[idx], ...patch };
    setForm({ ...form, items });
  };
  const appendPastedBoqItems = (pastedItems: QuoteItemInput[]) => {
    if (!form) return;
    const existingItems = form.items ?? [];
    const items = normalizeBoqSortOrder([...existingItems, ...pastedItems]);
    setForm({ ...form, items });
    toast({ title: t("quotes.paste.ok"), description: `+${pastedItems.length}` });
  };

  // ---------- save + workflow ----------

  const handleSave = async () => {
    if (!quote || !form) return;
    const validationIssue = validateQuoteValues(quote.method, form);
    if (validationIssue) {
      toast({
        title: t("common.error"),
        description: t(`quotes.validation.${validationIssue}`),
        variant: "destructive",
      });
      return;
    }
    if (quote.method === "UnitCost") {
      if (!form.materialRateCatalogId || !form.pricingEffectiveDate) {
        toast({ title: t("common.error"), description: t("quotes.validation.rateSelectionRequired"), variant: "destructive" });
        return;
      }
      if (!effectiveRevision) {
        toast({ title: t("common.error"), description: t("quotes.validation.noEffectiveRate"), variant: "destructive" });
        return;
      }
      const isOverride = form.unitPricePerSqm !== effectiveRevision.totalRatePerSqm;
      if (isOverride && !canOverrideRate) {
        toast({ title: t("common.error"), description: t("quotes.validation.overridePermission"), variant: "destructive" });
        return;
      }
      if (isOverride && !isValidVietnameseOverrideReason(form.rateOverrideReason)) {
        toast({ title: t("common.error"), description: t("quotes.validation.overrideReason"), variant: "destructive" });
        return;
      }
    }
    setSaving(true);
    try {
      const { data } = await adminApi.updateQuote(quote.id, {
        ...form,
        packageDescription: form.packageDescription?.trim() || undefined,
        note: form.note?.trim() || undefined,
        validUntil: form.validUntil || null,
      });
      setQuote(data);
      setForm(toFormState(data));
      setEditing(false);
      toast({ title: t("quotes.updated") });
      // If we're viewing the Versions tab, refresh it too.
      if (versions) void loadVersions();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
      if (isConcurrencyConflict(err)) await load();
    } finally {
      setSaving(false);
    }
  };

  const downloadPdf = async () => {
    if (!quote) return;
    setExportingPdf(true);
    try {
      const { data } = await adminApi.exportQuotePdf(quote.id, lang);
      const url = URL.createObjectURL(data);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${quote.code}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setExportingPdf(false);
    }
  };

  const runWorkflow = async (kind?: WorkflowKind) => {
    const targetWorkflow = kind ?? workflow;
    if (!targetWorkflow || !quote) return;
    setWorkflowBusy(true);
    try {
      const body = { note: workflowNote.trim() || undefined, rowVersion: quote.rowVersion };
      const fn: Record<WorkflowKind, () => Promise<{ data: QuoteResponse }>> = {
        submit: () => adminApi.submitQuote(quote.id, body),
        approve: () => adminApi.approveQuote(quote.id, body),
        rejectInternal: () => adminApi.rejectQuoteInternal(quote.id, body),
        send: () => adminApi.sendQuoteToCustomer(quote.id, body),
        customerApprove: () => adminApi.markQuoteCustomerApproved(quote.id, body),
        customerReject: () => adminApi.markQuoteCustomerRejected(quote.id, body),
        cancel: () => adminApi.cancelQuote(quote.id, body),
      };
      const { data } = await fn[targetWorkflow]();
      setQuote(data);
      setForm(toFormState(data));
      setWorkflow(null);
      setWorkflowNote("");
      toast({ title: t(`quotes.action.${targetWorkflow}`) });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
      if (isConcurrencyConflict(err)) await load();
    } finally {
      setWorkflowBusy(false);
    }
  };

  // Actions that can run directly without a dialog (note is optional)
  const directActions: WorkflowKind[] = ["submit", "approve", "rejectInternal", "send", "customerApprove"];

  const handleWorkflowClick = (k: WorkflowKind) => {
    if (directActions.includes(k)) {
      void runWorkflow(k);
    } else {
      setWorkflowNote("");
      setWorkflow(k);
    }
  };

  const handleDelete = async () => {
    if (!quote) return;
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteQuote(quote.id, quote.rowVersion);
      toast({ title: t("quotes.updated") });
      navigate("/admin/quotes");
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    }
  };

  const handleUploadDocument = async () => {
    if (!quote || !documentFile) return;
    setUploadingDocument(true);
    try {
      await adminApi.uploadQuoteDocument(quote.id, documentFile, documentLabel);
      setDocumentFile(null);
      setDocumentLabel("");
      setDocumentInputKey((value) => value + 1);
      await loadDocuments();
      toast({ title: t("quotes.document.uploaded") });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setUploadingDocument(false);
    }
  };

  const handleDeleteDocument = async (documentId: number) => {
    if (!quote || !window.confirm(t("quotes.document.deleteConfirm"))) return;
    setDeletingDocumentId(documentId);
    try {
      await adminApi.deleteQuoteDocument(quote.id, documentId);
      setDocuments((current) => current?.filter((document) => document.id !== documentId) ?? []);
      toast({ title: t("quotes.document.deleted") });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setDeletingDocumentId(null);
    }
  };

  const preview = useMemo(() => {
    if (!quote || !form) return null;
    return computePreview(quote.method, form);
  }, [quote, form]);

  // -------- render --------

  if (!Number.isFinite(quoteId)) return <AdminLayout><PageError message="Invalid id" /></AdminLayout>;
  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={() => void load()} /></AdminLayout>;
  if (!quote || !form) return null;

  const workflowKinds = WORKFLOW_BY_STATUS[quote.status];
  const isTerminal =
    quote.status === "CustomerApproved" ||
    quote.status === "Rejected" ||
    quote.status === "Cancelled";
  const showEditToggle = canManage && !editing && !isTerminal;

  // A quote the customer or an approver has signed off on is the point where a
  // contract can be raised from it.
  const canRaiseContract =
    canManage && !editing && (quote.status === "Approved" || quote.status === "CustomerApproved");

  // QuoteResponse.customerId is optional, so older quotes may not carry one.
  // Build with URLSearchParams so empty parameters drop out entirely rather than
  // reaching the contract form as the string "undefined".
  const goToContractForm = () => {
    const params = new URLSearchParams({ fromQuote: String(quote.id) });
    if (quote.opportunityId) params.set("opportunityId", String(quote.opportunityId));
    if (quote.customerId) params.set("customerId", String(quote.customerId));
    if (quote.grandTotal > 0) params.set("value", String(quote.grandTotal));
    navigate(`/admin/contracts?${params.toString()}`);
  };
  const canDelete = canManage;

  return (
    <AdminLayout>
      {/* ---------- Header ---------- */}
      <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="mb-1 flex items-center gap-2 text-sm text-muted-foreground">
            <Button variant="ghost" size="sm" asChild className="h-7 px-2">
              <Link to="/admin/quotes">
                <ArrowLeft className="h-4 w-4" />
              </Link>
            </Button>
            <span className="truncate">
              {t("quotes.field.opportunity")}: {quote.opportunityName ?? "—"}
            </span>
          </div>
          <h1 className="flex flex-wrap items-center gap-3 text-2xl font-semibold tracking-tight">
            {quote.code}
            <Badge variant="outline" className={cn("whitespace-nowrap text-xs", STATUS_STYLES[quote.status])}>
              {t(`quotes.status.${quote.status}`)}
            </Badge>
            <span className="text-sm font-normal text-muted-foreground">V{quote.version}</span>
          </h1>
          {quote.customerName && (
            <p className="mt-1 truncate text-sm text-muted-foreground">
              <User className="mr-1 inline h-3.5 w-3.5" />
              {t("quotes.field.customer")}: {quote.customerName}
            </p>
          )}
        </div>

        <div className="flex flex-wrap gap-1.5">
          {!editing && (
            <Button variant="outline" onClick={() => void downloadPdf()} disabled={exportingPdf}>
              {exportingPdf ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Download className="mr-1.5 h-4 w-4" />}
              {t("quotes.action.downloadPdf")}
            </Button>
          )}
          {showEditToggle && (
            <Button variant="outline" data-testid="quote-edit" onClick={() => setEditing(true)}>
              <Pencil className="mr-1.5 h-4 w-4" />
              {t("common.edit")}
            </Button>
          )}
          {editing && (
            <>
              <Button variant="outline" onClick={() => { setForm(toFormState(quote)); setEditing(false); }}>
                {t("common.cancel")}
              </Button>
              <Button data-testid="quote-save" onClick={() => void handleSave()} disabled={saving}>
                {saving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                <Save className="mr-1.5 h-4 w-4" />
                {t("common.save")}
              </Button>
            </>
          )}
          {!editing &&
            workflowKinds.map((k) => (
              <Button
                key={k}
                variant={k === "rejectInternal" || k === "customerReject" || k === "cancel" ? "outline" : "default"}
                onClick={() => handleWorkflowClick(k)}
                disabled={
                  workflowBusy ||
                  ((k === "approve" || k === "rejectInternal") ? !canApprove
                  : k === "send" ? !canSend
                  : !canManage)
                }
              >
                {workflowIcon(k)}
                {t(`quotes.action.${k}`)}
              </Button>
            ))}
          {canRaiseContract && (
            <Button variant="outline" onClick={goToContractForm}>
              <FileText className="mr-1.5 h-4 w-4" />
              {t("quotes.createContract.action")}
            </Button>
          )}
          {!editing && canDelete && (
            <Button variant="outline" className="text-destructive hover:text-destructive" onClick={() => void handleDelete()}>
              <Trash2 className="mr-1.5 h-4 w-4" />
              {t("quotes.action.delete")}
            </Button>
          )}
        </div>
      </div>

      {/* ---------- Meta strip ---------- */}
      {/* At-a-glance summary so the key numbers stay visible without scrolling
          to the side panel on narrow screens. */}
      <dl className="mb-4 grid grid-cols-2 gap-2 rounded-lg border bg-card p-3 text-sm sm:grid-cols-4">
        <div className="min-w-0">
          <dt className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.grandTotal")}
          </dt>
          <dd className="truncate text-base font-semibold">
            {formatVnd(quote.grandTotal)} ₫
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.validUntil")}
          </dt>
          <dd className="flex items-center gap-1 truncate">
            <Calendar className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            <span className="truncate">{new Date(quote.validUntil).toLocaleDateString()}</span>
            {quote.isExpired && (
              <Badge variant="outline" className="ml-1 border-rose-200 bg-rose-50 text-[10px] text-rose-700">
                {t("quotes.status.Expired")}
              </Badge>
            )}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.owner")}
          </dt>
          <dd className="flex items-center gap-1 truncate">
            <User className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            <span className="truncate">{quote.ownerName ?? "—"}</span>
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.method")}
          </dt>
          <dd className="truncate">{t(`quotes.method.${quote.method}`)}</dd>
        </div>
      </dl>

      {/* ---------- Body ---------- */}
      <Tabs
        defaultValue="content"
        className="w-full"
        onValueChange={(v) => {
          if (v === "versions" && !versions) void loadVersions();
          if (v === "documents" && documents === null && !documentsLoading) void loadDocuments();
        }}
      >
        <TabsList className="w-full justify-start overflow-x-auto">
          <TabsTrigger value="content">
            <ListChecks className="mr-1.5 h-4 w-4" />
            {t("quotes.tab.content")}
          </TabsTrigger>
          <TabsTrigger value="versions">
            <History className="mr-1.5 h-4 w-4" />
            {t("quotes.tab.versions")}
          </TabsTrigger>
          <TabsTrigger value="documents">
            <FileText className="mr-1.5 h-4 w-4" />
            {t("quotes.tab.documents")}
          </TabsTrigger>
          <TabsTrigger value="workflow">
            {t("quotes.tab.workflow")}
          </TabsTrigger>
        </TabsList>

        {/* ---------- CONTENT ---------- */}
        <TabsContent value="content" className="mt-4">
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px] xl:gap-6">
            <div className="min-w-0 space-y-4">
              {quote.method === "UnitCost" ? (
                <div className="space-y-3 rounded-lg border bg-card p-4">
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <FormField label={t("quotes.field.areaSqm")}>
                    <Input
                      inputMode="decimal"
                      value={form.areaSqm ?? ""}
                      disabled={!editing}
                      onChange={(e) => setForm({ ...form, areaSqm: e.target.value ? Number(e.target.value) : null })}
                    />
                  </FormField>
                  </div>
                  {editing ? (
                    <QuoteRateFields
                      catalogId={form.materialRateCatalogId}
                      pricingDate={form.pricingEffectiveDate}
                      unitPrice={form.unitPricePerSqm}
                      overrideReason={form.rateOverrideReason}
                      rateSource={quote.rateSource}
                      canOverride={canOverrideRate}
                      onEffectiveRevisionChange={setEffectiveRevision}
                      onChange={(patch) => setForm((current) => current ? { ...current, ...patch } : current)}
                    />
                  ) : (
                    <RateProvenance value={quote} t={t} />
                  )}
                  <div>
                    <FormField label={t("quotes.field.packageDescription")}>
                      <Textarea
                        rows={3}
                        value={form.packageDescription ?? ""}
                        disabled={!editing}
                        onChange={(e) => setForm({ ...form, packageDescription: e.target.value })}
                      />
                    </FormField>
                  </div>
                </div>
              ) : (
                <BoqTable
                  form={form}
                  editing={editing}
                  onAdd={addBoqRow}
                  onRemove={removeBoqRow}
                  onChange={updateBoqRow}
                  onPaste={() => setBoqPasteOpen(true)}
                  t={t}
                />
              )}

              <div className="grid grid-cols-2 gap-3 rounded-lg border bg-card p-4 sm:grid-cols-4">
                <FormField label={t("quotes.field.discountPercent")}>
                  <Input
                    type="number" min={0} max={100}
                    value={form.discountPercent}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, discountPercent: Number(e.target.value) })}
                  />
                </FormField>
                <FormField label={t("quotes.field.vatPercent")}>
                  <Input
                    type="number" min={0} max={100}
                    value={form.vatPercent}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, vatPercent: Number(e.target.value) })}
                  />
                </FormField>
                <FormField label={t("quotes.field.validUntil")}>
                  <Input
                    type="date"
                    value={form.validUntil?.slice(0, 10) ?? ""}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, validUntil: e.target.value ? `${e.target.value}T23:59:59Z` : null })}
                  />
                </FormField>
                <FormField label={t("quotes.field.note")}>
                  <Input
                    value={form.note ?? ""}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, note: e.target.value })}
                  />
                </FormField>
              </div>
            </div>

            {/* ---------- Summary side panel ---------- */}
            <aside className="space-y-2 rounded-lg border bg-card p-4 text-sm">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">{t("quotes.field.subtotal")}</span>
                <span className="font-medium">{formatVnd(preview?.subtotal ?? quote.subtotal)} ₫</span>
              </div>
              <div className="flex items-center justify-between text-muted-foreground">
                <span>{t("quotes.field.discountPercent")}</span>
                <span>{form.discountPercent}%</span>
              </div>
              <div className="flex items-center justify-between text-muted-foreground">
                <span>{t("quotes.field.vatPercent")}</span>
                <span>{form.vatPercent}%</span>
              </div>
              <div className="my-2 border-t" />
              <div className="flex items-center justify-between">
                <span className="font-semibold">{t("quotes.field.grandTotal")}</span>
                <span className="text-lg font-bold text-primary">{formatVnd(preview?.grandTotal ?? quote.grandTotal)} ₫</span>
              </div>
              <p className="break-words text-xs italic text-muted-foreground">
                <span className="mr-1 font-medium not-italic">{t("quotes.field.grandTotalInWords")}:</span>
                {quote.grandTotalInWords}
              </p>
              {editing && preview && preview.grandTotal !== quote.grandTotal && (
                <p className="mt-2 rounded bg-amber-50 p-2 text-[11px] text-amber-800">
                  {t("quotes.preview.unsaved")}
                </p>
              )}
            </aside>
          </div>
        </TabsContent>

        <TabsContent value="documents" className="mt-4 space-y-4">
          {canManage && (
            <div className="grid gap-3 rounded-lg border bg-card p-4 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] md:items-end">
              <div className="space-y-1.5">
                <Label htmlFor="quote-document-file">{t("quotes.document.file")}</Label>
                <Input
                  key={documentInputKey}
                  id="quote-document-file"
                  type="file"
                  accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
                  onChange={(event) => setDocumentFile(event.target.files?.[0] ?? null)}
                />
                <p className="text-xs text-muted-foreground">{t("quotes.document.fileHint")}</p>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="quote-document-label">{t("quotes.document.label")}</Label>
                <Input
                  id="quote-document-label"
                  value={documentLabel}
                  onChange={(event) => setDocumentLabel(event.target.value)}
                  maxLength={300}
                  placeholder={t("quotes.document.labelPlaceholder")}
                />
              </div>
              <Button
                type="button"
                onClick={() => void handleUploadDocument()}
                disabled={!documentFile || uploadingDocument}
              >
                {uploadingDocument ? (
                  <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                ) : (
                  <Upload className="mr-1.5 h-4 w-4" />
                )}
                {uploadingDocument ? t("quotes.document.uploading") : t("quotes.document.upload")}
              </Button>
            </div>
          )}

          {documentsLoading ? (
            <PageLoading />
          ) : documentsError ? (
            <PageError message={documentsError} onRetry={() => void loadDocuments()} />
          ) : documents?.length === 0 ? (
            <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
              {t("quotes.document.empty")}
            </p>
          ) : documents ? (
            <ul className="grid gap-3 lg:grid-cols-2">
              {documents.map((document) => (
                <li key={document.id} className="flex min-w-0 items-start gap-3 rounded-lg border bg-card p-3">
                  <div className="rounded-md bg-muted p-2">
                    <FileText className="h-5 w-5 text-muted-foreground" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium" title={document.originalFileName}>
                      {document.label || document.originalFileName}
                    </p>
                    {document.label && (
                      <p className="truncate text-xs text-muted-foreground" title={document.originalFileName}>
                        {document.originalFileName}
                      </p>
                    )}
                    <p className="text-xs text-muted-foreground">
                      {formatFileSize(document.fileSize)} · {new Date(document.createdAt).toLocaleString()}
                    </p>
                    {document.uploadedByName && (
                      <p className="truncate text-xs text-muted-foreground">
                        {t("quotes.document.uploadedBy").replace("{name}", document.uploadedByName)}
                      </p>
                    )}
                  </div>
                  <div className="flex shrink-0 gap-1">
                    <AdminFilePreview
                      url={document.filePath}
                      fileName={document.originalFileName}
                      contentType={document.contentType}
                      fetchFile={async () => (
                        await adminApi.getQuoteDocumentContent(quote.id, document.id)
                      ).data}
                    />
                    {canManage && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        disabled={deletingDocumentId === document.id}
                        onClick={() => void handleDeleteDocument(document.id)}
                        title={t("common.delete")}
                        aria-label={t("common.delete")}
                      >
                        {deletingDocumentId === document.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Trash2 className="h-4 w-4" />
                        )}
                      </Button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          ) : null}
        </TabsContent>

        {/* ---------- VERSIONS ---------- */}
        <TabsContent value="versions" className="mt-4">
          {versionsLoading ? (
            <PageLoading />
          ) : versions === null ? (
            <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
              {t("common.loading")}...
            </div>
          ) : (
            <div className="space-y-2">
              <p className="rounded-lg border border-dashed bg-muted/20 p-3 text-sm text-muted-foreground">
                {t("quotes.version.description")}
              </p>
              {versions.versions
                .slice()
                .sort((a, b) => b.version - a.version)
                .map((v) => (
                  <button
                    type="button"
                    key={v.version}
                    className={cn(
                      "w-full rounded-lg border p-3 text-left text-sm transition-colors hover:border-primary/50 hover:bg-muted/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
                      v.isCurrent ? "border-primary bg-primary/5" : "bg-card",
                    )}
                    onClick={() => setSelectedVersion(v)}
                    aria-label={`${t("quotes.version.view")} V${v.version}`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2 font-medium">
                        V{v.version}
                        {v.isCurrent && (
                          <Badge variant="outline" className="border-primary text-primary">
                            {t("quotes.version.current")}
                          </Badge>
                        )}
                      </div>
                      <div className="text-right">
                        <div className="font-medium">{formatVnd(v.grandTotal)} ₫</div>
                        <div className="text-xs text-muted-foreground">
                          {new Date(v.capturedAt).toLocaleString()}
                        </div>
                      </div>
                    </div>
                    <div className="mt-1 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                      <span>
                        {t(`quotes.method.${v.method}`)} · {t("quotes.field.subtotal")} {formatVnd(v.subtotal)} · VAT {v.vatPercent}%
                      </span>
                      <span className="font-medium text-primary">{t("quotes.version.view")}</span>
                    </div>
                  </button>
                ))}
            </div>
          )}
        </TabsContent>

        {/* ---------- WORKFLOW LOG ---------- */}
        <TabsContent value="workflow" className="mt-4">
          <div className="rounded-lg border bg-card p-4">
            <ul className="space-y-2 text-sm">
              {quote.approvalLogs.map((l) => (
                <li key={l.id} className="border-b pb-2 last:border-b-0 last:pb-0">
                  <div className="flex flex-wrap items-center justify-between gap-1">
                    <span className="font-medium">{translateAction(l.action, t)}</span>
                    <span className="text-xs text-muted-foreground">
                      {new Date(l.createdAt).toLocaleString()}
                    </span>
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {l.fromStatus ? `${translateStatus(l.fromStatus, t)} → ` : ""}
                    {translateStatus(l.toStatus, t)}
                    {l.byUserName ? ` · ${l.byUserName}` : ""}
                  </div>
                  {l.note && <div className="mt-1 text-xs italic">"{l.note}"</div>}
                </li>
              ))}
              {quote.approvalLogs.length === 0 && (
                <li className="text-center text-muted-foreground">—</li>
              )}
            </ul>
          </div>
        </TabsContent>
      </Tabs>

      {/* ---------- Workflow note dialog ---------- */}
      <Dialog open={!!workflow} onOpenChange={(o) => !o && setWorkflow(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{workflow && t(`quotes.action.${workflow}`)}</DialogTitle>
            <DialogDescription>{t("quotes.action.noteOptional")}</DialogDescription>
          </DialogHeader>
          <Textarea
            rows={4}
            value={workflowNote}
            onChange={(e) => setWorkflowNote(e.target.value)}
            placeholder={t("quotes.field.note")}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setWorkflow(null)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void runWorkflow()} disabled={workflowBusy}>
              {workflowBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <BoqPasteDialog
        open={boqPasteOpen}
        onOpenChange={setBoqPasteOpen}
        onConfirm={appendPastedBoqItems}
      />

      <Dialog open={selectedVersion !== null} onOpenChange={(open) => !open && setSelectedVersion(null)}>
        <DialogContent className="max-h-[90vh] w-[95vw] max-w-4xl overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle className="flex flex-wrap items-center gap-2">
              {t("quotes.version.snapshot")} V{selectedVersion?.version}
              {selectedVersion?.isCurrent && (
                <Badge variant="outline" className="border-primary text-primary">
                  {t("quotes.version.current")}
                </Badge>
              )}
            </DialogTitle>
            <DialogDescription>
              {selectedVersion && (
                <>
                  {t("quotes.version.capturedAt")}: {new Date(selectedVersion.capturedAt).toLocaleString()}
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          {selectedVersion && <QuoteVersionDetails version={selectedVersion} t={t} />}
          <DialogFooter>
            <Button variant="outline" onClick={() => setSelectedVersion(null)}>
              {t("common.close")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

// -------- helpers --------

/** Map the backend PascalCase action name to the shared t() key. */
function translateAction(action: string, t: (k: string) => string): string {
  const map: Record<string, string> = {
    Create: "quotes.log.create",
    Update: "quotes.log.update",
    Submit: "quotes.action.submit",
    Approve: "quotes.action.approve",
    RejectInternal: "quotes.action.rejectInternal",
    Send: "quotes.action.send",
    CustomerApprove: "quotes.action.customerApprove",
    CustomerReject: "quotes.action.customerReject",
    Cancel: "quotes.action.cancel",
    ExtendValidity: "quotes.log.extendValidity",
    NewVersion: "quotes.log.newVersion",
  };
  const key = map[action];
  if (!key) return action;
  const translated = t(key);
  return translated === key ? action : translated;
}

/** Backend enum → translated status label; leaves unknown values as-is. */
function translateStatus(status: string, t: (k: string) => string): string {
  const key = `quotes.status.${status}`;
  const translated = t(key);
  return translated === key ? status : translated;
}

function workflowIcon(k: WorkflowKind) {
  const cls = "mr-1.5 h-4 w-4";
  switch (k) {
    case "submit": return <ThumbsUp className={cls} />;
    case "approve": return <CheckCheck className={cls} />;
    case "rejectInternal": return <ThumbsDown className={cls} />;
    case "send": return <Send className={cls} />;
    case "customerApprove": return <CheckCheck className={cls} />;
    case "customerReject": return <XCircle className={cls} />;
    case "cancel": return <Ban className={cls} />;
  }
}

const FormField = ({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) => (
  <div>
    <Label className="text-xs uppercase tracking-wide text-muted-foreground">{label}</Label>
    {children}
  </div>
);

const QuoteVersionDetails = ({
  version,
  t,
}: {
  version: QuoteVersionResponse;
  t: (k: string) => string;
}) => (
  <div className="space-y-4">
    <dl className="grid grid-cols-2 gap-3 rounded-lg border bg-muted/20 p-4 text-sm sm:grid-cols-4">
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.method")}</dt>
        <dd className="font-medium">{t(`quotes.method.${version.method}`)}</dd>
      </div>
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.subtotal")}</dt>
        <dd className="font-medium">{formatVnd(version.subtotal)} ₫</dd>
      </div>
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.discountPercent")}</dt>
        <dd className="font-medium">{version.discountPercent}%</dd>
      </div>
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.vatPercent")}</dt>
        <dd className="font-medium">{version.vatPercent}%</dd>
      </div>
    </dl>

    {version.method === "UnitCost" ? (
      <div className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-2">
        <div>
          <div className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.areaSqm")}</div>
          <div className="font-medium">{version.areaSqm ?? "—"}</div>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.unitPricePerSqm")}</div>
          <div className="font-medium">{version.unitPricePerSqm != null ? `${formatVnd(version.unitPricePerSqm)} ₫` : "—"}</div>
        </div>
        <div className="sm:col-span-2">
          <div className="text-xs uppercase tracking-wide text-muted-foreground">{t("quotes.field.packageDescription")}</div>
          <div className="mt-1 whitespace-pre-wrap">{version.packageDescription || "—"}</div>
        </div>
        <div className="sm:col-span-2"><RateProvenance value={version} t={t} /></div>
      </div>
    ) : (
      <div className="rounded-lg border bg-card">
        <div className="border-b bg-muted/30 p-2 text-xs uppercase tracking-wide text-muted-foreground">
          {t("quotes.boq.title")}
        </div>
        <div className="hidden overflow-x-auto md:block">
          <table className="w-full min-w-[560px] divide-y text-sm">
            <thead className="bg-muted/20 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-2 py-1.5 text-left font-medium">{t("quotes.boq.code")}</th>
                <th className="px-2 py-1.5 text-left font-medium">{t("quotes.boq.name")}</th>
                <th className="px-2 py-1.5 text-left font-medium">{t("quotes.boq.unit")}</th>
                <th className="px-2 py-1.5 text-right font-medium">{t("quotes.boq.qty")}</th>
                <th className="px-2 py-1.5 text-right font-medium">{t("quotes.boq.unitPrice")}</th>
                <th className="px-2 py-1.5 text-right font-medium">{t("quotes.boq.amount")}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {version.items.map((item, index) => (
                <tr key={`${item.id}-${index}`}>
                  <td className="px-2 py-2">{item.itemCode || "—"}</td>
                  <td className="px-2 py-2 font-medium">{item.name}</td>
                  <td className="px-2 py-2">{item.unit}</td>
                  <td className="px-2 py-2 text-right">{item.quantity}</td>
                  <td className="px-2 py-2 text-right">{formatVnd(item.unitPrice)} ₫</td>
                  <td className="px-2 py-2 text-right font-medium">{formatVnd(item.amount)} ₫</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <ul className="divide-y md:hidden">
          {version.items.map((item, index) => (
            <li key={`${item.id}-${index}`} className="space-y-1 p-3 text-sm">
              <div className="flex items-start justify-between gap-2">
                <span className="font-medium">{item.name}</span>
                <span className="whitespace-nowrap font-semibold">{formatVnd(item.amount)} ₫</span>
              </div>
              {item.itemCode && <div className="text-xs text-muted-foreground">{t("quotes.boq.code")}: {item.itemCode}</div>}
              <div className="text-xs text-muted-foreground">
                {item.quantity} {item.unit} × {formatVnd(item.unitPrice)} ₫
              </div>
            </li>
          ))}
        </ul>
      </div>
    )}

    <div className="flex items-center justify-between rounded-lg border border-primary/20 bg-primary/5 p-4">
      <span className="font-semibold">{t("quotes.field.grandTotal")}</span>
      <span className="text-xl font-bold text-primary">{formatVnd(version.grandTotal)} ₫</span>
    </div>
  </div>
);

type RateProvenanceValue = Pick<QuoteResponse | QuoteVersionResponse,
  "materialRateCatalogCode" | "materialRateCatalogName" | "materialRateRevisionVersion" |
  "pricingEffectiveDate" | "catalogUnitPricePerSqm" | "unitPricePerSqm" | "rateSource" |
  "rateOverrideReason"
>;

const RateProvenance = ({ value, t }: { value: RateProvenanceValue; t: (key: string) => string }) => (
  <dl className="grid gap-2 rounded-md border bg-muted/20 p-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
    <div><dt className="text-xs text-muted-foreground">{t("quotes.field.materialRateCatalog")}</dt><dd className="font-medium">{value.materialRateCatalogCode ? `${value.materialRateCatalogCode} · ${value.materialRateCatalogName ?? ""}` : "—"}</dd></div>
    <div><dt className="text-xs text-muted-foreground">{t("quotes.field.materialRateRevision")}</dt><dd className="font-medium">{value.materialRateRevisionVersion != null ? `V${value.materialRateRevisionVersion}` : "—"}</dd></div>
    <div><dt className="text-xs text-muted-foreground">{t("quotes.field.pricingEffectiveDate")}</dt><dd className="font-medium">{value.pricingEffectiveDate ?? "—"}</dd></div>
    <div><dt className="text-xs text-muted-foreground">{t("quotes.field.catalogRate")}</dt><dd className="font-medium">{value.catalogUnitPricePerSqm != null ? `${formatVnd(value.catalogUnitPricePerSqm)} ₫/m²` : "—"}</dd></div>
    <div><dt className="text-xs text-muted-foreground">{t("quotes.field.appliedRate")}</dt><dd className="font-medium">{value.unitPricePerSqm != null ? `${formatVnd(value.unitPricePerSqm)} ₫/m²` : "—"}</dd></div>
    <div><dt className="text-xs text-muted-foreground">{t("quotes.field.rateSource")}</dt><dd className="font-medium">{t(`quotes.rateSource.${value.rateSource}`)}</dd></div>
    {value.rateOverrideReason && <div className="sm:col-span-2 lg:col-span-3"><dt className="text-xs text-muted-foreground">{t("quotes.field.rateOverrideReason")}</dt><dd>{value.rateOverrideReason}</dd></div>}
  </dl>
);

const BoqTable = ({
  form,
  editing,
  onAdd,
  onRemove,
  onChange,
  onPaste,
  t,
}: {
  form: UpdateQuoteRequest;
  editing: boolean;
  onAdd: () => void;
  onRemove: (idx: number) => void;
  onChange: (idx: number, patch: Partial<QuoteItemInput>) => void;
  onPaste: () => void;
  t: (k: string) => string;
}) => {
  const items = form.items ?? [];
  return (
    <div className="rounded-lg border bg-card">
      <div className="flex items-center justify-between border-b bg-muted/30 p-2 text-xs uppercase tracking-wide text-muted-foreground">
        <span>{t("quotes.boq.title")}</span>
        {editing && (
          <div className="flex gap-1.5">
            <Button size="sm" variant="ghost" className="h-7 px-2" data-testid="boq-paste-open" onClick={onPaste}>
              <Clipboard className="mr-1 h-3.5 w-3.5" />
              {t("quotes.boq.paste")}
            </Button>
            <Button size="sm" variant="outline" className="h-7 px-2" onClick={onAdd}>
              <Plus className="mr-1 h-3.5 w-3.5" />
              {t("quotes.boq.addRow")}
            </Button>
          </div>
        )}
      </div>
      <div className="border-b bg-sky-50 px-3 py-2 text-xs leading-relaxed text-sky-900">
        {t("quotes.boq.materialRateHint")} {t("quotes.boq.materialRateExistingQuote")}{" "}
        <Link className="font-medium underline underline-offset-2" to="/admin/material-rates">
          {t("quotes.boq.openMaterialRates")}
        </Link>
      </div>

      {/* Desktop table (md+). */}
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[560px] divide-y text-sm">
          <thead className="bg-muted/20 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="hidden w-24 px-2 py-1.5 text-left font-medium lg:table-cell">{t("quotes.boq.code")}</th>
              <th className="px-2 py-1.5 text-left font-medium">{t("quotes.boq.name")}</th>
              <th className="w-16 px-2 py-1.5 text-left font-medium">{t("quotes.boq.unit")}</th>
              <th className="w-24 px-2 py-1.5 text-right font-medium">{t("quotes.boq.qty")}</th>
              <th className="w-32 px-2 py-1.5 text-right font-medium">{t("quotes.boq.unitPrice")}</th>
              <th className="w-32 px-2 py-1.5 text-right font-medium">{t("quotes.boq.amount")}</th>
              {editing && <th className="w-10 px-2 py-1.5" />}
            </tr>
          </thead>
          <tbody className="divide-y">
            {items.map((row, idx) => {
              const amount = Math.round(row.quantity * row.unitPrice * 100) / 100;
              return (
                <tr key={idx}>
                  <td className="hidden px-2 py-1 lg:table-cell">
                    <Input
                      className="h-8"
                      value={row.itemCode ?? ""}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { itemCode: e.target.value || null })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8"
                      value={row.name}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { name: e.target.value })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8"
                      value={row.unit}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { unit: e.target.value })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8 text-right"
                      inputMode="decimal"
                      value={row.quantity}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { quantity: Number(e.target.value) || 0 })}
                    />
                  </td>
                  <td className="px-2 py-1">
                    <Input
                      className="h-8 text-right"
                      inputMode="numeric"
                      value={row.unitPrice ? formatVnd(row.unitPrice) : ""}
                      disabled={!editing}
                      onChange={(e) => onChange(idx, { unitPrice: parseVnd(e.target.value) || 0 })}
                    />
                  </td>
                  <td className="px-2 py-1 text-right font-medium">{formatVnd(amount)} ₫</td>
                  {editing && (
                    <td className="px-2 py-1 text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        data-testid={`boq-remove-desktop-${idx}`}
                        className="h-7 px-1 text-destructive hover:text-destructive"
                        onClick={() => onRemove(idx)}
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  )}
                </tr>
              );
            })}
            {items.length === 0 && (
              <tr>
                <td colSpan={editing ? 7 : 6} className="px-2 py-6 text-center text-muted-foreground">
                  {t("quotes.validation.boqRequired")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Mobile card list (<md). Every row becomes a stacked card so no
          horizontal scroll is required on narrow screens. Fields stay
          editable when the parent is in edit mode. */}
      <ul className="divide-y md:hidden">
        {items.length === 0 && (
          <li className="p-4 text-center text-sm text-muted-foreground">
            {t("quotes.validation.boqRequired")}
          </li>
        )}
        {items.map((row, idx) => {
          const amount = Math.round(row.quantity * row.unitPrice * 100) / 100;
          return (
            <li key={idx} className="space-y-2 p-3">
              {editing ? (
                <>
                  <div className="grid grid-cols-[1fr,auto] items-start gap-2">
                    <Input
                      className="h-9 text-sm font-medium"
                      placeholder={t("quotes.boq.name")}
                      value={row.name}
                      onChange={(e) => onChange(idx, { name: e.target.value })}
                    />
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-9 px-2 text-destructive hover:text-destructive"
                      onClick={() => onRemove(idx)}
                      aria-label={t("quotes.action.delete")}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <Input
                      className="h-9"
                      placeholder={t("quotes.boq.code")}
                      value={row.itemCode ?? ""}
                      onChange={(e) => onChange(idx, { itemCode: e.target.value || null })}
                    />
                    <Input
                      className="h-9"
                      placeholder={t("quotes.boq.unit")}
                      value={row.unit}
                      onChange={(e) => onChange(idx, { unit: e.target.value })}
                    />
                    <Input
                      className="h-9 text-right"
                      inputMode="decimal"
                      placeholder={t("quotes.boq.qty")}
                      value={row.quantity}
                      onChange={(e) => onChange(idx, { quantity: Number(e.target.value) || 0 })}
                    />
                    <Input
                      className="h-9 text-right"
                      inputMode="numeric"
                      placeholder={t("quotes.boq.unitPrice")}
                      value={row.unitPrice ? formatVnd(row.unitPrice) : ""}
                      onChange={(e) => onChange(idx, { unitPrice: parseVnd(e.target.value) || 0 })}
                    />
                  </div>
                  <div className="flex items-center justify-between rounded bg-muted/30 px-2 py-1 text-sm">
                    <span className="text-muted-foreground">{t("quotes.boq.amount")}</span>
                    <span className="font-semibold">{formatVnd(amount)} ₫</span>
                  </div>
                </>
              ) : (
                <>
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium">{row.name}</div>
                      {row.itemCode && (
                        <div className="text-xs text-muted-foreground">
                          {t("quotes.boq.code")}: {row.itemCode}
                        </div>
                      )}
                    </div>
                    <div className="whitespace-nowrap text-sm font-semibold">
                      {formatVnd(amount)} ₫
                    </div>
                  </div>
                  <div className="text-xs text-muted-foreground">
                    {row.quantity} {row.unit} × {formatVnd(row.unitPrice)} ₫
                  </div>
                </>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
};

export default AdminQuoteDetail;
