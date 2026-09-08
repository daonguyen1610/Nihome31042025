import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import {
  Boxes,
  Check,
  ClipboardCheck,
  Download,
  Eye,
  FilePlus2,
  PackageCheck,
  PackageMinus,
  Plus,
  RefreshCw,
  Search,
  Send,
  ShoppingCart,
  Star,
  Trash2,
  X,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ContractResponse,
  type KpiUserOptionResponse,
  type MaterialRequestBoqContextResponse,
  type MaterialRequestListParams,
  type MaterialRequestResponse,
  type OperationalProjectListItemResponse,
  type ProcurementContractLineResponse,
  type ProcurementWorkspaceResponse,
  type ProjectBoqLineResponse,
  type ProjectBoqRevisionListItemResponse,
  type ProjectBoqRevisionListParams,
  type ProjectBoqRevisionListResponse,
  type ProjectBoqRevisionResponse,
  type VendorRatingResponse,
  type WarehouseIssueResponse,
  type WarehouseReceiptResponse,
  type WarehouseStockItemResponse,
  type WarehouseTransactionListItemResponse,
  type WarehouseTransactionListParams,
} from "@/services/adminApi";
import { useAppSelector } from "@/store";
import MaterialRequestListToolbar from "./MaterialRequestListToolbar";
import WarehouseWorkspace from "./WarehouseWorkspace";

type DialogKind = "boq" | "request" | "contract" | "receipt" | "issue" | "rating" | null;
type DecisionKind = "boq" | "request" | "rating";
type UserOption = { userId: number; userName: string };
type BoqLineDraft = { itemCode: string; description: string; unit: string; approvedQuantity: number; budgetUnitPrice: number };
type RequestLineDraft = { projectBoqLineId: number; requestedQuantity: number };
type ReceiptLineDraft = { materialRequestLineId: number; contractLineId: number | null; receivedQuantity: number };
type IssueLineDraft = { projectBoqLineId: number; issuedQuantity: number };
const procurementTabs = ["boq", "requests", "contracts", "warehouse", "ratings"] as const;

const emptyBoqLine = (): BoqLineDraft => ({ itemCode: "", description: "", unit: "", approvedQuantity: 1, budgetUnitPrice: 0 });
const emptyRequestLine = (): RequestLineDraft => ({ projectBoqLineId: 0, requestedQuantity: 1 });
const emptyReceiptLine = (): ReceiptLineDraft => ({ materialRequestLineId: 0, contractLineId: null, receivedQuantity: 1 });
const emptyIssueLine = (): IssueLineDraft => ({ projectBoqLineId: 0, issuedQuantity: 1 });
const localDateTime = (daysFromNow = 0) => {
  const now = new Date();
  now.setDate(now.getDate() + daysFromNow);
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  return now.toISOString().slice(0, 16);
};
const apiDateTime = (value: string) => new Date(value).toISOString();

const statusClass: Record<string, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  PartiallyFulfilled: "border-amber-200 bg-amber-50 text-amber-800",
  Fulfilled: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-slate-200 bg-slate-50 text-slate-500",
  Posted: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Reversed: "border-rose-200 bg-rose-50 text-rose-700",
  Superseded: "border-slate-200 bg-slate-50 text-slate-500",
};

const ProcurementControlPage = () => {
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUser = useAppSelector((state) => state.auth.user);
  const canViewBoq = has(ADMIN_PERMS.procurement);
  const canManageBoq = has(ADMIN_PERMS.procurementManage);
  const canApproveBoq = has(ADMIN_PERMS.procurementApprove);
  const canViewRequests = has(ADMIN_PERMS.procurementMaterialRequests);
  const canManageRequests = has(ADMIN_PERMS.procurementMaterialRequestsManage);
  const canApproveRequests = has(ADMIN_PERMS.procurementMaterialRequestsApprove);
  const canViewContracts = has(ADMIN_PERMS.procurementContractLines);
  const canManageContracts = has(ADMIN_PERMS.procurementContractLinesManage);
  const canViewWarehouse = has(ADMIN_PERMS.procurementWarehouse);
  const canPostWarehouse = has(ADMIN_PERMS.procurementWarehousePost);
  const canViewRatings = has(ADMIN_PERMS.procurementRatings);
  const canManageRatings = has(ADMIN_PERMS.procurementRatingsManage);
  const canApproveRatings = has(ADMIN_PERMS.procurementRatingsApprove);
  const canReadContracts = has(ADMIN_PERMS.contracts);
  const canReadKpiUsers = has(ADMIN_PERMS.kpi);
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedProjectId = Number(searchParams.get("projectId") ?? 0);
  const projectId = Number.isInteger(requestedProjectId) && requestedProjectId > 0 ? requestedProjectId : 0;
  const availableTabs = procurementTabs.filter((tab) =>
    (tab === "boq" && canViewBoq)
    || (tab === "requests" && canViewRequests)
    || (tab === "contracts" && canViewContracts)
    || (tab === "warehouse" && canViewWarehouse)
    || (tab === "ratings" && canViewRatings));
  const requestedTab = searchParams.get("tab");
  const activeTab = requestedTab && availableTabs.includes(requestedTab as typeof procurementTabs[number])
    ? requestedTab
    : availableTabs[0] ?? "boq";

  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const projectIdRef = useRef(projectId);
  const requestLoadIdRef = useRef(0);
  const dialogTriggerRef = useRef<HTMLElement | null>(null);
  const [workspace, setWorkspace] = useState<ProcurementWorkspaceResponse | null>(null);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [procurementUsers, setProcurementUsers] = useState<UserOption[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [contractsLookupAvailable, setContractsLookupAvailable] = useState(true);
  const [projectsLoading, setProjectsLoading] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<DialogKind>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [decision, setDecision] = useState<{ kind: DecisionKind; id: number; rowVersion: string; approved: boolean } | null>(null);
  const [decisionReason, setDecisionReason] = useState("");
  const [requestSearchInput, setRequestSearchInput] = useState("");
  const [requestSearch, setRequestSearch] = useState("");
  const [requestStatus, setRequestStatus] = useState("all");
  const [requestOwner, setRequestOwner] = useState("all");
  const [requestRequiredFrom, setRequestRequiredFrom] = useState("");
  const [requestRequiredTo, setRequestRequiredTo] = useState("");
  const [requestSortDirection, setRequestSortDirection] = useState<"asc" | "desc">("asc");
  const [requestPage, setRequestPage] = useState(1);
  const [requestTotal, setRequestTotal] = useState(0);
  const [requestRows, setRequestRows] = useState<MaterialRequestResponse[]>([]);
  const [requestBoqContext, setRequestBoqContext] = useState<MaterialRequestBoqContextResponse | null>(null);
  const [requestListLoading, setRequestListLoading] = useState(false);
  const [requestListError, setRequestListError] = useState<string | null>(null);
  const [requestExporting, setRequestExporting] = useState(false);
  const [warehouseSearchInput, setWarehouseSearchInput] = useState("");
  const [warehouseSearch, setWarehouseSearch] = useState("");
  const [warehouseType, setWarehouseType] = useState("all");
  const [warehouseStatus, setWarehouseStatus] = useState("all");
  const [warehouseResponsibleUser, setWarehouseResponsibleUser] = useState("all");
  const [warehouseOccurredFrom, setWarehouseOccurredFrom] = useState("");
  const [warehouseOccurredTo, setWarehouseOccurredTo] = useState("");
  const [warehouseSortDirection, setWarehouseSortDirection] = useState<"asc" | "desc">("desc");
  const [warehousePage, setWarehousePage] = useState(1);
  const [warehouseTotal, setWarehouseTotal] = useState(0);
  const [warehouseRows, setWarehouseRows] = useState<WarehouseTransactionListItemResponse[]>([]);
  const [warehouseStock, setWarehouseStock] = useState<WarehouseStockItemResponse[]>([]);
  const [warehouseLoading, setWarehouseLoading] = useState(false);
  const [warehouseError, setWarehouseError] = useState<string | null>(null);
  const [warehouseExporting, setWarehouseExporting] = useState(false);

  const [boqCurrency, setBoqCurrency] = useState("VND");
  const [boqLines, setBoqLines] = useState<BoqLineDraft[]>([emptyBoqLine()]);
  const [boqList, setBoqList] = useState<ProjectBoqRevisionListResponse>({ total: 0, page: 1, pageSize: 10, items: [] });
  const [boqSearch, setBoqSearch] = useState("");
  const [boqDebouncedSearch, setBoqDebouncedSearch] = useState("");
  const [boqStatus, setBoqStatus] = useState("all");
  const [boqSortBy, setBoqSortBy] = useState<ProjectBoqRevisionListParams["sortBy"]>("revision");
  const [boqSortDirection, setBoqSortDirection] = useState<ProjectBoqRevisionListParams["sortDirection"]>("desc");
  const [boqPage, setBoqPage] = useState(1);
  const [boqListLoading, setBoqListLoading] = useState(false);
  const [boqListError, setBoqListError] = useState<string | null>(null);
  const [requestForm, setRequestForm] = useState({ responsibleSiteUserId: 0, assignedProcurementUserId: 0, requiredAt: localDateTime(1), note: "" });
  const [requestLines, setRequestLines] = useState<RequestLineDraft[]>([emptyRequestLine()]);
  const [contractForm, setContractForm] = useState({ contractId: 0, projectBoqLineId: 0, procurementOwnerUserId: 0, quantity: 1, negotiatedUnitPrice: 0 });
  const [receiptAt, setReceiptAt] = useState(localDateTime());
  const [receiptLines, setReceiptLines] = useState<ReceiptLineDraft[]>([emptyReceiptLine()]);
  const [issueForm, setIssueForm] = useState({ issuedAt: localDateTime(), responsibleSiteUserId: 0, workItemCode: "" });
  const [issueLines, setIssueLines] = useState<IssueLineDraft[]>([emptyIssueLine()]);
  const [ratingForm, setRatingForm] = useState({ contractId: 0, qualityScore: 80, scheduleScore: 80, costScore: 80, hseScore: 80, comments: "" });

  const number = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }), [lang]);
  const money = useMemo(() => new Intl.NumberFormat(lang, { maximumFractionDigits: 0 }), [lang]);
  const date = useMemo(() => new Intl.DateTimeFormat(lang, { dateStyle: "medium" }), [lang]);

  useEffect(() => {
    projectIdRef.current = projectId;
    requestLoadIdRef.current += 1;
    setRequestRows([]);
    setRequestBoqContext(null);
    setRequestTotal(0);
    setRequestPage(1);
  }, [projectId]);

  useEffect(() => {
    let cancelled = false;
    setProjectsLoading(true);
    const loadProjects = async () => {
      const pageSize = 100;
      const first = (await adminApi.listOperationalProjects({ page: 1, pageSize })).data;
      const items = [...(first.items ?? [])];
      for (let page = 2; page <= Math.ceil(first.total / pageSize); page += 1) {
        items.push(...((await adminApi.listOperationalProjects({ page, pageSize })).data.items ?? []));
      }
      return items;
    };
    loadProjects()
      .then((items) => { if (!cancelled) setProjects(items); })
      .catch((reason) => { if (!cancelled) setError(extractApiError(reason)); })
      .finally(() => { if (!cancelled) setProjectsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setRequestSearch(requestSearchInput.trim());
      setRequestPage(1);
    }, 300);
    return () => window.clearTimeout(timeout);
  }, [requestSearchInput]);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setWarehouseSearch(warehouseSearchInput.trim());
      setWarehousePage(1);
    }, 300);
    return () => window.clearTimeout(timeout);
  }, [warehouseSearchInput]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setBoqDebouncedSearch(boqSearch.trim());
      setBoqPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [boqSearch]);

  const loadWorkspace = useCallback(async () => {
    if (!projectId) return;
    const requestedProjectId = projectId;
    setLoading(true);
    setError(null);
    try {
      const [workspaceResult, teamResult, kpiUsersResult, contractsResult] = await Promise.all([
        canViewBoq
          ? adminApi.getProcurementWorkspace(projectId)
          : Promise.resolve({ data: { boqRevisions: [], materialRequests: [], contractLines: [], receipts: [], issues: [], vendorRatings: [] } as ProcurementWorkspaceResponse }),
        adminApi.getOperationalProjectTeam(projectId).catch(() => ({ data: null })),
        canReadKpiUsers
          ? adminApi.listKpiEligibleUsers().catch(() => ({ data: [] as KpiUserOptionResponse[] }))
          : Promise.resolve({ data: [] as KpiUserOptionResponse[] }),
        canReadContracts
          ? adminApi.listContracts({ page: 1, pageSize: 200 }).catch(() => null)
          : Promise.resolve(null),
      ]);
      if (projectIdRef.current !== requestedProjectId) return;
      setWorkspace(workspaceResult.data);
      const activeMembers = (teamResult.data?.members ?? []).filter((member) => member.isActive);
      const teamUsers: UserOption[] = activeMembers
        .map((member) => ({ userId: member.userId, userName: member.userName }));
      const fallbackUsers = kpiUsersResult.data.map((user) => ({ userId: user.userId, userName: user.userName }));
      const merged = [...teamUsers, ...fallbackUsers];
      if (currentUser) merged.push({ userId: currentUser.userId, userName: currentUser.fullName });
      setUsers(Array.from(new Map(merged.map((user) => [user.userId, user])).values()));
      const procurementCandidates: UserOption[] = [
        ...activeMembers
          .filter((member) => member.position.toUpperCase() === "PROCUREMENT" || member.roles.some((role) => role.roleCode === "PROCUREMENT" && !role.endedAt))
          .map((member) => ({ userId: member.userId, userName: member.userName })),
        ...kpiUsersResult.data
          .filter((user) => user.positionCode === "PROCUREMENT")
          .map((user) => ({ userId: user.userId, userName: user.userName })),
      ];
      if (currentUser?.role === "PROCUREMENT") procurementCandidates.push({ userId: currentUser.userId, userName: currentUser.fullName });
      setProcurementUsers(Array.from(new Map(procurementCandidates.map((user) => [user.userId, user])).values()));
      setContractsLookupAvailable(contractsResult != null);
      setContracts((contractsResult?.data.items ?? []).filter((contract) => contract.operationalProjectId === projectId));
    } catch (reason) {
      if (projectIdRef.current !== requestedProjectId) return;
      setWorkspace(null);
      setError(extractApiError(reason));
    } finally {
      if (projectIdRef.current === requestedProjectId) setLoading(false);
    }
  }, [canReadContracts, canReadKpiUsers, canViewBoq, currentUser, projectId]);

  const requestParams = useCallback((page = requestPage, pageSize = 20): MaterialRequestListParams => ({
    page,
    pageSize,
    sortBy: "requiredAt",
    sortDirection: requestSortDirection,
    ...(requestSearch ? { search: requestSearch } : {}),
    ...(requestStatus !== "all" ? { status: requestStatus } : {}),
    ...(requestOwner !== "all" ? { assignedProcurementUserId: Number(requestOwner) } : {}),
    ...(requestRequiredFrom ? { requiredFrom: requestRequiredFrom } : {}),
    ...(requestRequiredTo ? { requiredTo: requestRequiredTo } : {}),
  }), [requestOwner, requestPage, requestRequiredFrom, requestRequiredTo, requestSearch, requestSortDirection, requestStatus]);

  const loadMaterialRequests = useCallback(async () => {
    if (!projectId || !canViewRequests) return;
    const requestedProjectId = projectId;
    const requestLoadId = ++requestLoadIdRef.current;
    setRequestListLoading(true);
    setRequestListError(null);
    try {
      const { data } = await adminApi.listMaterialRequests(projectId, requestParams());
      if (projectIdRef.current !== requestedProjectId || requestLoadIdRef.current !== requestLoadId) return;
      setRequestTotal(data.total);
      setRequestRows(data.items);
      setRequestBoqContext(data.currentApprovedBoq ?? null);
    } catch (reason) {
      if (projectIdRef.current !== requestedProjectId || requestLoadIdRef.current !== requestLoadId) return;
      setRequestListError(extractApiError(reason));
    } finally {
      if (projectIdRef.current === requestedProjectId && requestLoadIdRef.current === requestLoadId) setRequestListLoading(false);
    }
  }, [canViewRequests, projectId, requestParams]);

  useEffect(() => { void loadMaterialRequests(); }, [loadMaterialRequests]);

  const warehouseParams = useCallback((page = warehousePage, pageSize = 20): WarehouseTransactionListParams => ({
    page,
    pageSize,
    sortBy: "occurredAt",
    sortDirection: warehouseSortDirection,
    ...(warehouseSearch ? { search: warehouseSearch } : {}),
    ...(warehouseType !== "all" ? { type: warehouseType as "receipt" | "issue" } : {}),
    ...(warehouseStatus !== "all" ? { status: warehouseStatus } : {}),
    ...(warehouseResponsibleUser !== "all" ? { responsibleUserId: Number(warehouseResponsibleUser) } : {}),
    ...(warehouseOccurredFrom ? { occurredFrom: warehouseOccurredFrom } : {}),
    ...(warehouseOccurredTo ? { occurredTo: warehouseOccurredTo } : {}),
  }), [warehouseOccurredFrom, warehouseOccurredTo, warehousePage, warehouseResponsibleUser, warehouseSearch, warehouseSortDirection, warehouseStatus, warehouseType]);

  const loadWarehouseTransactions = useCallback(async () => {
    if (!projectId || !canViewWarehouse) return;
    setWarehouseLoading(true);
    setWarehouseError(null);
    try {
      const { data } = await adminApi.listWarehouseTransactions(projectId, warehouseParams());
      if (projectIdRef.current !== projectId) return;
      setWarehouseTotal(data.total);
      setWarehouseRows(data.items);
      setWarehouseStock(data.stock);
    } catch (reason) {
      if (projectIdRef.current === projectId) setWarehouseError(extractApiError(reason));
    } finally {
      if (projectIdRef.current === projectId) setWarehouseLoading(false);
    }
  }, [canViewWarehouse, projectId, warehouseParams]);

  useEffect(() => { void loadWarehouseTransactions(); }, [loadWarehouseTransactions]);

  const refreshData = async () => {
    await Promise.all([loadWorkspace(), loadMaterialRequests(), loadBoqList(), loadWarehouseTransactions()]);
  };

  const exportWarehouseTransactions = async () => {
    if (!projectId) return;
    setWarehouseExporting(true);
    try {
      const first = (await adminApi.listWarehouseTransactions(projectId, warehouseParams(1, 100))).data;
      const rows = [...first.items];
      for (let page = 2; page <= Math.ceil(first.total / first.pageSize); page += 1) {
        rows.push(...(await adminApi.listWarehouseTransactions(projectId, warehouseParams(page, 100))).data.items);
      }
      downloadCsv({
        filename: createCsvFilename(`warehouse-transactions-project-${projectId}`),
        rows,
        columns: [
          { header: t("procurement.warehouse.type"), value: (row) => t(`procurement.warehouse.${row.type.toLowerCase()}`) },
          { header: t("procurement.field.code"), value: "code" },
          { header: t("procurement.field.status"), value: (row) => t(`procurement.status.${row.status}`) },
          { header: t("procurement.warehouse.occurredAt"), value: (row) => formatDate(row.occurredAt) },
          { header: t("procurement.warehouse.responsibleUser"), value: (row) => row.responsibleUserName ?? row.actorName ?? `#${row.actorUserId}` },
          { header: t("procurement.field.lines"), value: (row) => row.itemCodes.join(" | ") },
          { header: t("procurement.field.quantity"), value: "totalQuantity" },
        ],
      });
    } catch (reason) {
      toast({ variant: "destructive", title: extractApiError(reason) || t("common.error") });
    } finally {
      setWarehouseExporting(false);
    }
  };

  const exportMaterialRequests = async () => {
    if (!projectId) return;
    setRequestExporting(true);
    try {
      const first = (await adminApi.listMaterialRequests(projectId, requestParams(1, 100))).data;
      const rows = [...first.items];
      for (let page = 2; page <= Math.ceil(first.total / first.pageSize); page += 1) {
        rows.push(...(await adminApi.listMaterialRequests(projectId, requestParams(page, 100))).data.items);
      }
      downloadCsv({
        filename: createCsvFilename(`material-requests-project-${projectId}`),
        rows,
        columns: [
          { header: t("procurement.field.code"), value: "code" },
          { header: t("procurement.field.status"), value: (row) => t(`procurement.status.${row.status}`) },
          { header: t("procurement.field.requester"), value: (row) => row.siteRequesterName ?? `#${row.siteRequesterUserId}` },
          { header: t("procurement.field.responsibleSite"), value: (row) => row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}` },
          { header: t("procurement.field.procurementOwner"), value: (row) => row.assignedProcurementUserName ?? `#${row.assignedProcurementUserId}` },
          { header: t("procurement.field.requiredAt"), value: (row) => formatDate(row.requiredAt) },
          { header: t("procurement.field.lines"), value: (row) => row.lines.map((line) => `${line.itemCode}: ${line.requestedQuantity}`).join(" | ") },
        ],
      });
    } catch (reason) {
      toast({ variant: "destructive", title: extractApiError(reason) || t("common.error") });
    } finally {
      setRequestExporting(false);
    }
  };

  useEffect(() => { void loadWorkspace(); }, [loadWorkspace]);

  const boqListParams = useMemo<ProjectBoqRevisionListParams>(() => ({
    search: boqDebouncedSearch || undefined,
    status: boqStatus === "all" ? undefined : boqStatus,
    sortBy: boqSortBy,
    sortDirection: boqSortDirection,
    page: boqPage,
    pageSize: boqList.pageSize,
  }), [boqDebouncedSearch, boqList.pageSize, boqPage, boqSortBy, boqSortDirection, boqStatus]);

  const loadBoqList = useCallback(async () => {
    if (!projectId || !canViewBoq) {
      setBoqList((current) => ({ ...current, total: 0, page: 1, items: [] }));
      return;
    }
    setBoqListLoading(true);
    setBoqListError(null);
    try {
      const response = await adminApi.listProjectBoqRevisions(projectId, boqListParams);
      setBoqList(response.data);
    } catch (reason) {
      setBoqListError(extractApiError(reason));
    } finally {
      setBoqListLoading(false);
    }
  }, [boqListParams, canViewBoq, projectId]);

  useEffect(() => { void loadBoqList(); }, [loadBoqList]);

  useEffect(() => { setBoqPage(1); }, [projectId]);

  const approvedBoq = useMemo(
    () => workspace?.boqRevisions.find((revision) => revision.isFinal)
      ?? workspace?.boqRevisions.find((revision) => revision.status === "Approved"),
    [workspace],
  );
  const approvedBoqLines = approvedBoq?.lines ?? [];
  const requestBoqLines = requestBoqContext?.lines ?? [];
  const receivableLines = useMemo(() => (workspace?.materialRequests ?? [])
    .filter((request) => ["Approved", "PartiallyFulfilled"].includes(request.status))
    .flatMap((request) => request.lines.map((line) => ({ ...line, requestCode: request.code })))
    .filter((line) => line.requestedQuantity - line.receivedQuantity > 0), [workspace]);
  const ratingContracts = contracts.filter((contract) => contract.vendorId != null);

  const status = (value: string) => (
    <Badge variant="outline" className={statusClass[value] ?? ""}>{t(`procurement.status.${value}`)}</Badge>
  );
  const formatDate = (value?: string | null) => value ? date.format(new Date(value)) : "—";

  const openDialog = (kind: Exclude<DialogKind, null>) => {
    dialogTriggerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setFormError(null);
    if (kind === "boq") { setBoqCurrency("VND"); setBoqLines([emptyBoqLine()]); }
    if (kind === "request") {
      setRequestForm({ responsibleSiteUserId: 0, assignedProcurementUserId: currentUser?.userId ?? 0, requiredAt: localDateTime(1), note: "" });
      setRequestLines([emptyRequestLine()]);
    }
    if (kind === "contract") setContractForm({ contractId: 0, projectBoqLineId: 0, procurementOwnerUserId: currentUser?.userId ?? 0, quantity: 1, negotiatedUnitPrice: 0 });
    if (kind === "receipt") { setReceiptAt(localDateTime()); setReceiptLines([emptyReceiptLine()]); }
    if (kind === "issue") { setIssueForm({ issuedAt: localDateTime(), responsibleSiteUserId: 0, workItemCode: "" }); setIssueLines([emptyIssueLine()]); }
    if (kind === "rating") setRatingForm({ contractId: 0, qualityScore: 80, scheduleScore: 80, costScore: 80, hseScore: 80, comments: "" });
    setDialog(kind);
  };

  const runMutation = async (operation: () => Promise<unknown>, successKey: string, close = true) => {
    setBusy(true);
    setFormError(null);
    try {
      await operation();
      if (close) setDialog(null);
      setDecision(null);
      setDecisionReason("");
      toast({ title: t(successKey) });
      await Promise.all([loadWorkspace(), loadMaterialRequests(), loadBoqList(), loadWarehouseTransactions()]);
    } catch (reason) {
      const message = extractApiError(reason);
      setFormError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const createBoq = () => {
    const normalizedCodes = boqLines.map((line) => line.itemCode.trim().toUpperCase());
    if (normalizedCodes.length !== new Set(normalizedCodes).size) {
      setFormError(t("procurement.validation.duplicateItemCode"));
      return;
    }
    const valid = /^[A-Z]{3}$/.test(boqCurrency)
      && boqLines.length > 0
      && boqLines.every((line) => /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(line.itemCode.trim()) && line.description.trim() && line.unit.trim() && line.approvedQuantity >= 0 && line.budgetUnitPrice >= 0);
    if (!valid) { setFormError(t("procurement.validation.boq")); return; }
    void runMutation(() => adminApi.createProjectBoqRevision(projectId, {
      currency: boqCurrency,
      lines: boqLines.map((line) => ({ ...line, itemCode: line.itemCode.trim(), description: line.description.trim(), unit: line.unit.trim() })),
    }), "procurement.success.boqCreated");
  };

  const createRequest = () => {
    const valid = requestForm.responsibleSiteUserId > 0 && requestForm.assignedProcurementUserId > 0 && requestForm.requiredAt
      && new Date(requestForm.requiredAt) > new Date()
      && requestLines.length > 0 && requestLines.every((line) => line.projectBoqLineId > 0 && line.requestedQuantity > 0);
    if (!valid) { setFormError(t("procurement.validation.request")); return; }
    const ids = requestLines.map((line) => line.projectBoqLineId);
    if (ids.length !== new Set(ids).size) { setFormError(t("procurement.validation.requestLines")); return; }
    if (requestLines.some((line) => {
      const option = requestBoqLines.find((item) => item.id === line.projectBoqLineId);
      return !option || line.requestedQuantity > option.remainingQuantity;
    })) { setFormError(t("procurement.validation.requestAllowance")); return; }
    void runMutation(() => adminApi.createMaterialRequest(projectId, {
      ...requestForm,
      requiredAt: apiDateTime(requestForm.requiredAt),
      note: requestForm.note.trim() || null,
      lines: requestLines,
    }), "procurement.success.requestCreated");
  };

  const createContractLine = () => {
    if (contractForm.contractId < 1 || contractForm.projectBoqLineId < 1 || contractForm.procurementOwnerUserId < 1 || contractForm.quantity <= 0 || contractForm.negotiatedUnitPrice < 0) {
      setFormError(t("procurement.validation.contractLine")); return;
    }
    void runMutation(() => adminApi.createProcurementContractLine(projectId, contractForm), "procurement.success.contractLineCreated");
  };

  const createReceipt = () => {
    const valid = currentUser?.userId && receiptAt && receiptLines.length > 0
      && receiptLines.every((line) => {
        const source = receivableLines.find((item) => item.id === line.materialRequestLineId);
        return source && line.receivedQuantity > 0 && line.receivedQuantity <= source.requestedQuantity - source.receivedQuantity;
      });
    if (!valid) { setFormError(t("procurement.validation.receipt")); return; }
    void runMutation(() => adminApi.createWarehouseReceipt(projectId, {
      inspectedAt: apiDateTime(receiptAt),
      receivedByUserId: currentUser.userId,
      lines: receiptLines,
    }), "procurement.success.receiptCreated");
  };

  const createIssue = () => {
    const valid = currentUser?.userId && issueForm.issuedAt && issueForm.responsibleSiteUserId > 0
      && issueLines.length > 0 && issueLines.every((line) => {
        const stock = warehouseStock.find((item) => item.projectBoqLineId === line.projectBoqLineId)?.onHandQuantity ?? 0;
        return line.issuedQuantity > 0 && line.issuedQuantity <= stock;
      });
    if (!valid) { setFormError(t("procurement.validation.issue")); return; }
    void runMutation(() => adminApi.createWarehouseIssue(projectId, {
      issuedAt: apiDateTime(issueForm.issuedAt),
      responsibleSiteUserId: issueForm.responsibleSiteUserId,
      issuedByUserId: currentUser.userId,
      workItemCode: issueForm.workItemCode.trim() || undefined,
      lines: issueLines,
    }), "procurement.success.issueCreated");
  };

  const createRating = () => {
    const scores = [ratingForm.qualityScore, ratingForm.scheduleScore, ratingForm.costScore, ratingForm.hseScore];
    if (ratingForm.contractId < 1 || scores.some((score) => score < 0 || score > 100)) {
      setFormError(t("procurement.validation.rating")); return;
    }
    void runMutation(() => adminApi.createVendorRating(projectId, {
      ...ratingForm,
      comments: ratingForm.comments.trim() || null,
    }), "procurement.success.ratingCreated");
  };

  const submit = (kind: DecisionKind, record: { id: number; rowVersion: string }) => {
    const operation = kind === "boq"
      ? () => adminApi.submitProjectBoqRevision(projectId, record.id, record.rowVersion)
      : kind === "request"
        ? () => adminApi.submitMaterialRequest(projectId, record.id, record.rowVersion)
        : () => adminApi.submitVendorRating(projectId, record.id, record.rowVersion);
    void runMutation(operation, `procurement.success.${kind}Submitted`, false);
  };

  const decide = () => {
    if (!decision) return;
    if (!decision.approved && decisionReason.trim().length < 3) {
      setFormError(t("procurement.validation.rejectionReason")); return;
    }
    const reason = decisionReason.trim() || undefined;
    const operation = decision.kind === "boq"
      ? () => adminApi.decideProjectBoqRevision(projectId, decision.id, decision.approved, decision.rowVersion, reason)
      : decision.kind === "request"
        ? () => adminApi.decideMaterialRequest(projectId, decision.id, decision.approved, decision.rowVersion, reason)
        : () => adminApi.decideVendorRating(projectId, decision.id, decision.approved, decision.rowVersion, reason);
    void runMutation(operation, decision.approved ? "procurement.success.approved" : "procurement.success.rejected", false);
  };

  const exportBoq = async () => {
    if (!projectId) return;
    try {
      const response = await adminApi.exportProjectBoqRevisions(projectId, { ...boqListParams, page: undefined, pageSize: undefined });
      const url = URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = `project-${projectId}-boq.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (reason) {
      toast({ title: t("common.error"), description: extractApiError(reason), variant: "destructive" });
    }
  };

  if (projectsLoading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (!projectId && error) return <AdminLayout><PageError message={error} /></AdminLayout>;

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="flex items-center gap-2 text-primary">
              <ShoppingCart className="h-5 w-5" />
              <span className="text-xs font-semibold uppercase">
                {t("nav.procurement")}
              </span>
            </div>
            <h1 className="mt-1 text-2xl font-semibold">
              {t("procurement.title")}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {t("procurement.subtitle")}
            </p>
          </div>
          <div className="grid w-full grid-cols-[minmax(0,1fr)_44px] items-end gap-2 lg:max-w-xl">
            <Label htmlFor="procurement-project" className="sr-only">
              {t("procurement.project.label")}
            </Label>
            <Select
              value={projectId ? String(projectId) : ""}
              onValueChange={(value) => {
                setSearchParams((current) => {
                  const next = new URLSearchParams(current);
                  next.set("projectId", value);
                  next.set("tab", activeTab);
                  return next;
                }, { replace: true });
              }}
            >
              <SelectTrigger
                id="procurement-project"
                className="h-11 min-w-0"
              >
                <SelectValue
                  placeholder={t("procurement.project.placeholder")}
                />
              </SelectTrigger>
              <SelectContent className="max-h-80">
                {projects.map((project) => (
                  <SelectItem key={project.id} value={String(project.id)}>
                    {project.code} · {project.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              variant="outline"
              size="icon"
              className="h-11 w-11"
              title={t("procurement.refresh")}
              aria-label={t("procurement.refresh")}
              disabled={!projectId || loading || requestListLoading}
              onClick={() => void refreshData()}
            >
              <RefreshCw
                className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"}
              />
            </Button>
          </div>
        </header>

        {!projectId ? (
          <PageEmpty
            message={
              projects.length
                ? t("procurement.project.emptySelection")
                : t("procurement.project.empty")
            }
          />
        ) : loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void loadWorkspace()} />
        ) : workspace ? (
          <Tabs
            value={activeTab}
            onValueChange={(value) => {
              setSearchParams((current) => {
                const next = new URLSearchParams(current);
                next.set("tab", value);
                return next;
              }, { replace: true });
            }}
            className="space-y-4"
          >
            <div className="overflow-x-auto pb-1 [scrollbar-width:thin] [&::-webkit-scrollbar]:h-1.5 [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-border [&::-webkit-scrollbar-track]:bg-transparent">
              <TabsList className="h-auto w-max min-w-full justify-start">
                {canViewBoq && <TabsTrigger value="boq">
                  <ClipboardCheck className="mr-2 h-4 w-4" />
                  {t("procurement.tabs.boq")} ({boqList.total})
                </TabsTrigger>}
                {canViewRequests && (
                  <TabsTrigger value="requests">
                    <ShoppingCart className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.requests")} (
                    {requestTotal})
                  </TabsTrigger>
                )}
                {canViewContracts && (
                  <TabsTrigger value="contracts">
                    <FilePlus2 className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.contracts")} (
                    {workspace.contractLines.length})
                  </TabsTrigger>
                )}
                {canViewWarehouse && (
                  <TabsTrigger value="warehouse">
                    <Boxes className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.warehouse")} (
                    {warehouseTotal})
                  </TabsTrigger>
                )}
                {canViewRatings && (
                  <TabsTrigger value="ratings">
                    <Star className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.ratings")} (
                    {workspace.vendorRatings.length})
                  </TabsTrigger>
                )}
              </TabsList>
            </div>

            {canViewRequests && activeTab === "requests" && (
              <MaterialRequestListToolbar
                search={requestSearchInput}
                statusValue={requestStatus}
                owner={requestOwner}
                requiredFrom={requestRequiredFrom}
                requiredTo={requestRequiredTo}
                sortDirection={requestSortDirection}
                users={users}
                total={requestTotal}
                page={requestPage}
                pageSize={20}
                loading={requestListLoading}
                exporting={requestExporting}
                t={t}
                onSearch={setRequestSearchInput}
                onStatus={(value) => {
                  setRequestStatus(value);
                  setRequestPage(1);
                }}
                onOwner={(value) => {
                  setRequestOwner(value);
                  setRequestPage(1);
                }}
                onRequiredFrom={(value) => {
                  setRequestRequiredFrom(value);
                  setRequestPage(1);
                }}
                onRequiredTo={(value) => {
                  setRequestRequiredTo(value);
                  setRequestPage(1);
                }}
                onToggleSort={() => {
                  setRequestSortDirection((current) =>
                    current === "asc" ? "desc" : "asc",
                  );
                  setRequestPage(1);
                }}
                onPage={setRequestPage}
                onExport={() => void exportMaterialRequests()}
              />
            )}

            {canViewBoq && (
              <TabsContent value="boq" className="space-y-4">
                <div className="grid gap-3 border-y py-4 sm:grid-cols-2 lg:grid-cols-[minmax(220px,1fr)_180px_180px_150px_auto]">
                  <div className="space-y-1.5"><Label htmlFor="boq-search">{t("procurement.boq.filter.search")}</Label><div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input id="boq-search" className="pl-9" value={boqSearch} onChange={(event) => setBoqSearch(event.target.value)} placeholder={t("procurement.boq.filter.searchPlaceholder")} /></div></div>
                  <div className="space-y-1.5"><Label htmlFor="boq-status">{t("procurement.field.status")}</Label><Select value={boqStatus} onValueChange={(value) => { setBoqStatus(value); setBoqPage(1); }}><SelectTrigger id="boq-status"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.boq.filter.allStatuses")}</SelectItem>{["Draft", "Submitted", "Approved", "Rejected"].map((value) => <SelectItem value={value} key={value}>{t(`procurement.status.${value}`)}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-1.5"><Label htmlFor="boq-sort">{t("procurement.boq.filter.sort")}</Label><Select value={boqSortBy} onValueChange={(value) => { setBoqSortBy(value as ProjectBoqRevisionListParams["sortBy"]); setBoqPage(1); }}><SelectTrigger id="boq-sort"><SelectValue /></SelectTrigger><SelectContent>{["revision", "status", "total", "preparedBy", "createdAt", "updatedAt"].map((value) => <SelectItem value={value} key={value}>{t(`procurement.boq.sort.${value}`)}</SelectItem>)}</SelectContent></Select></div>
                  <div className="space-y-1.5"><Label htmlFor="boq-direction">{t("procurement.boq.filter.direction")}</Label><Select value={boqSortDirection} onValueChange={(value) => { setBoqSortDirection(value as "asc" | "desc"); setBoqPage(1); }}><SelectTrigger id="boq-direction"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="desc">{t("procurement.boq.sort.desc")}</SelectItem><SelectItem value="asc">{t("procurement.boq.sort.asc")}</SelectItem></SelectContent></Select></div>
                  <div className="flex items-end"><Button variant="outline" onClick={() => void exportBoq()} disabled={boqListLoading || boqList.total === 0}><Download className="mr-2 h-4 w-4" />{t("procurement.boq.export")}</Button></div>
                </div>
                {boqListLoading ? <PageLoading /> : boqListError ? <PageError message={boqListError} onRetry={() => void loadBoqList()} /> : (
                  <>
                    <BoqPanel rows={boqList.items} canManage={canManageBoq} canApprove={canApproveBoq} status={status} money={money} t={t} onCreate={() => openDialog("boq")} onSubmit={(row) => submit("boq", row)} onDecision={(row, approved) => { setFormError(null); setDecisionReason(""); setDecision({ kind: "boq", id: row.id, rowVersion: row.rowVersion, approved }); }} busy={busy} />
                    {boqList.total > boqList.pageSize && <div className="flex items-center justify-between gap-3"><p className="text-sm text-muted-foreground">{t("procurement.boq.pagination", { page: boqList.page, pages: Math.ceil(boqList.total / boqList.pageSize), total: boqList.total })}</p><div className="flex gap-2"><Button variant="outline" size="sm" disabled={boqPage <= 1} onClick={() => setBoqPage((current) => current - 1)}>{t("common.prev")}</Button><Button variant="outline" size="sm" disabled={boqPage >= Math.ceil(boqList.total / boqList.pageSize)} onClick={() => setBoqPage((current) => current + 1)}>{t("common.next")}</Button></div></div>}
                  </>
                )}
              </TabsContent>
            )}
            {canViewRequests && (
              <TabsContent value="requests">
                {requestListLoading &&
                requestRows.length === 0 ? (
                  <PageLoading />
                ) : requestListError ? (
                  <PageError
                    message={requestListError}
                    onRetry={() => void loadMaterialRequests()}
                  />
                ) : (
                  <RequestPanel
                    rows={requestRows}
                    canManage={canManageRequests}
                    canApprove={canApproveRequests}
                    status={status}
                    number={number}
                    formatDate={formatDate}
                    t={t}
                    onCreate={() => openDialog("request")}
                    onSubmit={(row) => submit("request", row)}
                    onDecision={(row, approved) => {
                      setFormError(null);
                      setDecisionReason("");
                      setDecision({
                        kind: "request",
                        id: row.id,
                        rowVersion: row.rowVersion,
                        approved,
                      });
                    }}
                    busy={busy}
                    hasApprovedBoq={requestBoqContext != null}
                    currentUserId={currentUser?.userId}
                  />
                )}
              </TabsContent>
            )}
            {canViewContracts && (
              <TabsContent value="contracts">
                <ContractLinesPanel
                  rows={workspace.contractLines}
                  canManage={canManageContracts}
                  money={money}
                  number={number}
                  t={t}
                  onCreate={() => openDialog("contract")}
                  hasDependencies={Boolean(
                    approvedBoqLines.length &&
                    (contracts.length || !contractsLookupAvailable),
                  )}
                />
              </TabsContent>
            )}
            {canViewWarehouse && (
              <TabsContent value="warehouse">
                <WarehouseWorkspace
                  projectId={projectId}
                  rows={warehouseRows}
                  stock={warehouseStock}
                  total={warehouseTotal}
                  page={warehousePage}
                  pageSize={20}
                  loading={warehouseLoading}
                  error={warehouseError}
                  search={warehouseSearchInput}
                  type={warehouseType}
                  status={warehouseStatus}
                  responsibleUserId={warehouseResponsibleUser}
                  occurredFrom={warehouseOccurredFrom}
                  occurredTo={warehouseOccurredTo}
                  sortDirection={warehouseSortDirection}
                  users={users}
                  canPost={canPostWarehouse}
                  canCreateReceipt={receivableLines.length > 0}
                  canCreateIssue={warehouseStock.some((item) => item.onHandQuantity > 0)}
                  exporting={warehouseExporting}
                  t={t}
                  formatDate={formatDate}
                  formatNumber={(value) => number.format(value)}
                  onSearch={setWarehouseSearchInput}
                  onType={(value) => { setWarehouseType(value); setWarehousePage(1); }}
                  onStatus={(value) => { setWarehouseStatus(value); setWarehousePage(1); }}
                  onResponsibleUser={(value) => { setWarehouseResponsibleUser(value); setWarehousePage(1); }}
                  onOccurredFrom={(value) => { setWarehouseOccurredFrom(value); setWarehousePage(1); }}
                  onOccurredTo={(value) => { setWarehouseOccurredTo(value); setWarehousePage(1); }}
                  onToggleSort={() => { setWarehouseSortDirection((current) => current === "asc" ? "desc" : "asc"); setWarehousePage(1); }}
                  onPage={setWarehousePage}
                  onExport={() => void exportWarehouseTransactions()}
                  onRetry={() => void loadWarehouseTransactions()}
                  onCreateReceipt={() => openDialog("receipt")}
                  onCreateIssue={() => openDialog("issue")}
                />
              </TabsContent>
            )}
            {canViewRatings && (
              <TabsContent value="ratings">
                <RatingsPanel
                  rows={workspace.vendorRatings}
                  canManage={canManageRatings}
                  canApprove={canApproveRatings}
                  status={status}
                  number={number}
                  t={t}
                  onCreate={() => openDialog("rating")}
                  onSubmit={(row) => submit("rating", row)}
                  onDecision={(row, approved) => {
                    setFormError(null);
                    setDecisionReason("");
                    setDecision({
                      kind: "rating",
                      id: row.id,
                      rowVersion: row.rowVersion,
                      approved,
                    });
                  }}
                  busy={busy}
                  hasContracts={
                    ratingContracts.length > 0 || !contractsLookupAvailable
                  }
                />
              </TabsContent>
            )}
          </Tabs>
        ) : null}
      </div>

      <Dialog
        open={dialog != null}
        onOpenChange={(open) => {
          if (!open && !busy) setDialog(null);
        }}
      >
        <DialogContent
          className="max-h-[92vh] w-[95vw] max-w-4xl overflow-y-auto"
          onCloseAutoFocus={(event) => {
            if (!dialogTriggerRef.current) return;
            event.preventDefault();
            dialogTriggerRef.current.focus();
          }}
        >
          <DialogHeader>
            <DialogTitle>
              {dialog ? t(`procurement.dialog.${dialog}.title`) : ""}
            </DialogTitle>
            <DialogDescription>
              {dialog ? t(`procurement.dialog.${dialog}.description`) : ""}
            </DialogDescription>
          </DialogHeader>
          {dialog === "boq" && (
            <BoqForm
              currency={boqCurrency}
              setCurrency={setBoqCurrency}
              lines={boqLines}
              setLines={setBoqLines}
              t={t}
            />
          )}
          {dialog === "request" && (
            <RequestForm
              form={requestForm}
              setForm={setRequestForm}
              lines={requestLines}
              setLines={setRequestLines}
              users={users}
              procurementUsers={procurementUsers}
              boqLines={requestBoqLines}
              t={t}
            />
          )}
          {dialog === "contract" && (
            <ContractLineForm
              form={contractForm}
              setForm={setContractForm}
              users={users}
              contracts={contracts}
              contractsLookupAvailable={contractsLookupAvailable}
              boqLines={approvedBoqLines}
              t={t}
            />
          )}
          {dialog === "receipt" && (
            <ReceiptForm
              inspectedAt={receiptAt}
              setInspectedAt={setReceiptAt}
              lines={receiptLines}
              setLines={setReceiptLines}
              requestLines={receivableLines}
              contractLines={workspace?.contractLines ?? []}
              t={t}
              number={number}
            />
          )}
          {dialog === "issue" && (
            <IssueForm
              form={issueForm}
              setForm={setIssueForm}
              lines={issueLines}
              setLines={setIssueLines}
              users={users}
              boqLines={approvedBoqLines}
              t={t}
            />
          )}
          {dialog === "rating" && (
            <RatingForm
              form={ratingForm}
              setForm={setRatingForm}
              contracts={ratingContracts}
              contractsLookupAvailable={contractsLookupAvailable}
              t={t}
            />
          )}
          {formError && (
            <p className="text-sm text-destructive" role="alert">
              {formError}
            </p>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setDialog(null)}
              disabled={busy}
            >
              {t("common.cancel")}
            </Button>
            <Button
              onClick={
                dialog === "boq"
                  ? createBoq
                  : dialog === "request"
                    ? createRequest
                    : dialog === "contract"
                      ? createContractLine
                      : dialog === "receipt"
                        ? createReceipt
                        : dialog === "issue"
                          ? createIssue
                          : createRating
              }
              disabled={busy}
            >
              {busy ? t("common.saving") : t("procurement.action.create")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={decision != null}
        onOpenChange={(open) => {
          if (!open && !busy) setDecision(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {t(
                decision?.approved
                  ? "procurement.decision.approveTitle"
                  : "procurement.decision.rejectTitle",
              )}
            </DialogTitle>
            <DialogDescription>
              {t("procurement.decision.description")}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="procurement-decision-reason">
              {t("procurement.field.decisionReason")}
            </Label>
            <Textarea
              id="procurement-decision-reason"
              maxLength={2000}
              value={decisionReason}
              onChange={(event) => setDecisionReason(event.target.value)}
              placeholder={t("procurement.decision.reasonPlaceholder")}
            />
          </div>
          {formError && (
            <p className="text-sm text-destructive" role="alert">
              {formError}
            </p>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setDecision(null)}
              disabled={busy}
            >
              {t("common.cancel")}
            </Button>
            <Button
              variant={decision?.approved ? "default" : "destructive"}
              onClick={decide}
              disabled={busy}
            >
              {t(
                decision?.approved
                  ? "procurement.action.approve"
                  : "procurement.action.reject",
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

type Translate = (key: string, params?: Record<string, string | number>) => string;

const PanelHeader = ({ title, description, action }: { title: string; description: string; action?: ReactNode }) => <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h2 className="text-lg font-semibold">{title}</h2><p className="text-sm text-muted-foreground">{description}</p></div>{action}</div>;
const Empty = ({ children }: { children: ReactNode }) => <div className="border border-dashed p-8 text-center text-sm text-muted-foreground">{children}</div>;
const ActionButton = ({ icon, children, ...props }: React.ComponentProps<typeof Button> & { icon: ReactNode }) => <Button size="sm" {...props}>{icon}{children}</Button>;
const TableShell = ({ children }: { children: ReactNode }) => <div className="hidden overflow-x-auto border-y md:block">{children}</div>;
const MobileList = ({ children }: { children: ReactNode }) => <div className="grid gap-3 md:hidden">{children}</div>;
const MobileCard = ({ title, badge, children, actions }: { title: string; badge?: ReactNode; children: ReactNode; actions?: ReactNode }) => <article className="rounded-md border p-4"><div className="flex items-start justify-between gap-3"><h3 className="min-w-0 break-words font-semibold">{title}</h3>{badge}</div><dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-sm">{children}</dl>{actions && <div className="mt-4 flex flex-wrap gap-2 border-t pt-3">{actions}</div>}</article>;
const Datum = ({ label, children }: { label: string; children: ReactNode }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-0.5 break-words">{children}</dd></div>;
const Table = ({ headers, children, minWidth = "min-w-[880px]" }: { headers: string[]; children: ReactNode; minWidth?: string }) => <table className={`w-full ${minWidth} text-sm`}><thead><tr className="border-b text-left text-xs uppercase text-muted-foreground">{headers.map((header) => <th className="px-3 py-3 font-medium" key={header}>{header}</th>)}</tr></thead><tbody>{children}</tbody></table>;
const AddButton = ({ onClick, children }: { onClick: () => void; children: ReactNode }) => <Button type="button" variant="outline" size="sm" onClick={onClick}><Plus className="mr-2 h-4 w-4" />{children}</Button>;
const RemoveButton = ({ onClick, label, disabled }: { onClick: () => void; label: string; disabled: boolean }) => <Button type="button" variant="ghost" size="icon" title={label} aria-label={label} onClick={onClick} disabled={disabled}><Trash2 className="h-4 w-4 text-destructive" /></Button>;

const RecordActions = ({ statusValue, canManage, canApprove, busy, t, onSubmit, onDecision, allowRejectedSubmit = false }: { statusValue: string; canManage: boolean; canApprove: boolean; busy: boolean; t: Translate; onSubmit: () => void; onDecision: (approved: boolean) => void; allowRejectedSubmit?: boolean }) => <div className="flex flex-wrap gap-2">{canManage && (statusValue === "Draft" || (allowRejectedSubmit && statusValue === "Rejected")) && <ActionButton variant="outline" disabled={busy} onClick={onSubmit} icon={<Send className="mr-2 h-4 w-4" />}>{t("procurement.action.submit")}</ActionButton>}{canApprove && statusValue === "Submitted" && <><ActionButton disabled={busy} onClick={() => onDecision(true)} icon={<Check className="mr-2 h-4 w-4" />}>{t("procurement.action.approve")}</ActionButton><ActionButton variant="destructive" disabled={busy} onClick={() => onDecision(false)} icon={<X className="mr-2 h-4 w-4" />}>{t("procurement.action.reject")}</ActionButton></>}</div>;

const BoqPanel = ({ rows, canManage, canApprove, status, money, t, onCreate, onSubmit, onDecision, busy }: { rows: ProjectBoqRevisionListItemResponse[]; canManage: boolean; canApprove: boolean; status: (value: string) => ReactNode; money: Intl.NumberFormat; t: Translate; onCreate: () => void; onSubmit: (row: ProjectBoqRevisionListItemResponse) => void; onDecision: (row: ProjectBoqRevisionListItemResponse, approved: boolean) => void; busy: boolean }) => (
  <section>
    <PanelHeader title={t("procurement.boq.title")} description={t("procurement.boq.description")} action={canManage && <Button onClick={onCreate}><Plus className="mr-2 h-4 w-4" />{t("procurement.boq.create")}</Button>} />
    {rows.length === 0 ? <Empty>{t("procurement.boq.empty")}</Empty> : <>
      <TableShell>
        <Table headers={[t("procurement.field.revision"), t("procurement.field.status"), t("procurement.field.lines"), t("procurement.field.total"), t("procurement.field.preparedBy"), t("procurement.field.actions")]}>
          <>{rows.map((row) => <tr className="border-b" key={row.id}>
            <td className="px-3 py-3 font-medium">R{row.revisionNumber}{row.isFinal && <Badge className="ml-2" variant="secondary">{t("procurement.boq.final")}</Badge>}</td>
            <td className="px-3 py-3">{status(row.status)}</td>
            <td className="px-3 py-3">{row.lineCount}</td>
            <td className="px-3 py-3">{money.format(row.costTotal)} {row.currency}</td>
            <td className="px-3 py-3">{row.preparedByName ?? `#${row.preparedByUserId}`}</td>
            <td className="px-3 py-3"><div className="flex flex-wrap gap-2"><Button asChild variant="outline" size="sm"><Link to={`/admin/procurement-control/projects/${row.operationalProjectId}/boq/${row.id}`}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button><RecordActions statusValue={row.status} canManage={canManage} canApprove={canApprove} busy={busy} t={t} onSubmit={() => onSubmit(row)} onDecision={(approved) => onDecision(row, approved)} allowRejectedSubmit /></div></td>
          </tr>)}</>
        </Table>
      </TableShell>
      <MobileList>{rows.map((row) => <MobileCard key={row.id} title={`R${row.revisionNumber}`} badge={status(row.status)} actions={<><Button asChild variant="outline" size="sm"><Link to={`/admin/procurement-control/projects/${row.operationalProjectId}/boq/${row.id}`}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button><RecordActions statusValue={row.status} canManage={canManage} canApprove={canApprove} busy={busy} t={t} onSubmit={() => onSubmit(row)} onDecision={(approved) => onDecision(row, approved)} allowRejectedSubmit /></>}><Datum label={t("procurement.field.lines")}>{row.lineCount}</Datum><Datum label={t("procurement.field.total")}>{money.format(row.costTotal)} {row.currency}</Datum><Datum label={t("procurement.field.preparedBy")}>{row.preparedByName ?? `#${row.preparedByUserId}`}</Datum><Datum label={t("procurement.field.revision")}>{row.isFinal ? t("procurement.boq.final") : `R${row.revisionNumber}`}</Datum></MobileCard>)}</MobileList>
    </>}
  </section>
);

const RequestPanel = ({
  rows,
  canManage,
  canApprove,
  status,
  number,
  formatDate,
  t,
  onCreate,
  onSubmit,
  onDecision,
  busy,
  hasApprovedBoq,
  currentUserId,
}: {
  rows: MaterialRequestResponse[];
  canManage: boolean;
  canApprove: boolean;
  status: (value: string) => ReactNode;
  number: Intl.NumberFormat;
  formatDate: (value?: string | null) => string;
  t: Translate;
  onCreate: () => void;
  onSubmit: (row: MaterialRequestResponse) => void;
  onDecision: (row: MaterialRequestResponse, approved: boolean) => void;
  busy: boolean;
  hasApprovedBoq: boolean;
  currentUserId?: number;
}) => (
  <section>
    <PanelHeader
      title={t("procurement.request.title")}
      description={t("procurement.request.description")}
      action={
        canManage && hasApprovedBoq && (
          <Button onClick={onCreate}>
            <Plus className="mr-2 h-4 w-4" />
            {t("procurement.request.create")}
          </Button>
        )
      }
    />
    {!hasApprovedBoq && (
      <p className="mb-4 border-l-2 border-amber-400 px-3 text-sm text-amber-800">
        {t("procurement.request.requiresBoq")}
      </p>
    )}
    {rows.length === 0 ? (
      <Empty>{t("procurement.request.empty")}</Empty>
    ) : (
      <>
        <TableShell>
          <Table
            headers={[
              t("procurement.field.code"),
              t("procurement.field.status"),
              t("procurement.field.requiredAt"),
              t("procurement.field.requester"),
              t("procurement.field.responsibleSite"),
              t("procurement.field.procurementOwner"),
              t("procurement.field.lines"),
              t("procurement.field.actions"),
            ]}
            minWidth="min-w-[1240px]"
          >
            <>
              {rows.map((row) => (
                <tr className="border-b" key={row.id}>
                  <td className="px-3 py-3 font-medium">{row.code}</td>
                  <td className="px-3 py-3">{status(row.status)}</td>
                  <td className="px-3 py-3">{formatDate(row.requiredAt)}</td>
                  <td className="px-3 py-3">
                    {row.siteRequesterName ?? `#${row.siteRequesterUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    {row.responsibleSiteUserName ??
                      `#${row.responsibleSiteUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    {row.assignedProcurementUserName ??
                      `#${row.assignedProcurementUserId}`}
                  </td>
                  <td className="px-3 py-3">
                    <div className="space-y-1">
                      {row.lines.map((line) => (
                        <p key={line.id} className="whitespace-nowrap">
                          <span className="font-medium">{line.itemCode}</span>: {number.format(line.requestedQuantity)} {line.unit}
                          <span className="ml-2 text-xs text-muted-foreground">
                            {t("procurement.field.receivedQuantity")}: {number.format(line.receivedQuantity)} · {t("procurement.field.boqRemainingQuantity")}: {number.format(line.boqRemainingQuantity)}
                          </span>
                        </p>
                      ))}
                    </div>
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex flex-wrap gap-2">
                      <Button asChild variant="outline" size="sm"><Link to={`/admin/procurement-control/projects/${row.operationalProjectId}/material-requests/${row.id}`}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button>
                      <RecordActions
                        statusValue={row.status}
                        canManage={canManage && row.siteRequesterUserId === currentUserId}
                        canApprove={canApprove}
                        busy={busy}
                        t={t}
                        onSubmit={() => onSubmit(row)}
                        onDecision={(approved) => onDecision(row, approved)}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </>
          </Table>
        </TableShell>
        <MobileList>
          {rows.map((row) => (
            <MobileCard
              key={row.id}
              title={row.code}
              badge={status(row.status)}
              actions={
                <>
                  <Button asChild variant="outline" size="sm"><Link to={`/admin/procurement-control/projects/${row.operationalProjectId}/material-requests/${row.id}`}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button>
                  <RecordActions
                    statusValue={row.status}
                    canManage={canManage && row.siteRequesterUserId === currentUserId}
                    canApprove={canApprove}
                    busy={busy}
                    t={t}
                    onSubmit={() => onSubmit(row)}
                    onDecision={(approved) => onDecision(row, approved)}
                  />
                </>
              }
            >
              <Datum label={t("procurement.field.requiredAt")}>
                {formatDate(row.requiredAt)}
              </Datum>
              <Datum label={t("procurement.field.requester")}>
                {row.siteRequesterName ?? `#${row.siteRequesterUserId}`}
              </Datum>
              <Datum label={t("procurement.field.responsibleSite")}>
                {row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}
              </Datum>
              <Datum label={t("procurement.field.procurementOwner")}>
                {row.assignedProcurementUserName ??
                  `#${row.assignedProcurementUserId}`}
              </Datum>
              <Datum label={t("procurement.field.lines")}>
                <span className="space-y-1">
                  {row.lines.map((line) => (
                    <span key={line.id} className="block">
                      {line.itemCode}: {number.format(line.requestedQuantity)} {line.unit} · {t("procurement.field.receivedQuantity")}: {number.format(line.receivedQuantity)} · {t("procurement.field.boqRemainingQuantity")}: {number.format(line.boqRemainingQuantity)}
                    </span>
                  ))}
                </span>
              </Datum>
            </MobileCard>
          ))}
        </MobileList>
      </>
    )}
  </section>
);

const ContractLinesPanel = ({
  rows,
  canManage,
  money,
  number,
  t,
  onCreate,
  hasDependencies,
}: {
  rows: ProcurementContractLineResponse[];
  canManage: boolean;
  money: Intl.NumberFormat;
  number: Intl.NumberFormat;
  t: Translate;
  onCreate: () => void;
  hasDependencies: boolean;
}) => (
  <section>
    <PanelHeader
      title={t("procurement.contractLine.title")}
      description={t("procurement.contractLine.description")}
      action={
        canManage && (
          <Button onClick={onCreate} disabled={!hasDependencies}>
            <Plus className="mr-2 h-4 w-4" />
            {t("procurement.contractLine.create")}
          </Button>
        )
      }
    />
    {!hasDependencies && (
      <p className="mb-4 border-l-2 border-amber-400 px-3 text-sm text-amber-800">
        {t("procurement.contractLine.requiresData")}
      </p>
    )}
    {rows.length === 0 ? (
      <Empty>{t("procurement.contractLine.empty")}</Empty>
    ) : (
      <>
        <TableShell>
          <Table
            headers={[
              t("procurement.field.contract"),
              t("procurement.field.item"),
              t("procurement.field.owner"),
              t("procurement.field.quantity"),
              t("procurement.field.budgetPrice"),
              t("procurement.field.negotiatedPrice"),
            ]}
          >
            <>
              {rows.map((row) => (
                <tr className="border-b" key={row.id}>
                  <td className="px-3 py-3 font-medium">
                    {row.contractNumber}
                  </td>
                  <td className="px-3 py-3">{row.itemCode}</td>
                  <td className="px-3 py-3">
                    {row.procurementOwnerName ??
                      `#${row.procurementOwnerUserId}`}
                  </td>
                  <td className="px-3 py-3">{number.format(row.quantity)}</td>
                  <td className="px-3 py-3">
                    {money.format(row.budgetUnitPrice)}
                  </td>
                  <td className="px-3 py-3">
                    {money.format(row.negotiatedUnitPrice)}
                  </td>
                </tr>
              ))}
            </>
          </Table>
        </TableShell>
        <MobileList>
          {rows.map((row) => (
            <MobileCard
              key={row.id}
              title={`${row.contractNumber} · ${row.itemCode}`}
            >
              <Datum label={t("procurement.field.owner")}>
                {row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}
              </Datum>
              <Datum label={t("procurement.field.quantity")}>
                {number.format(row.quantity)}
              </Datum>
              <Datum label={t("procurement.field.budgetPrice")}>
                {money.format(row.budgetUnitPrice)}
              </Datum>
              <Datum label={t("procurement.field.negotiatedPrice")}>
                {money.format(row.negotiatedUnitPrice)}
              </Datum>
            </MobileCard>
          ))}
        </MobileList>
      </>
    )}
  </section>
);

const WarehousePanel = ({ receipts, issues, canPost, status, number, formatDate, t, onCreateReceipt, onCreateIssue, onPostReceipt, onPostIssue, busy, canCreateReceipt, canCreateIssue }: { receipts: WarehouseReceiptResponse[]; issues: WarehouseIssueResponse[]; canPost: boolean; status: (value: string) => ReactNode; number: Intl.NumberFormat; formatDate: (value?: string | null) => string; t: Translate; onCreateReceipt: () => void; onCreateIssue: () => void; onPostReceipt: (row: WarehouseReceiptResponse) => void; onPostIssue: (row: WarehouseIssueResponse) => void; busy: boolean; canCreateReceipt: boolean; canCreateIssue: boolean }) => <section className="space-y-7"><div><PanelHeader title={t("procurement.receipt.title")} description={t("procurement.receipt.description")} action={canPost && <Button onClick={onCreateReceipt} disabled={!canCreateReceipt}><PackageCheck className="mr-2 h-4 w-4" />{t("procurement.receipt.create")}</Button>} />{receipts.length === 0 ? <Empty>{t("procurement.receipt.empty")}</Empty> : <><TableShell><Table headers={[t("procurement.field.code"), t("procurement.field.status"), t("procurement.field.inspectedAt"), t("procurement.field.lines"), t("procurement.field.actions")]}><>{receipts.map((row) => <tr className="border-b" key={row.id}><td className="px-3 py-3 font-medium">{row.code}</td><td className="px-3 py-3">{status(row.status)}</td><td className="px-3 py-3">{formatDate(row.inspectedAt)}</td><td className="px-3 py-3">{row.lines.map((line) => `${line.itemCode}: ${number.format(line.receivedQuantity)}`).join(", ")}</td><td className="px-3 py-3">{canPost && row.status === "Draft" && <ActionButton onClick={() => onPostReceipt(row)} disabled={busy} icon={<PackageCheck className="mr-2 h-4 w-4" />}>{t("procurement.action.post")}</ActionButton>}</td></tr>)}</></Table></TableShell><MobileList>{receipts.map((row) => <MobileCard key={row.id} title={row.code} badge={status(row.status)} actions={canPost && row.status === "Draft" ? <ActionButton onClick={() => onPostReceipt(row)} disabled={busy} icon={<PackageCheck className="mr-2 h-4 w-4" />}>{t("procurement.action.post")}</ActionButton> : undefined}><Datum label={t("procurement.field.inspectedAt")}>{formatDate(row.inspectedAt)}</Datum><Datum label={t("procurement.field.lines")}>{row.lines.length}</Datum></MobileCard>)}</MobileList></>}</div><div className="border-t pt-6"><PanelHeader title={t("procurement.issue.title")} description={t("procurement.issue.description")} action={canPost && <Button onClick={onCreateIssue} disabled={!canCreateIssue}><PackageMinus className="mr-2 h-4 w-4" />{t("procurement.issue.create")}</Button>} />{issues.length === 0 ? <Empty>{t("procurement.issue.empty")}</Empty> : <><TableShell><Table headers={[t("procurement.field.code"), t("procurement.field.status"), t("procurement.field.issuedAt"), t("procurement.field.responsibleSite"), t("procurement.field.workItem"), t("procurement.field.lines"), t("procurement.field.actions")]} minWidth="min-w-[980px]"><>{issues.map((row) => <tr className="border-b" key={row.id}><td className="px-3 py-3 font-medium">{row.code}</td><td className="px-3 py-3">{status(row.status)}</td><td className="px-3 py-3">{formatDate(row.issuedAt)}</td><td className="px-3 py-3">{row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}</td><td className="px-3 py-3">{row.workItemCode ?? "—"}</td><td className="px-3 py-3">{row.lines.map((line) => `${line.itemCode}: ${number.format(line.issuedQuantity)}`).join(", ")}</td><td className="px-3 py-3">{canPost && row.status === "Draft" && <ActionButton onClick={() => onPostIssue(row)} disabled={busy} icon={<PackageMinus className="mr-2 h-4 w-4" />}>{t("procurement.action.post")}</ActionButton>}</td></tr>)}</></Table></TableShell><MobileList>{issues.map((row) => <MobileCard key={row.id} title={row.code} badge={status(row.status)} actions={canPost && row.status === "Draft" ? <ActionButton onClick={() => onPostIssue(row)} disabled={busy} icon={<PackageMinus className="mr-2 h-4 w-4" />}>{t("procurement.action.post")}</ActionButton> : undefined}><Datum label={t("procurement.field.issuedAt")}>{formatDate(row.issuedAt)}</Datum><Datum label={t("procurement.field.responsibleSite")}>{row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}</Datum><Datum label={t("procurement.field.workItem")}>{row.workItemCode ?? "—"}</Datum><Datum label={t("procurement.field.lines")}>{row.lines.length}</Datum></MobileCard>)}</MobileList></>}</div></section>;

const RatingsPanel = ({ rows, canManage, canApprove, status, number, t, onCreate, onSubmit, onDecision, busy, hasContracts }: { rows: VendorRatingResponse[]; canManage: boolean; canApprove: boolean; status: (value: string) => ReactNode; number: Intl.NumberFormat; t: Translate; onCreate: () => void; onSubmit: (row: VendorRatingResponse) => void; onDecision: (row: VendorRatingResponse, approved: boolean) => void; busy: boolean; hasContracts: boolean }) => <section><PanelHeader title={t("procurement.rating.title")} description={t("procurement.rating.description")} action={canManage && <Button onClick={onCreate} disabled={!hasContracts}><Star className="mr-2 h-4 w-4" />{t("procurement.rating.create")}</Button>} />{!hasContracts && <p className="mb-4 border-l-2 border-amber-400 px-3 text-sm text-amber-800">{t("procurement.rating.requiresContract")}</p>}{rows.length === 0 ? <Empty>{t("procurement.rating.empty")}</Empty> : <><TableShell><Table headers={[t("procurement.field.vendor"), t("procurement.field.contract"), t("procurement.field.version"), t("procurement.field.status"), t("procurement.field.overallScore"), t("procurement.field.owner"), t("procurement.field.actions")]} minWidth="min-w-[980px]"><>{rows.map((row) => <tr className="border-b" key={row.id}><td className="px-3 py-3 font-medium">{row.vendorName}</td><td className="px-3 py-3">{row.contractNumber}</td><td className="px-3 py-3">v{row.versionNumber}</td><td className="px-3 py-3">{status(row.status)}</td><td className="px-3 py-3 font-semibold">{number.format(row.overallScore)}</td><td className="px-3 py-3">{row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}</td><td className="px-3 py-3"><RecordActions statusValue={row.status} canManage={canManage} canApprove={canApprove} busy={busy} t={t} onSubmit={() => onSubmit(row)} onDecision={(approved) => onDecision(row, approved)} /></td></tr>)}</></Table></TableShell><MobileList>{rows.map((row) => <MobileCard key={row.id} title={row.vendorName} badge={status(row.status)} actions={<RecordActions statusValue={row.status} canManage={canManage} canApprove={canApprove} busy={busy} t={t} onSubmit={() => onSubmit(row)} onDecision={(approved) => onDecision(row, approved)} />}><Datum label={t("procurement.field.contract")}>{row.contractNumber}</Datum><Datum label={t("procurement.field.version")}>v{row.versionNumber}</Datum><Datum label={t("procurement.field.overallScore")}>{number.format(row.overallScore)}</Datum><Datum label={t("procurement.field.owner")}>{row.procurementOwnerName ?? `#${row.procurementOwnerUserId}`}</Datum></MobileCard>)}</MobileList></>}</section>;

const UserField = ({ id, label, value, onChange, users, t }: { id: string; label: string; value: number; onChange: (value: number) => void; users: UserOption[]; t: Translate }) => <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{users.length ? <Select value={value ? String(value) : ""} onValueChange={(next) => onChange(Number(next))}><SelectTrigger id={id}><SelectValue placeholder={t("procurement.lookup.userPlaceholder")} /></SelectTrigger><SelectContent>{users.map((user) => <SelectItem value={String(user.userId)} key={user.userId}>{user.userName}</SelectItem>)}</SelectContent></Select> : <Input id={id} type="number" min={1} value={value || ""} onChange={(event) => onChange(Number(event.target.value))} placeholder={t("procurement.lookup.userIdPlaceholder")} />}</div>;
const BoqLineField = ({ value, onChange, t }: { value: number; onChange: (value: number) => void; t: Translate }) => <Input type="number" min={0} step="any" value={value} onChange={(event) => onChange(Number(event.target.value))} aria-label={t("procurement.field.quantity")} />;

const BoqForm = ({ currency, setCurrency, lines, setLines, t }: { currency: string; setCurrency: (value: string) => void; lines: BoqLineDraft[]; setLines: React.Dispatch<React.SetStateAction<BoqLineDraft[]>>; t: Translate }) => <div className="space-y-4"><div className="max-w-xs space-y-2"><Label htmlFor="boq-currency">{t("procurement.field.currency")}</Label><Input id="boq-currency" maxLength={3} value={currency} onChange={(event) => setCurrency(event.target.value.toUpperCase())} /></div><div className="space-y-3"><div className="flex items-center justify-between"><Label>{t("procurement.field.lines")}</Label><AddButton onClick={() => setLines((current) => [...current, emptyBoqLine()])}>{t("procurement.action.addLine")}</AddButton></div>{lines.map((line, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-2 lg:grid-cols-[1fr_2fr_0.8fr_1fr_1fr_auto]" key={index}><div><Label>{t("procurement.field.itemCode")}</Label><Input aria-label={t("procurement.field.itemCode")} maxLength={80} value={line.itemCode} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, itemCode: event.target.value } : item))} /></div><div><Label>{t("procurement.field.description")}</Label><Input aria-label={t("procurement.field.description")} maxLength={500} value={line.description} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, description: event.target.value } : item))} /></div><div><Label>{t("procurement.field.unit")}</Label><Input aria-label={t("procurement.field.unit")} maxLength={50} value={line.unit} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, unit: event.target.value } : item))} /></div><div><Label>{t("procurement.field.quantity")}</Label><BoqLineField value={line.approvedQuantity} onChange={(value) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, approvedQuantity: value } : item))} t={t} /></div><div><Label>{t("procurement.field.budgetPrice")}</Label><Input aria-label={t("procurement.field.budgetPrice")} type="number" min={0} step="any" value={line.budgetUnitPrice} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, budgetUnitPrice: Number(event.target.value) } : item))} /></div><div className="self-end"><RemoveButton label={t("procurement.action.removeLine")} disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))} /></div></div>)}</div></div>;

const RequestForm = ({ form, setForm, lines, setLines, users, procurementUsers, boqLines, t }: { form: { responsibleSiteUserId: number; assignedProcurementUserId: number; requiredAt: string; note: string }; setForm: React.Dispatch<React.SetStateAction<typeof form>>; lines: RequestLineDraft[]; setLines: React.Dispatch<React.SetStateAction<RequestLineDraft[]>>; users: UserOption[]; procurementUsers: UserOption[]; boqLines: Array<{ id: number; itemCode: string; description: string; remainingQuantity: number }>; t: Translate }) => <div className="space-y-4"><div className="grid gap-4 sm:grid-cols-2"><UserField id="request-site-user" label={t("procurement.field.responsibleSite")} value={form.responsibleSiteUserId} onChange={(value) => setForm((current) => ({ ...current, responsibleSiteUserId: value }))} users={users} t={t} /><UserField id="request-proc-user" label={t("procurement.field.procurementOwner")} value={form.assignedProcurementUserId} onChange={(value) => setForm((current) => ({ ...current, assignedProcurementUserId: value }))} users={procurementUsers} t={t} /><div className="space-y-2"><Label htmlFor="request-required-at">{t("procurement.field.requiredAt")}</Label><Input id="request-required-at" type="datetime-local" value={form.requiredAt} onChange={(event) => setForm((current) => ({ ...current, requiredAt: event.target.value }))} /></div><div className="space-y-2"><Label htmlFor="request-note">{t("procurement.field.note")}</Label><Input id="request-note" maxLength={2000} value={form.note} onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))} /></div></div><DynamicChoiceLines lines={lines} setLines={setLines} choices={boqLines.map((line) => ({ id: line.id, label: `${line.itemCode} · ${line.description} · ${t("procurement.request.remaining", { quantity: line.remainingQuantity })}` }))} idKey="projectBoqLineId" quantityKey="requestedQuantity" add={emptyRequestLine} t={t} /></div>;

const ContractLineForm = ({ form, setForm, users, contracts, contractsLookupAvailable, boqLines, t }: { form: { contractId: number; projectBoqLineId: number; procurementOwnerUserId: number; quantity: number; negotiatedUnitPrice: number }; setForm: React.Dispatch<React.SetStateAction<typeof form>>; users: UserOption[]; contracts: ContractResponse[]; contractsLookupAvailable: boolean; boqLines: ProjectBoqLineResponse[]; t: Translate }) => <div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label htmlFor="contract-id">{contractsLookupAvailable ? t("procurement.field.contract") : t("procurement.field.contractId")}</Label>{contractsLookupAvailable ? <Select value={form.contractId ? String(form.contractId) : undefined} onValueChange={(value) => setForm((current) => ({ ...current, contractId: Number(value) }))}><SelectTrigger id="contract-id"><SelectValue placeholder={t("procurement.lookup.contractPlaceholder")} /></SelectTrigger><SelectContent>{contracts.map((contract) => <SelectItem value={String(contract.id)} key={contract.id}>{contract.contractNumber}{contract.vendorName ? ` · ${contract.vendorName}` : ""}</SelectItem>)}</SelectContent></Select> : <Input id="contract-id" type="number" min={1} value={form.contractId || ""} onChange={(event) => setForm((current) => ({ ...current, contractId: Number(event.target.value) }))} placeholder={t("procurement.lookup.contractIdPlaceholder")} />}</div><div className="space-y-2"><Label>{t("procurement.field.item")}</Label><Select value={form.projectBoqLineId ? String(form.projectBoqLineId) : undefined} onValueChange={(value) => setForm((current) => ({ ...current, projectBoqLineId: Number(value) }))}><SelectTrigger><SelectValue placeholder={t("procurement.lookup.boqLinePlaceholder")} /></SelectTrigger><SelectContent>{boqLines.map((line) => <SelectItem value={String(line.id)} key={line.id}>{line.itemCode} · {line.description}</SelectItem>)}</SelectContent></Select></div><UserField id="contract-owner" label={t("procurement.field.owner")} value={form.procurementOwnerUserId} onChange={(value) => setForm((current) => ({ ...current, procurementOwnerUserId: value }))} users={users} t={t} /><div className="space-y-2"><Label htmlFor="contract-quantity">{t("procurement.field.quantity")}</Label><Input id="contract-quantity" type="number" min={0.000001} step="any" value={form.quantity} onChange={(event) => setForm((current) => ({ ...current, quantity: Number(event.target.value) }))} /></div><div className="space-y-2"><Label htmlFor="contract-price">{t("procurement.field.negotiatedPrice")}</Label><Input id="contract-price" type="number" min={0} step="any" value={form.negotiatedUnitPrice} onChange={(event) => setForm((current) => ({ ...current, negotiatedUnitPrice: Number(event.target.value) }))} /></div></div>;

const ReceiptForm = ({ inspectedAt, setInspectedAt, lines, setLines, requestLines, contractLines, t, number }: { inspectedAt: string; setInspectedAt: (value: string) => void; lines: ReceiptLineDraft[]; setLines: React.Dispatch<React.SetStateAction<ReceiptLineDraft[]>>; requestLines: Array<{ id: number; projectBoqLineId: number; itemCode: string; requestedQuantity: number; receivedQuantity: number; requestCode: string }>; contractLines: ProcurementContractLineResponse[]; t: Translate; number: Intl.NumberFormat }) => <div className="space-y-4"><div className="max-w-sm space-y-2"><Label htmlFor="receipt-at">{t("procurement.field.inspectedAt")}</Label><Input id="receipt-at" type="datetime-local" value={inspectedAt} onChange={(event) => setInspectedAt(event.target.value)} /></div><div className="space-y-3"><div className="flex items-center justify-between"><Label>{t("procurement.field.lines")}</Label><AddButton onClick={() => setLines((current) => [...current, emptyReceiptLine()])}>{t("procurement.action.addLine")}</AddButton></div>{lines.map((line, index) => { const selectedRequestLine = requestLines.find((item) => item.id === line.materialRequestLineId); const matchingContracts = contractLines.filter((item) => !selectedRequestLine || item.projectBoqLineId === selectedRequestLine.projectBoqLineId); return <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_2fr_1fr_auto]" key={index}><div className="space-y-2"><Label>{t("procurement.field.requestLine")}</Label><Select value={line.materialRequestLineId ? String(line.materialRequestLineId) : ""} onValueChange={(value) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, materialRequestLineId: Number(value), contractLineId: null } : item))}><SelectTrigger><SelectValue placeholder={t("procurement.lookup.requestLinePlaceholder")} /></SelectTrigger><SelectContent>{requestLines.map((item) => <SelectItem value={String(item.id)} key={item.id}>{item.requestCode} · {item.itemCode} · {number.format(item.requestedQuantity - item.receivedQuantity)} {t("procurement.receipt.remaining")}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>{t("procurement.field.contractLine")}</Label><Select value={line.contractLineId ? String(line.contractLineId) : "none"} onValueChange={(value) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, contractLineId: value === "none" ? null : Number(value) } : item))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">{t("procurement.lookup.noContractLine")}</SelectItem>{matchingContracts.map((item) => <SelectItem value={String(item.id)} key={item.id}>{item.contractNumber} · {item.itemCode}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>{t("procurement.field.receivedQuantity")}</Label><Input type="number" min={0.000001} step="any" value={line.receivedQuantity} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, receivedQuantity: Number(event.target.value) } : item))} /></div><div className="self-end"><RemoveButton label={t("procurement.action.removeLine")} disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))} /></div></div>; })}</div></div>;

const IssueForm = ({ form, setForm, lines, setLines, users, boqLines, t }: { form: { issuedAt: string; responsibleSiteUserId: number; workItemCode: string }; setForm: React.Dispatch<React.SetStateAction<typeof form>>; lines: IssueLineDraft[]; setLines: React.Dispatch<React.SetStateAction<IssueLineDraft[]>>; users: UserOption[]; boqLines: ProjectBoqLineResponse[]; t: Translate }) => <div className="space-y-4"><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label htmlFor="issue-at">{t("procurement.field.issuedAt")}</Label><Input id="issue-at" type="datetime-local" value={form.issuedAt} onChange={(event) => setForm((current) => ({ ...current, issuedAt: event.target.value }))} /></div><UserField id="issue-site-user" label={t("procurement.field.responsibleSite")} value={form.responsibleSiteUserId} onChange={(value) => setForm((current) => ({ ...current, responsibleSiteUserId: value }))} users={users} t={t} /><div className="space-y-2 sm:col-span-2"><Label htmlFor="issue-work-item">{t("procurement.field.workItem")}</Label><Input id="issue-work-item" maxLength={100} value={form.workItemCode} onChange={(event) => setForm((current) => ({ ...current, workItemCode: event.target.value }))} /></div></div><DynamicChoiceLines lines={lines} setLines={setLines} choices={boqLines.map((line) => ({ id: line.id, label: `${line.itemCode} · ${line.description}` }))} idKey="projectBoqLineId" quantityKey="issuedQuantity" add={emptyIssueLine} t={t} /></div>;

const RatingForm = ({ form, setForm, contracts, contractsLookupAvailable, t }: { form: { contractId: number; qualityScore: number; scheduleScore: number; costScore: number; hseScore: number; comments: string }; setForm: React.Dispatch<React.SetStateAction<typeof form>>; contracts: ContractResponse[]; contractsLookupAvailable: boolean; t: Translate }) => <div className="space-y-4"><div className="space-y-2"><Label htmlFor="rating-contract-id">{contractsLookupAvailable ? t("procurement.field.contract") : t("procurement.field.contractId")}</Label>{contractsLookupAvailable ? <Select value={form.contractId ? String(form.contractId) : undefined} onValueChange={(value) => setForm((current) => ({ ...current, contractId: Number(value) }))}><SelectTrigger id="rating-contract-id"><SelectValue placeholder={t("procurement.lookup.contractPlaceholder")} /></SelectTrigger><SelectContent>{contracts.map((contract) => <SelectItem value={String(contract.id)} key={contract.id}>{contract.contractNumber} · {contract.vendorName}</SelectItem>)}</SelectContent></Select> : <Input id="rating-contract-id" type="number" min={1} value={form.contractId || ""} onChange={(event) => setForm((current) => ({ ...current, contractId: Number(event.target.value) }))} placeholder={t("procurement.lookup.contractIdPlaceholder")} />}</div><div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">{(["qualityScore", "scheduleScore", "costScore", "hseScore"] as const).map((field) => <div className="space-y-2" key={field}><Label htmlFor={`rating-${field}`}>{t(`procurement.field.${field}`)}</Label><Input id={`rating-${field}`} type="number" min={0} max={100} step="any" value={form[field]} onChange={(event) => setForm((current) => ({ ...current, [field]: Number(event.target.value) }))} /></div>)}</div><div className="space-y-2"><Label htmlFor="rating-comments">{t("procurement.field.comments")}</Label><Textarea id="rating-comments" maxLength={4000} value={form.comments} onChange={(event) => setForm((current) => ({ ...current, comments: event.target.value }))} /></div></div>;

const DynamicChoiceLines = <T extends Record<I | Q, number>, I extends keyof T, Q extends keyof T>({ lines, setLines, choices, idKey, quantityKey, add, t }: { lines: T[]; setLines: React.Dispatch<React.SetStateAction<T[]>>; choices: Array<{ id: number; label: string }>; idKey: I; quantityKey: Q; add: () => T; t: Translate }) => <div className="space-y-3"><div className="flex items-center justify-between"><Label>{t("procurement.field.lines")}</Label><AddButton onClick={() => setLines((current) => [...current, add()])}>{t("procurement.action.addLine")}</AddButton></div>{lines.map((line, index) => <div className="grid gap-3 border-t pt-3 sm:grid-cols-[2fr_1fr_auto]" key={index}><div className="space-y-2"><Label>{t("procurement.field.item")}</Label><Select value={line[idKey] ? String(line[idKey]) : ""} onValueChange={(value) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, [idKey]: Number(value) } : item))}><SelectTrigger aria-label={t("procurement.field.item")}><SelectValue placeholder={t("procurement.lookup.boqLinePlaceholder")} /></SelectTrigger><SelectContent>{choices.map((choice) => <SelectItem value={String(choice.id)} key={choice.id}>{choice.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>{t("procurement.field.quantity")}</Label><Input aria-label={t("procurement.field.quantity")} type="number" min={0.000001} step="any" value={line[quantityKey]} onChange={(event) => setLines((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, [quantityKey]: Number(event.target.value) } : item))} /></div><div className="self-end"><RemoveButton label={t("procurement.action.removeLine")} disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, itemIndex) => itemIndex !== index))} /></div></div>)}</div>;

export default ProcurementControlPage;