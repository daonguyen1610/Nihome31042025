import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  ArrowLeft,
  BriefcaseBusiness,
  CheckCircle2,
  ClipboardList,
  Plus,
  Search,
  RotateCcw,
  Trash2,
  Users,
} from "lucide-react";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import type { WorkPackageApprovalPreview, DeletionImpactResponse } from "@/services/adminApi";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { MoneyInput } from "@/components/ui/money-input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { isValidEmail } from "@/lib/validation";
import {
  adminApi,
  type ContractResponse,
  type CreateDepartmentWorkPackageRequest,
  type DepartmentWorkPackageOptionsResponse,
  type DepartmentWorkPackageResponse,
  type DepartmentWorkTaskResponse,
  type InputPriceOfferResponse,
  type UpsertFinanceReviewRequest,
  type UpsertInputPriceOfferRequest,
  type OperationalProjectListItemResponse,
  type UpsertDepartmentWorkTaskRequest,
  type WorkPackageKind,
  type WorkPackagePurpose,
  type WorkPackageStatus,
} from "@/services/adminApi";

const dateValue = (value?: string | null) => value?.slice(0, 10) ?? "";
const apiDate = (value: string) => `${value}T00:00:00.000Z`;
const emptyOptions: DepartmentWorkPackageOptionsResponse = {
  departments: [],
  users: [],
  kpiDefinitions: [],
  vendors: [],
};
const money = (value: number, currency: string) =>
  `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value)} ${currency}`;
const formatPriceNumber = (value: number) =>
  new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 4 }).format(value);
const newPriceLine = () => ({
  itemCode: "",
  description: "",
  unit: "",
  quantity: 1,
  unitPrice: 0,
  vatPercent: 0,
  sortOrder: 0,
});

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
  const canViewProjects = has(ADMIN_PERMS.operationalProjects);
  const canManage = has(ADMIN_PERMS.workPackagesManage);
  const canAssign = has(ADMIN_PERMS.workPackagesAssign);
  const canProgress = has(ADMIN_PERMS.workPackagesProgress);
  const canVerifyBusiness = has(ADMIN_PERMS.workPackagesVerify);
  const canCreate = canManage && (kind === "Project" || canAssign);

  const [rows, setRows] = useState<DepartmentWorkPackageResponse[]>([]);
  const [detail, setDetail] = useState<DepartmentWorkPackageResponse | null>(null);
  const [options, setOptions] = useState(emptyOptions);
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const search = params.get("search") ?? "";
  const departmentFilter = params.get("departmentId") ?? "";
  const statusFilter = params.get("status") ?? "";
  const purposeFilter = params.get("purpose") ?? "";
  const projectFilter = kind === "Project" ? (params.get("projectId") ?? "") : "";
  const updateFilter = (key: string, value: string) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value) next.set(key, value);
        else next.delete(key);
        return next;
      },
      { replace: true },
    );
  };
  const requestSequence = useRef(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [packageOpen, setPackageOpen] = useState(false);
  const [taskOpen, setTaskOpen] = useState(false);
  const [progressOpen, setProgressOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [approvalPreview, setApprovalPreview] = useState<
    | (WorkPackageApprovalPreview & {
        packageId: number;
        offerId?: number;
        rowVersion: string;
        idempotencyKey: string;
      })
    | null
  >(null);
  const [approvalBusy, setApprovalBusy] = useState(false);
  const [approvalTo, setApprovalTo] = useState("");
  const [approvalCc, setApprovalCc] = useState("");
  const [approvalDirty, setApprovalDirty] = useState(false);
  const [approvalError, setApprovalError] = useState("");
  const [approvalFeedback, setApprovalFeedback] = useState<{
    message: string;
    failed: boolean;
  } | null>(null);
  const [approvalTracking, setApprovalTracking] = useState<{
    packageId: number;
    recordId: number;
    type: string;
  } | null>(null);
  const [approvalDeliveries, setApprovalDeliveries] = useState<
    DepartmentWorkPackageResponse["approvalEmails"]
  >([]);
  const approvalNoticeRef = useRef<HTMLDivElement>(null);
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [taskEditing, setTaskEditing] = useState<DepartmentWorkTaskResponse | null>(null);
  const [progressTaskEditing, setProgressTaskEditing] = useState<DepartmentWorkTaskResponse | null>(
    null,
  );
  const [progressPercent, setProgressPercent] = useState(0);
  const [rejectTaskEditing, setRejectTaskEditing] = useState<DepartmentWorkTaskResponse | null>(
    null,
  );
  const [rejectReason, setRejectReason] = useState("");
  const [priceOpen, setPriceOpen] = useState(false);
  const [priceEditing, setPriceEditing] = useState<{ id: number; rowVersion: string } | null>(null);
  const [priceForm, setPriceForm] = useState<UpsertInputPriceOfferRequest>({
    vendorId: 0,
    supplierReference: "",
    currency: "VND",
    validUntil: "",
    note: "",
    lines: [newPriceLine()],
  });
  const [reviewForm, setReviewForm] = useState<UpsertFinanceReviewRequest>({
    contractConditionsReviewed: false,
    guaranteesReviewed: false,
    paymentTermsReviewed: false,
    financialDossierReviewed: false,
    findings: "",
  });
  const [packageForm, setPackageForm] = useState<CreateDepartmentWorkPackageRequest>({
    kind,
    purpose: "General",
    name: "",
    description: "",
    operationalProjectId: null,
    contractId: null,
    sourceOpportunityId: null,
    sourceQuoteId: null,
    sourceTenderId: null,
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
    const request = ++requestSequence.current;
    setLoading(true);
    setError(null);
    try {
      if (packageId) {
        const response = await adminApi.getWorkPackage(packageId);
        if (request !== requestSequence.current) return;
        setDetail(response.data);
        setApprovalDeliveries(response.data.approvalEmails ?? []);
      } else {
        setDetail(null);
        const projectIdFilter = Number(projectFilter);
        const response = await adminApi.listWorkPackages({
          kind,
          search: search || undefined,
          operationalProjectId:
            Number.isFinite(projectIdFilter) && projectIdFilter > 0 ? projectIdFilter : undefined,
          departmentRoleGroupId:
            Number(departmentFilter) > 0 ? Number(departmentFilter) : undefined,
          status: (statusFilter || undefined) as WorkPackageStatus | undefined,
          purpose: (purposeFilter || undefined) as WorkPackagePurpose | undefined,
          pageSize: 100,
        });
        if (request !== requestSequence.current) return;
        setRows(response.data.items);
      }
    } catch (cause) {
      if (request === requestSequence.current)
        setError(extractApiError(cause, t("workPackages.error.load")));
    } finally {
      if (request === requestSequence.current) setLoading(false);
    }
  }, [kind, packageId, projectFilter, departmentFilter, statusFilter, purposeFilter, search, t]);

  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    setApprovalTracking(null);
    setApprovalFeedback(null);
  }, [packageId]);
  useEffect(() => {
    if (!approvalTracking || approvalTracking.packageId !== packageId) return;
    let active = true;
    let timer: ReturnType<typeof setTimeout>;
    const deadline = Date.now() + 120_000;
    const check = async () => {
      try {
        const { data } = await adminApi.getWorkPackage(approvalTracking.packageId);
        if (!active) return;
        setApprovalDeliveries(data.approvalEmails);
        const message = data.approvalEmails.find(
          (item) =>
            item.recordType === approvalTracking.type &&
            item.recordId === approvalTracking.recordId,
        );
        if (message?.status === "Sent" || message?.status === "NeedsReview") {
          const failed = message.status === "NeedsReview";
          const title = t(failed ? "approvalEmail.deliveryFailed" : "approvalEmail.delivered");
          setApprovalFeedback({ message: title, failed });
          toast({ title, variant: failed ? "destructive" : "default" });
          return;
        }
      } catch {
        // Keep the queued confirmation visible; the delivery list can be refreshed
        // if connectivity is unavailable during this short polling window.
      }
      if (active && Date.now() < deadline) timer = setTimeout(() => void check(), 3000);
    };
    void check();
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [approvalTracking, packageId, t, toast]);
  useEffect(() => {
    if (!detail) return;
    const taskId = params.get("taskId");
    if (taskId)
      document
        .getElementById(`task-${taskId}`)
        ?.scrollIntoView({ behavior: "smooth", block: "center" });
  }, [detail, params]);
  useEffect(() => {
    adminApi
      .getWorkPackageDepartments()
      .then((response) => setOptions((current) => ({ ...current, departments: response.data })))
      .catch(() => undefined);
    if (canManage || canAssign)
      adminApi
        .getWorkPackageOptions()
        .then((response) => setOptions(response.data))
        .catch(() => undefined);
    if (canViewProjects) {
      const loadProjects = async () => {
        const allProjects: OperationalProjectListItemResponse[] = [];
        let page = 1;
        while (true) {
          const { data } = await adminApi.listOperationalProjects({ page, pageSize: 100 });
          allProjects.push(...data.items);
          if (allProjects.length >= data.total || data.items.length === 0) break;
          page += 1;
        }
        setProjects(allProjects);
      };
      void loadProjects().catch(() => undefined);
    }
  }, [canViewProjects, canManage, canAssign]);
  useEffect(() => {
    const projectIdValue = packageForm.operationalProjectId;
    if (!projectIdValue) {
      setContracts([]);
      return;
    }
    adminApi
      .listContracts({ operationalProjectId: projectIdValue, pageSize: 100 })
      .then((response) => setContracts(response.data.items))
      .catch(() => setContracts([]));
  }, [packageForm.operationalProjectId]);
  useEffect(() => {
    if (!packageId && params.get("create") === "1" && canManage) {
      const project = Number(params.get("projectId"));
      const opportunity = Number(params.get("opportunityId"));
      const quote = Number(params.get("quoteId"));
      const tender = Number(params.get("tenderId"));
      const requestedPurpose = params.get("purpose") as WorkPackagePurpose | null;
      if (requestedPurpose === "LegalPermitting") {
        navigate(
          project > 0 ? `/admin/permits?operationalProjectId=${project}` : "/admin/permits",
          { replace: true },
        );
        return;
      }
      const purpose: WorkPackagePurpose = [
        "ProcurementInputPricing",
        "FinanceCommercialReview",
        "FinanceTenderReview",
        "TenderPlanning",
      ].includes(requestedPurpose ?? "")
        ? requestedPurpose!
        : tender > 0
          ? "TenderPlanning"
          : "General";
      const expectedCode =
        purpose === "ProcurementInputPricing"
          ? "PROCUREMENT"
          : purpose.startsWith("Finance")
            ? "FINANCE"
            : "";
      const expectedDepartment = options.departments.find((group) => group.code === expectedCode);
      setPackageForm((current) => ({
        ...current,
        kind: "Project",
        purpose,
        operationalProjectId: Number.isFinite(project) && project > 0 ? project : null,
        sourceOpportunityId: Number.isFinite(opportunity) && opportunity > 0 ? opportunity : null,
        sourceQuoteId: Number.isFinite(quote) && quote > 0 ? quote : null,
        sourceTenderId: Number.isFinite(tender) && tender > 0 ? tender : null,
        departmentRoleGroupId: expectedDepartment?.id ?? current.departmentRoleGroupId,
        leadUserId: 0,
      }));
      setPackageOpen(true);
    }
  }, [canManage, navigate, options.departments, packageId, params]);
  useEffect(() => {
    if (!detail?.financeReview) return;
    setReviewForm({
      contractConditionsReviewed: detail.financeReview.contractConditionsReviewed,
      guaranteesReviewed: detail.financeReview.guaranteesReviewed,
      paymentTermsReviewed: detail.financeReview.paymentTermsReviewed,
      financialDossierReviewed: detail.financeReview.financialDossierReviewed,
      findings: detail.financeReview.findings ?? "",
      rowVersion: detail.financeReview.rowVersion,
    });
  }, [detail?.financeReview]);

  const departmentUsers = useMemo(
    () =>
      options.users.filter(
        (user) => user.roleGroupId === packageForm.departmentRoleGroupId && user.canLead,
      ),
    [options.users, packageForm.departmentRoleGroupId],
  );
  const taskUsers = useMemo(
    () =>
      options.users.filter(
        (user) =>
          user.roleGroupId === (taskForm.departmentRoleGroupId || detail?.departmentRoleGroupId),
      ),
    [detail?.departmentRoleGroupId, options.users, taskForm.departmentRoleGroupId],
  );
  const availableDepartments = useMemo(
    () =>
      packageForm.kind === "Project"
        ? options.departments.filter(
            (group) => packageForm.purpose === "TenderPlanning" || group.canReceiveProjectPackage,
          )
        : options.departments,
    [options.departments, packageForm.kind, packageForm.purpose],
  );

  const openCreatePackage = () => {
    setPackageForm({
      kind,
      purpose: "General",
      name: "",
      description: "",
      operationalProjectId: null,
      contractId: null,
      sourceOpportunityId: null,
      sourceQuoteId: null,
      sourceTenderId: null,
      departmentRoleGroupId: 0,
      participantDepartmentRoleGroupIds: [],
      leadUserId: 0,
      plannedStart: null,
      plannedEnd: null,
    });
    setPackageOpen(true);
  };

  const savePackage = async () => {
    if (
      packageForm.name.trim().length < 2 ||
      packageForm.name.length > 300 ||
      !packageForm.departmentRoleGroupId ||
      !packageForm.leadUserId ||
      (packageForm.kind === "Project" && !packageForm.operationalProjectId) ||
      (packageForm.purpose === "TenderPlanning" &&
        (!packageForm.plannedStart || !packageForm.plannedEnd)) ||
      (packageForm.plannedStart &&
        packageForm.plannedEnd &&
        packageForm.plannedEnd < packageForm.plannedStart)
    ) {
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
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    } finally {
      setSaving(false);
    }
  };

  const openTask = (task?: DepartmentWorkTaskResponse) => {
    setTaskEditing(task ?? null);
    setTaskForm(
      task
        ? {
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
          }
        : {
            wbsCode: "",
            title: "",
            description: "",
            resources: "",
            departmentRoleGroupId:
              detail?.kind === "Internal" ? null : detail?.departmentRoleGroupId,
            assigneeUserId: 0,
            approverUserId: null,
            kpiDefinitionId: null,
            plannedStart: dateValue(detail?.plannedStart),
            plannedEnd: dateValue(detail?.plannedEnd),
            ccUserIds: [],
            predecessorTaskIds: [],
          },
    );
    setTaskOpen(true);
  };

  const previewDelete = async () => {
    if (!detail) return;
    setDeleteOpen(true);
    setDeleteLoading(true);
    setDeleteError(null);
    setDeleteImpact(null);
    try {
      setDeleteImpact((await adminApi.getWorkPackageDeletionImpact(detail.id)).data);
    } catch (cause) {
      setDeleteError(extractApiError(cause));
    } finally {
      setDeleteLoading(false);
    }
  };

  const confirmDelete = async (confirmation: string) => {
    if (!detail || !deleteImpact) return null;
    setDeleting(true);
    setDeleteError(null);
    try {
      await adminApi.deleteWorkPackage(
        detail.id,
        { confirmation, planToken: deleteImpact.planToken, rowVersion: detail.rowVersion },
        newIdempotencyKey(),
      );
      return null;
    } catch (cause) {
      setDeleteError(extractApiError(cause));
      throw cause;
    } finally {
      setDeleting(false);
    }
  };

  const saveTask = async () => {
    if (
      !detail ||
      !taskForm.wbsCode.trim() ||
      taskForm.title.trim().length < 2 ||
      !taskForm.assigneeUserId ||
      !taskForm.plannedStart ||
      !taskForm.plannedEnd ||
      taskForm.plannedEnd < taskForm.plannedStart ||
      taskForm.assigneeUserId === taskForm.approverUserId ||
      (detail.purpose === "TenderPlanning" &&
        taskForm.approverUserId == null &&
        taskForm.assigneeUserId === detail.leadUserId)
    ) {
      toast({ variant: "destructive", title: t("workPackages.validation.task") });
      return;
    }
    setSaving(true);
    const body = {
      ...taskForm,
      plannedStart: apiDate(taskForm.plannedStart),
      plannedEnd: apiDate(taskForm.plannedEnd),
    };
    try {
      if (taskEditing)
        await adminApi.updateWorkTask(detail.id, taskEditing.id, body, newIdempotencyKey());
      else await adminApi.addWorkTask(detail.id, body, newIdempotencyKey());
      setTaskOpen(false);
      toast({
        title: t(taskEditing ? "workPackages.toast.taskUpdated" : "workPackages.toast.taskCreated"),
      });
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    } finally {
      setSaving(false);
    }
  };

  const transitionPackage = async (status: WorkPackageStatus) => {
    if (!detail) return;
    try {
      await adminApi.updateWorkPackage(
        detail.id,
        {
          name: detail.name,
          description: detail.description,
          contractId: detail.contractId,
          status,
          plannedStart: detail.plannedStart,
          plannedEnd: detail.plannedEnd,
          rowVersion: detail.rowVersion,
        },
        newIdempotencyKey(),
      );
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
  };

  const progressTask = async (
    task: DepartmentWorkTaskResponse,
    complete = false,
    percent?: number,
  ) => {
    if (!detail) return;
    const nextProgress = complete
      ? 100
      : Math.min(99, Math.max(1, percent ?? task.progressPercent));
    try {
      await adminApi.updateWorkTaskProgress(
        detail.id,
        task.id,
        {
          progressPercent: nextProgress,
          status: complete ? "Completed" : "InProgress",
          rowVersion: task.rowVersion,
        },
        newIdempotencyKey(),
      );
      setProgressOpen(false);
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
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
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
  };

  const rejectTask = async () => {
    if (!detail || !rejectTaskEditing || !rejectReason.trim()) return;
    try {
      await adminApi.rejectWorkTask(
        detail.id,
        rejectTaskEditing.id,
        rejectTaskEditing.rowVersion,
        rejectReason.trim(),
        newIdempotencyKey(),
      );
      setRejectTaskEditing(null);
      setRejectReason("");
      toast({ title: t("workPackages.toast.returned") });
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
  };

  const resetPriceForm = () => {
    setPriceEditing(null);
    setPriceForm({
      vendorId: 0,
      supplierReference: "",
      currency: "VND",
      validUntil: "",
      note: "",
      lines: [newPriceLine()],
    });
  };
  const openPriceOffer = (offer?: InputPriceOfferResponse) => {
    if (!offer) {
      resetPriceForm();
      setPriceOpen(true);
      return;
    }
    setPriceEditing({ id: offer.id, rowVersion: offer.rowVersion });
    setPriceForm({
      vendorId: offer.vendorId,
      supplierReference: offer.supplierReference,
      currency: offer.currency,
      validUntil: dateValue(offer.validUntil),
      note: offer.note ?? "",
      lines: offer.lines.map((line) => ({
        itemCode: line.itemCode,
        description: line.description,
        unit: line.unit,
        quantity: line.quantity,
        unitPrice: line.unitPrice,
        vatPercent: line.vatPercent,
        sortOrder: line.sortOrder,
      })),
    });
    setPriceOpen(true);
  };
  const savePriceOffer = async () => {
    if (
      !detail ||
      !priceForm.vendorId ||
      !priceForm.supplierReference.trim() ||
      !priceForm.validUntil ||
      priceForm.lines.some(
        (line) =>
          !line.itemCode.trim() ||
          !line.description.trim() ||
          !line.unit.trim() ||
          !Number.isFinite(line.quantity) ||
          line.quantity < 0.0001 ||
          line.quantity > 999999999999 ||
          !Number.isFinite(line.unitPrice) ||
          line.unitPrice < 0 ||
          line.unitPrice > 999999999999 ||
          !Number.isFinite(line.vatPercent) ||
          line.vatPercent < 0 ||
          line.vatPercent > 100,
      )
    ) {
      toast({ variant: "destructive", title: t("workPackages.inputPrice.validation") });
      return;
    }
    setSaving(true);
    const body: UpsertInputPriceOfferRequest = {
      ...priceForm,
      validUntil: `${priceForm.validUntil}T23:59:59.000Z`,
    };
    try {
      if (priceEditing)
        await adminApi.updateInputPriceOffer(
          detail.id,
          priceEditing.id,
          { ...body, rowVersion: priceEditing.rowVersion },
          newIdempotencyKey(),
        );
      else await adminApi.addInputPriceOffer(detail.id, body, newIdempotencyKey());
      setPriceOpen(false);
      resetPriceForm();
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    } finally {
      setSaving(false);
    }
  };

  const previewApproval = async (rowVersion: string, offerId?: number) => {
    if (!detail || approvalBusy) return;
    setApprovalBusy(true);
    try {
      const { data } = await adminApi.previewWorkPackageApprovalEmail(detail.id, offerId);
      setApprovalPreview({
        ...data,
        packageId: detail.id,
        offerId,
        rowVersion,
        idempotencyKey: newIdempotencyKey(),
      });
      setApprovalTo(data.toEmail);
      setApprovalCc((data.ccEmails ?? []).join(", "));
      setApprovalDirty(false);
      setApprovalError("");
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    } finally {
      setApprovalBusy(false);
    }
  };
  const submitPriceOffer = (offerId: number, rowVersion: string) =>
    previewApproval(rowVersion, offerId);
  const ccAddresses = approvalCc
    .split(/[;,\n]+/)
    .map((address) => address.trim())
    .filter(Boolean);
  const recipientsValid =
    Boolean(approvalTo.trim()) &&
    isValidEmail(approvalTo) &&
    ccAddresses.length <= 20 &&
    ccAddresses.every((address) => isValidEmail(address));
  const refreshApprovalPreview = async () => {
    if (!approvalPreview || approvalBusy || !recipientsValid) return;
    setApprovalBusy(true);
    setApprovalError("");
    try {
      const { data } = await adminApi.previewWorkPackageApprovalEmail(
        approvalPreview.packageId,
        approvalPreview.offerId,
        { toEmail: approvalTo.trim(), ccEmails: ccAddresses },
      );
      setApprovalPreview({ ...approvalPreview, ...data, idempotencyKey: newIdempotencyKey() });
      setApprovalTo(data.toEmail);
      setApprovalCc((data.ccEmails ?? []).join(", "));
      setApprovalDirty(false);
    } catch (cause) {
      setApprovalError(extractApiError(cause, t("workPackages.error.save")));
    } finally {
      setApprovalBusy(false);
    }
  };
  const confirmApproval = async () => {
    if (!approvalPreview || approvalBusy || approvalDirty || !recipientsValid) return;
    setApprovalBusy(true);
    try {
      const { packageId: id, offerId, rowVersion, previewToken, idempotencyKey } = approvalPreview;
      if (offerId)
        await adminApi.submitInputPriceOffer(id, offerId, rowVersion, idempotencyKey, previewToken);
      else await adminApi.submitFinanceReview(id, rowVersion, idempotencyKey, previewToken);
      setApprovalPreview(null);
      setApprovalFeedback({ message: t("approvalEmail.queued"), failed: false });
      setApprovalTracking({
        packageId: id,
        recordId: offerId ?? detail!.financeReview!.id,
        type: offerId ? "InputPriceOffer" : "FinanceReview",
      });
      toast({ title: t("approvalEmail.queued") });
      await load();
      requestAnimationFrame(() =>
        approvalNoticeRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" }),
      );
    } catch (cause) {
      setApprovalError(extractApiError(cause, t("workPackages.error.save")));
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    } finally {
      setApprovalBusy(false);
    }
  };
  const decidePriceOffer = async (offerId: number, approved: boolean, rowVersion: string) => {
    if (!detail) return;
    const reason = approved ? null : window.prompt(t("workPackages.decision.reason"));
    if (!approved && !reason?.trim()) return;
    try {
      await adminApi.decideInputPriceOffer(
        detail.id,
        offerId,
        approved,
        reason,
        rowVersion,
        newIdempotencyKey(),
      );
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
  };
  const saveFinanceReview = async () => {
    if (!detail) return;
    try {
      await adminApi.saveFinanceReview(detail.id, reviewForm, newIdempotencyKey());
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
  };
  const submitFinanceReview = async () => {
    if (detail?.financeReview) await previewApproval(detail.financeReview.rowVersion);
  };
  const decideFinanceReview = async (approved: boolean) => {
    if (!detail?.financeReview) return;
    const reason = approved ? null : window.prompt(t("workPackages.decision.reason"));
    if (!approved && !reason?.trim()) return;
    try {
      await adminApi.decideFinanceReview(
        detail.id,
        approved,
        reason,
        detail.financeReview.rowVersion,
        newIdempotencyKey(),
      );
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("workPackages.error.save")),
      });
    }
  };

  if (loading && packageId)
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

  return (
    <AdminLayout>
      <DeletionImpactDialog
        open={deleteOpen}
        impact={deleteImpact}
        loading={deleteLoading}
        deleting={deleting}
        error={deleteError}
        onOpenChange={setDeleteOpen}
        onConfirm={confirmDelete}
        onCompleted={() => {
          setDeleteOpen(false);
          navigate("/admin/work-packages");
        }}
      />
      <div className="space-y-6" data-testid="work-packages-page">
        {approvalFeedback && (
          <div
            ref={approvalNoticeRef}
            role={approvalFeedback.failed ? "alert" : "status"}
            data-testid="approval-email-feedback"
            className={`rounded-lg border p-4 text-sm ${approvalFeedback.failed ? "border-destructive text-destructive" : "border-emerald-300 bg-emerald-50 text-emerald-900"}`}
          >
            {approvalFeedback.message}
          </div>
        )}
        {detail ? (
          <>
            <div className="flex min-w-0 flex-col gap-4" data-testid="work-package-detail-header">
              <div className="min-w-0 w-full space-y-2">
                <Button variant="ghost" asChild className="-ml-3">
                  <Link data-testid="work-package-back" to="/admin/work-packages">
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    {t("workPackages.back")}
                  </Link>
                </Button>
                <div className="flex flex-wrap items-center gap-2">
                  <h1 className="min-w-0 max-w-full text-2xl font-bold [overflow-wrap:anywhere]">
                    {detail.code} · {detail.name}
                  </h1>
                  <Badge className={statusTone[detail.status]} variant="outline">
                    {t(`workPackages.status.${detail.status}`)}
                  </Badge>
                  <Badge variant="secondary">{t(`workPackages.purpose.${detail.purpose}`)}</Badge>
                </div>
                <p className="text-sm text-muted-foreground">
                  {detail.description || t("workPackages.noDescription")}
                </p>
              </div>
              <div className="flex min-w-0 flex-wrap gap-2 [&_button]:h-auto [&_button]:min-h-10 [&_button]:whitespace-normal">
                {canManage && detail.canManagePackage && detail.status === "Completed" && (
                  <Button onClick={() => void transitionPackage("Active")}>
                    <RotateCcw className="mr-2 h-4 w-4" />
                    {t("workPackages.action.reopen")}
                  </Button>
                )}
                {canManage && detail.canManagePackage && (
                  <Button variant="destructive" onClick={() => void previewDelete()}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    {t("common.delete")}
                  </Button>
                )}
                {detail.canManagePackage && detail.status === "Planned" && (
                  <Button onClick={() => void transitionPackage("Active")}>
                    {t("workPackages.action.start")}
                  </Button>
                )}
                {detail.canManagePackage && detail.status === "Active" && (
                  <Button onClick={() => void transitionPackage("Completed")}>
                    {t("workPackages.action.completePackage")}
                  </Button>
                )}
                {canAssign &&
                  detail.canAssignTasks &&
                  detail.purpose !== "LegalPermitting" &&
                  !["Completed", "Cancelled"].includes(detail.status) && (
                    <Button onClick={() => openTask()}>
                      <Plus className="mr-2 h-4 w-4" />
                      {t("workPackages.action.addTask")}
                    </Button>
                  )}
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Summary
                icon={<BriefcaseBusiness />}
                label={t("workPackages.field.scope")}
                value={
                  detail.kind === "Project"
                    ? `${detail.operationalProjectCode} · ${detail.operationalProjectName}`
                    : t("workPackages.kind.Internal")
                }
                href={
                  detail.operationalProjectId
                    ? `/admin/operational-projects/${detail.operationalProjectId}`
                    : undefined
                }
              />
              <Summary
                icon={<Users />}
                label={t("workPackages.field.department")}
                value={t(detail.departmentLabelKey)}
              />
              <Summary
                icon={<Users />}
                label={t("workPackages.field.lead")}
                value={detail.leadUserName}
              />
              <Summary
                icon={<ClipboardList />}
                label={t("workPackages.field.contract")}
                value={detail.contractNumber || "—"}
                href={detail.contractId ? `/admin/contracts/${detail.contractId}` : undefined}
              />
            </div>

            {detail.purpose === "ProcurementInputPricing" && (
              <section className="rounded-xl border bg-card">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
                  <div>
                    <h2 className="font-semibold">{t("workPackages.inputPrice.title")}</h2>
                    <p className="text-sm text-muted-foreground">
                      {t("workPackages.inputPrice.help")}
                    </p>
                  </div>
                  {canProgress && !["Completed", "Cancelled"].includes(detail.status) && (
                    <Button onClick={() => openPriceOffer()}>
                      <Plus className="mr-2 h-4 w-4" />
                      {t("workPackages.inputPrice.add")}
                    </Button>
                  )}
                </div>
                <div className="divide-y">
                  {detail.inputPriceOffers.map((offer) => (
                    <article key={offer.id} className="space-y-3 p-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <div className="flex flex-wrap items-center gap-2">
                            <strong>
                              {offer.vendorCode} · {offer.vendorName}
                            </strong>
                            <Badge variant="outline">
                              {t(`workPackages.businessStatus.${offer.status}`)}
                            </Badge>
                          </div>
                          <p className="text-sm text-muted-foreground">
                            {offer.supplierReference} · {t("workPackages.inputPrice.validUntil")}{" "}
                            {dateValue(offer.validUntil)}
                          </p>
                        </div>
                        <strong>{money(offer.grandTotal, offer.currency)}</strong>
                      </div>
                      <div className="overflow-x-auto">
                        <table className="w-full min-w-[640px] text-sm">
                          <thead>
                            <tr className="text-left text-muted-foreground">
                              <th className="py-2">{t("workPackages.inputPrice.item")}</th>
                              <th>{t("workPackages.inputPrice.unit")}</th>
                              <th className="text-right">
                                {t("workPackages.inputPrice.quantity")}
                              </th>
                              <th className="text-right">
                                {t("workPackages.inputPrice.unitPrice")}
                              </th>
                              <th className="text-right">{t("workPackages.inputPrice.amount")}</th>
                            </tr>
                          </thead>
                          <tbody>
                            {offer.lines.map((line) => (
                              <tr key={line.id} className="border-t">
                                <td className="py-2">
                                  {line.itemCode} · {line.description}
                                </td>
                                <td>{line.unit}</td>
                                <td className="text-right">
                                  {new Intl.NumberFormat("vi-VN").format(line.quantity)}
                                </td>
                                <td className="text-right">
                                  {money(line.unitPrice, offer.currency)}
                                </td>
                                <td className="text-right">{money(line.amount, offer.currency)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                      <div className="flex flex-wrap justify-end gap-2">
                        {canProgress && ["Draft", "Rejected"].includes(offer.status) && (
                          <Button size="sm" variant="outline" onClick={() => openPriceOffer(offer)}>
                            {t("common.edit")}
                          </Button>
                        )}
                        {canProgress && ["Draft", "Rejected"].includes(offer.status) && (
                          <Button
                            size="sm"
                            onClick={() => void submitPriceOffer(offer.id, offer.rowVersion)}
                          >
                            {t("workPackages.action.submit")}
                          </Button>
                        )}
                        {canVerifyBusiness && offer.status === "Submitted" && (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                void decidePriceOffer(offer.id, false, offer.rowVersion)
                              }
                            >
                              {t("common.reject")}
                            </Button>
                            <Button
                              size="sm"
                              onClick={() =>
                                void decidePriceOffer(offer.id, true, offer.rowVersion)
                              }
                            >
                              {t("common.approve")}
                            </Button>
                          </>
                        )}
                      </div>
                    </article>
                  ))}
                  {detail.inputPriceOffers.length === 0 && (
                    <p className="p-8 text-center text-sm text-muted-foreground">
                      {t("workPackages.inputPrice.empty")}
                    </p>
                  )}
                </div>
              </section>
            )}

            {(detail.purpose === "FinanceCommercialReview" ||
              detail.purpose === "FinanceTenderReview") && (
              <section className="rounded-xl border bg-card p-4">
                <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h2 className="font-semibold">
                      {t(`workPackages.review.title.${detail.purpose}`)}
                    </h2>
                    <p className="text-sm text-muted-foreground">{t("workPackages.review.help")}</p>
                  </div>
                  <Badge variant="outline">
                    {t(`workPackages.businessStatus.${detail.financeReview?.status ?? "Draft"}`)}
                  </Badge>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  {detail.purpose === "FinanceCommercialReview" && (
                    <>
                      <ReviewCheck
                        label={t("workPackages.review.contractConditions")}
                        checked={reviewForm.contractConditionsReviewed}
                        disabled={
                          !canProgress ||
                          detail.financeReview?.status === "Submitted" ||
                          detail.financeReview?.status === "Approved"
                        }
                        onChange={(contractConditionsReviewed) =>
                          setReviewForm((current) => ({ ...current, contractConditionsReviewed }))
                        }
                      />
                      <ReviewCheck
                        label={t("workPackages.review.paymentTerms")}
                        checked={reviewForm.paymentTermsReviewed}
                        disabled={
                          !canProgress ||
                          detail.financeReview?.status === "Submitted" ||
                          detail.financeReview?.status === "Approved"
                        }
                        onChange={(paymentTermsReviewed) =>
                          setReviewForm((current) => ({ ...current, paymentTermsReviewed }))
                        }
                      />
                    </>
                  )}
                  <ReviewCheck
                    label={t("workPackages.review.guarantees")}
                    checked={reviewForm.guaranteesReviewed}
                    disabled={
                      !canProgress ||
                      detail.financeReview?.status === "Submitted" ||
                      detail.financeReview?.status === "Approved"
                    }
                    onChange={(guaranteesReviewed) =>
                      setReviewForm((current) => ({ ...current, guaranteesReviewed }))
                    }
                  />
                  {detail.purpose === "FinanceTenderReview" && (
                    <ReviewCheck
                      label={t("workPackages.review.financialDossier")}
                      checked={reviewForm.financialDossierReviewed}
                      disabled={
                        !canProgress ||
                        detail.financeReview?.status === "Submitted" ||
                        detail.financeReview?.status === "Approved"
                      }
                      onChange={(financialDossierReviewed) =>
                        setReviewForm((current) => ({ ...current, financialDossierReviewed }))
                      }
                    />
                  )}
                  <Field label={t("workPackages.review.findings")} className="sm:col-span-2">
                    <Textarea
                      maxLength={4000}
                      disabled={
                        !canProgress ||
                        detail.financeReview?.status === "Submitted" ||
                        detail.financeReview?.status === "Approved"
                      }
                      value={reviewForm.findings ?? ""}
                      onChange={(event) =>
                        setReviewForm((current) => ({ ...current, findings: event.target.value }))
                      }
                    />
                  </Field>
                </div>
                <div className="mt-4 flex flex-wrap justify-end gap-2">
                  {canProgress &&
                    (!detail.financeReview ||
                      ["Draft", "Rejected"].includes(detail.financeReview.status)) && (
                      <Button variant="outline" onClick={() => void saveFinanceReview()}>
                        {t("common.save")}
                      </Button>
                    )}
                  {canProgress && detail.financeReview?.status === "Draft" && (
                    <Button onClick={() => void submitFinanceReview()}>
                      {t("workPackages.action.submit")}
                    </Button>
                  )}
                  {canVerifyBusiness && detail.financeReview?.status === "Submitted" && (
                    <>
                      <Button variant="outline" onClick={() => void decideFinanceReview(false)}>
                        {t("common.reject")}
                      </Button>
                      <Button onClick={() => void decideFinanceReview(true)}>
                        {t("common.approve")}
                      </Button>
                    </>
                  )}
                </div>
              </section>
            )}

            <section className="rounded-xl border bg-card">
              <div className="flex items-center justify-between border-b p-4">
                <div>
                  <h2 className="font-semibold">{t("workPackages.tasks.title")}</h2>
                  <p className="text-sm text-muted-foreground">{t("workPackages.tasks.help")}</p>
                </div>
                <span className="text-sm font-medium">{detail.progressPercent}%</span>
              </div>
              <div className="p-4">
                <Progress value={detail.progressPercent} />
              </div>
              <div className="divide-y">
                {detail.tasks.map((task) => (
                  <article
                    key={task.id}
                    id={`task-${task.id}`}
                    data-testid="work-task-row"
                    className={params.get("taskId") === String(task.id) ? "bg-amber-50 p-4" : "p-4"}
                  >
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                      <div className="min-w-0 space-y-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <Badge variant="outline">{task.wbsCode}</Badge>
                          <h3 className="font-medium">{task.title}</h3>
                          <Badge variant="secondary">
                            {t(`workPackages.taskStatus.${task.status}`)}
                          </Badge>
                          {task.kpiDefinitionCode && <Badge>{t("workPackages.tasks.kpi")}</Badge>}
                        </div>
                        <p className="text-sm text-muted-foreground">
                          {task.assigneeName} · {dateValue(task.plannedStart)} →{" "}
                          {dateValue(task.plannedEnd)}
                        </p>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="w-12 text-right text-sm font-medium">
                          {task.progressPercent}%
                        </span>
                        {canAssign &&
                          detail.canAssignTasks &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button variant="outline" size="sm" onClick={() => openTask(task)}>
                              {t("common.edit")}
                            </Button>
                          )}
                        {detail.status === "Active" &&
                          task.canUpdateProgress &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button variant="outline" size="sm" onClick={() => openProgress(task)}>
                              {t("workPackages.action.progress")}
                            </Button>
                          )}
                        {detail.status === "Active" &&
                          task.canUpdateProgress &&
                          ["Planned", "InProgress"].includes(task.status) && (
                            <Button size="sm" onClick={() => void progressTask(task, true)}>
                              {t("workPackages.action.completeTask")}
                            </Button>
                          )}
                        {detail.status === "Active" && task.canVerify && (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                setRejectTaskEditing(task);
                                setRejectReason("");
                              }}
                            >
                              {t("common.reject")}
                            </Button>
                            <Button size="sm" onClick={() => void verifyTask(task)}>
                              <CheckCircle2 className="mr-2 h-4 w-4" />
                              {t("workPackages.action.verify")}
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  </article>
                ))}
                {detail.tasks.length === 0 && (
                  <div className="p-10 text-center text-sm text-muted-foreground">
                    {t("workPackages.tasks.empty")}
                  </div>
                )}
              </div>
            </section>
          </>
        ) : (
          <>
            <div className="flex flex-wrap items-end justify-between gap-4">
              <div>
                <h1 className="text-3xl font-bold">{t("workPackages.title")}</h1>
                <p className="mt-1 text-muted-foreground">{t("workPackages.subtitle")}</p>
              </div>
              {canCreate && (
                <Button onClick={openCreatePackage}>
                  <Plus className="mr-2 h-4 w-4" />
                  {t("workPackages.action.create")}
                </Button>
              )}
            </div>
            <Tabs
              value={kind}
              onValueChange={(value) => {
                const next = new URLSearchParams(params);
                next.set("kind", value);
                next.delete("projectId");
                setParams(next);
              }}
            >
              <TabsList>
                <TabsTrigger value="Project">{t("workPackages.tab.project")}</TabsTrigger>
                <TabsTrigger value="Internal">{t("workPackages.tab.internal")}</TabsTrigger>
              </TabsList>
            </Tabs>
            <section
              className="grid gap-3 rounded-xl border bg-card p-4 sm:grid-cols-2 xl:grid-cols-4"
              aria-label={t("workPackages.filters.title")}
            >
              <div className="space-y-1 sm:col-span-2">
                <Label htmlFor="wp-search">{t("workPackages.filters.search")}</Label>
                <div className="relative">
                  <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                  <Input
                    id="wp-search"
                    value={search}
                    onChange={(event) => updateFilter("search", event.target.value)}
                    placeholder={t("workPackages.search")}
                    className="pl-9"
                  />
                </div>
              </div>
              <div className="min-w-0 space-y-1">
                <Label htmlFor="wp-department">{t("workPackages.field.department")}</Label>
                <select
                  id="wp-department"
                  className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                  value={departmentFilter}
                  onChange={(event) => updateFilter("departmentId", event.target.value)}
                >
                  <option value="">{t("workPackages.filters.allDepartments")}</option>
                  {options.departments.map((department) => (
                    <option key={department.id} value={department.id}>
                      {t(department.labelKey)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="min-w-0 space-y-1">
                <Label htmlFor="wp-status">{t("workPackages.filters.status")}</Label>
                <select
                  id="wp-status"
                  className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                  value={statusFilter}
                  onChange={(event) => updateFilter("status", event.target.value)}
                >
                  <option value="">{t("workPackages.filters.allStatuses")}</option>
                  {(Object.keys(statusTone) as WorkPackageStatus[]).map((status) => (
                    <option key={status} value={status}>
                      {t(`workPackages.status.${status}`)}
                    </option>
                  ))}
                </select>
              </div>
              {kind === "Project" && canViewProjects && (
                <div className="min-w-0 space-y-1 sm:col-span-2">
                  <Label htmlFor="wp-project">{t("workPackages.field.project")}</Label>
                  <select
                    id="wp-project"
                    className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                    value={projectFilter}
                    onChange={(event) => updateFilter("projectId", event.target.value)}
                  >
                    <option value="">{t("workPackages.filters.allProjects")}</option>
                    {projects.map((project) => (
                      <option key={project.id} value={project.id}>
                        {project.code} · {project.name}
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <div className="min-w-0 space-y-1">
                <Label htmlFor="wp-purpose">{t("workPackages.field.purpose")}</Label>
                <select
                  id="wp-purpose"
                  className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                  value={purposeFilter}
                  onChange={(event) => updateFilter("purpose", event.target.value)}
                >
                  <option value="">{t("workPackages.filters.allPurposes")}</option>
                  {(
                    [
                      "General",
                      "ProcurementInputPricing",
                      "FinanceCommercialReview",
                      "FinanceTenderReview",
                      "TenderPlanning",
                      "LegalPermitting",
                    ] as WorkPackagePurpose[]
                  ).map((purpose) => (
                    <option key={purpose} value={purpose}>
                      {t(`workPackages.purpose.${purpose}`)}
                    </option>
                  ))}
                </select>
              </div>
              <div className="flex items-end">
                <Button
                  variant="outline"
                  className="w-full"
                  disabled={
                    !search &&
                    !departmentFilter &&
                    !statusFilter &&
                    !purposeFilter &&
                    !projectFilter
                  }
                  onClick={() => setParams(kind === "Internal" ? { kind } : {})}
                >
                  {t("workPackages.filters.clear")}
                </Button>
              </div>
            </section>
            {loading && <PageLoading />}
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {!loading &&
                rows.map((item) => (
                  <button
                    key={item.id}
                    data-testid="work-package-card"
                    type="button"
                    className="rounded-xl border bg-card p-5 text-left transition hover:border-primary/50 hover:shadow-sm"
                    onClick={() => navigate(`/admin/work-packages/${item.id}`)}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <Badge variant="outline">{item.code}</Badge>
                      <Badge className={statusTone[item.status]} variant="outline">
                        {t(`workPackages.status.${item.status}`)}
                      </Badge>
                    </div>
                    <h2 className="mt-4 font-semibold">{item.name}</h2>
                    <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
                      {item.kind === "Project"
                        ? `${item.operationalProjectCode} · ${item.operationalProjectName}`
                        : t("workPackages.kind.Internal")}
                    </p>
                    <div className="mt-4 flex items-center justify-between text-sm">
                      <span>
                        {t(item.departmentLabelKey)} · {item.leadUserName}
                      </span>
                      <strong>{item.progressPercent}%</strong>
                    </div>
                    <Progress value={item.progressPercent} className="mt-2" />
                  </button>
                ))}
              {!loading && rows.length === 0 && (
                <div className="col-span-full rounded-xl border border-dashed p-12 text-center text-muted-foreground">
                  {t("workPackages.empty")}
                </div>
              )}
            </div>
          </>
        )}
      </div>

      <Dialog open={packageOpen} onOpenChange={setPackageOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t("workPackages.create.title")}</DialogTitle>
            <DialogDescription>{t("workPackages.create.help")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t("workPackages.field.type")}>
              <Select
                value={packageForm.kind}
                onValueChange={(value: WorkPackageKind) =>
                  setPackageForm((current) => ({
                    ...current,
                    kind: value,
                    purpose: "General",
                    operationalProjectId: null,
                    contractId: null,
                    sourceOpportunityId: null,
                    sourceQuoteId: null,
                    sourceTenderId: null,
                    departmentRoleGroupId: 0,
                    participantDepartmentRoleGroupIds: [],
                    leadUserId: 0,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Project">{t("workPackages.kind.Project")}</SelectItem>
                  {canAssign && (
                    <SelectItem value="Internal">{t("workPackages.kind.Internal")}</SelectItem>
                  )}
                </SelectContent>
              </Select>
            </Field>
            {packageForm.kind === "Project" && (
              <Field label={t("workPackages.field.purpose")}>
                <Select
                  disabled={packageForm.sourceTenderId != null}
                  value={packageForm.purpose}
                  onValueChange={(purpose: WorkPackagePurpose) => {
                    const code =
                      purpose === "ProcurementInputPricing"
                        ? "PROCUREMENT"
                        : purpose.startsWith("Finance")
                          ? "FINANCE"
                          : "";
                    const department = options.departments.find((group) => group.code === code);
                    setPackageForm((current) => ({
                      ...current,
                      purpose,
                      contractId: purpose === "General" ? current.contractId : null,
                      departmentRoleGroupId: department?.id ?? 0,
                      leadUserId: 0,
                    }));
                  }}
                >
                  <SelectTrigger data-testid="work-package-purpose">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="General">{t("workPackages.purpose.General")}</SelectItem>
                    <SelectItem value="ProcurementInputPricing">
                      {t("workPackages.purpose.ProcurementInputPricing")}
                    </SelectItem>
                    <SelectItem value="FinanceCommercialReview">
                      {t("workPackages.purpose.FinanceCommercialReview")}
                    </SelectItem>
                    <SelectItem value="FinanceTenderReview">
                      {t("workPackages.purpose.FinanceTenderReview")}
                    </SelectItem>
                    {packageForm.sourceTenderId != null && (
                      <SelectItem value="TenderPlanning">
                        {t("workPackages.purpose.TenderPlanning")}
                      </SelectItem>
                    )}
                  </SelectContent>
                </Select>
              </Field>
            )}
            {packageForm.kind === "Project" && (
              <Field label={t("workPackages.field.project")}>
                <Select
                  disabled={packageForm.sourceTenderId != null}
                  value={packageForm.operationalProjectId?.toString() ?? ""}
                  onValueChange={(value) =>
                    setPackageForm((current) => ({
                      ...current,
                      operationalProjectId: Number(value),
                      contractId: null,
                    }))
                  }
                >
                  <SelectTrigger data-testid="work-package-project">
                    <SelectValue placeholder={t("workPackages.placeholder.project")} />
                  </SelectTrigger>
                  <SelectContent>
                    {projects.map((project) => (
                      <SelectItem key={project.id} value={String(project.id)}>
                        {project.code} · {project.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            {packageForm.kind === "Project" && packageForm.purpose === "General" && (
              <Field label={t("workPackages.field.contract")}>
                <Select
                  value={packageForm.contractId?.toString() ?? "none"}
                  onValueChange={(value) =>
                    setPackageForm((current) => ({
                      ...current,
                      contractId: value === "none" ? null : Number(value),
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">{t("workPackages.contract.none")}</SelectItem>
                    {contracts.map((contract) => (
                      <SelectItem key={contract.id} value={String(contract.id)}>
                        {contract.contractNumber}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            <Field label={t("workPackages.field.name")} className="sm:col-span-2">
              <Input
                maxLength={300}
                value={packageForm.name}
                onChange={(event) =>
                  setPackageForm((current) => ({ ...current, name: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.department")}>
              <Select
                disabled={
                  packageForm.purpose !== "General" && packageForm.purpose !== "TenderPlanning"
                }
                value={
                  packageForm.departmentRoleGroupId ? String(packageForm.departmentRoleGroupId) : ""
                }
                onValueChange={(value) =>
                  setPackageForm((current) => ({
                    ...current,
                    departmentRoleGroupId: Number(value),
                    participantDepartmentRoleGroupIds:
                      current.participantDepartmentRoleGroupIds.filter(
                        (id) => id !== Number(value),
                      ),
                    leadUserId: 0,
                  }))
                }
              >
                <SelectTrigger data-testid="work-package-department">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {availableDepartments.map((group) => (
                    <SelectItem key={group.id} value={String(group.id)}>
                      {t(group.labelKey)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label={t("workPackages.field.lead")}>
              <Select
                value={packageForm.leadUserId ? String(packageForm.leadUserId) : ""}
                onValueChange={(value) =>
                  setPackageForm((current) => ({ ...current, leadUserId: Number(value) }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {departmentUsers.map((user) => (
                    <SelectItem key={user.userId} value={String(user.userId)}>
                      {user.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            {packageForm.kind === "Internal" && (
              <Field label={t("workPackages.field.participants")} className="sm:col-span-2">
                <ChoiceGrid
                  emptyLabel={t("workPackages.participants.none")}
                  items={options.departments
                    .filter((group) => group.id !== packageForm.departmentRoleGroupId)
                    .map((group) => ({ id: group.id, label: t(group.labelKey) }))}
                  selected={packageForm.participantDepartmentRoleGroupIds}
                  onChange={(participantDepartmentRoleGroupIds) =>
                    setPackageForm((current) => ({ ...current, participantDepartmentRoleGroupIds }))
                  }
                />
              </Field>
            )}
            <Field label={t("workPackages.field.start")}>
              <Input
                data-testid="work-package-start"
                type="date"
                value={dateValue(packageForm.plannedStart)}
                onChange={(event) =>
                  setPackageForm((current) => ({
                    ...current,
                    plannedStart: event.target.value ? apiDate(event.target.value) : null,
                  }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.end")}>
              <Input
                data-testid="work-package-end"
                type="date"
                value={dateValue(packageForm.plannedEnd)}
                onChange={(event) =>
                  setPackageForm((current) => ({
                    ...current,
                    plannedEnd: event.target.value ? apiDate(event.target.value) : null,
                  }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.description")} className="sm:col-span-2">
              <Textarea
                maxLength={4000}
                value={packageForm.description ?? ""}
                onChange={(event) =>
                  setPackageForm((current) => ({ ...current, description: event.target.value }))
                }
              />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPackageOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={saving} onClick={() => void savePackage()}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {detail && approvalDeliveries.length > 0 && (
        <section className="mx-4 space-y-3 rounded-lg border bg-background p-4 sm:mx-6">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="font-semibold">{t("approvalEmail.delivery")}</h2>
            <Button variant="outline" size="sm" onClick={() => void load()}>
              {t("common.refresh")}
            </Button>
          </div>
          {approvalDeliveries.map((email) => (
            <div
              key={email.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded border p-3 text-sm"
            >
              <div className="min-w-0 space-y-1">
                <p className="break-words font-medium">
                  {email.recordType === "InputPriceOffer"
                    ? detail.inputPriceOffers.find((offer) => offer.id === email.recordId)
                        ?.supplierReference
                    : t(`workPackages.purpose.${detail.purpose}`)}
                </p>
                <p className="break-all text-muted-foreground">{email.toEmail}</p>
                {(email.ccEmails?.length ?? 0) > 0 && (
                  <p className="break-all text-muted-foreground">CC: {email.ccEmails.join(", ")}</p>
                )}
              </div>
              <Badge variant={email.status === "NeedsReview" ? "destructive" : "secondary"}>
                {t(`approvalEmail.status.${email.status}`)}
              </Badge>
            </div>
          ))}
          {approvalDeliveries.some(
            (email) => email.status === "Sending" || email.status === "NeedsReview",
          ) && <p className="text-sm text-muted-foreground">{t("approvalEmail.reviewHint")}</p>}
        </section>
      )}
      <Dialog
        open={!!approvalPreview}
        onOpenChange={(open) => {
          if (!open && !approvalBusy) setApprovalPreview(null);
        }}
      >
        <DialogContent className="flex max-h-[92dvh] w-[calc(100%_-_2rem)] max-w-4xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>{t("approvalEmail.preview")}</DialogTitle>
            <DialogDescription>{t("approvalEmail.confirmHint")}</DialogDescription>
          </DialogHeader>
          {approvalPreview && (
            <div className="min-h-0 space-y-3 overflow-y-auto">
              <div className="space-y-2">
                <Label htmlFor="approval-to">{t("approvalEmail.recipient")}</Label>
                <Input
                  id="approval-to"
                  type="email"
                  maxLength={150}
                  value={approvalTo}
                  disabled={approvalBusy}
                  onChange={(event) => {
                    setApprovalTo(event.target.value);
                    setApprovalDirty(true);
                    setApprovalError("");
                  }}
                />
                <Label htmlFor="approval-cc">{t("approvalEmail.cc")}</Label>
                <Textarea
                  id="approval-cc"
                  rows={2}
                  maxLength={4000}
                  value={approvalCc}
                  disabled={approvalBusy}
                  placeholder="cm@nicon.vn, qs@nicon.vn"
                  aria-describedby="approval-recipient-hint"
                  onChange={(event) => {
                    setApprovalCc(event.target.value);
                    setApprovalDirty(true);
                    setApprovalError("");
                  }}
                />
                <p id="approval-recipient-hint" className="text-xs text-muted-foreground">
                  {t("approvalEmail.recipientHint")}
                </p>
                {!recipientsValid && (
                  <p role="alert" className="text-sm text-destructive">
                    {t("approvalEmail.invalidRecipients")}
                  </p>
                )}
                {approvalDirty && (
                  <p className="text-sm text-muted-foreground">
                    {t("approvalEmail.refreshRequired")}
                  </p>
                )}
                {approvalError && (
                  <p role="alert" className="text-sm text-destructive">
                    {approvalError}
                  </p>
                )}
                <Button
                  variant="outline"
                  disabled={approvalBusy || !recipientsValid}
                  onClick={() => void refreshApprovalPreview()}
                >
                  {t("approvalEmail.refreshPreview")}
                </Button>
              </div>
              <p className="break-words text-sm">
                <strong>{t("contractReminder.subject")}: </strong>
                {approvalPreview.subject}
              </p>
              <iframe
                title={t("approvalEmail.preview")}
                srcDoc={approvalPreview.body}
                sandbox=""
                className="h-96 w-full rounded border bg-white"
              />
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              disabled={approvalBusy}
              onClick={() => setApprovalPreview(null)}
            >
              {t("common.cancel")}
            </Button>
            <Button
              disabled={approvalBusy || approvalDirty || !recipientsValid}
              onClick={() => void confirmApproval()}
            >
              {t("approvalEmail.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <PriceOfferDialog
        open={priceOpen}
        onOpenChange={(value) => {
          setPriceOpen(value);
          if (!value) resetPriceForm();
        }}
        editing={priceEditing !== null}
        form={priceForm}
        setForm={setPriceForm}
        vendors={options.vendors}
        saving={saving}
        onSave={() => void savePriceOffer()}
        t={t}
      />

      <Dialog open={taskOpen} onOpenChange={setTaskOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {t(taskEditing ? "workPackages.task.edit" : "workPackages.task.create")}
            </DialogTitle>
            <DialogDescription>{t("workPackages.task.help")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t("workPackages.field.wbs")}>
              <Input
                maxLength={120}
                value={taskForm.wbsCode}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, wbsCode: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.taskName")}>
              <Input
                maxLength={300}
                value={taskForm.title}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, title: event.target.value }))
                }
              />
            </Field>
            {detail?.kind === "Internal" && (
              <Field label={t("workPackages.field.department")}>
                <Select
                  value={taskForm.departmentRoleGroupId?.toString() ?? ""}
                  onValueChange={(value) =>
                    setTaskForm((current) => ({
                      ...current,
                      departmentRoleGroupId: Number(value),
                      assigneeUserId: 0,
                      approverUserId: null,
                      ccUserIds: [],
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {detail.participantDepartments.map((group) => (
                      <SelectItem key={group.id} value={String(group.id)}>
                        {t(group.labelKey)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            )}
            <Field label={t("workPackages.field.assignee")}>
              <Select
                value={taskForm.assigneeUserId ? String(taskForm.assigneeUserId) : ""}
                onValueChange={(value) =>
                  setTaskForm((current) => ({
                    ...current,
                    assigneeUserId: Number(value),
                    ccUserIds: current.ccUserIds.filter((id) => id !== Number(value)),
                    approverUserId:
                      current.approverUserId === Number(value) ? null : current.approverUserId,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {taskUsers.map((user) => (
                    <SelectItem key={user.userId} value={String(user.userId)}>
                      {user.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field
              label={t(
                detail?.purpose === "TenderPlanning"
                  ? "workPackages.field.independentApprover"
                  : "workPackages.field.approver",
              )}
            >
              <Select
                value={taskForm.approverUserId?.toString() ?? "lead"}
                onValueChange={(value) =>
                  setTaskForm((current) => ({
                    ...current,
                    approverUserId: value === "lead" ? null : Number(value),
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {detail?.leadUserId !== taskForm.assigneeUserId && (
                    <SelectItem value="lead">{t("workPackages.approver.lead")}</SelectItem>
                  )}
                  {taskUsers
                    .filter((user) => user.userId !== taskForm.assigneeUserId)
                    .map((user) => (
                      <SelectItem key={user.userId} value={String(user.userId)}>
                        {user.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label={t("workPackages.field.start")}>
              <Input
                type="date"
                value={taskForm.plannedStart}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, plannedStart: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.end")}>
              <Input
                type="date"
                value={taskForm.plannedEnd}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, plannedEnd: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.kpi")} className="sm:col-span-2">
              <Select
                value={taskForm.kpiDefinitionId?.toString() ?? "none"}
                onValueChange={(value) =>
                  setTaskForm((current) => ({
                    ...current,
                    kpiDefinitionId: value === "none" ? null : Number(value),
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{t("workPackages.kpi.none")}</SelectItem>
                  {options.kpiDefinitions.map((definition) => (
                    <SelectItem key={definition.id} value={String(definition.id)}>
                      {t(definition.nameKey)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label={t("workPackages.field.predecessors")} className="sm:col-span-2">
              <ChoiceGrid
                emptyLabel={t("workPackages.predecessors.none")}
                items={(detail?.tasks ?? [])
                  .filter((task) => task.id !== taskEditing?.id)
                  .map((task) => ({ id: task.id, label: `${task.wbsCode} · ${task.title}` }))}
                selected={taskForm.predecessorTaskIds}
                onChange={(predecessorTaskIds) =>
                  setTaskForm((current) => ({ ...current, predecessorTaskIds }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.cc")} className="sm:col-span-2">
              <ChoiceGrid
                emptyLabel={t("workPackages.cc.none")}
                items={taskUsers
                  .filter((user) => user.userId !== taskForm.assigneeUserId)
                  .map((user) => ({ id: user.userId, label: user.name }))}
                selected={taskForm.ccUserIds}
                onChange={(ccUserIds) => setTaskForm((current) => ({ ...current, ccUserIds }))}
              />
            </Field>
            <Field label={t("workPackages.field.description")} className="sm:col-span-2">
              <Textarea
                maxLength={4000}
                value={taskForm.description ?? ""}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, description: event.target.value }))
                }
              />
            </Field>
            <Field label={t("workPackages.field.resources")} className="sm:col-span-2">
              <Textarea
                maxLength={2000}
                value={taskForm.resources ?? ""}
                onChange={(event) =>
                  setTaskForm((current) => ({ ...current, resources: event.target.value }))
                }
              />
            </Field>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTaskOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={saving} onClick={() => void saveTask()}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={progressOpen} onOpenChange={setProgressOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("workPackages.progress.title")}</DialogTitle>
            <DialogDescription>
              {progressTaskEditing?.wbsCode} · {progressTaskEditing?.title}
            </DialogDescription>
          </DialogHeader>
          <Field label={t("workPackages.progress.percent")}>
            <Input
              type="number"
              inputMode="numeric"
              min={1}
              max={99}
              value={progressPercent}
              onChange={(event) => setProgressPercent(Number(event.target.value))}
            />
            <p className="text-xs text-muted-foreground">{t("workPackages.progress.help")}</p>
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setProgressOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              disabled={!progressTaskEditing || progressPercent < 1 || progressPercent > 99}
              onClick={() =>
                progressTaskEditing &&
                void progressTask(progressTaskEditing, false, progressPercent)
              }
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={rejectTaskEditing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setRejectTaskEditing(null);
            setRejectReason("");
          }
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("workPackages.reject.taskTitle")}</DialogTitle>
            <DialogDescription>
              {rejectTaskEditing?.wbsCode} · {rejectTaskEditing?.title}
            </DialogDescription>
          </DialogHeader>
          <Field label={t("workPackages.reject.reason")}>
            <Textarea
              maxLength={2000}
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
            />
          </Field>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectTaskEditing(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="destructive"
              disabled={!rejectReason.trim()}
              onClick={() => void rejectTask()}
            >
              {t("common.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const Field = ({
  label,
  children,
  className = "",
  htmlFor,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
  htmlFor?: string;
}) => (
  <div className={`min-w-0 space-y-2 ${className}`}>
    <Label htmlFor={htmlFor}>{label}</Label>
    {children}
  </div>
);
const ChoiceGrid = ({
  items,
  selected,
  onChange,
  emptyLabel,
}: {
  items: { id: number; label: string }[];
  selected: number[];
  onChange: (ids: number[]) => void;
  emptyLabel: string;
}) =>
  items.length === 0 ? (
    <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
      {emptyLabel}
    </p>
  ) : (
    <div className="grid max-h-36 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
      {items.map((item) => (
        <label key={item.id} className="flex cursor-pointer items-start gap-2 text-sm">
          <Checkbox
            checked={selected.includes(item.id)}
            onCheckedChange={(checked) =>
              onChange(checked ? [...selected, item.id] : selected.filter((id) => id !== item.id))
            }
          />
          <span>{item.label}</span>
        </label>
      ))}
    </div>
  );
const ReviewCheck = ({
  label,
  checked,
  disabled,
  onChange,
}: {
  label: string;
  checked: boolean;
  disabled: boolean;
  onChange: (checked: boolean) => void;
}) => (
  <label className="flex items-start gap-3 rounded-lg border p-3 text-sm">
    <Checkbox
      checked={checked}
      disabled={disabled}
      onCheckedChange={(value) => onChange(value === true)}
    />
    <span>{label}</span>
  </label>
);

type InputPriceLine = UpsertInputPriceOfferRequest["lines"][number];

const InputPriceNumberField = ({
  id,
  label,
  value,
  min,
  max,
  onChange,
}: {
  id: string;
  label: string;
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
}) => {
  const invalid = !Number.isFinite(value) || value < min || value > max;
  return (
    <Field label={label} htmlFor={id}>
      <MoneyInput
        id={id}
        value={value}
        decimalScale={4}
        formatValue={formatPriceNumber}
        preferDecimalComma
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? `${id}-error` : undefined}
        onChange={onChange}
      />
      {invalid && (
        <p id={`${id}-error`} role="alert" className="break-words text-xs text-destructive">
          {formatPriceNumber(min)} ≤ {label} ≤ {formatPriceNumber(max)}
        </p>
      )}
    </Field>
  );
};

const InputPriceLineEditor = ({
  line,
  index,
  currency,
  canRemove,
  onChange,
  onRemove,
  t,
}: {
  line: InputPriceLine;
  index: number;
  currency: string;
  canRemove: boolean;
  onChange: (patch: Partial<InputPriceLine>) => void;
  onRemove: () => void;
  t: (key: string) => string;
}) => {
  const id = (field: string) => `input-price-${index}-${field}`;
  return (
    <fieldset
      className="min-w-0 rounded-xl border bg-card p-4"
      data-testid={`input-price-line-${index}`}
    >
      <legend className="px-2 text-sm font-semibold">
        {t("workPackages.inputPrice.item")} {index + 1}
      </legend>
      <div className="grid min-w-0 gap-4 sm:grid-cols-2 xl:grid-cols-[minmax(0,3fr)_minmax(5rem,0.75fr)_minmax(7rem,1fr)_minmax(9rem,1.25fr)_minmax(10rem,1.25fr)]">
        <div className="min-w-0 space-y-4 sm:col-span-2 xl:col-span-1">
          <Field label={t("workPackages.inputPrice.itemCode")} htmlFor={id("code")}>
            <Input
              id={id("code")}
              maxLength={120}
              placeholder={t("workPackages.inputPrice.itemCode")}
              value={line.itemCode}
              onChange={(event) => onChange({ itemCode: event.target.value })}
            />
          </Field>
          <Field label={t("workPackages.inputPrice.description")} htmlFor={id("description")}>
            <Textarea
              id={id("description")}
              maxLength={500}
              className="min-h-20"
              placeholder={t("workPackages.inputPrice.description")}
              value={line.description}
              onChange={(event) => onChange({ description: event.target.value })}
            />
          </Field>
        </div>
        <Field label={t("workPackages.inputPrice.unit")} htmlFor={id("unit")}>
          <Input
            id={id("unit")}
            maxLength={30}
            placeholder={t("workPackages.inputPrice.unit")}
            value={line.unit}
            onChange={(event) => onChange({ unit: event.target.value })}
          />
        </Field>
        <InputPriceNumberField
          label={t("workPackages.inputPrice.quantity")}
          id={id("quantity")}
          value={line.quantity}
          min={0.0001}
          max={999999999999}
          onChange={(quantity) => onChange({ quantity })}
        />
        <div className="min-w-0 space-y-4">
          <InputPriceNumberField
            label={t("workPackages.inputPrice.unitPrice")}
            id={id("price")}
            value={line.unitPrice}
            min={0}
            max={999999999999}
            onChange={(unitPrice) => onChange({ unitPrice })}
          />
          <InputPriceNumberField
            label={t("quotes.field.vatPercent")}
            id={id("vat")}
            value={line.vatPercent}
            min={0}
            max={100}
            onChange={(vatPercent) => onChange({ vatPercent })}
          />
        </div>
        <div className="min-w-0 space-y-4">
          <Field label={t("workPackages.inputPrice.amount")} htmlFor={id("amount")}>
            <output
              id={id("amount")}
              htmlFor={`${id("quantity")} ${id("price")}`}
              className="block min-h-10 break-words rounded-md border bg-muted/40 px-3 py-2 text-right font-semibold tabular-nums"
              data-testid={`input-price-amount-${index}`}
            >
              {Number.isFinite(line.quantity * line.unitPrice)
                ? money(line.quantity * line.unitPrice, currency)
                : "—"}
            </output>
          </Field>
          <Button
            type="button"
            variant="outline"
            className="w-full whitespace-normal"
            disabled={!canRemove}
            onClick={onRemove}
            aria-label={`${t("common.delete")} ${index + 1}`}
          >
            <Trash2 className="mr-2 h-4 w-4 shrink-0" />
            {t("common.delete")}
          </Button>
        </div>
      </div>
    </fieldset>
  );
};

const PriceOfferDialog = ({
  open,
  onOpenChange,
  editing,
  form,
  setForm,
  vendors,
  saving,
  onSave,
  t,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: boolean;
  form: UpsertInputPriceOfferRequest;
  setForm: React.Dispatch<React.SetStateAction<UpsertInputPriceOfferRequest>>;
  vendors: DepartmentWorkPackageOptionsResponse["vendors"];
  saving: boolean;
  onSave: () => void;
  t: (key: string) => string;
}) => (
  <Dialog open={open} onOpenChange={onOpenChange}>
    <DialogContent
      className="flex max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-[90rem] flex-col gap-0 overflow-hidden p-0 sm:max-w-[90rem]"
      data-testid="input-price-dialog"
    >
      <DialogHeader className="shrink-0 border-b p-4 pr-12 sm:p-6 sm:pr-12">
        <DialogTitle>
          {t(editing ? "workPackages.inputPrice.edit" : "workPackages.inputPrice.add")}
        </DialogTitle>
        <DialogDescription>{t("workPackages.inputPrice.formHelp")}</DialogDescription>
      </DialogHeader>
      <div
        className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6"
        data-testid="input-price-dialog-body"
      >
        <div className="grid min-w-0 gap-5 sm:grid-cols-2">
          <Field label={t("workPackages.inputPrice.vendor")} htmlFor="input-price-vendor">
            <Select
              value={form.vendorId ? String(form.vendorId) : ""}
              onValueChange={(value) =>
                setForm((current) => ({ ...current, vendorId: Number(value) }))
              }
            >
              <SelectTrigger id="input-price-vendor" className="min-w-0 [&>span]:truncate">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {vendors.length === 0 && (
                  <div className="px-2 py-1.5 text-sm text-muted-foreground">
                    {t("common.noData")}
                  </div>
                )}
                {vendors.map((vendor) => (
                  <SelectItem key={vendor.id} value={String(vendor.id)}>
                    {vendor.code} · {vendor.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label={t("workPackages.inputPrice.reference")} htmlFor="input-price-reference">
            <Input
              id="input-price-reference"
              maxLength={120}
              value={form.supplierReference}
              onChange={(event) =>
                setForm((current) => ({ ...current, supplierReference: event.target.value }))
              }
            />
          </Field>
          <Field label={t("workPackages.inputPrice.currency")} htmlFor="input-price-currency">
            <Input
              id="input-price-currency"
              maxLength={3}
              value={form.currency}
              onChange={(event) =>
                setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))
              }
            />
          </Field>
          <Field label={t("workPackages.inputPrice.validUntil")} htmlFor="input-price-valid-until">
            <Input
              id="input-price-valid-until"
              type="date"
              value={form.validUntil}
              onChange={(event) =>
                setForm((current) => ({ ...current, validUntil: event.target.value }))
              }
            />
          </Field>
          <div className="space-y-3 sm:col-span-2">
            <div className="flex items-center justify-between">
              <Label>{t("workPackages.inputPrice.lines")}</Label>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  setForm((current) => ({ ...current, lines: [...current.lines, newPriceLine()] }))
                }
              >
                <Plus className="mr-1 h-4 w-4" />
                {t("workPackages.inputPrice.addLine")}
              </Button>
            </div>
            {form.lines.map((line, index) => (
              <InputPriceLineEditor
                key={index}
                line={line}
                index={index}
                currency={form.currency}
                canRemove={form.lines.length > 1}
                t={t}
                onChange={(patch) =>
                  setForm((current) => ({
                    ...current,
                    lines: current.lines.map((item, i) =>
                      i === index ? { ...item, ...patch } : item,
                    ),
                  }))
                }
                onRemove={() =>
                  setForm((current) => ({
                    ...current,
                    lines: current.lines.filter((_, i) => i !== index),
                  }))
                }
              />
            ))}
          </div>
          <Field
            label={t("workPackages.field.description")}
            className="sm:col-span-2"
            htmlFor="input-price-note"
          >
            <Textarea
              id="input-price-note"
              maxLength={2000}
              value={form.note ?? ""}
              onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))}
            />
          </Field>
        </div>
      </div>
      <DialogFooter className="shrink-0 border-t bg-background p-4 sm:p-6">
        <Button variant="outline" onClick={() => onOpenChange(false)}>
          {t("common.cancel")}
        </Button>
        <Button disabled={saving} onClick={onSave}>
          {t("common.save")}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
);
const Summary = ({
  icon,
  label,
  value,
  href,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  href?: string;
}) => (
  <div className="rounded-xl border bg-card p-4">
    <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
      <span className="[&>svg]:h-4 [&>svg]:w-4">{icon}</span>
      {label}
    </div>
    {href ? (
      <Link className="font-medium text-primary hover:underline" to={href}>
        {value}
      </Link>
    ) : (
      <p className="font-medium">{value}</p>
    )}
  </div>
);

export default DepartmentWorkPackages;
