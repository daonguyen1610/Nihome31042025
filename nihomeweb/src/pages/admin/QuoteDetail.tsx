import { useUiError } from "@/hooks/useUiError";
import { useUploadLimit } from "@/hooks/useUploadLimit";
import { getUiError } from "@/lib/apiError";
import { FieldScope } from "@/components/ui/field-scope";
import { useConfirm } from "@/components/ui/confirmation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  Ban,
  Calendar,
  Check,
  CheckCheck,
  ChevronDown,
  ChevronUp,
  Download,
  FileText,
  History,
  Link2,
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
import { SearchableSelect } from "@/components/ui/searchable-select";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import { personOption } from "@/lib/people";
import { selectQuoteFinanceReview } from "@/lib/financeReview";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { QuoteProjectBoqSource } from "@/components/admin/QuoteProjectBoqSource";
import { QuoteProgressPanel } from "@/components/admin/QuoteProgressPanel";
import { DEFAULT_REMINDER_DAYS, ReminderDaysField } from "@/components/admin/ReminderDaysField";
import QuoteRateFields from "@/components/admin/QuoteRateFields";
import QuoteContractLinkDialog from "@/components/admin/QuoteContractLinkDialog";
import { isContractReadyQuote } from "@/lib/contractQuotes";
import { isValidEmail } from "@/lib/validation";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { useAppSelector } from "@/store";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { isConcurrencyConflict } from "@/lib/apiError";
import { formatFileSize } from "@/lib/numberFormat";
import { calculateQuoteTotals, validateQuoteValues } from "@/lib/quoteTotals";
import { isValidVietnameseOverrideReason } from "@/lib/quoteRate";
import { PageLoading, PageError } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
  type QuoteEmailPreviewResponse,
  type QuoteApprovalChainStep,
  type QuoteApprovalPreview,
  type QuoteItemResponse,
  type QuoteMethod,
  type QuoteProjectBoq,
  type QuoteResponse,
  type QuoteStatus,
  type QuoteVersionResponse,
  type QuoteVersionsResponse,
  type UpdateQuoteRequest,
  type MaterialRateRevisionResponse,
  type DeletionImpactResponse,
  type InputPriceOfferResponse,
  type DepartmentWorkPackageResponse,
} from "@/services/adminApi";
import {
  formatDate,
  formatDateTime,
  formatMoney,
  normalizeCurrency,
  useDisplaySettings,
} from "@/lib/displaySettings";

// Matches the server-side body limit in QuoteService.SendToCustomerAsync.
const QUOTE_EMAIL_BODY_MAX = 8000;
// A BOQ preview shows this many lines until the user expands it.
const BOQ_PREVIEW_ROWS = 8;

const STATUS_STYLES: Record<QuoteStatus, string> = {
  Draft: "border-border bg-muted/30 text-foreground",
  PendingApproval: "border-warning/40 bg-warning-soft text-warning-strong",
  Approved: "border-success/30 bg-success-soft text-success-strong",
  SentToCustomer: "border-info/30 bg-info-soft text-info-strong",
  CustomerApproved: "border-success/40 bg-success-soft text-success-strong",
  Rejected: "border-destructive/20 bg-destructive/10 text-destructive",
  Expired: "border-warning/40 bg-warning-soft text-warning-strong",
  Cancelled: "border-border bg-muted text-muted-foreground",
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
 * outside because its authorization is independent of workflow status.
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
    method: q.status === "Draft" && q.grandTotal === 0 ? null : q.method,
    ownerUserId: q.ownerUserId ?? null,
    inputPriceOfferId: q.inputPriceOfferId ?? null,
    areaSqm: q.areaSqm ?? null,
    unitPricePerSqm: q.unitPricePerSqm ?? null,
    materialRateCatalogId: q.materialRateCatalogId ?? null,
    pricingEffectiveDate: q.pricingEffectiveDate ?? null,
    rateOverrideReason: q.rateOverrideReason ?? null,
    packageDescription: q.packageDescription ?? "",
    projectBoqRevisionId: q.projectBoqRevisionId ?? null,
    items: [],
    discountPercent: q.discountPercent,
    vatPercent: q.vatPercent,
    validUntil: q.validUntil,
    expiryReminderDays: q.expiryReminderDays,
    note: q.note ?? "",
  };
}

/** Server-side rounding: match the QuoteService implementation. */
function computePreview(
  method: "UnitCost" | "Boq",
  form: UpdateQuoteRequest,
  boqSubtotal: number,
): { subtotal: number; grandTotal: number } {
  return calculateQuoteTotals(method, { ...form, boqSubtotal });
}

// -------- Component --------

const AdminQuoteDetail = () => {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const { id } = useParams();
  const quoteId = Number(id);
  const navigate = useNavigate();
  const { t, lang } = useI18n();
  // The per-file limit from Settings, so the picker refuses what the server would.
  const quoteUploadLimitMb = useUploadLimit("quotes");
  const { toast } = useToast();
  const display = useDisplaySettings();
  const { has } = usePermissions();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);

  const canManage = has(ADMIN_PERMS.quotesManage);
  const canSeeAllQuotes = has(ADMIN_PERMS.quotesViewAll);
  const canViewWorkPackages = has(ADMIN_PERMS.workPackages);
  const canManageWorkPackages = has(ADMIN_PERMS.workPackagesManage);
  const canApprove = has(ADMIN_PERMS.quotesApprove);
  const canSend = has(ADMIN_PERMS.quotesSend);
  const canOverrideRate = has(ADMIN_PERMS.quotesRateOverride);
  const canManageContracts = has(ADMIN_PERMS.contractsManage);
  const [linkContractOpen, setLinkContractOpen] = useState(false);

  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<UpdateQuoteRequest | null>(null);
  const [approvedInputOffers, setApprovedInputOffers] = useState<InputPriceOfferResponse[]>([]);
  const [inputPricePackages, setInputPricePackages] = useState<DepartmentWorkPackageResponse[]>([]);
  const [financeReviewPackages, setFinanceReviewPackages] = useState<
    DepartmentWorkPackageResponse[]
  >([]);
  const [inputOffersUnavailable, setInputOffersUnavailable] = useState(false);
  const [saving, setSaving] = useState(false);
  const [effectiveRevision, setEffectiveRevision] = useState<MaterialRateRevisionResponse | null>(
    null,
  );
  const [exportingSpreadsheet, setExportingSpreadsheet] = useState(false);
  // The approved project BOQ applied in this edit session but not saved yet.
  const [pendingBoq, setPendingBoq] = useState<QuoteProjectBoq | null>(null);
  // The project's latest approved BOQ: undefined while loading, null when there is none.
  const [projectBoq, setProjectBoq] = useState<QuoteProjectBoq | null | undefined>(undefined);
  const [projectBoqError, setProjectBoqError] = useState<string | null>(null);

  const [versions, setVersions] = useState<QuoteVersionsResponse | null>(null);
  const [versionsLoading, setVersionsLoading] = useState(false);
  const [selectedVersion, setSelectedVersion] = useState<QuoteVersionResponse | null>(null);

  const [documents, setDocuments] = useState<QuoteDocumentResponse[] | null>(null);
  const [documentsLoading, setDocumentsLoading] = useState(false);
  const [documentsError, setDocumentsError] = useState<string | null>(null);
  const [documentFiles, setDocumentFiles] = useState<File[]>([]);
  const [documentLabel, setDocumentLabel] = useState("");
  const [uploadingDocument, setUploadingDocument] = useState(false);
  const [deletingDocumentId, setDeletingDocumentId] = useState<number | null>(null);

  const [workflow, setWorkflow] = useState<WorkflowKind | null>(null);
  const [workflowNote, setWorkflowNote] = useState("");
  const [workflowBusy, setWorkflowBusy] = useState(false);
  const [sendPreview, setSendPreview] = useState<QuoteEmailPreviewResponse | null>(null);
  const [sendDraft, setSendDraft] = useState<QuoteEmailPreviewResponse | null>(null);
  const [htmlBodyOpen, setHtmlBodyOpen] = useState(false);
  // The approval chain from Cấu hình → Luồng duyệt, and the person chosen for each step.
  const [approvalChain, setApprovalChain] = useState<QuoteApprovalChainStep[]>([]);
  const [chosenApprovers, setChosenApprovers] = useState<(number | null)[]>([]);
  const chosenApproverIds = approvalChain.flatMap((step, index) =>
    !step.everyoneApproves && chosenApprovers[index] != null ? [chosenApprovers[index]!] : [],
  );
  const chainReady =
    approvalChain.length > 0 &&
    approvalChain.every((step, index) =>
      step.everyoneApproves ? step.candidates.length > 0 : chosenApprovers[index] != null,
    );
  const [approvalPreview, setApprovalPreview] = useState<QuoteApprovalPreview | null>(null);
  const [approvalError, setApprovalError] = useState("");
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteImpactLoading, setDeleteImpactLoading] = useState(false);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);

  // ---------- data load ----------

  const load = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getQuote(quoteId);
      setQuote(data);
      setForm(toFormState(data));
      setProjectBoqError(null);
      adminApi
        .getQuoteProjectBoq(quoteId)
        .then((response) => setProjectBoq(response.data.latest))
        .catch((reason) => {
          setProjectBoq(null);
          setProjectBoqError(uiError(reason));
        });
      setInputOffersUnavailable(false);
      // Show requests tied to this quote. Keep legacy opportunity-level offers
      // selectable so existing quotes can still be priced after this change.
      if (data.operationalProjectId && canViewWorkPackages) {
        try {
          const packages = [];
          let page = 1;
          while (true) {
            const response = await adminApi.listWorkPackages({
              purpose: "ProcurementInputPricing",
              operationalProjectId: data.operationalProjectId,
              page,
              pageSize: 100,
            });
            packages.push(...response.data.items);
            if (page * response.data.pageSize >= response.data.totalCount) break;
            page += 1;
          }
          const linkedPackages = packages.filter(
            (item) => item.sourceQuoteId === data.id && item.status !== "Cancelled",
          );
          setInputPricePackages(linkedPackages);
          setApprovedInputOffers(
            packages
              .filter(
                (item) =>
                  item.sourceQuoteId === data.id ||
                  (item.sourceQuoteId == null && item.sourceOpportunityId === data.opportunityId),
              )
              .flatMap((item) => item.inputPriceOffers)
              .filter(
                (offer) => offer.status === "Approved" && new Date(offer.validUntil) >= new Date(),
              ),
          );
        } catch {
          setInputPricePackages([]);
          setApprovedInputOffers([]);
          setInputOffersUnavailable(true);
        }
      } else {
        setInputPricePackages([]);
        setApprovedInputOffers([]);
        setInputOffersUnavailable(!canViewWorkPackages);
      }
      if (data.status === "Approved" && data.operationalProjectId && canViewWorkPackages) {
        const linkedPackages: DepartmentWorkPackageResponse[] = [];
        let page = 1;
        while (true) {
          const response = await adminApi.listWorkPackages({
            purpose: "FinanceCommercialReview",
            sourceQuoteId: data.id,
            page,
            pageSize: 100,
          });
          linkedPackages.push(...response.data.items);
          if (page * response.data.pageSize >= response.data.totalCount) break;
          page += 1;
        }
        setFinanceReviewPackages(linkedPackages);
      } else {
        setFinanceReviewPackages([]);
      }
    } catch (err) {
      setError(uiError(err));
    } finally {
      setLoading(false);
    }
  }, [canViewWorkPackages, quoteId, uiError]);

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
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setVersionsLoading(false);
    }
  }, [quoteId, toast, t, uiError]);

  const loadDocuments = useCallback(async () => {
    if (!Number.isFinite(quoteId)) return;
    setDocumentsLoading(true);
    setDocumentsError(null);
    try {
      const { data } = await adminApi.listQuoteDocuments(quoteId);
      setDocuments(data);
    } catch (err) {
      setDocumentsError(uiError(err));
    } finally {
      setDocumentsLoading(false);
    }
  }, [quoteId, uiError]);

  // ---------- BOQ source ----------

  const applyProjectBoq = (revision: QuoteProjectBoq) => {
    if (!form) return;
    setPendingBoq(revision);
    setForm({ ...form, projectBoqRevisionId: revision.revisionId });
  };

  const boqSubtotal =
    pendingBoq && form?.projectBoqRevisionId === pendingBoq.revisionId
      ? pendingBoq.total
      : (quote?.subtotal ?? 0);

  // ---------- save + workflow ----------

  const handleSave = async () => {
    if (!quote || !form) return;
    if (!form.method) {
      toast({
        title: t("common.error"),
        description: t("quotes.validation.methodRequired"),
        variant: "destructive",
      });
      return;
    }
    // The approved project BOQ is itself approved input pricing for a BOQ quote.
    if (
      quote.grandTotal === 0 &&
      !form.inputPriceOfferId &&
      !(form.method === "Boq" && form.projectBoqRevisionId)
    ) {
      toast({
        title: t("common.error"),
        description: t("quotes.validation.inputPriceRequired"),
        variant: "destructive",
      });
      return;
    }
    if (form.method === "Boq" && !form.projectBoqRevisionId) {
      toast({
        title: t("common.error"),
        description: t("quotes.projectBoq.required"),
        variant: "destructive",
      });
      return;
    }
    const validationIssue = validateQuoteValues(form.method, { ...form, boqSubtotal });
    if (validationIssue) {
      toast({
        title: t("common.error"),
        description: t(`quotes.validation.${validationIssue}`),
        variant: "destructive",
      });
      return;
    }
    if (form.method === "UnitCost") {
      if (!form.materialRateCatalogId || !form.pricingEffectiveDate) {
        toast({
          title: t("common.error"),
          description: t("quotes.validation.rateSelectionRequired"),
          variant: "destructive",
        });
        return;
      }
      if (!effectiveRevision) {
        toast({
          title: t("common.error"),
          description: t("quotes.validation.noEffectiveRate"),
          variant: "destructive",
        });
        return;
      }
      const isOverride = form.unitPricePerSqm !== Number(effectiveRevision.totalRatePerSqm);
      if (isOverride && !canOverrideRate) {
        toast({
          title: t("common.error"),
          description: t("quotes.validation.overridePermission"),
          variant: "destructive",
        });
        return;
      }
      if (isOverride && !isValidVietnameseOverrideReason(form.rateOverrideReason)) {
        toast({
          title: t("common.error"),
          description: t("quotes.validation.overrideReason"),
          variant: "destructive",
        });
        return;
      }
    }
    setSaving(true);
    try {
      const { data } = await adminApi.updateQuote(quote.id, {
        ...form,
        // A BOQ quote copies its lines from the project BOQ on the server.
        ...(form.method === "Boq"
          ? { materialRateCatalogId: null, pricingEffectiveDate: null, items: [] }
          : { projectBoqRevisionId: null }),
        packageDescription: form.packageDescription?.trim() || undefined,
        note: form.note?.trim() || undefined,
        validUntil: form.validUntil || null,
      });
      setQuote(data);
      setForm(toFormState(data));
      setPendingBoq(null);
      setEditing(false);
      toast({ title: t("quotes.updated") });
      // If we're viewing the Versions tab, refresh it too.
      if (versions) void loadVersions();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
      if (isConcurrencyConflict(err)) await load();
    } finally {
      setSaving(false);
    }
  };

  const downloadSpreadsheet = async () => {
    if (!quote) return;
    setExportingSpreadsheet(true);
    try {
      const { data } = await adminApi.exportQuoteSpreadsheet(quote.id, lang);
      const url = URL.createObjectURL(data);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${quote.code}.xlsx`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setExportingSpreadsheet(false);
    }
  };

  const runWorkflow = async (kind?: WorkflowKind) => {
    const targetWorkflow = kind ?? workflow;
    if (!targetWorkflow || !quote) return;
    setWorkflowBusy(true);
    try {
      const body = { note: workflowNote.trim() || undefined, rowVersion: quote.rowVersion };
      const fn: Record<WorkflowKind, () => Promise<{ data: QuoteResponse }>> = {
        submit: () =>
          adminApi.submitQuote(quote.id, {
            ...body,
            approverUserIds: chosenApproverIds,
            previewToken: approvalPreview!.previewToken,
          }),
        approve: () => adminApi.approveQuote(quote.id, body),
        rejectInternal: () => adminApi.rejectQuoteInternal(quote.id, body),
        send: () =>
          adminApi.sendQuoteToCustomer(quote.id, {
            ...body,
            toEmail: sendDraft?.toEmail ?? "",
            subject: sendDraft?.subject ?? "",
            body: sendDraft?.body ?? "",
            languageCode: lang,
          }),
        customerApprove: () => adminApi.markQuoteCustomerApproved(quote.id, body),
        customerReject: () => adminApi.markQuoteCustomerRejected(quote.id, body),
        cancel: () => adminApi.cancelQuote(quote.id, body),
      };
      const { data } = await fn[targetWorkflow]();
      setQuote(data);
      setForm(toFormState(data));
      setWorkflow(null);
      setSendPreview(null);
      setSendDraft(null);
      setApprovalPreview(null);
      setWorkflowNote("");
      const done: Partial<Record<WorkflowKind, string>> = {
        submit: "quotes.done.submit",
        approve: "quotes.done.approve",
        rejectInternal: "quotes.done.rejectInternal",
      };
      // Approving a step before the last passes the quote to the next approver.
      const doneKey =
        targetWorkflow === "approve" && data.status === "PendingApproval"
          ? "quotes.done.approveStep"
          : done[targetWorkflow];
      toast({
        title: doneKey
          ? t(doneKey, { code: data.code, name: data.approvalApproverName ?? "" })
          : t(`quotes.action.${targetWorkflow}`),
      });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
      if (isConcurrencyConflict(err)) await load();
    } finally {
      setWorkflowBusy(false);
    }
  };

  // Actions that can run directly without a dialog (note is optional). The internal
  // decision is confirmed in the dialog, and a rejection says why.
  const directActions: WorkflowKind[] = ["customerApprove"];

  const handleWorkflowClick = async (k: WorkflowKind) => {
    if (k === "submit" && quote) {
      setWorkflowBusy(true);
      setApprovalError("");
      setApprovalPreview(null);
      try {
        const { data } = await adminApi.getQuoteApprovalChain(quote.id);
        setApprovalChain(data);
        setChosenApprovers(data.map(() => null));
        setWorkflowNote("");
        setWorkflow(k);
      } catch (err) {
        toast({
          title: t("common.error"),
          description: uiError(err),
          variant: "destructive",
        });
      } finally {
        setWorkflowBusy(false);
      }
      return;
    }
    if (k === "send" && quote) {
      setWorkflowBusy(true);
      try {
        const { data } = await adminApi.getQuoteEmailPreview(quote.id);
        setWorkflowNote("");
        setSendPreview(data);
        setSendDraft(data);
        setHtmlBodyOpen(false);
        setWorkflow(k);
      } catch (err) {
        toast({
          title: t("common.error"),
          description: uiError(err),
          variant: "destructive",
        });
      } finally {
        setWorkflowBusy(false);
      }
      return;
    }
    if (directActions.includes(k)) {
      void runWorkflow(k);
    } else {
      setWorkflowNote("");
      setWorkflow(k);
    }
  };

  // Sales takes back a waiting quote whose approver can no longer decide it.
  const withdrawApproval = async () => {
    if (
      !quote ||
      !(await confirmAction(t("quotes.approval.withdrawConfirm"), {
        destructive: false,
        confirmLabel: t("quotes.approval.withdrawAction"),
      }))
    )
      return;
    setWorkflowBusy(true);
    try {
      const { data } = await adminApi.withdrawQuoteApproval(quote.id, {
        rowVersion: quote.rowVersion,
      });
      setQuote(data);
      setForm(toFormState(data));
      toast({ title: t("quotes.done.withdrawApproval", { code: data.code }) });
    } catch (err) {
      toast({ title: t("common.error"), description: uiError(err), variant: "destructive" });
      if (isConcurrencyConflict(err)) await load();
    } finally {
      setWorkflowBusy(false);
    }
  };

  const previewApprovalEmail = async () => {
    if (!quote || !chainReady) return;
    setWorkflowBusy(true);
    setApprovalError("");
    setApprovalPreview(null);
    try {
      const { data } = await adminApi.previewQuoteApprovalEmail(quote.id, {
        approverUserIds: chosenApproverIds,
        rowVersion: quote.rowVersion,
      });
      setApprovalPreview(data);
    } catch (err) {
      setApprovalError(uiError(err));
    } finally {
      setWorkflowBusy(false);
    }
  };

  const handleDelete = async () => {
    if (!quote) return;
    setDeleteOpen(true);
    setDeleteImpact(null);
    setDeleteError(null);
    setDeleteImpactLoading(true);
    try {
      const response = await adminApi.getQuoteDeletionImpact(quote.id);
      setDeleteImpact(response.data);
    } catch (err) {
      setDeleteError(uiError(err));
    } finally {
      setDeleteImpactLoading(false);
    }
  };

  const confirmDelete = async (confirmation: string) => {
    if (!quote || !deleteImpact) return null;
    setDeleteBusy(true);
    setDeleteError(null);
    try {
      const response = await adminApi.deleteQuote(quote.id, {
        planToken: deleteImpact.planToken,
        confirmation,
        rowVersion: quote.rowVersion,
      });
      return response.status === 204 ? null : response.data;
    } catch (err) {
      setDeleteError(uiError(err));
      throw err;
    } finally {
      setDeleteBusy(false);
    }
  };

  const completeDelete = () => {
    toast({ title: t("quotes.deleted") });
    navigate("/admin/quotes");
  };

  const handleUploadDocument = async () => {
    if (!quote || documentFiles.length === 0) return;
    setUploadingDocument(true);
    const failed: string[] = [];
    let uploaded = 0;
    try {
      for (const file of documentFiles) {
        try {
          await adminApi.uploadQuoteDocument(quote.id, file, documentLabel);
          uploaded += 1;
        } catch (err) {
          failed.push(`${file.name}: ${uiError(err)}`);
        }
      }
    } finally {
      setDocumentFiles([]);
      setUploadingDocument(false);
    }
    if (uploaded > 0) {
      setDocumentLabel("");
      await loadDocuments();
      toast({ title: t("quotes.document.uploaded") });
    }
    if (failed.length > 0) {
      toast({
        title: t("common.error"),
        description: failed.join(" · "),
        variant: "destructive",
      });
    }
  };

  const handleDeleteDocument = async (documentId: number) => {
    if (!quote || !(await confirmAction(t("quotes.document.deleteConfirm")))) return;
    setDeletingDocumentId(documentId);
    try {
      await adminApi.deleteQuoteDocument(quote.id, documentId);
      setDocuments((current) => current?.filter((document) => document.id !== documentId) ?? []);
      toast({ title: t("quotes.document.deleted") });
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setDeletingDocumentId(null);
    }
  };

  const preview = useMemo(() => {
    if (!quote || !form?.method) return null;
    return computePreview(form.method, form, boqSubtotal);
  }, [quote, form, boqSubtotal]);

  // -------- render --------

  if (!Number.isFinite(quoteId))
    return (
      <AdminLayout>
        <PageError message={t("common.notFound")} />
      </AdminLayout>
    );
  if (loading)
    return (
      <AdminLayout>
        <PageLoading />
      </AdminLayout>
    );
  if (error)
    return (
      <AdminLayout>
        <PageError message={error} onRetry={() => void load()} />
      </AdminLayout>
    );
  if (!quote || !form) return null;

  // Amounts in the edit form come verbatim from the selected catalog or project BOQ, so
  // the form's currency follows that source until the save adopts it.
  const appliedBoq =
    pendingBoq && form.projectBoqRevisionId === pendingBoq.revisionId ? pendingBoq : null;
  const previewCurrency =
    (editing
      ? form.method === "Boq"
        ? appliedBoq?.currency
        : effectiveRevision?.currency
      : null) ?? quote.currency;

  const workflowKinds = WORKFLOW_BY_STATUS[quote.status];
  const awaitingInputPrice = quote.status === "Draft" && quote.grandTotal === 0;
  const isTerminal =
    quote.status === "CustomerApproved" ||
    quote.status === "Rejected" ||
    quote.status === "Cancelled";
  // Sales can price the quote once an approved input price or project BOQ exists.
  const priceSourceReady = approvedInputOffers.length > 0 || projectBoq != null;
  // Who decides now, from the chain fixed when the quote was sent. A quote sent before
  // chains has one approver, the chosen Procurement Manager.
  const approvalSteps = quote.approvalSteps ?? [];
  const currentStepIndex = approvalSteps.findIndex((step) => step.state === "Current");
  const currentStep = approvalSteps[currentStepIndex];
  const nextApprover = currentStep ? approvalSteps[currentStepIndex + 1] : undefined;
  // A later approver who can no longer approve would stop the quote when it reaches them.
  const laterApproverUnavailable = approvalSteps.find(
    (step) => step.state === "Waiting" && step.available === false,
  );
  const currentApproval =
    quote.status !== "PendingApproval"
      ? undefined
      : currentStep
        ? {
            name: currentStep.approverName,
            role: currentStep.roleLabelKey ? t(currentStep.roleLabelKey) : currentStep.roleCode,
            current: currentStepIndex + 1,
            total: approvalSteps.length,
            available: quote.approvalApproverAvailable !== false,
          }
        : {
            name: quote.approvalApproverName ?? "—",
            role: t("rbac.role.PROCUREMENT_MANAGER.label"),
            current: 1,
            total: 1,
            available: quote.approvalApproverAvailable !== false,
          };
  // Taking a quote back is a change, so it stays with its owner (or someone who sees every
  // quote), even though everyone in the chain can read it.
  const canWithdraw = canManage && (canSeeAllQuotes || quote.ownerUserId === currentUserId);
  // Approving now would stop: the next person can no longer approve, so say so up front.
  const nextApproverBlocked = nextApprover?.available === false;
  const showEditToggle =
    canManage && !editing && !isTerminal && (!awaitingInputPrice || priceSourceReady);
  const primaryInputPackage =
    inputPricePackages.find((item) => item.status !== "Completed") ?? inputPricePackages[0];
  const primaryFinancePackage = selectQuoteFinanceReview(financeReviewPackages, quote.version);

  // A quote the customer or an approver has signed off on is the point where a
  // contract can be raised from it.
  const canRaiseContract = canManageContracts && !editing && isContractReadyQuote(quote);
  const canLinkContract = canRaiseContract && quote.customerId != null;

  // Build with URLSearchParams so absent optional references do not reach the
  // contract form as the string "undefined".
  const goToContractForm = () => {
    const params = new URLSearchParams({ fromQuote: String(quote.id) });
    if (quote.opportunityId) params.set("opportunityId", String(quote.opportunityId));
    if (quote.customerId) params.set("customerId", String(quote.customerId));
    if (quote.operationalProjectId)
      params.set("operationalProjectId", String(quote.operationalProjectId));
    if (quote.grandTotal > 0) params.set("value", String(quote.grandTotal));
    navigate(`/admin/contracts?${params.toString()}`);
  };
  const canDelete = canManage;

  return (
    <AdminLayout>
      {/* ---------- Header ---------- */}
      <div className="mb-3 flex min-w-0 flex-col gap-4" data-testid="quote-detail-header">
        <div className="min-w-0 w-full">
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
            <span className="min-w-0 max-w-full break-all">{quote.code}</span>
            <Badge
              variant="outline"
              className={cn("max-w-full whitespace-normal text-xs", STATUS_STYLES[quote.status])}
            >
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

        <div className="grid w-full min-w-0 gap-2 sm:flex sm:flex-wrap [&_button]:h-auto [&_button]:min-h-10 [&_button]:whitespace-normal [&_svg]:shrink-0">
          {!editing && canManageWorkPackages && quote.operationalProjectId && (
            <>
              {quote.status === "Draft" && (
                <Button variant="outline" asChild>
                  <Link
                    to={
                      primaryInputPackage
                        ? `/admin/work-packages/${primaryInputPackage.id}`
                        : `/admin/work-packages?create=1&purpose=ProcurementInputPricing&projectId=${quote.operationalProjectId}&opportunityId=${quote.opportunityId}&quoteId=${quote.id}`
                    }
                  >
                    {primaryInputPackage ? (
                      <Link2 className="mr-1.5 h-4 w-4" />
                    ) : (
                      <Plus className="mr-1.5 h-4 w-4" />
                    )}
                    {t(
                      primaryInputPackage
                        ? "quotes.action.viewInputPriceRequest"
                        : "quotes.action.requestInputPrice",
                    )}
                  </Link>
                </Button>
              )}
              {quote.status === "Approved" && (
                <Button variant="outline" asChild>
                  <Link
                    to={
                      primaryFinancePackage
                        ? `/admin/work-packages/${primaryFinancePackage.id}`
                        : `/admin/work-packages?create=1&purpose=FinanceCommercialReview&projectId=${quote.operationalProjectId}&opportunityId=${quote.opportunityId}&quoteId=${quote.id}`
                    }
                  >
                    {primaryFinancePackage ? (
                      <Link2 className="mr-1.5 h-4 w-4" />
                    ) : (
                      <CheckCheck className="mr-1.5 h-4 w-4" />
                    )}
                    {t(
                      primaryFinancePackage
                        ? "quotes.action.viewFinanceReview"
                        : "quotes.action.requestFinanceReview",
                    )}
                  </Link>
                </Button>
              )}
            </>
          )}
          {!editing && !awaitingInputPrice && (
            <Button
              variant="outline"
              data-testid="quote-download-excel"
              onClick={() => void downloadSpreadsheet()}
              disabled={exportingSpreadsheet}
            >
              {exportingSpreadsheet ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Download className="mr-1.5 h-4 w-4" />
              )}
              {t("quotes.action.downloadExcel")}
            </Button>
          )}
          {showEditToggle && (
            <Button variant="outline" data-testid="quote-edit" onClick={() => setEditing(true)}>
              <Pencil className="mr-1.5 h-4 w-4" />
              {t(awaitingInputPrice ? "quotes.action.prepareOutput" : "common.edit")}
            </Button>
          )}
          {editing && (
            <>
              <Button
                variant="outline"
                onClick={() => {
                  setForm(toFormState(quote));
                  setPendingBoq(null);
                  setEditing(false);
                }}
              >
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
            workflowKinds
              .filter((kind) => kind !== "submit" || !awaitingInputPrice)
              .map((k) => (
                <Button
                  key={k}
                  variant={
                    k === "rejectInternal" || k === "customerReject" || k === "cancel"
                      ? "outline"
                      : "default"
                  }
                  onClick={() => handleWorkflowClick(k)}
                  disabled={
                    workflowBusy ||
                    (k === "approve" || k === "rejectInternal"
                      ? !canApprove || quote.approvalApproverUserId !== currentUserId
                      : k === "send"
                        ? !canSend || quote.isExpired
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
          {canLinkContract && (
            <Button
              variant="outline"
              data-testid="quote-link-contract"
              onClick={() => setLinkContractOpen(true)}
            >
              <Link2 className="mr-1.5 h-4 w-4" />
              {t("quoteLink.fromQuote.action")}
            </Button>
          )}
          {!editing && canDelete && (
            <Button
              variant="outline"
              className="text-destructive hover:text-destructive"
              onClick={() => void handleDelete()}
            >
              <Trash2 className="mr-1.5 h-4 w-4" />
              {t("quotes.action.delete")}
            </Button>
          )}
        </div>
      </div>

      <QuoteProgressPanel
        currentApproval={currentApproval}
        status={quote.status}
        grandTotal={quote.grandTotal}
        priceSourceReady={priceSourceReady}
        financeApproved={
          primaryFinancePackage?.financeReview?.status === "Approved" &&
          primaryFinancePackage.financeReview.reviewedQuoteVersion === quote.version
        }
      />

      {quote.approvalApproverUserId && (
        <section
          className="mb-4 rounded-lg border bg-card p-4 text-sm"
          data-testid="quote-approval-email-status"
        >
          {(quote.approvalSteps?.length ?? 0) <= 1 && (
            <p className="font-medium">
              {t("quotes.approval.recipient")}: {quote.approvalApproverName ?? "—"}
            </p>
          )}
          {(quote.approvalSteps?.length ?? 0) > 1 && (
            <ol className="space-y-2" data-testid="quote-approval-steps">
              {quote.approvalSteps!.map((step, index) => (
                <li key={`${step.order}-${index}`} className="flex gap-2">
                  <span
                    className={
                      step.state === "Approved"
                        ? "mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-success-strong text-xs font-semibold text-white"
                        : "mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-xs font-semibold tabular-nums"
                    }
                    aria-hidden
                  >
                    {step.state === "Approved" ? <Check className="h-3 w-3" /> : index + 1}
                  </span>
                  <span className="min-w-0">
                    <span className="block font-medium">
                      {step.approverName}
                      <span className="font-normal text-muted-foreground">
                        {" "}
                        · {step.roleLabelKey ? t(step.roleLabelKey) : step.roleCode}
                      </span>
                    </span>
                    <span
                      className={
                        step.state === "Approved"
                          ? "block text-xs text-success-strong"
                          : step.state === "Current"
                            ? "block text-xs font-medium text-foreground"
                            : "block text-xs text-muted-foreground"
                      }
                    >
                      {t(`quotes.approval.stepState.${step.state}`)}
                      {step.state === "Approved" && step.decidedAt
                        ? ` ${formatDateTime(step.decidedAt)}`
                        : step.state === "Current" && step.dueAt
                          ? ` · ${t("quotes.approval.dueAt", { at: formatDateTime(step.dueAt) })}`
                          : ""}
                    </span>
                    {step.state !== "Approved" && step.available === false && (
                      <span className="block text-xs font-medium text-destructive">
                        {t("quotes.approval.stepUnavailable")}
                      </span>
                    )}
                  </span>
                </li>
              ))}
            </ol>
          )}
          <p className="mt-1 text-muted-foreground">
            {t(`quotes.approval.status.${quote.approvalEmailStatus ?? "Pending"}`)}
          </p>
          {quote.status === "PendingApproval" &&
            quote.approvalApproverAvailable !== false &&
            quote.approvalApproverUserId !== currentUserId && (
              <p className="mt-1 text-muted-foreground">
                {t("quotes.approval.onlyChosen", { name: quote.approvalApproverName ?? "—" })}
              </p>
            )}
          {quote.status === "PendingApproval" && laterApproverUnavailable && (
            <p role="alert" className="mt-1 text-destructive">
              {t("quotes.approval.laterUnavailable", {
                name: laterApproverUnavailable.approverName,
              })}
            </p>
          )}
          {quote.status === "PendingApproval" && quote.approvalApproverAvailable === false && (
            <div
              role="alert"
              className="mt-2 space-y-2 rounded-md border border-destructive/40 p-3"
            >
              <p className="text-destructive">
                {t("quotes.approval.currentUnavailable", {
                  name: quote.approvalApproverName ?? "—",
                })}
              </p>
              {canWithdraw ? (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={workflowBusy}
                  onClick={() => void withdrawApproval()}
                  data-testid="quote-withdraw-approval"
                >
                  {t("quotes.approval.withdraw")}
                </Button>
              ) : (
                <p className="text-muted-foreground">
                  {t("quotes.approval.withdrawByOwner", { owner: quote.ownerName ?? "—" })}
                </p>
              )}
            </div>
          )}
          {quote.approvalEmailStatus === "NeedsReview" && (
            <p className="mt-1 text-destructive">{t("quotes.approval.needsReviewAction")}</p>
          )}
          {(quote.approvalEmailStatus === "Pending" || quote.approvalEmailStatus === "Sending") && (
            <Button variant="outline" size="sm" className="mt-2" onClick={() => void load()}>
              {t("common.refresh")}
            </Button>
          )}
        </section>
      )}

      {awaitingInputPrice && !editing && (
        <div className="mb-4 rounded-lg border border-warning/40 bg-warning-soft p-4 text-sm text-warning-strong">
          {t(
            inputOffersUnavailable
              ? "quotes.inputPrice.unavailable"
              : approvedInputOffers.length > 0
                ? "quotes.inputPrice.readyForOutput"
                : projectBoq != null
                  ? "quotes.inputPrice.readyFromProjectBoq"
                  : "quotes.inputPrice.awaitingApproval",
          )}
        </div>
      )}

      {inputPricePackages.length > 0 && (
        <section
          className="mb-4 overflow-hidden rounded-xl border bg-card"
          data-testid="quote-input-requests"
        >
          <div className="border-b p-4">
            <h2 className="font-semibold">{t("quotes.inputPrice.requests")}</h2>
            <p className="text-sm text-muted-foreground">{t("quotes.inputPrice.requestHelp")}</p>
          </div>
          <div className="divide-y">
            {inputPricePackages.map((item) => (
              <div key={item.id} className="space-y-3 p-4" data-testid="quote-input-request">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <Link
                    className="min-w-0 font-medium text-primary hover:underline"
                    to={`/admin/work-packages/${item.id}`}
                  >
                    {item.code} · {item.name}
                  </Link>
                  <Badge variant="outline">{t(`workPackages.status.${item.status}`)}</Badge>
                </div>
                {item.inputPriceOffers.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    {t("workPackages.inputPrice.empty")}
                  </p>
                ) : (
                  <>
                    <div className="flex flex-wrap gap-2 text-xs">
                      {(["Draft", "Submitted", "Approved", "Rejected"] as const).map((status) => {
                        const count = item.inputPriceOffers.filter(
                          (offer) => offer.status === status,
                        ).length;
                        return count > 0 ? (
                          <Badge key={status} variant="secondary">
                            {t(`workPackages.businessStatus.${status}`)}: {count}
                          </Badge>
                        ) : null;
                      })}
                    </div>
                    <div className="divide-y rounded-lg border text-sm">
                      {item.inputPriceOffers.map((offer) => (
                        <div
                          key={offer.id}
                          className="flex flex-col gap-1 p-3 sm:flex-row sm:items-center sm:justify-between sm:gap-3"
                        >
                          <span className="min-w-0 break-words">
                            {offer.vendorName} · {offer.supplierReference}
                          </span>
                          <Badge className="w-fit shrink-0" variant="outline">
                            {t(`workPackages.businessStatus.${offer.status}`)}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>
        </section>
      )}

      {/* ---------- Meta strip ---------- */}
      {/* At-a-glance summary so the key numbers stay visible without scrolling
          to the side panel on narrow screens. */}
      <dl className="mb-4 grid grid-cols-2 gap-2 rounded-lg border bg-card p-3 text-sm sm:grid-cols-4">
        <div className="min-w-0">
          <dt className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.grandTotal")}
          </dt>
          <dd className="truncate text-base font-semibold">
            {formatMoney(quote.grandTotal, quote.currency)}
          </dd>
        </div>
        <div className="min-w-0">
          <dt className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.validUntil")}
          </dt>
          <dd className="flex items-center gap-1 truncate">
            <Calendar className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            <span className="truncate">{formatDate(quote.validUntil)}</span>
            {quote.isExpired && (
              <Badge
                variant="outline"
                className="ml-1 border-destructive/20 bg-destructive/10 text-xs text-destructive"
              >
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
          <dd className="truncate">
            {awaitingInputPrice
              ? t("quotes.method.notSelected")
              : t(`quotes.method.${quote.method}`)}
          </dd>
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
          <TabsTrigger value="workflow">{t("quotes.tab.workflow")}</TabsTrigger>
        </TabsList>

        {/* ---------- CONTENT ---------- */}
        <TabsContent value="content" className="mt-4">
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px] xl:gap-6">
            <div className="min-w-0 space-y-4">
              <div className="rounded-lg border bg-card p-4">
                <FormField label={t("quotes.field.inputPriceOffer")}>
                  {editing ? (
                    <Select
                      value={form.inputPriceOfferId ? String(form.inputPriceOfferId) : "none"}
                      onValueChange={(value) =>
                        setForm({
                          ...form,
                          inputPriceOfferId: value === "none" ? null : Number(value),
                        })
                      }
                    >
                      <SelectTrigger>
                        <SelectValue placeholder={t("quotes.inputPrice.none")} />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">{t("quotes.inputPrice.none")}</SelectItem>
                        {approvedInputOffers.map((offer) => (
                          <SelectItem key={offer.id} value={String(offer.id)}>
                            {offer.vendorName} · {offer.supplierReference} ·{" "}
                            {formatMoney(offer.grandTotal, offer.currency)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    <p className="text-sm font-medium">
                      {quote.inputPriceOfferId
                        ? `${quote.inputPriceVendorName ?? "—"} · ${quote.inputPriceSupplierReference ?? "—"}`
                        : t("quotes.inputPrice.none")}
                    </p>
                  )}
                </FormField>
                <p className="mt-2 text-xs text-muted-foreground">{t("quotes.inputPrice.hint")}</p>
              </div>
              {editing && awaitingInputPrice && (
                <div className="rounded-lg border bg-card p-4">
                  <FormField label={t("quotes.field.method")}>
                    <Select
                      value={form.method ?? undefined}
                      onValueChange={(value) =>
                        setForm({
                          ...form,
                          method: value as QuoteMethod,
                          areaSqm: null,
                          unitPricePerSqm: null,
                          materialRateCatalogId: null,
                          pricingEffectiveDate: null,
                          rateOverrideReason: null,
                          packageDescription: "",
                          projectBoqRevisionId: null,
                          items: [],
                        })
                      }
                    >
                      <SelectTrigger data-testid="quote-output-method">
                        <SelectValue placeholder={t("quotes.method.notSelected")} />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="UnitCost">{t("quotes.method.UnitCost")}</SelectItem>
                        <SelectItem value="Boq">{t("quotes.method.Boq")}</SelectItem>
                      </SelectContent>
                    </Select>
                  </FormField>
                </div>
              )}
              {awaitingInputPrice && !form.method ? null : form.method === "UnitCost" ? (
                <div className="space-y-3 rounded-lg border bg-card p-4">
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <FormField label={t("quotes.field.areaSqm")}>
                      <Input
                        inputMode="decimal"
                        value={form.areaSqm ?? ""}
                        disabled={!editing}
                        onChange={(e) =>
                          setForm({
                            ...form,
                            areaSqm: e.target.value ? Number(e.target.value) : null,
                          })
                        }
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
                      currency={quote.currency}
                      canOverride={canOverrideRate}
                      onEffectiveRevisionChange={setEffectiveRevision}
                      onChange={(patch) =>
                        setForm((current) => (current ? { ...current, ...patch } : current))
                      }
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
                <div className="space-y-4">
                  <QuoteProjectBoqSource
                    latest={projectBoq}
                    error={projectBoqError}
                    projectId={quote.operationalProjectId}
                    currentRevisionNumber={quote.projectBoqRevisionNumber}
                    selectedRevisionId={form.projectBoqRevisionId}
                    savedRevisionId={quote.projectBoqRevisionId}
                    editing={editing}
                    onApply={applyProjectBoq}
                  />
                  {!editing && quote.materialRateCatalogId ? (
                    <RateProvenance value={quote} t={t} />
                  ) : null}
                  <BoqTable
                    items={
                      pendingBoq && form.projectBoqRevisionId === pendingBoq.revisionId
                        ? null
                        : quote.items
                    }
                    t={t}
                    currency={previewCurrency}
                  />
                </div>
              )}

              <div className="grid grid-cols-2 gap-3 rounded-lg border bg-card p-4 sm:grid-cols-4">
                <FormField label={t("quotes.field.discountPercent")}>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={form.discountPercent}
                    disabled={!editing}
                    onChange={(e) => setForm({ ...form, discountPercent: Number(e.target.value) })}
                  />
                </FormField>
                <FormField label={t("quotes.field.vatPercent")}>
                  <Input
                    type="number"
                    min={0}
                    max={100}
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
                    onChange={(e) =>
                      setForm({
                        ...form,
                        validUntil: e.target.value ? `${e.target.value}T23:59:59Z` : null,
                      })
                    }
                  />
                </FormField>
                <ReminderDaysField
                  id="quote-expiry-reminder-days"
                  labelClassName={FORM_FIELD_LABEL}
                  value={form.expiryReminderDays ?? DEFAULT_REMINDER_DAYS}
                  disabled={!editing}
                  onChange={(days) => setForm({ ...form, expiryReminderDays: days })}
                />
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
                <span className="font-medium">
                  {formatMoney(preview?.subtotal ?? quote.subtotal, previewCurrency)}
                </span>
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
                <span className="text-lg font-bold text-primary">
                  {formatMoney(preview?.grandTotal ?? quote.grandTotal, previewCurrency)}
                </span>
              </div>
              <p className="break-words text-xs italic text-muted-foreground">
                <span className="mr-1 font-medium not-italic">
                  {t("quotes.field.grandTotalInWords")}
                  {display.currency !== normalizeCurrency(quote.currency)
                    ? ` (${normalizeCurrency(quote.currency)})`
                    : ""}
                  :
                </span>
                {quote.grandTotalInWords}
              </p>
              {editing && preview && preview.grandTotal !== quote.grandTotal && (
                <p className="mt-2 rounded bg-warning-soft p-2 text-xs text-warning-strong">
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
                <MultiFilePicker
                  id="quote-document-file"
                  files={documentFiles}
                  onChange={setDocumentFiles}
                  accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
                  maxSizeMb={quoteUploadLimitMb}
                  hint={t("quotes.document.fileTypes")}
                  disabled={uploadingDocument}
                  testId="quote-document-file"
                />
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
                disabled={documentFiles.length === 0 || uploadingDocument}
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
                <li
                  key={document.id}
                  className="flex min-w-0 items-start gap-3 rounded-lg border bg-card p-3"
                >
                  <div className="rounded-md bg-muted p-2">
                    <FileText className="h-5 w-5 text-muted-foreground" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium" title={document.originalFileName}>
                      {document.label || document.originalFileName}
                    </p>
                    {document.label && (
                      <p
                        className="truncate text-xs text-muted-foreground"
                        title={document.originalFileName}
                      >
                        {document.originalFileName}
                      </p>
                    )}
                    <p className="text-xs text-muted-foreground">
                      {formatFileSize(document.fileSize)} · {formatDateTime(document.createdAt)}
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
                      fetchFile={async () =>
                        (await adminApi.getQuoteDocumentContent(quote.id, document.id)).data
                      }
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
                        <div className="font-medium">{formatMoney(v.grandTotal, v.currency)}</div>
                        <div className="text-xs text-muted-foreground">
                          {formatDateTime(v.capturedAt)}
                        </div>
                      </div>
                    </div>
                    <div className="mt-1 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                      <span>
                        {t(`quotes.method.${v.method}`)} · {t("quotes.field.subtotal")}{" "}
                        {formatMoney(v.subtotal, v.currency)} · VAT {v.vatPercent}%
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
                      {formatDateTime(l.createdAt)}
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
        <DialogContent
          className={
            workflow === "send" || workflow === "submit"
              ? "sm:max-w-2xl max-h-[90vh] overflow-y-auto"
              : "sm:max-w-md"
          }
        >
          <DialogHeader>
            <DialogTitle>{workflow && t(`quotes.action.${workflow}`)}</DialogTitle>
            <DialogDescription>
              {workflow === "approve" || workflow === "rejectInternal"
                ? workflow === "approve" && nextApproverBlocked
                  ? t("quotes.decide.approveStepBlocked", {
                      current: currentApproval!.current + 1,
                      total: currentApproval!.total,
                      name: nextApprover!.approverName,
                    })
                  : workflow === "approve" && nextApprover
                    ? t("quotes.decide.approveStepHint", {
                        code: quote.code,
                        current: currentApproval!.current,
                        total: currentApproval!.total,
                        name: nextApprover.approverName,
                      })
                    : t(`quotes.decide.${workflow}Hint`, { code: quote.code })
                : t(
                    workflow === "submit"
                      ? "quotes.approval.chainHint"
                      : workflow === "send"
                        ? "quotes.email.previewHint"
                        : "quotes.action.noteOptional",
                  )}
            </DialogDescription>
          </DialogHeader>
          {workflow === "submit" && (
            <div className="space-y-3">
              <ol className="space-y-3" data-testid="quote-approval-chain">
                {approvalChain.map((step, index) => {
                  const role = step.roleLabelKey ? t(step.roleLabelKey) : step.roleName;
                  // One person approves one step: people chosen for another step, or already
                  // in a step everyone in the role approves, are not offered.
                  const takenElsewhere = new Set(
                    approvalChain.flatMap((other, otherIndex) =>
                      otherIndex === index
                        ? []
                        : other.everyoneApproves
                          ? other.candidates.map((candidate) => candidate.id)
                          : chosenApprovers[otherIndex] != null
                            ? [chosenApprovers[otherIndex]!]
                            : [],
                    ),
                  );
                  const options = step.candidates.filter((c) => !takenElsewhere.has(c.id));
                  return (
                    <li
                      key={`${step.order}-${index}`}
                      className="space-y-1.5 rounded-md border p-3"
                    >
                      <p className="text-sm font-medium">
                        {t("quotes.approval.stepTitle", { n: index + 1, role })}
                      </p>
                      {step.everyoneApproves ? (
                        <p className="text-sm text-muted-foreground">
                          {t("quotes.approval.everyoneInOrder", {
                            names: step.candidates.map((c) => c.name).join(", ") || "—",
                          })}
                        </p>
                      ) : (
                        <div
                          data-testid={
                            index === 0
                              ? "quote-approval-approver"
                              : `quote-approval-approver-${index}`
                          }
                        >
                          <SearchableSelect
                            ariaLabel={t("quotes.approval.stepTitle", { n: index + 1, role })}
                            value={chosenApprovers[index]?.toString() ?? null}
                            onChange={(value) => {
                              setChosenApprovers((prev) =>
                                prev.map((id, other) => (other === index ? Number(value) : id)),
                              );
                              setApprovalPreview(null);
                              setApprovalError("");
                            }}
                            placeholder={t("quotes.approval.selectApprover")}
                            options={options.map((candidate) => personOption(candidate, t))}
                          />
                        </div>
                      )}
                      {step.candidates.length === 0 && (
                        <p role="alert" className="text-sm text-destructive">
                          {t("quotes.approval.noCandidatesForStep", { role })}
                        </p>
                      )}
                    </li>
                  );
                })}
              </ol>
              <Button
                type="button"
                variant="outline"
                disabled={!chainReady || workflowBusy}
                onClick={() => void previewApprovalEmail()}
              >
                {t("quotes.approval.preview")}
              </Button>
              {approvalError && (
                <p role="alert" className="text-sm text-destructive">
                  {approvalError}
                </p>
              )}
              {approvalPreview && (
                <div
                  className="space-y-2 rounded-md border p-3"
                  data-testid="quote-approval-preview"
                >
                  <p>
                    <strong>{t("quotes.approval.to")}:</strong> {approvalPreview.toEmail}
                  </p>
                  <p className="break-words">
                    <strong>{t("quotes.approval.cc")}:</strong>{" "}
                    {approvalPreview.ccEmails.length
                      ? approvalPreview.ccEmails.join(", ")
                      : t("quotes.approval.noCc")}
                  </p>
                  <p>
                    <strong>{t("quotes.email.subject")}:</strong> {approvalPreview.subject}
                  </p>
                  <iframe
                    title={t("quotes.approval.preview")}
                    srcDoc={approvalPreview.body}
                    sandbox=""
                    className="h-64 w-full rounded-md border bg-card"
                  />
                  <p className="text-xs text-muted-foreground">{t("quotes.approval.queuedHint")}</p>
                </div>
              )}
            </div>
          )}
          {workflow === "send" && sendDraft && (
            <div className="space-y-3">
              <div className="space-y-1">
                <Label htmlFor="quote-email-to">{t("quotes.email.to")}</Label>
                <Input
                  id="quote-email-to"
                  type="email"
                  maxLength={150}
                  value={sendDraft.toEmail}
                  onChange={(event) => setSendDraft({ ...sendDraft, toEmail: event.target.value })}
                />
                {(!sendDraft.toEmail.trim() || !isValidEmail(sendDraft.toEmail)) && (
                  <p className="text-xs text-destructive">
                    {t(sendDraft.toEmail.trim() ? "quotes.email.invalid" : "quotes.email.required")}
                  </p>
                )}
              </div>
              <div className="space-y-1">
                <Label htmlFor="quote-email-subject">{t("quotes.email.subject")}</Label>
                <Input
                  id="quote-email-subject"
                  maxLength={200}
                  value={sendDraft.subject}
                  disabled={!sendPreview?.canEdit}
                  onChange={(event) => setSendDraft({ ...sendDraft, subject: event.target.value })}
                />
                {!sendDraft.subject.trim() && (
                  <p className="text-xs text-destructive">{t("quotes.email.subjectRequired")}</p>
                )}
              </div>
              <div className="space-y-1">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <Label htmlFor="quote-email-body">{t("quotes.email.body")}</Label>
                  <span
                    className={cn(
                      "text-xs tabular-nums",
                      sendDraft.body.length > QUOTE_EMAIL_BODY_MAX
                        ? "text-destructive"
                        : "text-muted-foreground",
                    )}
                  >
                    {sendDraft.body.length.toLocaleString()} /{" "}
                    {QUOTE_EMAIL_BODY_MAX.toLocaleString()}
                  </span>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  aria-expanded={htmlBodyOpen}
                  aria-controls="quote-email-body"
                  onClick={() => setHtmlBodyOpen((open) => !open)}
                >
                  {htmlBodyOpen ? (
                    <ChevronUp className="mr-1.5 h-4 w-4" />
                  ) : (
                    <ChevronDown className="mr-1.5 h-4 w-4" />
                  )}
                  {t(htmlBodyOpen ? "quotes.email.hideHtml" : "quotes.email.showHtml")}
                </Button>
                <Textarea
                  id="quote-email-body"
                  rows={10}
                  className={cn("font-mono text-xs", !htmlBodyOpen && "hidden")}
                  value={sendDraft.body}
                  disabled={!sendPreview?.canEdit}
                  onChange={(event) => setSendDraft({ ...sendDraft, body: event.target.value })}
                />
                {sendDraft.body.length > QUOTE_EMAIL_BODY_MAX && (
                  <p className="text-xs text-destructive">
                    {t("quotes.email.tooLong", { max: QUOTE_EMAIL_BODY_MAX.toLocaleString() })}
                  </p>
                )}
                {!sendDraft.body.trim() && (
                  <p className="text-xs text-destructive">{t("quotes.email.bodyRequired")}</p>
                )}
                {sendDraft.body.trim() && !sendDraft.body.trimStart().startsWith("<") && (
                  <p className="text-xs text-destructive">{t("quotes.email.htmlRequired")}</p>
                )}
              </div>
              <div className="space-y-1">
                <span>{t("quotes.email.renderedPreview")}</span>
                <iframe
                  title={t("quotes.email.renderedPreview")}
                  srcDoc={sendDraft.body}
                  sandbox=""
                  className="h-72 w-full rounded-md border bg-card"
                />
              </div>
              <div
                className="flex flex-wrap items-center gap-x-1 gap-y-2 rounded-md border bg-muted/30 p-3 text-sm"
                data-testid="quote-email-attachment"
              >
                <span className="font-medium">{t("quotes.email.attachment")}:</span>
                {/* Same export endpoint and language the send request attaches. */}
                <Button
                  type="button"
                  variant="link"
                  className="h-auto p-0"
                  title={t("quotes.action.downloadExcel")}
                  onClick={() => void downloadSpreadsheet()}
                  disabled={exportingSpreadsheet}
                >
                  {exportingSpreadsheet ? (
                    <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                  ) : (
                    <Download className="mr-1.5 h-4 w-4" />
                  )}
                  <span className="break-all">{sendDraft.attachmentFileName}</span>
                </Button>
              </div>
            </div>
          )}
          {workflow !== "send" && (
            <Textarea
              aria-label={t("a11yUi.reason")}
              rows={4}
              value={workflowNote}
              onChange={(e) => setWorkflowNote(e.target.value)}
              placeholder={t(
                workflow === "rejectInternal" ? "quotes.decide.rejectReason" : "quotes.field.note",
              )}
            />
          )}
          <DialogFooter className="sticky bottom-0 z-10 border-t bg-background pt-3">
            <Button variant="outline" onClick={() => setWorkflow(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              onClick={() => void runWorkflow()}
              disabled={
                workflowBusy ||
                (workflow === "submit" && !approvalPreview) ||
                (workflow === "rejectInternal" && !workflowNote.trim()) ||
                (workflow === "approve" && nextApproverBlocked) ||
                (workflow === "send" &&
                  (!sendDraft ||
                    !isValidEmail(sendDraft.toEmail) ||
                    !sendDraft.toEmail.trim() ||
                    !sendDraft.subject.trim() ||
                    !sendDraft.body.trim() ||
                    sendDraft.body.length > QUOTE_EMAIL_BODY_MAX ||
                    !sendDraft.body.trimStart().startsWith("<")))
              }
            >
              {workflowBusy && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              {t(
                workflow === "send" ||
                  workflow === "submit" ||
                  workflow === "approve" ||
                  workflow === "rejectInternal"
                  ? `quotes.action.${workflow}`
                  : "common.save",
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeletionImpactDialog
        open={deleteOpen}
        impact={deleteImpact}
        loading={deleteImpactLoading}
        deleting={deleteBusy}
        error={deleteError}
        onOpenChange={(open) => {
          if (!open && !deleteBusy) {
            setDeleteOpen(false);
            setDeleteImpact(null);
            setDeleteError(null);
          }
        }}
        onConfirm={confirmDelete}
        onCompleted={completeDelete}
      />

      <Dialog
        open={selectedVersion !== null}
        onOpenChange={(open) => !open && setSelectedVersion(null)}
      >
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
                  {t("quotes.version.capturedAt")}: {formatDateTime(selectedVersion.capturedAt)}
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
      {canLinkContract && (
        <QuoteContractLinkDialog
          mode="fromQuote"
          quote={{
            id: quote.id,
            code: quote.code,
            customerId: quote.customerId!,
            opportunityId: quote.opportunityId,
          }}
          open={linkContractOpen}
          onOpenChange={setLinkContractOpen}
          onLinked={(contract) => navigate(`/admin/contracts/${contract.id}`)}
        />
      )}
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
    ApproveStep: "quotes.log.approveStep",
    WithdrawApproval: "quotes.log.withdrawApproval",
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
    case "submit":
      return <ThumbsUp className={cls} />;
    case "approve":
      return <CheckCheck className={cls} />;
    case "rejectInternal":
      return <ThumbsDown className={cls} />;
    case "send":
      return <Send className={cls} />;
    case "customerApprove":
      return <CheckCheck className={cls} />;
    case "customerReject":
      return <XCircle className={cls} />;
    case "cancel":
      return <Ban className={cls} />;
  }
}

const FORM_FIELD_LABEL = "text-xs uppercase tracking-wide text-muted-foreground";

const FormField = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <FieldScope>
    <Label className={FORM_FIELD_LABEL}>{label}</Label>
    {children}
  </FieldScope>
);

const QuoteVersionDetails = ({
  version,
  t,
}: {
  version: QuoteVersionResponse;
  t: (k: string, params?: Record<string, string | number>) => string;
}) => (
  <div className="space-y-4">
    <dl className="grid grid-cols-2 gap-3 rounded-lg border bg-muted/20 p-4 text-sm sm:grid-cols-4">
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">
          {t("quotes.field.method")}
        </dt>
        <dd className="font-medium">{t(`quotes.method.${version.method}`)}</dd>
      </div>
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">
          {t("quotes.field.subtotal")}
        </dt>
        <dd className="font-medium">{formatMoney(version.subtotal, version.currency)}</dd>
      </div>
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">
          {t("quotes.field.discountPercent")}
        </dt>
        <dd className="font-medium">{version.discountPercent}%</dd>
      </div>
      <div>
        <dt className="text-xs uppercase tracking-wide text-muted-foreground">
          {t("quotes.field.vatPercent")}
        </dt>
        <dd className="font-medium">{version.vatPercent}%</dd>
      </div>
    </dl>

    {version.method === "UnitCost" ? (
      <div className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-2">
        <div>
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.areaSqm")}
          </div>
          <div className="font-medium">{version.areaSqm ?? "—"}</div>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.unitPricePerSqm")}
          </div>
          <div className="font-medium">
            {version.unitPricePerSqm != null
              ? formatMoney(version.unitPricePerSqm, version.currency)
              : "—"}
          </div>
        </div>
        <div className="sm:col-span-2">
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            {t("quotes.field.packageDescription")}
          </div>
          <div className="mt-1 whitespace-pre-wrap">{version.packageDescription || "—"}</div>
        </div>
        <div className="sm:col-span-2">
          <RateProvenance value={version} t={t} />
        </div>
      </div>
    ) : (
      <BoqTable items={version.items} t={t} currency={version.currency} />
    )}

    <div className="flex items-center justify-between rounded-lg border border-primary/20 bg-primary/5 p-4">
      <span className="font-semibold">{t("quotes.field.grandTotal")}</span>
      <span className="text-xl font-bold text-primary">
        {formatMoney(version.grandTotal, version.currency)}
      </span>
    </div>
  </div>
);

type RateProvenanceValue = Pick<
  QuoteResponse | QuoteVersionResponse,
  | "materialRateCatalogCode"
  | "materialRateCatalogName"
  | "materialRateRevisionVersion"
  | "pricingEffectiveDate"
  | "catalogUnitPricePerSqm"
  | "unitPricePerSqm"
  | "rateSource"
  | "rateOverrideReason"
  | "currency"
>;

const RateProvenance = ({
  value,
  t,
}: {
  value: RateProvenanceValue;
  t: (key: string) => string;
}) => (
  <dl className="grid gap-2 rounded-md border bg-muted/20 p-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
    <div>
      <dt className="text-xs text-muted-foreground">{t("quotes.field.materialRateCatalog")}</dt>
      <dd className="font-medium">
        {value.materialRateCatalogCode
          ? `${value.materialRateCatalogCode} · ${value.materialRateCatalogName ?? ""}`
          : "—"}
      </dd>
    </div>
    <div>
      <dt className="text-xs text-muted-foreground">{t("quotes.field.materialRateRevision")}</dt>
      <dd className="font-medium">
        {value.materialRateRevisionVersion != null ? `V${value.materialRateRevisionVersion}` : "—"}
      </dd>
    </div>
    <div>
      <dt className="text-xs text-muted-foreground">{t("quotes.field.pricingEffectiveDate")}</dt>
      <dd className="font-medium">{value.pricingEffectiveDate ?? "—"}</dd>
    </div>
    <div>
      <dt className="text-xs text-muted-foreground">{t("quotes.field.catalogRate")}</dt>
      <dd className="font-medium">
        {value.catalogUnitPricePerSqm != null
          ? `${formatMoney(value.catalogUnitPricePerSqm, value.currency)}/m²`
          : "—"}
      </dd>
    </div>
    <div>
      <dt className="text-xs text-muted-foreground">{t("quotes.field.appliedRate")}</dt>
      <dd className="font-medium">
        {value.unitPricePerSqm != null
          ? `${formatMoney(value.unitPricePerSqm, value.currency)}/m²`
          : "—"}
      </dd>
    </div>
    <div>
      <dt className="text-xs text-muted-foreground">{t("quotes.field.rateSource")}</dt>
      <dd className="font-medium">{t(`quotes.rateSource.${value.rateSource}`)}</dd>
    </div>
    {value.rateOverrideReason && (
      <div className="sm:col-span-2 lg:col-span-3">
        <dt className="text-xs text-muted-foreground">{t("quotes.field.rateOverrideReason")}</dt>
        <dd>{value.rateOverrideReason}</dd>
      </div>
    )}
  </dl>
);

// Lines are copied from the approved project BOQ and are read-only on the quote.
// `items` is null while a newly applied BOQ revision waits to be saved.
const BoqTable = ({
  items,
  t,
  currency,
}: {
  items: QuoteItemResponse[] | null;
  t: (k: string, params?: Record<string, string | number>) => string;
  currency: string;
}) => {
  const [expanded, setExpanded] = useState(false);
  const total = items?.reduce((sum, row) => sum + Number(row.amount), 0) ?? 0;
  const collapsible = (items?.length ?? 0) > BOQ_PREVIEW_ROWS;
  const rows = items && collapsible && !expanded ? items.slice(0, BOQ_PREVIEW_ROWS) : items;

  return (
    <div className="rounded-lg border bg-card" data-testid="quote-boq-lines">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-b px-4 py-3">
        <h3 className="text-sm font-medium">{t("quotes.boq.title")}</h3>
        {items && items.length > 0 && (
          <span className="text-xs text-muted-foreground">
            {t("quotes.boq.summary", {
              count: items.length,
              total: formatMoney(total, currency),
            })}
          </span>
        )}
      </div>
      {rows === null ? (
        <p className="p-4 text-sm text-muted-foreground">{t("quotes.projectBoq.pendingSave")}</p>
      ) : rows.length === 0 ? (
        <p className="p-4 text-center text-sm text-muted-foreground">
          {t("quotes.projectBoq.noLines")}
        </p>
      ) : (
        <>
          {/* Desktop table (md+); expanded long lists scroll inside the panel. */}
          <div
            className={cn(
              "hidden overflow-x-auto md:block",
              expanded && "max-h-[32rem] overflow-y-auto",
            )}
          >
            <table className="w-full min-w-[560px] divide-y text-sm">
              <thead className="sticky top-0 z-10 bg-muted text-xs text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">{t("quotes.boq.name")}</th>
                  <th className="w-16 px-3 py-2 text-left font-medium">{t("quotes.boq.unit")}</th>
                  <th className="w-24 px-3 py-2 text-right font-medium">{t("quotes.boq.qty")}</th>
                  <th className="w-32 px-3 py-2 text-right font-medium">
                    {t("quotes.boq.unitPrice")}
                    <span className="ml-1 font-normal">({currency})</span>
                  </th>
                  <th className="w-32 px-3 py-2 text-right font-medium">
                    {t("quotes.boq.amount")}
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((row, index) => (
                  <tr key={`${row.id}-${index}`}>
                    <td className="px-3 py-2">
                      <div>{row.name}</div>
                      {row.itemCode && (
                        <div className="text-xs text-muted-foreground">{row.itemCode}</div>
                      )}
                    </td>
                    <td className="px-3 py-2">{row.unit}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{row.quantity}</td>
                    <td className="whitespace-nowrap px-3 py-2 text-right tabular-nums">
                      {formatMoney(row.unitPrice, currency)}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 text-right font-medium tabular-nums">
                      {formatMoney(row.amount, currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile card list (<md): every row becomes a stacked card. */}
          <ul className={cn("divide-y md:hidden", expanded && "max-h-[32rem] overflow-y-auto")}>
            {rows.map((row, index) => (
              <li key={`${row.id}-${index}`} className="space-y-1 p-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0 flex-1">
                    <div className="break-words text-sm font-medium">{row.name}</div>
                    {row.itemCode && (
                      <div className="text-xs text-muted-foreground">
                        {t("quotes.boq.code")}: {row.itemCode}
                      </div>
                    )}
                  </div>
                  <div className="whitespace-nowrap text-sm font-semibold tabular-nums">
                    {formatMoney(row.amount, currency)}
                  </div>
                </div>
                <div className="text-xs text-muted-foreground">
                  {row.quantity} {row.unit} × {formatMoney(row.unitPrice, currency)}
                </div>
              </li>
            ))}
          </ul>

          {collapsible && items && (
            <div className="flex flex-wrap items-center justify-between gap-2 border-t px-4 py-2">
              <span className="text-xs text-muted-foreground">
                {t("quotes.boq.showing", { shown: rows.length, count: items.length })}
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-expanded={expanded}
                onClick={() => setExpanded((open) => !open)}
              >
                {expanded ? (
                  <ChevronUp className="mr-1.5 h-4 w-4" />
                ) : (
                  <ChevronDown className="mr-1.5 h-4 w-4" />
                )}
                {expanded
                  ? t("quotes.boq.collapse")
                  : t("quotes.boq.showAll", { count: items.length })}
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default AdminQuoteDetail;
