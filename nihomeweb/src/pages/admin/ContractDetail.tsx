import { useUiError } from "@/hooks/useUiError";
import {
  getErrorMessage,
  MilestoneDraft,
  ContractEditForm,
  toIsoDate,
  getLocalIsoDate,
  toIsoTimestamp,
  toEditForm,
  hasScheduleChanges,
  toMilestoneRequests,
} from "./contracts/contractDetailShared";
import { ContractHeader } from "./contracts/ContractHeader";
import { InfoTab } from "./contracts/ContractInfoTab";
import { EditInfoTab } from "./contracts/ContractEditTab";
import { ScheduleTab, EditScheduleTab } from "./contracts/ContractScheduleTabs";
import { VoTab } from "./contracts/ContractAppendicesTab";
import { DocumentsTab } from "./contracts/ContractDocumentsTab";
import { TimelineTab } from "./contracts/ContractTimelineTab";
import { collectLookupPages } from "@/lib/pagination";
import { getUiError } from "@/lib/apiError";
import { useConfirm } from "@/components/ui/confirmation";
import { FieldScope, FieldControl } from "@/components/ui/field-scope";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import { personOption } from "@/lib/people";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  CheckCircle2,
  ChevronDown,
  Clock,
  FileText,
  History,
  Link2,
  Loader2,
  Pencil,
  Play,
  Plus,
  Save,
  Send,
  ShieldCheck,
  ShieldX,
  Trash2,
  Upload,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import QuoteContractLinkDialog from "@/components/admin/QuoteContractLinkDialog";
import ContractFinanceReviewLink from "@/components/admin/ContractFinanceReviewLink";
import ContractSourceLeads from "@/components/admin/ContractSourceLeads";
import ContractExecutionProgress from "@/components/admin/ContractExecutionProgress";
import ContractCareActivities from "@/components/admin/ContractCareActivities";
import ContractContinuations from "@/components/admin/ContractContinuations";
import ContractReminders from "@/components/admin/ContractReminders";
import LegalActivationWizard from "@/components/admin/LegalActivationWizard";
import { useI18n } from "@/lib/i18n";
import { formatVnd, formatVndWithSymbol } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { extractApiError, isConcurrencyConflict } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsTrigger } from "@/components/ui/tabs";
import { ScrollableTabsList } from "@/components/ui/scrollable-tabs-list";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import {
  adminApi,
  type ContractClassificationOptions,
  type ContractAppendixResponse,
  type ContractAppendixStatus,
  type ContractAttachmentKind,
  type ContractAttachmentResponse,
  type ContractPaymentMilestoneRequest,
  type ContractResponse,
  type ContractDirection,
  type ContractStatus,
  type ContractType,
  type CustomerResponse,
  type ContractTimelineEvent,
  type PaymentMilestoneStatus,
  type KpiUserOptionResponse,
  type OperationalProjectListItemResponse,
  type UpsertContractRequest,
  type UpsertContractAppendixRequest,
} from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useAppSelector } from "@/store";

// -------- main page --------

type TabId = "info" | "schedule" | "appendices" | "documents" | "timeline" | "care";

const isContractTab = (value: string | null): value is TabId =>
  value === "info" ||
  value === "schedule" ||
  value === "appendices" ||
  value === "documents" ||
  value === "care" ||
  value === "timeline";

type ContractDetailProps = {
  mode?: "all" | "upstream" | "downstream";
};

const ContractDetail = ({ mode = "all" }: ContractDetailProps) => {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);
  const params = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedReturnTo = searchParams.get("returnTo");
  const requestedReturnPath = requestedReturnTo?.split("?", 1)[0];
  const defaultReturnTo =
    mode === "upstream"
      ? "/admin/finance/contracts"
      : mode === "downstream"
        ? "/admin/finance/input-contracts"
        : "/admin/contracts";
  const returnTo =
    requestedReturnPath === "/admin/contracts" ||
    requestedReturnPath === "/admin/finance/contracts" ||
    requestedReturnPath === "/admin/finance/input-contracts"
      ? requestedReturnTo
      : defaultReturnTo;
  const scopedDirection: ContractDirection | undefined =
    mode === "upstream" ? "Upstream" : mode === "downstream" ? "Downstream" : undefined;
  const idNum = Number(params.id);
  const canManage = has(ADMIN_PERMS.contractsManage);
  const isFinanceWorkspace = mode !== "all";
  const canManageMaster = canManage && !isFinanceWorkspace;
  const canManageMilestones = canManageMaster || has(ADMIN_PERMS.financePaymentsManage);
  const canDecideVo = has(ADMIN_PERMS.contractsViewAll) && !isFinanceWorkspace;

  const [contract, setContract] = useState<ContractResponse | null>(null);
  const [ensuringDesignProject, setEnsuringDesignProject] = useState(false);
  const [linkQuoteOpen, setLinkQuoteOpen] = useState(false);
  // A customer contract drafted apart from its quotation can still be tied to
  // the approved quote afterwards (one source quote per contract).
  const canLinkQuote =
    canManageMaster &&
    contract != null &&
    contract.direction === "Upstream" &&
    contract.quoteId == null &&
    contract.status !== "Cancelled" &&
    contract.status !== "Completed";
  const [appendices, setAppendices] = useState<ContractAppendixResponse[]>([]);
  const [attachments, setAttachments] = useState<ContractAttachmentResponse[]>([]);
  const [attachmentsError, setAttachmentsError] = useState<string | null>(null);
  const [supplementalError, setSupplementalError] = useState<string | null>(null);
  const [timeline, setTimeline] = useState<ContractTimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const requestedTab = searchParams.get("tab");
  const [tab, setTab] = useState<TabId>(isContractTab(requestedTab) ? requestedTab : "info");
  const [transitionBusy, setTransitionBusy] = useState<ContractStatus | null>(null);
  const [legalWizardOpen, setLegalWizardOpen] = useState(false);
  const [busyMilestoneId, setBusyMilestoneId] = useState<number | null>(null);
  const [milestoneActionDraft, setMilestoneActionDraft] = useState<{
    milestoneId: number;
    status: "Requested" | "Paid";
    value: string;
    accountantUserId: number | null;
  } | null>(null);
  const [accountants, setAccountants] = useState<KpiUserOptionResponse[]>([]);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<ContractEditForm | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [editingSchedule, setEditingSchedule] = useState(false);
  const [scheduleDraft, setScheduleDraft] = useState<MilestoneDraft[]>([]);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [scheduleSaving, setScheduleSaving] = useState(false);
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [classification, setClassification] = useState<ContractClassificationOptions | null>(null);

  const handleEnsureDesignProject = async () => {
    if (!contract) return;
    setEnsuringDesignProject(true);
    try {
      const { data } = await adminApi.ensureContractDesignProject(contract.id);
      toast({ title: t("contracts.designProject.created") });
      if (has(ADMIN_PERMS.designProjects)) {
        navigate(`/admin/design-projects/${data.id}`);
      } else {
        setContract((current) =>
          current
            ? {
                ...current,
                designProjectId: data.id,
                designProjectCode: data.projectCode,
                designProjectName: data.name,
                designProjectCurrentStage: data.currentStage,
              }
            : current,
        );
      }
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setEnsuringDesignProject(false);
    }
  };

  const load = useCallback(async () => {
    if (!Number.isFinite(idNum)) return;
    setLoading(true);
    setLoadError(null);
    setAttachmentsError(null);
    setSupplementalError(null);
    try {
      const [c, vos, atts, tl, options] = await Promise.all([
        adminApi.getContract(idNum, scopedDirection),
        adminApi.listContractAppendices(idNum).catch(() => {
          setSupplementalError(t("contracts.detail.partialLoadError"));
          return { data: [] as ContractAppendixResponse[] };
        }),
        adminApi.listContractAttachments(idNum).catch((error) => {
          setAttachmentsError(uiError(error));
          return { data: [] as ContractAttachmentResponse[] };
        }),
        adminApi.getContractTimeline(idNum).catch(() => {
          setSupplementalError(t("contracts.detail.partialLoadError"));
          return { data: [] as ContractTimelineEvent[] };
        }),
        adminApi.getContractClassificationOptions(scopedDirection),
      ]);
      setContract(c.data);
      setForm(toEditForm(c.data));
      setAppendices(vos.data);
      setAttachments(atts.data);
      setTimeline(tl.data);
      setClassification(options.data);
      try {
        const eligible = await adminApi.listKpiEligibleUsers();
        setAccountants(eligible.data.filter((user) => user.positionCode === "PROJECT_ACCOUNTING"));
      } catch {
        /* milestone actions remain server-protected */
      }
    } catch (err) {
      setLoadError(uiError(err));
    } finally {
      setLoading(false);
    }
  }, [idNum, scopedDirection, t, uiError]);

  useEffect(() => {
    void load();
  }, [load]);

  const refreshContract = useCallback(async () => {
    if (!Number.isFinite(idNum)) return;
    setAttachmentsError(null);
    const [c, vos, tl] = await Promise.all([
      adminApi.getContract(idNum, scopedDirection),
      adminApi.listContractAppendices(idNum),
      adminApi.getContractTimeline(idNum),
    ]);
    setContract(c.data);
    setAppendices(vos.data);
    setTimeline(tl.data);
    try {
      const atts = await adminApi.listContractAttachments(idNum);
      setAttachments(atts.data);
    } catch (error) {
      setAttachmentsError(uiError(error));
    }
  }, [idNum, scopedDirection, uiError]);

  const handleTransition = useCallback(
    async (next: ContractStatus) => {
      if (!contract) return;
      // Client-side gate mirrors the server rule so we can point users at
      // the right tab before they get a 400.
      if (
        (next === "Signed" || (next === "InProgress" && contract.status === "Signed")) &&
        !contract.hasSignedScan
      ) {
        toast({
          variant: "destructive",
          title: t("contracts.detail.transition.needScan"),
        });
        setTab("documents");
        return;
      }
      if (
        next === "InProgress" &&
        contract.status === "Signed" &&
        contract.direction === "Upstream" &&
        classification?.allowedTypes.Upstream.includes(
          contract.type as Exclude<ContractType, "Unclassified">,
        )
      ) {
        setLegalWizardOpen(true);
        return;
      }
      if (next === "Completed") {
        const anyUnpaid = contract.paymentMilestones.some((m) => m.status !== "Paid");
        if (contract.paymentMilestones.length === 0 || anyUnpaid) {
          toast({ variant: "destructive", title: t("contracts.detail.transition.needAllPaid") });
          setTab("schedule");
          return;
        }
      }
      if (
        next === "Cancelled" &&
        !(await confirmAction(t("contracts.detail.transition.markCancelled") + "?", {
          destructive: false,
        }))
      ) {
        return;
      }
      setTransitionBusy(next);
      try {
        await adminApi.transitionContract(contract.id, next, contract.rowVersion);
        await refreshContract();
      } catch (err) {
        toast({ variant: "destructive", title: uiError(err) });
        if (isConcurrencyConflict(err)) await refreshContract();
      } finally {
        setTransitionBusy(null);
      }
    },
    [
      classification?.allowedTypes.Upstream,
      confirmAction,
      contract,
      refreshContract,
      t,
      toast,
      uiError,
    ],
  );

  const handleMilestoneStatus = useCallback(
    async (milestoneId: number, nextStatus: PaymentMilestoneStatus) => {
      if (!contract) return;
      if (nextStatus === "Requested") {
        const milestone = contract.paymentMilestones.find((item) => item.id === milestoneId);
        setMilestoneActionDraft({
          milestoneId,
          status: "Requested",
          value: "",
          accountantUserId: milestone?.responsibleAccountantUserId ?? null,
        });
        return;
      }
      setBusyMilestoneId(milestoneId);
      try {
        await adminApi.updateMilestoneStatus(
          contract.id,
          milestoneId,
          nextStatus,
          null,
          null,
          null,
          contract.rowVersion,
        );
        await refreshContract();
      } catch (err) {
        toast({ variant: "destructive", title: uiError(err) });
        if (isConcurrencyConflict(err)) await refreshContract();
      } finally {
        setBusyMilestoneId(null);
      }
    },
    [contract, refreshContract, toast, uiError],
  );

  const openPaidDateDialog = useCallback(
    (milestoneId: number, actualPaymentDate?: string | null) => {
      const milestone = contract?.paymentMilestones.find((item) => item.id === milestoneId);
      setMilestoneActionDraft({
        milestoneId,
        status: "Paid",
        value: toIsoDate(actualPaymentDate) || getLocalIsoDate(),
        accountantUserId: milestone?.responsibleAccountantUserId ?? null,
      });
    },
    [contract],
  );

  const confirmPaidDate = useCallback(async () => {
    if (!contract || !milestoneActionDraft) return;
    if (!milestoneActionDraft.accountantUserId) {
      toast({ variant: "destructive", title: t("contracts.milestoneAccountantRequired") });
      return;
    }
    if (milestoneActionDraft.status === "Paid" && !milestoneActionDraft.value) {
      toast({ variant: "destructive", title: t("contracts.milestoneActualPaymentDateRequired") });
      return;
    }
    setBusyMilestoneId(milestoneActionDraft.milestoneId);
    try {
      await adminApi.updateMilestoneStatus(
        contract.id,
        milestoneActionDraft.milestoneId,
        milestoneActionDraft.status,
        milestoneActionDraft.status === "Paid" ? toIsoTimestamp(milestoneActionDraft.value) : null,
        milestoneActionDraft.accountantUserId,
        null,
        contract.rowVersion,
      );
      setMilestoneActionDraft(null);
      await refreshContract();
    } catch (err) {
      toast({ variant: "destructive", title: uiError(err) });
      if (isConcurrencyConflict(err)) await refreshContract();
    } finally {
      setBusyMilestoneId(null);
    }
  }, [contract, milestoneActionDraft, refreshContract, t, toast, uiError]);

  const beginEdit = useCallback(async () => {
    if (!contract) return;
    setForm(toEditForm(contract));
    setFormError(null);
    setTab("info");
    if (customers.length === 0 || projects.length === 0) {
      try {
        const [customersResponse, projectsResponse] = await Promise.all([
          customers.length === 0
            ? collectLookupPages((page) => adminApi.listCustomers({ pageSize: 200, page }))
            : null,
          projects.length === 0
            ? collectLookupPages((page) =>
                adminApi.listOperationalProjects({ pageSize: 100, page }),
              )
            : null,
        ]);
        if (customersResponse) setCustomers(customersResponse.data.items);
        if (projectsResponse) setProjects(projectsResponse.data.items);
      } catch (err) {
        toast({ variant: "destructive", title: getErrorMessage(err) ?? t("common.error") });
        return;
      }
    }
    setEditing(true);
  }, [contract, customers.length, projects.length, t, toast]);

  useEffect(() => {
    if (searchParams.get("edit") !== "true" || !contract || !canManageMaster || editing) return;
    const next = new URLSearchParams(searchParams);
    next.delete("edit");
    setSearchParams(next, { replace: true });
    void beginEdit();
  }, [beginEdit, canManageMaster, contract, editing, searchParams, setSearchParams]);

  const cancelEdit = useCallback(() => {
    if (contract) setForm(toEditForm(contract));
    setFormError(null);
    setEditing(false);
  }, [contract]);

  const beginScheduleEdit = useCallback(() => {
    if (!contract) return;
    setScheduleDraft(toEditForm(contract).milestones);
    setScheduleError(null);
    setEditingSchedule(true);
    setTab("schedule");
  }, [contract]);

  const cancelScheduleEdit = useCallback(() => {
    if (contract) setScheduleDraft(toEditForm(contract).milestones);
    setScheduleError(null);
    setEditingSchedule(false);
  }, [contract]);

  const saveSchedule = useCallback(async () => {
    if (!contract) return;
    setScheduleError(null);
    const total = scheduleDraft.reduce((sum, milestone) => sum + milestone.percentValue, 0);
    if (scheduleDraft.length > 0 && Math.abs(total - 100) > 0.01) {
      setScheduleError(t("contracts.milestoneSumInvalid"));
      return;
    }
    if (scheduleDraft.some((milestone) => !milestone.name.trim())) {
      setScheduleError(t("contracts.milestoneNameRequired"));
      return;
    }
    if (
      scheduleDraft.some((milestone) => milestone.status === "Paid" && !milestone.actualPaymentDate)
    ) {
      setScheduleError(t("contracts.milestoneActualPaymentDateRequired"));
      return;
    }
    setScheduleSaving(true);
    try {
      const { data } = await adminApi.updateContractPaymentSchedule(contract.id, {
        rowVersion: contract.rowVersion,
        paymentMilestones: toMilestoneRequests(scheduleDraft),
      });
      setContract(data);
      setForm(toEditForm(data));
      setScheduleDraft(toEditForm(data).milestones);
      setEditingSchedule(false);
      toast({ title: t("form.updated") });
      await refreshContract();
    } catch (err) {
      setScheduleError(getErrorMessage(err) ?? t("common.error"));
      if (isConcurrencyConflict(err)) await refreshContract();
    } finally {
      setScheduleSaving(false);
    }
  }, [contract, refreshContract, scheduleDraft, t, toast]);

  const saveEdit = useCallback(async () => {
    if (!contract || !form) return;
    const effectiveDirection = scopedDirection ?? form.direction;
    setFormError(null);
    if (form.type === "Unclassified") {
      setFormError(t("contracts.validation.typeRequired"));
      setTab("info");
      return;
    }
    if (form.operationalProjectId == null) {
      setFormError(t("contracts.validation.operationalProjectRequired"));
      setTab("info");
      return;
    }
    if (effectiveDirection === "Downstream" && form.vendorId == null) {
      setFormError(t("contracts.validation.vendorRequired"));
      setTab("info");
      return;
    }
    const scheduleChanged = hasScheduleChanges(contract, form.milestones);
    if (form.signedDate && form.startDate && form.startDate < form.signedDate) {
      setFormError(t("form.invalidDateRange"));
      setTab("info");
      return;
    }
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      setFormError(t("form.invalidDateRange"));
      setTab("info");
      return;
    }
    const milestoneTotal = form.milestones.reduce(
      (sum, milestone) => sum + milestone.percentValue,
      0,
    );
    if (form.milestones.length > 0 && Math.abs(milestoneTotal - 100) > 0.01) {
      setFormError(t("contracts.milestoneSumInvalid"));
      setTab("schedule");
      return;
    }
    if (form.milestones.some((milestone) => !milestone.name.trim())) {
      setFormError(t("contracts.milestoneNameRequired"));
      setTab("schedule");
      return;
    }
    if (
      scheduleChanged &&
      form.milestones.some(
        (milestone) => milestone.status === "Paid" && !milestone.actualPaymentDate,
      )
    ) {
      setFormError(t("contracts.milestoneActualPaymentDateRequired"));
      setTab("schedule");
      return;
    }
    const paymentMilestones: ContractPaymentMilestoneRequest[] | null = scheduleChanged
      ? toMilestoneRequests(form.milestones)
      : null;
    const payload: UpsertContractRequest = {
      rowVersion: contract.rowVersion,
      contractNumber: form.contractNumber.trim() || null,
      customerId: form.customerId,
      direction: effectiveDirection,
      type: form.type,
      vendorId: effectiveDirection === "Downstream" ? form.vendorId : null,
      operationalProjectId: form.operationalProjectId,
      opportunityId: contract.opportunityId,
      quoteId: contract.quoteId,
      status: contract.status,
      signedDate: toIsoTimestamp(form.signedDate),
      startDate: toIsoTimestamp(form.startDate),
      endDate: toIsoTimestamp(form.endDate),
      value: Number.isFinite(form.value) ? Math.max(0, form.value) : 0,
      scopeOfWork: form.scopeOfWork.trim() || null,
      note: form.note.trim() || null,
      paymentMilestones,
    };
    setSaving(true);
    try {
      const { data } = await adminApi.updateContract(contract.id, payload);
      setContract(data);
      setForm(toEditForm(data));
      setEditing(false);
      toast({ title: t("form.updated") });
      await refreshContract();
    } catch (err) {
      const code = (err as { response?: { data?: { code?: string } } })?.response?.data?.code;
      setFormError(
        code === "CONTRACT_REMINDER_DIRECTION_LOCKED"
          ? t("contractReminder.directionLocked")
          : code === "CONTRACT_CARE_DIRECTION_LOCKED"
            ? t("contracts.care.directionLocked")
            : (getErrorMessage(err) ?? t("common.error")),
      );
      if (isConcurrencyConflict(err)) await refreshContract();
    } finally {
      setSaving(false);
    }
  }, [contract, form, refreshContract, scopedDirection, t, toast]);

  if (!Number.isFinite(idNum)) {
    return (
      <AdminLayout>
        <PageError message={t("contracts.detail.notFound")} />
      </AdminLayout>
    );
  }

  if (loading) {
    return (
      <AdminLayout>
        <PageLoading />
      </AdminLayout>
    );
  }

  if (loadError || !contract) {
    return (
      <AdminLayout>
        <PageError message={loadError ?? t("contracts.detail.notFound")} />
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4">
        {isFinanceWorkspace && (
          <div className="flex flex-col gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-900 sm:flex-row sm:items-center sm:justify-between">
            <p>{t("contracts.finance.masterManagedElsewhere")}</p>
            {has(ADMIN_PERMS.contracts) && (
              <Button asChild variant="outline" size="sm" className="shrink-0 bg-card">
                <Link to={`/admin/contracts/${contract.id}`}>
                  {t("contracts.finance.openMasterRecord")}
                </Link>
              </Button>
            )}
          </div>
        )}
        <ContractHeader
          contract={contract}
          returnTo={returnTo}
          canEdit={canManageMaster}
          canTransition={canManageMaster}
          editing={editing}
          saving={saving}
          onEditInfo={() => void beginEdit()}
          onCancelEdit={cancelEdit}
          onSave={() => void saveEdit()}
          onTransition={handleTransition}
          transitionBusy={transitionBusy}
        />
        {contract.direction === "Upstream" && (
          <ContractFinanceReviewLink quoteId={contract.quoteId} />
        )}
        <LegalActivationWizard
          contract={contract}
          open={legalWizardOpen}
          onOpenChange={setLegalWizardOpen}
          onActivated={refreshContract}
        />

        {supplementalError && (
          <div
            role="alert"
            className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
          >
            {supplementalError}
          </div>
        )}

        <Tabs value={tab} onValueChange={(v) => setTab(v as TabId)}>
          <ScrollableTabsList previousLabel={t("common.prev")} nextLabel={t("common.next")}>
            <TabsTrigger value="info">{t("contracts.detail.tab.info")}</TabsTrigger>
            <TabsTrigger value="schedule">
              {t(
                contract.direction === "Upstream"
                  ? "contracts.detail.tab.receivableSchedule"
                  : "contracts.detail.tab.payableSchedule",
              )}
              {contract.paymentMilestones.length > 0 ? (
                <span className="ml-1 text-xs text-muted-foreground">
                  ({contract.paymentMilestones.length})
                </span>
              ) : null}
            </TabsTrigger>
            <TabsTrigger value="appendices">
              {t("contracts.detail.tab.appendices")}
              {appendices.length > 0 ? (
                <span className="ml-1 text-xs text-muted-foreground">({appendices.length})</span>
              ) : null}
            </TabsTrigger>
            <TabsTrigger value="documents">
              {t("contracts.detail.tab.documents")}
              {attachments.length > 0 ? (
                <span className="ml-1 text-xs text-muted-foreground">({attachments.length})</span>
              ) : null}
            </TabsTrigger>
            {contract.direction === "Upstream" ? (
              <TabsTrigger value="care">{t("contracts.care.title")}</TabsTrigger>
            ) : null}
            <TabsTrigger value="timeline">{t("contracts.detail.tab.timeline")}</TabsTrigger>
          </ScrollableTabsList>

          <TabsContent value="info" className="mt-4">
            {editing && form ? (
              <div data-testid="contract-inline-edit-form">
                <EditInfoTab
                  form={form}
                  customers={customers}
                  projects={projects}
                  classification={classification}
                  lockDirection={scopedDirection !== undefined}
                  error={formError}
                  onChange={setForm}
                />
              </div>
            ) : (
              <>
                <InfoTab
                  contract={contract}
                  onEnsureDesignProject={
                    !isFinanceWorkspace &&
                    has(ADMIN_PERMS.opportunitiesManage) &&
                    has(ADMIN_PERMS.operationalProjectsManage)
                      ? () => void handleEnsureDesignProject()
                      : undefined
                  }
                  ensuringDesignProject={ensuringDesignProject}
                  canViewDesignProject={has(ADMIN_PERMS.designProjects)}
                  canViewLeads={has(ADMIN_PERMS.leads)}
                  canViewTender={has(ADMIN_PERMS.tenders)}
                  onLinkQuote={canLinkQuote ? () => setLinkQuoteOpen(true) : undefined}
                />
                <ContractExecutionProgress
                  contract={contract}
                  onPayment={() => {
                    setTab("schedule");
                    setSearchParams((current) => {
                      current.set("tab", "schedule");
                      return current;
                    });
                  }}
                />
              </>
            )}
          </TabsContent>
          <TabsContent value="schedule" className="mt-4">
            {editing && form ? (
              <div className="space-y-3">
                <EditScheduleTab
                  direction={contract.direction}
                  value={form.value}
                  milestones={form.milestones}
                  onChange={(milestones) => setForm({ ...form, milestones })}
                />
                {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
              </div>
            ) : editingSchedule ? (
              <div className="space-y-3" data-testid="contract-finance-schedule-form">
                <EditScheduleTab
                  direction={contract.direction}
                  value={contract.value}
                  milestones={scheduleDraft}
                  onChange={setScheduleDraft}
                />
                {scheduleError ? <p className="text-sm text-destructive">{scheduleError}</p> : null}
                <div className="flex justify-end gap-2">
                  <Button variant="outline" disabled={scheduleSaving} onClick={cancelScheduleEdit}>
                    {t("common.cancel")}
                  </Button>
                  <Button
                    data-testid="contract-finance-schedule-save"
                    disabled={scheduleSaving}
                    onClick={() => void saveSchedule()}
                  >
                    {scheduleSaving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                    {t("common.save")}
                  </Button>
                </div>
              </div>
            ) : (
              <div className="space-y-3">
                {canManageMilestones ? (
                  <div className="flex justify-end">
                    <Button
                      data-testid="contract-finance-schedule-edit"
                      size="sm"
                      variant="outline"
                      onClick={beginScheduleEdit}
                    >
                      <Pencil className="mr-1 h-4 w-4" />
                      {t("contracts.schedule.edit")}
                    </Button>
                  </div>
                ) : null}
                <ScheduleTab
                  contract={contract}
                  canManage={canManageMilestones}
                  onMilestoneStatus={handleMilestoneStatus}
                  onEditActualPaymentDate={openPaidDateDialog}
                  busyMilestoneId={busyMilestoneId}
                />
              </div>
            )}
          </TabsContent>
          <TabsContent value="appendices" className="mt-4">
            <VoTab
              contract={contract}
              rows={appendices}
              canManage={canManageMaster}
              canDecide={canDecideVo}
              refresh={refreshContract}
            />
          </TabsContent>
          <TabsContent value="documents" className="mt-4">
            <DocumentsTab
              contract={contract}
              rows={attachments}
              canManage={canManageMaster}
              error={attachmentsError}
              refresh={refreshContract}
            />
          </TabsContent>
          {contract.direction === "Upstream" ? (
            <TabsContent value="care" className="mt-4">
              <ContractContinuations
                contract={contract}
                canManage={
                  canManageMaster &&
                  (has(ADMIN_PERMS.contractsViewAll) || contract.ownerUserId === currentUserId)
                }
              />
              <ContractReminders
                key={`reminders-${contract.id}`}
                contractId={contract.id}
                canManage={
                  canManageMaster &&
                  (has(ADMIN_PERMS.contractsViewAll) || contract.ownerUserId === currentUserId)
                }
              />
              <ContractCareActivities
                key={contract.id}
                contractId={contract.id}
                canManage={
                  canManageMaster &&
                  (has(ADMIN_PERMS.contractsViewAll) || contract.ownerUserId === currentUserId)
                }
              />
            </TabsContent>
          ) : null}
          <TabsContent value="timeline" className="mt-4">
            <TimelineTab events={timeline} />
          </TabsContent>
        </Tabs>

        <Dialog
          open={milestoneActionDraft !== null}
          onOpenChange={(open) => (!open ? setMilestoneActionDraft(null) : null)}
        >
          <DialogContent className="w-[95vw] max-w-md">
            <DialogHeader>
              <DialogTitle>
                {milestoneActionDraft?.status === "Paid"
                  ? t(
                      contract.direction === "Upstream"
                        ? "contracts.collection.dateDialogTitle"
                        : "contracts.schedule.paidDateDialogTitle",
                    )
                  : t(
                      contract.direction === "Upstream"
                        ? "contracts.collection.requestDialogTitle"
                        : "contracts.schedule.requestDialogTitle",
                    )}
              </DialogTitle>
              <DialogDescription>
                {t(
                  contract.direction === "Upstream"
                    ? "contracts.collection.dateDialogDescription"
                    : "contracts.schedule.paidDateDialogDescription",
                )}
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-3 py-2">
              <FieldScope className="space-y-1.5">
                <Label>{t("contracts.milestone.responsibleAccountant")} *</Label>
                <SearchableSelect
                  value={
                    milestoneActionDraft?.accountantUserId
                      ? String(milestoneActionDraft.accountantUserId)
                      : ""
                  }
                  onChange={(value) =>
                    setMilestoneActionDraft((current) =>
                      current ? { ...current, accountantUserId: Number(value) } : current,
                    )
                  }
                  ariaLabel={t("people.select")}
                  options={[...accountants.map((person) => personOption(person, t))]}
                />
              </FieldScope>
              {milestoneActionDraft?.status === "Paid" && (
                <div className="space-y-1.5">
                  <Label htmlFor="contract-actual-payment-date">
                    {t(
                      contract.direction === "Upstream"
                        ? "contracts.collection.actualDate"
                        : "contracts.milestone.actualPaymentDate",
                    )}{" "}
                    *
                  </Label>
                  <Input
                    id="contract-actual-payment-date"
                    type="date"
                    value={milestoneActionDraft?.value ?? ""}
                    onChange={(event) =>
                      setMilestoneActionDraft((current) =>
                        current ? { ...current, value: event.target.value } : current,
                      )
                    }
                  />
                </div>
              )}
            </div>
            <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
              <Button type="button" variant="outline" onClick={() => setMilestoneActionDraft(null)}>
                {t("common.cancel")}
              </Button>
              <Button
                type="button"
                disabled={
                  !milestoneActionDraft?.accountantUserId ||
                  (milestoneActionDraft.status === "Paid" && !milestoneActionDraft.value) ||
                  busyMilestoneId !== null
                }
                onClick={() => void confirmPaidDate()}
              >
                {busyMilestoneId !== null ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : null}
                {t(
                  contract.direction === "Upstream"
                    ? "contracts.collection.confirm"
                    : "contracts.schedule.confirmPaid",
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
      {contract && canLinkQuote && (
        <QuoteContractLinkDialog
          mode="fromContract"
          contract={contract}
          open={linkQuoteOpen}
          onOpenChange={setLinkQuoteOpen}
          onLinked={(updated) => setContract(updated)}
        />
      )}
    </AdminLayout>
  );
};

export default ContractDetail;
