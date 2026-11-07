import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { newPriceLine } from "./workPackageFormatting";
import { money } from "./workPackageFormatting";
import { Field, ChoiceGrid, ReviewCheck, PriceOfferDialog, Summary } from "./WorkPackageControls";
import { collectLookupPages } from "@/lib/pagination";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { useConfirm } from "@/components/ui/confirmation";
import { personOption } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { PeopleChecklist } from "@/components/admin/PeopleChecklist";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  ArrowLeft,
  BriefcaseBusiness,
  CheckCircle2,
  ChevronDown,
  ClipboardList,
  Download,
  Loader2,
  Paperclip,
  Plus,
  Search,
  SlidersHorizontal,
  RotateCcw,
  Trash2,
  Upload,
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
import { MultiFilePicker } from "@/components/ui/multi-file-picker";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useAppSelector } from "@/store";
import { useToast } from "@/hooks/use-toast";
import { newIdempotencyKey } from "@/lib/api";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { translateError, useI18n } from "@/lib/i18n";
import { isValidEmail } from "@/lib/validation";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type ContractResponse,
  type CreateDepartmentWorkPackageRequest,
  type DepartmentWorkPackageOptionsResponse,
  type DepartmentWorkPackageResponse,
  type DepartmentWorkTaskResponse,
  type DepartmentWorkTaskAttachmentResponse,
  type InputPriceOfferResponse,
  type UpsertFinanceReviewRequest,
  type UpsertInputPriceOfferRequest,
  type OperationalProjectListItemResponse,
  type ProjectScheduleResponse,
  type UpsertDepartmentWorkTaskRequest,
  type UpdateDepartmentWorkPackageRequest,
  type WorkPackageKind,
  type WorkPackagePurpose,
  type WorkPackageStatus,
} from "@/services/adminApi";

const dateValue = (value?: string | null) => value?.slice(0, 10) ?? "";
function suggestTaskWbsCode(tasks: DepartmentWorkTaskResponse[], parentTaskId: number | null) {
  const parent = tasks.find((task) => task.id === parentTaskId);
  const prefix = parent ? `${parent.wbsCode}.` : "";
  const ordinals = tasks
    .filter((task) => task.parentTaskId === parentTaskId)
    .map((task) => {
      const code = parent ? task.wbsCode.slice(prefix.length) : task.wbsCode;
      return /^\d+$/.test(code) ? Number(code) : 0;
    })
    .filter(Number.isSafeInteger);
  return `${prefix}${Math.max(0, ...ordinals) + 1}`;
}
const apiDate = (value: string) => `${value}T00:00:00.000Z`;
const emptyOptions: DepartmentWorkPackageOptionsResponse = {
  departments: [],
  users: [],
  kpiDefinitions: [],
  vendors: [],
};
const TASK_FILE_EXTENSIONS = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"];
const TASK_FILE_MAX_SIZE = 20 * 1024 * 1024;
const statusTone: Record<WorkPackageStatus, string> = {
  Planned: "border-sky-200 bg-sky-50 text-sky-700",
  Active: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Completed: "border-border bg-muted/30 text-foreground",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

export function useDepartmentWorkPackages() {
  const uiError = useUiError();
  const confirmAction = useConfirm();
  const LIST_PAGE_SIZE = 20;
  const { id } = useParams();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const { toast } = useToast();
  const { has } = usePermissions();
  const packageId = id && /^\d+$/.test(id) ? Number(id) : null;
  const kindFromPath: WorkPackageKind | null = location.pathname.endsWith("/contracts")
    ? "Contract"
    : location.pathname.endsWith("/internal")
      ? "Internal"
      : location.pathname.endsWith("/projects")
        ? "Project"
        : null;
  const kind: WorkPackageKind =
    kindFromPath ??
    (params.get("kind") === "Internal"
      ? "Internal"
      : params.get("kind") === "Contract"
        ? "Contract"
        : "Project");
  const subPages = [
    {
      kind: "Project" as WorkPackageKind,
      path: "/admin/work-packages/projects",
      tabKey: "workPackages.tab.project",
      titleKey: "workPackages.subpage.projectsTitle",
      descriptionKey: "workPackages.subpage.projectsDescription",
    },
    {
      kind: "Contract" as WorkPackageKind,
      path: "/admin/work-packages/contracts",
      tabKey: "workPackages.tab.contract",
      titleKey: "workPackages.subpage.contractsTitle",
      descriptionKey: "workPackages.subpage.contractsDescription",
    },
    {
      kind: "Internal" as WorkPackageKind,
      path: "/admin/work-packages/internal",
      tabKey: "workPackages.tab.internal",
      titleKey: "workPackages.subpage.internalTitle",
      descriptionKey: "workPackages.subpage.internalDescription",
    },
  ];
  const activeSubPage = subPages.find((page) => page.kind === kind) ?? subPages[0];
  const canViewProjects = has(ADMIN_PERMS.operationalProjects);
  const canManage = has(ADMIN_PERMS.workPackagesManage);
  const canAssign = has(ADMIN_PERMS.workPackagesAssign);
  const canProgress = has(ADMIN_PERMS.workPackagesProgress);
  const canVerifyBusiness = has(ADMIN_PERMS.workPackagesVerify);
  const canCreate = canManage && (kind !== "Internal" || canAssign);
  const [rows, setRows] = useState<DepartmentWorkPackageResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [detail, setDetail] = useState<DepartmentWorkPackageResponse | null>(null);
  const originListPath = (location.state as { workPackageListPath?: string } | null)
    ?.workPackageListPath;
  const detailListPath =
    originListPath &&
    ["/admin/work-packages", ...subPages.map((page) => page.path)].includes(originListPath)
      ? originListPath
      : (subPages.find((page) => page.kind === detail?.kind)?.path ?? activeSubPage.path);
  const detailBackTarget = `${detailListPath}${location.search}`;
  const [options, setOptions] = useState(emptyOptions);
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [projectSchedule, setProjectSchedule] = useState<ProjectScheduleResponse | null>(null);
  const search = params.get("search") ?? "";
  const settledSearch = useDebouncedValue(search);
  const departmentFilter = params.get("departmentId") ?? "";
  const statusFilter = params.get("status") ?? "";
  const purposeFilter = params.get("purpose") ?? "";
  const projectFilter = kind !== "Internal" ? (params.get("projectId") ?? "") : "";
  const page = Math.max(1, Number(params.get("page")) || 1);
  const activeFilterCount = [departmentFilter, statusFilter, purposeFilter, projectFilter].filter(
    Boolean,
  ).length;
  const setPage = (next: number) => {
    setParams(
      (current) => {
        const params = new URLSearchParams(current);
        if (next > 1) params.set("page", String(next));
        else params.delete("page");
        return params;
      },
      { replace: true },
    );
  };
  const updateFilter = (key: string, value: string) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        if (value) next.set(key, value);
        else next.delete(key);
        next.delete("page");
        return next;
      },
      { replace: true },
    );
  };
  const requestSequence = useRef(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [packageOpen, setPackageOpen] = useState(false);
  const [packageTouched, setPackageTouched] = useState<Record<string, boolean>>({});
  const [packageSaveError, setPackageSaveError] = useState<string | null>(null);
  useEffect(() => {
    if (!packageOpen) {
      setPackageTouched({});
      setPackageSaveError(null);
    }
  }, [packageOpen]);
  const [packageEditOpen, setPackageEditOpen] = useState(false);
  const [packageEditForm, setPackageEditForm] = useState<UpdateDepartmentWorkPackageRequest | null>(
    null,
  );
  const [taskOpen, setTaskOpen] = useState(false);
  const [taskFiles, setTaskFiles] = useState<File[]>([]);
  const taskFileKeys = useRef(new Map<File, string>());
  const [attachmentsOpen, setAttachmentsOpen] = useState(false);
  const [attachmentTask, setAttachmentTask] = useState<DepartmentWorkTaskResponse | null>(null);
  const [attachments, setAttachments] = useState<DepartmentWorkTaskAttachmentResponse[]>([]);
  const [attachmentFiles, setAttachmentFiles] = useState<File[]>([]);
  const [attachmentBusy, setAttachmentBusy] = useState(false);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);
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
    parentTaskId: null,
    wbsCode: "",
    title: "",
    description: "",
    resources: "",
    departmentRoleGroupId: null,
    assigneeUserId: 0,
    approverUserId: null,
    kpiDefinitionId: null,
    projectScheduleNodeId: null,
    plannedStart: "",
    plannedEnd: "",
    ccUserIds: [],
    predecessorTaskIds: [],
  });
  const unavailablePredecessorIds = useMemo(() => {
    if (!taskEditing) return new Set<number>();
    const unavailable = new Set([taskEditing.id]);
    let changed = true;
    while (changed) {
      changed = false;
      for (const task of detail?.tasks ?? []) {
        if (unavailable.has(task.id)) continue;
        if (task.predecessorTaskIds.some((id) => unavailable.has(id))) {
          unavailable.add(task.id);
          changed = true;
        }
      }
    }
    return unavailable;
  }, [detail?.tasks, taskEditing]);
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
          search: settledSearch || undefined,
          operationalProjectId:
            Number.isFinite(projectIdFilter) && projectIdFilter > 0 ? projectIdFilter : undefined,
          departmentRoleGroupId:
            Number(departmentFilter) > 0 ? Number(departmentFilter) : undefined,
          status: (statusFilter || undefined) as WorkPackageStatus | undefined,
          purpose: (purposeFilter || undefined) as WorkPackagePurpose | undefined,
          page,
          pageSize: LIST_PAGE_SIZE,
        });
        if (request !== requestSequence.current) return;
        setRows(response.data.items);
        setTotal(response.data.totalCount);
      }
    } catch (cause) {
      if (request === requestSequence.current)
        setError(uiError(cause, t("workPackages.error.load")));
    } finally {
      if (request === requestSequence.current) setLoading(false);
    }
  }, [
    packageId,
    projectFilter,
    kind,
    settledSearch,
    departmentFilter,
    statusFilter,
    purposeFilter,
    page,
    uiError,
    t,
  ]);
  useEffect(() => {
    void load();
  }, [load]);
  useEffect(() => {
    if (loading || packageId || page <= 1 || rows.length > 0 || total <= 0) return;
    setParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.delete("page");
        return next;
      },
      { replace: true },
    );
  }, [loading, packageId, page, rows.length, total, setParams]);
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
    collectLookupPages((page) =>
      adminApi.listContracts({ operationalProjectId: projectIdValue, pageSize: 100, page }),
    )
      .then((response) => setContracts(response.data.items))
      .catch(() => setContracts([]));
  }, [packageForm.operationalProjectId]);
  useEffect(() => {
    const projectIdValue = detail?.operationalProjectId;
    if (!projectIdValue) {
      setProjectSchedule(null);
      return;
    }
    adminApi
      .getProjectSchedule(projectIdValue)
      .then((response) => setProjectSchedule(response.data))
      .catch(() => setProjectSchedule(null));
  }, [detail?.operationalProjectId]);
  const autoOpenedCreateRef = useRef<string | null>(null);
  useEffect(() => {
    if (packageId || params.get("create") !== "1" || !canManage) return;
    const requestKey = ["projectId", "opportunityId", "quoteId", "tenderId", "purpose"]
      .map((key) => params.get(key) ?? "")
      .join("|");
    if (autoOpenedCreateRef.current === requestKey) return;
    const project = Number(params.get("projectId"));
    const opportunity = Number(params.get("opportunityId"));
    const quote = Number(params.get("quoteId"));
    const tender = Number(params.get("tenderId"));
    const requestedPurpose = params.get("purpose") as WorkPackagePurpose | null;
    if (requestedPurpose === "LegalPermitting") {
      autoOpenedCreateRef.current = requestKey;
      navigate(project > 0 ? `/admin/permits?operationalProjectId=${project}` : "/admin/permits", {
        replace: true,
      });
      return;
    }
    // Departments arrive in a separate request. Opening before they load would
    // leave the receiving department unset, so wait for them instead of
    // re-opening the form on every options refresh.
    if (options.departments.length === 0) return;
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
    autoOpenedCreateRef.current = requestKey;
    setPackageForm((current) => ({
      ...current,
      kind: "Project",
      purpose,
      operationalProjectId: Number.isFinite(project) && project > 0 ? project : null,
      sourceOpportunityId: Number.isFinite(opportunity) && opportunity > 0 ? opportunity : null,
      sourceQuoteId: Number.isFinite(quote) && quote > 0 ? quote : null,
      sourceTenderId: Number.isFinite(tender) && tender > 0 ? tender : null,
      departmentRoleGroupId: expectedDepartment?.id ?? current.departmentRoleGroupId,
    }));
    setPackageOpen(true);
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
  const orderedTasks = useMemo(() => {
    const tasks = detail?.tasks ?? [];
    const ids = new Set(tasks.map((task) => task.id));
    const children = new Map<number, DepartmentWorkTaskResponse[]>();
    for (const task of tasks) {
      if (!task.parentTaskId) continue;
      const siblings = children.get(task.parentTaskId) ?? [];
      siblings.push(task);
      children.set(task.parentTaskId, siblings);
    }
    const seen = new Set<number>();
    const result: { task: DepartmentWorkTaskResponse; depth: number }[] = [];
    const visit = (task: DepartmentWorkTaskResponse, depth: number) => {
      if (seen.has(task.id)) return;
      seen.add(task.id);
      result.push({ task, depth });
      children.get(task.id)?.forEach((child) => visit(child, depth + 1));
    };
    tasks
      .filter((task) => !task.parentTaskId || !ids.has(task.parentTaskId))
      .forEach((task) => visit(task, 0));
    tasks.forEach((task) => visit(task, 0));
    return result;
  }, [detail?.tasks]);
  const availableDepartments = useMemo(
    () =>
      packageForm.kind !== "Internal"
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
  const packageErrors: Record<string, string> = {};
  if (packageForm.kind !== "Internal" && !packageForm.operationalProjectId)
    packageErrors.project = t("workPackages.validation.projectRequired");
  if (packageForm.kind === "Contract" && !packageForm.contractId)
    packageErrors.contract = t("workPackages.validation.contractRequired");
  if (packageForm.name.trim().length < 2 || packageForm.name.length > 300)
    packageErrors.name = t("workPackages.validation.name");
  if (!packageForm.departmentRoleGroupId)
    packageErrors.department = t("workPackages.validation.departmentRequired");
  if (!packageForm.leadUserId) packageErrors.lead = t("workPackages.validation.leadRequired");
  if (packageForm.purpose === "TenderPlanning") {
    if (!packageForm.plannedStart) packageErrors.start = t("workPackages.validation.datesRequired");
    if (!packageForm.plannedEnd) packageErrors.end = t("workPackages.validation.datesRequired");
  }
  if (
    packageForm.plannedStart &&
    packageForm.plannedEnd &&
    packageForm.plannedEnd < packageForm.plannedStart
  )
    packageErrors.end = t("workPackages.validation.dateOrder");
  if ((packageForm.description?.length ?? 0) > 4000)
    packageErrors.description = t("workPackages.validation.description");
  const packageFieldProps = (field: string, required = false) => ({
    htmlFor: `create-package-${field}`,
    required,
    error: packageTouched[field] ? packageErrors[field] : undefined,
    onBlur: () => setPackageTouched((current) => ({ ...current, [field]: true })),
  });
  const packageControlProps = (field: string, required = false) => ({
    id: `create-package-${field}`,
    "aria-required": required || undefined,
    "aria-invalid": (packageTouched[field] && Boolean(packageErrors[field])) || undefined,
    "aria-describedby":
      packageTouched[field] && packageErrors[field] ? `create-package-${field}-error` : undefined,
  });
  const savePackage = async () => {
    setPackageSaveError(null);
    const invalidFields = Object.keys(packageErrors);
    if (invalidFields.length > 0) {
      setPackageTouched(Object.fromEntries(invalidFields.map((field) => [field, true])));
      document.getElementById(`create-package-${invalidFields[0]}`)?.focus();
      return;
    }
    setSaving(true);
    try {
      const response = await adminApi.createWorkPackage(packageForm, newIdempotencyKey());
      setPackageOpen(false);
      toast({ title: t("workPackages.toast.created") });
      navigate(`/admin/work-packages/${response.data.id}`);
    } catch (cause) {
      setPackageSaveError(translateError(t, uiError(cause, t("workPackages.error.save"))));
    } finally {
      setSaving(false);
    }
  };
  const openEditPackage = async () => {
    if (!detail) return;
    setPackageEditForm({
      name: detail.name,
      description: detail.description,
      contractId: detail.contractId,
      status: detail.status,
      plannedStart: detail.plannedStart,
      plannedEnd: detail.plannedEnd,
      rowVersion: detail.rowVersion,
    });
    setPackageEditOpen(true);
    if (detail.kind !== "Internal" && detail.purpose === "General" && detail.operationalProjectId) {
      setContracts([]);
      try {
        const response = await collectLookupPages((page) =>
          adminApi.listContracts({
            operationalProjectId: detail.operationalProjectId,
            pageSize: 100,
            page,
          }),
        );
        setContracts(response.data.items);
      } catch (cause) {
        toast({
          variant: "destructive",
          title: uiError(cause, t("workPackages.error.load")),
        });
      }
    }
  };
  const saveEditedPackage = async () => {
    if (!detail || !packageEditForm) return;
    if (
      packageEditForm.name.trim().length < 2 ||
      packageEditForm.name.length > 300 ||
      (packageEditForm.description?.length ?? 0) > 4000 ||
      (packageEditForm.plannedStart &&
        packageEditForm.plannedEnd &&
        packageEditForm.plannedEnd < packageEditForm.plannedStart) ||
      (detail.purpose === "TenderPlanning" &&
        (!packageEditForm.plannedStart || !packageEditForm.plannedEnd))
    ) {
      toast({ variant: "destructive", title: t("workPackages.validation.editPackage") });
      return;
    }
    setSaving(true);
    try {
      await adminApi.updateWorkPackage(detail.id, packageEditForm, newIdempotencyKey());
      setPackageEditOpen(false);
      await load();
      toast({ title: t("workPackages.toast.updated") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("workPackages.error.save")),
      });
    } finally {
      setSaving(false);
    }
  };
  const openTask = (task?: DepartmentWorkTaskResponse, parent?: DepartmentWorkTaskResponse) => {
    setTaskEditing(task ?? null);
    setTaskFiles([]);
    taskFileKeys.current.clear();
    setTaskForm(
      task
        ? {
            parentTaskId: task.parentTaskId ?? null,
            wbsCode: task.wbsCode,
            title: task.title,
            description: task.description ?? "",
            resources: task.resources ?? "",
            departmentRoleGroupId: task.departmentRoleGroupId,
            assigneeUserId: task.assigneeUserId,
            approverUserId: task.approverUserId ?? null,
            kpiDefinitionId: task.kpiDefinitionId ?? null,
            projectScheduleNodeId: task.projectScheduleNodeId ?? null,
            plannedStart: dateValue(task.plannedStart),
            plannedEnd: dateValue(task.plannedEnd),
            ccUserIds: task.ccUserIds,
            predecessorTaskIds: task.predecessorTaskIds,
            rowVersion: task.rowVersion,
          }
        : {
            parentTaskId: parent?.id ?? null,
            wbsCode: suggestTaskWbsCode(detail?.tasks ?? [], parent?.id ?? null),
            title: "",
            description: "",
            resources: "",
            departmentRoleGroupId:
              detail?.kind === "Internal" ? null : detail?.departmentRoleGroupId,
            assigneeUserId: 0,
            approverUserId: null,
            kpiDefinitionId: null,
            projectScheduleNodeId: null,
            plannedStart: dateValue(parent?.plannedStart ?? detail?.plannedStart),
            plannedEnd: dateValue(parent?.plannedEnd ?? detail?.plannedEnd),
            ccUserIds: [],
            predecessorTaskIds: [],
          },
    );
    setTaskOpen(true);
  };
  const selectTaskFiles = (files: File[], target: "form" | "attachments") => {
    if (
      files.length > 10 ||
      files.some(
        (file) =>
          file.size < 1 ||
          file.size > TASK_FILE_MAX_SIZE ||
          file.name.length > 255 ||
          !TASK_FILE_EXTENSIONS.some((extension) => file.name.toLowerCase().endsWith(extension)),
      )
    ) {
      if (target === "form") setTaskFiles([]);
      else setAttachmentFiles([]);
      toast({ variant: "destructive", title: t("workPackages.attachments.validation") });
      return;
    }
    if (target === "form") setTaskFiles(files);
    else setAttachmentFiles(files);
    taskFileKeys.current.clear();
  };
  const uploadTaskFiles = async (taskId: number, files: File[]) => {
    if (!detail) return { failed: files, error: t("workPackages.error.save") };
    const failed: File[] = [];
    let error: string | null = null;
    for (const file of files) {
      const key = taskFileKeys.current.get(file) ?? newIdempotencyKey();
      taskFileKeys.current.set(file, key);
      try {
        await adminApi.uploadWorkTaskAttachment(detail.id, taskId, file, key);
        taskFileKeys.current.delete(file);
      } catch (cause) {
        failed.push(file);
        error ??= `${file.name}: ${uiError(cause)}`;
      }
    }
    return { failed, error };
  };
  const openAttachments = async (task: DepartmentWorkTaskResponse) => {
    if (!detail) return;
    setAttachmentTask(task);
    setAttachmentsOpen(true);
    setAttachments([]);
    setAttachmentFiles([]);
    setAttachmentError(null);
    setAttachmentBusy(true);
    try {
      setAttachments((await adminApi.listWorkTaskAttachments(detail.id, task.id)).data);
    } catch (cause) {
      setAttachmentError(uiError(cause));
    } finally {
      setAttachmentBusy(false);
    }
  };
  const addAttachments = async () => {
    if (!detail || !attachmentTask || attachmentFiles.length === 0) return;
    setAttachmentBusy(true);
    setAttachmentError(null);
    try {
      const result = await uploadTaskFiles(attachmentTask.id, attachmentFiles);
      setAttachmentFiles(result.failed);
      setAttachments((await adminApi.listWorkTaskAttachments(detail.id, attachmentTask.id)).data);
      if (result.error) setAttachmentError(result.error);
      else toast({ title: t("workPackages.attachments.uploaded") });
    } catch (cause) {
      setAttachmentError(uiError(cause));
    } finally {
      setAttachmentBusy(false);
    }
  };
  const downloadAttachment = async (attachment: DepartmentWorkTaskAttachmentResponse) => {
    if (!detail || !attachmentTask) return;
    setAttachmentBusy(true);
    setAttachmentError(null);
    try {
      const response = await adminApi.downloadWorkTaskAttachment(
        detail.id,
        attachmentTask.id,
        attachment.id,
      );
      const url = URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = attachment.originalFileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (cause) {
      setAttachmentError(uiError(cause));
    } finally {
      setAttachmentBusy(false);
    }
  };
  const deleteAttachment = async (attachment: DepartmentWorkTaskAttachmentResponse) => {
    if (
      !detail ||
      !attachmentTask ||
      !(await confirmAction(
        t("workPackages.attachments.deleteConfirm", { name: attachment.originalFileName }),
      ))
    )
      return;
    setAttachmentBusy(true);
    setAttachmentError(null);
    try {
      await adminApi.deleteWorkTaskAttachment(detail.id, attachmentTask.id, attachment.id);
      setAttachments((current) => current.filter((item) => item.id !== attachment.id));
      toast({ title: t("workPackages.attachments.deleted") });
    } catch (cause) {
      setAttachmentError(uiError(cause));
    } finally {
      setAttachmentBusy(false);
    }
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
      setDeleteError(uiError(cause));
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
      setDeleteError(uiError(cause));
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
      const saved = taskEditing
        ? (await adminApi.updateWorkTask(detail.id, taskEditing.id, body, newIdempotencyKey())).data
        : (await adminApi.addWorkTask(detail.id, body, newIdempotencyKey())).data;
      const uploads = await uploadTaskFiles(saved.id, taskFiles);
      setTaskFiles(uploads.failed);
      if (uploads.error) {
        setTaskEditing(saved);
        setTaskForm((current) => ({ ...current, rowVersion: saved.rowVersion }));
        toast({ variant: "destructive", title: uploads.error });
        await load();
        return;
      }
      setTaskOpen(false);
      toast({
        title: t(taskEditing ? "workPackages.toast.taskUpdated" : "workPackages.toast.taskCreated"),
      });
      await load();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
      setApprovalError(uiError(cause, t("workPackages.error.save")));
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
      setApprovalError(uiError(cause, t("workPackages.error.save")));
      toast({
        variant: "destructive",
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
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
        title: uiError(cause, t("workPackages.error.save")),
      });
    }
  };
  return {
    LIST_PAGE_SIZE,
    id,
    location,
    params,
    setParams,
    navigate,
    t,
    currentUserId,
    has,
    packageId,
    kindFromPath,
    kind,
    subPages,
    activeSubPage,
    canViewProjects,
    canManage,
    canAssign,
    canProgress,
    canVerifyBusiness,
    canCreate,
    rows,
    total,
    detail,
    detailBackTarget,
    options,
    projects,
    contracts,
    projectSchedule,
    search,
    departmentFilter,
    statusFilter,
    purposeFilter,
    projectFilter,
    page,
    activeFilterCount,
    setPage,
    updateFilter,
    loading,
    error,
    filtersOpen,
    setFiltersOpen,
    packageOpen,
    setPackageOpen,
    packageSaveError,
    packageEditOpen,
    setPackageEditOpen,
    packageEditForm,
    setPackageEditForm,
    taskOpen,
    setTaskOpen,
    taskFiles,
    attachmentsOpen,
    setAttachmentsOpen,
    attachmentTask,
    attachments,
    attachmentFiles,
    attachmentBusy,
    attachmentError,
    progressOpen,
    setProgressOpen,
    saving,
    deleteOpen,
    setDeleteOpen,
    approvalPreview,
    setApprovalPreview,
    approvalBusy,
    approvalTo,
    setApprovalTo,
    approvalCc,
    setApprovalCc,
    approvalDirty,
    setApprovalDirty,
    approvalError,
    setApprovalError,
    approvalFeedback,
    approvalDeliveries,
    approvalNoticeRef,
    deleteImpact,
    deleteLoading,
    deleteError,
    deleting,
    taskEditing,
    progressTaskEditing,
    progressPercent,
    setProgressPercent,
    rejectTaskEditing,
    setRejectTaskEditing,
    rejectReason,
    setRejectReason,
    priceOpen,
    setPriceOpen,
    priceEditing,
    priceForm,
    setPriceForm,
    reviewForm,
    setReviewForm,
    packageForm,
    setPackageForm,
    taskForm,
    setTaskForm,
    unavailablePredecessorIds,
    load,
    departmentUsers,
    taskUsers,
    orderedTasks,
    availableDepartments,
    openCreatePackage,
    packageFieldProps,
    packageControlProps,
    savePackage,
    openEditPackage,
    saveEditedPackage,
    openTask,
    selectTaskFiles,
    openAttachments,
    addAttachments,
    downloadAttachment,
    deleteAttachment,
    previewDelete,
    confirmDelete,
    saveTask,
    transitionPackage,
    progressTask,
    openProgress,
    verifyTask,
    rejectTask,
    resetPriceForm,
    openPriceOffer,
    savePriceOffer,
    submitPriceOffer,
    recipientsValid,
    refreshApprovalPreview,
    confirmApproval,
    decidePriceOffer,
    saveFinanceReview,
    submitFinanceReview,
    decideFinanceReview,
    dateValue,
    suggestTaskWbsCode,
    apiDate,
    TASK_FILE_EXTENSIONS,
    statusTone,
  };
}
