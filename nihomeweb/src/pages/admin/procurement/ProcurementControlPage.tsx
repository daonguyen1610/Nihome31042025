import { ProcurementIfcInbox } from "./ProcurementIfcInbox";
import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import {
  DialogKind,
  DecisionKind,
  UserOption,
  BoqLineDraft,
  RequestLineDraft,
  ReceiptLineDraft,
  IssueLineDraft,
  procurementTabs,
  emptyBoqLine,
  emptyRequestLine,
  emptyReceiptLine,
  emptyIssueLine,
  localDateTime,
  apiDateTime,
  statusClass,
} from "./components/procurementValues";
import {
  BoqPanel,
  RequestPanel,
  ContractLinesPanel,
  RatingsPanel,
} from "./components/ProcurementPanels";
import {
  BoqForm,
  RequestForm,
  ContractLineForm,
  ReceiptForm,
  IssueForm,
  RatingForm,
} from "./components/ProcurementForms";
import { collectLookupPages } from "@/lib/pagination";
import { FieldScope } from "@/components/ui/field-scope";
import { personOption, mergePeople, type PersonIdentity } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
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
  TriangleAlert,
  X,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
import MaterialAlertsWorkspace from "./MaterialAlertsWorkspace";
import WarehouseWorkspace from "./WarehouseWorkspace";
import ConstructionResourcesWorkspace from "./ConstructionResourcesWorkspace";

const ProcurementControlPage = () => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const currentUser = useAppSelector((state) => state.auth.user);
  const canViewIfc = has(ADMIN_PERMS.procurementIfc);
  const canViewBoq = has(ADMIN_PERMS.procurement);
  const canViewResources = has(ADMIN_PERMS.procurementResources);
  const canManageBoq = has(ADMIN_PERMS.procurementManage);
  const canApproveBoq = has(ADMIN_PERMS.procurementApprove);
  const canViewRequests = has(ADMIN_PERMS.procurementMaterialRequests);
  const canManageRequests = has(ADMIN_PERMS.procurementMaterialRequestsManage);
  const canApproveRequests = has(ADMIN_PERMS.procurementMaterialRequestsApprove);
  const canViewContracts = has(ADMIN_PERMS.procurementContractLines);
  const canManageContracts = has(ADMIN_PERMS.procurementContractLinesManage);
  const canViewWarehouse = has(ADMIN_PERMS.procurementWarehouse);
  const canPostWarehouse = has(ADMIN_PERMS.procurementWarehousePost);
  const canViewAlerts = has(ADMIN_PERMS.procurementMaterialAlerts);
  const canManageAlerts = has(ADMIN_PERMS.procurementMaterialAlertsManage);
  const canViewRatings = has(ADMIN_PERMS.procurementRatings);
  const canManageRatings = has(ADMIN_PERMS.procurementRatingsManage);
  const canApproveRatings = has(ADMIN_PERMS.procurementRatingsApprove);
  const canReadContracts = has(ADMIN_PERMS.contracts);
  const canReadKpiUsers = has(ADMIN_PERMS.kpi);
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedProjectId = Number(searchParams.get("projectId") ?? 0);
  const projectId =
    Number.isInteger(requestedProjectId) && requestedProjectId > 0 ? requestedProjectId : 0;
  const availableTabs = procurementTabs.filter(
    (tab) =>
      (tab === "ifc" && canViewIfc) ||
      (tab === "alerts" && canViewAlerts) ||
      (tab === "resources" && canViewResources) ||
      (tab === "boq" && canViewBoq) ||
      (tab === "requests" && canViewRequests) ||
      (tab === "contracts" && canViewContracts) ||
      (tab === "warehouse" && canViewWarehouse) ||
      (tab === "ratings" && canViewRatings),
  );
  const requestedTab = searchParams.get("tab");
  const activeTab =
    requestedTab && availableTabs.includes(requestedTab as (typeof procurementTabs)[number])
      ? requestedTab
      : (availableTabs[0] ?? "boq");

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
  const [decision, setDecision] = useState<{
    kind: DecisionKind;
    id: number;
    rowVersion: string;
    approved: boolean;
  } | null>(null);
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
  const [requestBoqContext, setRequestBoqContext] =
    useState<MaterialRequestBoqContextResponse | null>(null);
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
  const [alertTotal, setAlertTotal] = useState(0);
  const [alertRefreshToken, setAlertRefreshToken] = useState(0);

  const [boqCurrency, setBoqCurrency] = useState("VND");
  const [boqUpstreamContractId, setBoqUpstreamContractId] = useState(0);
  const [boqLines, setBoqLines] = useState<BoqLineDraft[]>([emptyBoqLine()]);
  const [boqList, setBoqList] = useState<ProjectBoqRevisionListResponse>({
    total: 0,
    page: 1,
    pageSize: 10,
    items: [],
  });
  const [boqSearch, setBoqSearch] = useState("");
  const [boqDebouncedSearch, setBoqDebouncedSearch] = useState("");
  const [boqStatus, setBoqStatus] = useState("all");
  const [boqSortBy, setBoqSortBy] = useState<ProjectBoqRevisionListParams["sortBy"]>("revision");
  const [boqSortDirection, setBoqSortDirection] =
    useState<ProjectBoqRevisionListParams["sortDirection"]>("desc");
  const [boqPage, setBoqPage] = useState(1);
  const [boqListLoading, setBoqListLoading] = useState(false);
  const [boqListError, setBoqListError] = useState<string | null>(null);
  const [requestForm, setRequestForm] = useState({
    responsibleSiteUserId: 0,
    assignedProcurementUserId: 0,
    requiredAt: localDateTime(1),
    note: "",
  });
  const [requestLines, setRequestLines] = useState<RequestLineDraft[]>([emptyRequestLine()]);
  const [contractForm, setContractForm] = useState({
    contractId: 0,
    projectBoqLineId: 0,
    procurementOwnerUserId: 0,
    quantity: 1,
    negotiatedUnitPrice: 0,
  });
  const [receiptAt, setReceiptAt] = useState(localDateTime());
  const [receiptLines, setReceiptLines] = useState<ReceiptLineDraft[]>([emptyReceiptLine()]);
  const [issueForm, setIssueForm] = useState({
    issuedAt: localDateTime(),
    responsibleSiteUserId: 0,
    workItemCode: "",
  });
  const [issueLines, setIssueLines] = useState<IssueLineDraft[]>([emptyIssueLine()]);
  const [ratingForm, setRatingForm] = useState({
    contractId: 0,
    qualityScore: 80,
    scheduleScore: 80,
    costScore: 80,
    hseScore: 80,
    comments: "",
  });

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
        items.push(
          ...((await adminApi.listOperationalProjects({ page, pageSize })).data.items ?? []),
        );
      }
      return items;
    };
    loadProjects()
      .then((items) => {
        if (!cancelled) setProjects(items);
      })
      .catch((reason) => {
        if (!cancelled) setError(uiError(reason));
      })
      .finally(() => {
        if (!cancelled) setProjectsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [uiError]);

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
          : Promise.resolve({
              data: {
                boqRevisions: [],
                materialRequests: [],
                contractLines: [],
                receipts: [],
                issues: [],
                vendorRatings: [],
              } as ProcurementWorkspaceResponse,
            }),
        adminApi.getOperationalProjectTeam(projectId).catch(() => ({ data: null })),
        canReadKpiUsers
          ? adminApi.listKpiEligibleUsers().catch(() => ({ data: [] as KpiUserOptionResponse[] }))
          : Promise.resolve({ data: [] as KpiUserOptionResponse[] }),
        canReadContracts
          ? collectLookupPages((page) =>
              adminApi.listContracts({ pageSize: 200, operationalProjectId: projectId, page }),
            ).catch(() => null)
          : Promise.resolve(null),
      ]);
      if (projectIdRef.current !== requestedProjectId) return;
      setWorkspace(workspaceResult.data);
      const activeMembers = (teamResult.data?.members ?? []).filter((member) => member.isActive);
      const teamUsers: UserOption[] = activeMembers.map((member) => ({
        ...member,
        userId: member.userId,
        userName: member.userName,
      }));
      const fallbackUsers = kpiUsersResult.data.map((user) => ({
        ...user,
        userId: user.userId,
        userName: user.userName,
      }));
      const merged = [...teamUsers, ...fallbackUsers];
      if (currentUser) merged.push({ userId: currentUser.userId, userName: currentUser.fullName });
      setUsers(mergePeople(merged));
      const procurementCandidates: UserOption[] = [
        ...activeMembers
          .filter(
            (member) =>
              member.position.toUpperCase() === "PROCUREMENT" ||
              member.roles.some((role) => role.roleCode === "PROCUREMENT" && !role.endedAt),
          )
          .map((member) => ({ ...member, userId: member.userId, userName: member.userName })),
        ...kpiUsersResult.data
          .filter((user) => user.positionCode === "PROCUREMENT")
          .map((user) => ({ ...user, userId: user.userId, userName: user.userName })),
      ];
      if (currentUser?.role === "PROCUREMENT")
        procurementCandidates.push({ userId: currentUser.userId, userName: currentUser.fullName });
      setProcurementUsers(mergePeople(procurementCandidates));
      setContractsLookupAvailable(contractsResult != null);
      setContracts(
        (contractsResult?.data.items ?? []).filter(
          (contract) => contract.operationalProjectId === projectId,
        ),
      );
    } catch (reason) {
      if (projectIdRef.current !== requestedProjectId) return;
      setWorkspace(null);
      setError(uiError(reason));
    } finally {
      if (projectIdRef.current === requestedProjectId) setLoading(false);
    }
  }, [canReadContracts, canReadKpiUsers, canViewBoq, currentUser, projectId, uiError]);

  const requestParams = useCallback(
    (page = requestPage, pageSize = 20): MaterialRequestListParams => ({
      page,
      pageSize,
      sortBy: "requiredAt",
      sortDirection: requestSortDirection,
      ...(requestSearch ? { search: requestSearch } : {}),
      ...(requestStatus !== "all" ? { status: requestStatus } : {}),
      ...(requestOwner !== "all" ? { assignedProcurementUserId: Number(requestOwner) } : {}),
      ...(requestRequiredFrom ? { requiredFrom: requestRequiredFrom } : {}),
      ...(requestRequiredTo ? { requiredTo: requestRequiredTo } : {}),
    }),
    [
      requestOwner,
      requestPage,
      requestRequiredFrom,
      requestRequiredTo,
      requestSearch,
      requestSortDirection,
      requestStatus,
    ],
  );

  const loadMaterialRequests = useCallback(async () => {
    if (!projectId || !canViewRequests) return;
    const requestedProjectId = projectId;
    const requestLoadId = ++requestLoadIdRef.current;
    setRequestListLoading(true);
    setRequestListError(null);
    try {
      const { data } = await adminApi.listMaterialRequests(projectId, requestParams());
      if (projectIdRef.current !== requestedProjectId || requestLoadIdRef.current !== requestLoadId)
        return;
      setRequestTotal(data.total);
      setRequestRows(data.items);
      setRequestBoqContext(data.currentApprovedBoq ?? null);
    } catch (reason) {
      if (projectIdRef.current !== requestedProjectId || requestLoadIdRef.current !== requestLoadId)
        return;
      setRequestListError(uiError(reason));
    } finally {
      if (projectIdRef.current === requestedProjectId && requestLoadIdRef.current === requestLoadId)
        setRequestListLoading(false);
    }
  }, [canViewRequests, projectId, requestParams, uiError]);

  useEffect(() => {
    void loadMaterialRequests();
  }, [loadMaterialRequests]);

  const warehouseParams = useCallback(
    (page = warehousePage, pageSize = 20): WarehouseTransactionListParams => ({
      page,
      pageSize,
      sortBy: "occurredAt",
      sortDirection: warehouseSortDirection,
      ...(warehouseSearch ? { search: warehouseSearch } : {}),
      ...(warehouseType !== "all" ? { type: warehouseType as "receipt" | "issue" } : {}),
      ...(warehouseStatus !== "all" ? { status: warehouseStatus } : {}),
      ...(warehouseResponsibleUser !== "all"
        ? { responsibleUserId: Number(warehouseResponsibleUser) }
        : {}),
      ...(warehouseOccurredFrom ? { occurredFrom: warehouseOccurredFrom } : {}),
      ...(warehouseOccurredTo ? { occurredTo: warehouseOccurredTo } : {}),
    }),
    [
      warehouseOccurredFrom,
      warehouseOccurredTo,
      warehousePage,
      warehouseResponsibleUser,
      warehouseSearch,
      warehouseSortDirection,
      warehouseStatus,
      warehouseType,
    ],
  );

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
      if (projectIdRef.current === projectId) setWarehouseError(uiError(reason));
    } finally {
      if (projectIdRef.current === projectId) setWarehouseLoading(false);
    }
  }, [canViewWarehouse, projectId, uiError, warehouseParams]);

  useEffect(() => {
    void loadWarehouseTransactions();
  }, [loadWarehouseTransactions]);

  const refreshData = async () => {
    setAlertRefreshToken((value) => value + 1);
    await Promise.all([
      loadWorkspace(),
      loadMaterialRequests(),
      loadBoqList(),
      loadWarehouseTransactions(),
    ]);
  };

  const exportWarehouseTransactions = async () => {
    if (!projectId) return;
    setWarehouseExporting(true);
    try {
      const first = (await adminApi.listWarehouseTransactions(projectId, warehouseParams(1, 100)))
        .data;
      const rows = [...first.items];
      for (let page = 2; page <= Math.ceil(first.total / first.pageSize); page += 1) {
        rows.push(
          ...(await adminApi.listWarehouseTransactions(projectId, warehouseParams(page, 100))).data
            .items,
        );
      }
      downloadCsv({
        filename: createCsvFilename(`warehouse-transactions-project-${projectId}`),
        rows,
        columns: [
          {
            header: t("procurement.warehouse.type"),
            value: (row) => t(`procurement.warehouse.${row.type.toLowerCase()}`),
          },
          { header: t("procurement.field.code"), value: "code" },
          {
            header: t("procurement.field.status"),
            value: (row) => t(`procurement.status.${row.status}`),
          },
          {
            header: t("procurement.warehouse.occurredAt"),
            value: (row) => formatDate(row.occurredAt),
          },
          {
            header: t("procurement.warehouse.responsibleUser"),
            value: (row) => row.responsibleUserName ?? row.actorName ?? `#${row.actorUserId}`,
          },
          { header: t("procurement.field.lines"), value: (row) => row.itemCodes.join(" | ") },
          { header: t("procurement.field.quantity"), value: "totalQuantity" },
        ],
      });
    } catch (reason) {
      toast({ variant: "destructive", title: uiError(reason) || t("common.error") });
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
        rows.push(
          ...(await adminApi.listMaterialRequests(projectId, requestParams(page, 100))).data.items,
        );
      }
      downloadCsv({
        filename: createCsvFilename(`material-requests-project-${projectId}`),
        rows,
        columns: [
          { header: t("procurement.field.code"), value: "code" },
          {
            header: t("procurement.field.status"),
            value: (row) => t(`procurement.status.${row.status}`),
          },
          {
            header: t("procurement.field.requester"),
            value: (row) => row.siteRequesterName ?? `#${row.siteRequesterUserId}`,
          },
          {
            header: t("procurement.field.responsibleSite"),
            value: (row) => row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`,
          },
          {
            header: t("procurement.field.procurementOwner"),
            value: (row) => row.assignedProcurementUserName ?? `#${row.assignedProcurementUserId}`,
          },
          { header: t("procurement.field.requiredAt"), value: (row) => formatDate(row.requiredAt) },
          {
            header: t("procurement.field.lines"),
            value: (row) =>
              row.lines.map((line) => `${line.itemCode}: ${line.requestedQuantity}`).join(" | "),
          },
        ],
      });
    } catch (reason) {
      toast({ variant: "destructive", title: uiError(reason) || t("common.error") });
    } finally {
      setRequestExporting(false);
    }
  };

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const boqListParams = useMemo<ProjectBoqRevisionListParams>(
    () => ({
      search: boqDebouncedSearch || undefined,
      status: boqStatus === "all" ? undefined : boqStatus,
      sortBy: boqSortBy,
      sortDirection: boqSortDirection,
      page: boqPage,
      pageSize: boqList.pageSize,
    }),
    [boqDebouncedSearch, boqList.pageSize, boqPage, boqSortBy, boqSortDirection, boqStatus],
  );

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
      setBoqListError(uiError(reason));
    } finally {
      setBoqListLoading(false);
    }
  }, [boqListParams, canViewBoq, projectId, uiError]);

  useEffect(() => {
    void loadBoqList();
  }, [loadBoqList]);

  useEffect(() => {
    setBoqPage(1);
  }, [projectId]);

  const approvedBoq = useMemo(
    () =>
      workspace?.boqRevisions.find((revision) => revision.isFinal) ??
      workspace?.boqRevisions.find((revision) => revision.status === "Approved"),
    [workspace],
  );
  const approvedBoqLines = approvedBoq?.lines ?? [];
  const requestBoqLines = requestBoqContext?.lines ?? [];
  const receivableLines = useMemo(
    () =>
      (workspace?.materialRequests ?? [])
        .filter((request) => ["Approved", "PartiallyFulfilled"].includes(request.status))
        .flatMap((request) => request.lines.map((line) => ({ ...line, requestCode: request.code })))
        .filter((line) => line.requestedQuantity - line.receivedQuantity > 0),
    [workspace],
  );
  const ratingContracts = contracts.filter((contract) => contract.vendorId != null);

  const status = (value: string) => (
    <Badge variant="outline" className={statusClass[value] ?? ""}>
      {t(`procurement.status.${value}`)}
    </Badge>
  );
  const formatDate = (value?: string | null) => (value ? date.format(new Date(value)) : "—");

  const openDialog = (kind: Exclude<DialogKind, null>) => {
    dialogTriggerRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setFormError(null);
    if (kind === "boq") {
      setBoqCurrency("VND");
      setBoqUpstreamContractId(0);
      setBoqLines([emptyBoqLine()]);
    }
    if (kind === "request") {
      setRequestForm({
        responsibleSiteUserId: 0,
        assignedProcurementUserId: currentUser?.userId ?? 0,
        requiredAt: localDateTime(1),
        note: "",
      });
      setRequestLines([emptyRequestLine()]);
    }
    if (kind === "contract")
      setContractForm({
        contractId: 0,
        projectBoqLineId: 0,
        procurementOwnerUserId: currentUser?.userId ?? 0,
        quantity: 1,
        negotiatedUnitPrice: 0,
      });
    if (kind === "receipt") {
      setReceiptAt(localDateTime());
      setReceiptLines([emptyReceiptLine()]);
    }
    if (kind === "issue") {
      setIssueForm({ issuedAt: localDateTime(), responsibleSiteUserId: 0, workItemCode: "" });
      setIssueLines([emptyIssueLine()]);
    }
    if (kind === "rating")
      setRatingForm({
        contractId: 0,
        qualityScore: 80,
        scheduleScore: 80,
        costScore: 80,
        hseScore: 80,
        comments: "",
      });
    setDialog(kind);
  };

  const runMutation = async (
    operation: () => Promise<unknown>,
    successKey: string,
    close = true,
  ) => {
    setBusy(true);
    setFormError(null);
    try {
      await operation();
      if (close) setDialog(null);
      setDecision(null);
      setDecisionReason("");
      toast({ title: t(successKey) });
      await Promise.all([
        loadWorkspace(),
        loadMaterialRequests(),
        loadBoqList(),
        loadWarehouseTransactions(),
      ]);
    } catch (reason) {
      const message = uiError(reason);
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
    const valid =
      /^[A-Z]{3}$/.test(boqCurrency) &&
      boqUpstreamContractId > 0 &&
      boqLines.length > 0 &&
      boqLines.every(
        (line) =>
          /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(line.itemCode.trim()) &&
          line.description.trim() &&
          line.unit.trim() &&
          line.approvedQuantity >= 0 &&
          line.budgetUnitPrice >= 0,
      );
    if (!valid) {
      setFormError(t("procurement.validation.boq"));
      return;
    }
    void runMutation(
      () =>
        adminApi.createProjectBoqRevision(projectId, {
          upstreamContractId: boqUpstreamContractId,
          currency: boqCurrency,
          lines: boqLines.map((line) => ({
            ...line,
            itemCode: line.itemCode.trim(),
            description: line.description.trim(),
            unit: line.unit.trim(),
          })),
        }),
      "procurement.success.boqCreated",
    );
  };

  const createRequest = () => {
    const valid =
      requestForm.responsibleSiteUserId > 0 &&
      requestForm.assignedProcurementUserId > 0 &&
      requestForm.requiredAt &&
      new Date(requestForm.requiredAt) > new Date() &&
      requestLines.length > 0 &&
      requestLines.every((line) => line.projectBoqLineId > 0 && line.requestedQuantity > 0);
    if (!valid) {
      setFormError(t("procurement.validation.request"));
      return;
    }
    const ids = requestLines.map((line) => line.projectBoqLineId);
    if (ids.length !== new Set(ids).size) {
      setFormError(t("procurement.validation.requestLines"));
      return;
    }
    if (
      requestLines.some((line) => {
        const option = requestBoqLines.find((item) => item.id === line.projectBoqLineId);
        return !option || line.requestedQuantity > option.remainingQuantity;
      })
    ) {
      setFormError(t("procurement.validation.requestAllowance"));
      return;
    }
    void runMutation(
      () =>
        adminApi.createMaterialRequest(projectId, {
          ...requestForm,
          requiredAt: apiDateTime(requestForm.requiredAt),
          note: requestForm.note.trim() || null,
          lines: requestLines,
        }),
      "procurement.success.requestCreated",
    );
  };

  const createContractLine = () => {
    if (
      contractForm.contractId < 1 ||
      contractForm.projectBoqLineId < 1 ||
      contractForm.procurementOwnerUserId < 1 ||
      contractForm.quantity <= 0 ||
      contractForm.negotiatedUnitPrice < 0
    ) {
      setFormError(t("procurement.validation.contractLine"));
      return;
    }
    void runMutation(
      () => adminApi.createProcurementContractLine(projectId, contractForm),
      "procurement.success.contractLineCreated",
    );
  };

  const createReceipt = () => {
    const valid =
      currentUser?.userId &&
      receiptAt &&
      receiptLines.length > 0 &&
      receiptLines.every((line) => {
        const source = receivableLines.find((item) => item.id === line.materialRequestLineId);
        return (
          source &&
          line.receivedQuantity > 0 &&
          line.receivedQuantity <= source.requestedQuantity - source.receivedQuantity
        );
      });
    if (!valid) {
      setFormError(t("procurement.validation.receipt"));
      return;
    }
    void runMutation(
      () =>
        adminApi.createWarehouseReceipt(projectId, {
          inspectedAt: apiDateTime(receiptAt),
          receivedByUserId: currentUser.userId,
          lines: receiptLines,
        }),
      "procurement.success.receiptCreated",
    );
  };

  const createIssue = () => {
    const valid =
      currentUser?.userId &&
      issueForm.issuedAt &&
      issueForm.responsibleSiteUserId > 0 &&
      issueLines.length > 0 &&
      issueLines.every((line) => {
        const stock =
          warehouseStock.find((item) => item.projectBoqLineId === line.projectBoqLineId)
            ?.onHandQuantity ?? 0;
        return line.issuedQuantity > 0 && line.issuedQuantity <= stock;
      });
    if (!valid) {
      setFormError(t("procurement.validation.issue"));
      return;
    }
    void runMutation(
      () =>
        adminApi.createWarehouseIssue(projectId, {
          issuedAt: apiDateTime(issueForm.issuedAt),
          responsibleSiteUserId: issueForm.responsibleSiteUserId,
          issuedByUserId: currentUser.userId,
          workItemCode: issueForm.workItemCode.trim() || undefined,
          lines: issueLines,
        }),
      "procurement.success.issueCreated",
    );
  };

  const createRating = () => {
    const scores = [
      ratingForm.qualityScore,
      ratingForm.scheduleScore,
      ratingForm.costScore,
      ratingForm.hseScore,
    ];
    if (ratingForm.contractId < 1 || scores.some((score) => score < 0 || score > 100)) {
      setFormError(t("procurement.validation.rating"));
      return;
    }
    void runMutation(
      () =>
        adminApi.createVendorRating(projectId, {
          ...ratingForm,
          comments: ratingForm.comments.trim() || null,
        }),
      "procurement.success.ratingCreated",
    );
  };

  const submit = (kind: DecisionKind, record: { id: number; rowVersion: string }) => {
    const operation =
      kind === "boq"
        ? () => adminApi.submitProjectBoqRevision(projectId, record.id, record.rowVersion)
        : kind === "request"
          ? () => adminApi.submitMaterialRequest(projectId, record.id, record.rowVersion)
          : () => adminApi.submitVendorRating(projectId, record.id, record.rowVersion);
    void runMutation(operation, `procurement.success.${kind}Submitted`, false);
  };

  const decide = () => {
    if (!decision) return;
    if (!decision.approved && decisionReason.trim().length < 3) {
      setFormError(t("procurement.validation.rejectionReason"));
      return;
    }
    const reason = decisionReason.trim() || undefined;
    const operation =
      decision.kind === "boq"
        ? () =>
            adminApi.decideProjectBoqRevision(
              projectId,
              decision.id,
              decision.approved,
              decision.rowVersion,
              reason,
            )
        : decision.kind === "request"
          ? () =>
              adminApi.decideMaterialRequest(
                projectId,
                decision.id,
                decision.approved,
                decision.rowVersion,
                reason,
              )
          : () =>
              adminApi.decideVendorRating(
                projectId,
                decision.id,
                decision.approved,
                decision.rowVersion,
                reason,
              );
    void runMutation(
      operation,
      decision.approved ? "procurement.success.approved" : "procurement.success.rejected",
      false,
    );
  };

  const exportBoq = async () => {
    if (!projectId) return;
    try {
      const response = await adminApi.exportProjectBoqRevisions(projectId, {
        ...boqListParams,
        page: undefined,
        pageSize: undefined,
      });
      const url = URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = `project-${projectId}-boq.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (reason) {
      toast({
        title: t("common.error"),
        description: uiError(reason),
        variant: "destructive",
      });
    }
  };

  if (projectsLoading)
    return (
      <AdminLayout>
        <PageLoading />
      </AdminLayout>
    );
  if (!projectId && error)
    return (
      <AdminLayout>
        <PageError message={error} />
      </AdminLayout>
    );

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <header className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="flex items-center gap-2 text-primary">
              <ShoppingCart className="h-5 w-5" />
              <span className="text-xs font-semibold uppercase">{t("nav.procurement")}</span>
            </div>
            <h1 className="mt-1 text-2xl font-semibold">{t("procurement.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("procurement.subtitle")}</p>
          </div>
          <div className="grid w-full grid-cols-[minmax(0,1fr)_44px] items-end gap-2 lg:max-w-xl">
            <Label htmlFor="procurement-project" className="sr-only">
              {t("procurement.project.label")}
            </Label>
            <Select
              value={projectId ? String(projectId) : ""}
              onValueChange={(value) => {
                setSearchParams(
                  (current) => {
                    const next = new URLSearchParams(current);
                    next.set("projectId", value);
                    next.set("tab", activeTab);
                    return next;
                  },
                  { replace: true },
                );
              }}
            >
              <SelectTrigger id="procurement-project" className="h-11 min-w-0">
                <SelectValue placeholder={t("procurement.project.placeholder")} />
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
              <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
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
              setSearchParams(
                (current) => {
                  const next = new URLSearchParams(current);
                  next.set("tab", value);
                  return next;
                },
                { replace: true },
              );
            }}
            className="space-y-4"
          >
            <div className="overflow-x-auto pb-1 [scrollbar-width:thin] [&::-webkit-scrollbar]:h-1.5 [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-border [&::-webkit-scrollbar-track]:bg-transparent">
              <TabsList className="h-auto w-max min-w-full justify-start">
                {canViewIfc && <TabsTrigger value="ifc">{t("procurement.ifc.title")}</TabsTrigger>}
                {canViewResources && (
                  <TabsTrigger value="resources">{t("construction.handoff.title")}</TabsTrigger>
                )}
                {canViewAlerts && (
                  <TabsTrigger value="alerts">
                    <TriangleAlert className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.alerts")} ({alertTotal})
                  </TabsTrigger>
                )}
                {canViewBoq && (
                  <TabsTrigger value="boq">
                    <ClipboardCheck className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.boq")} ({boqList.total})
                  </TabsTrigger>
                )}
                {canViewRequests && (
                  <TabsTrigger value="requests">
                    <ShoppingCart className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.requests")} ({requestTotal})
                  </TabsTrigger>
                )}
                {canViewContracts && (
                  <TabsTrigger value="contracts">
                    <FilePlus2 className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.contracts")} ({workspace.contractLines.length})
                  </TabsTrigger>
                )}
                {canViewWarehouse && (
                  <TabsTrigger value="warehouse">
                    <Boxes className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.warehouse")} ({warehouseTotal})
                  </TabsTrigger>
                )}
                {canViewRatings && (
                  <TabsTrigger value="ratings">
                    <Star className="mr-2 h-4 w-4" />
                    {t("procurement.tabs.ratings")} ({workspace.vendorRatings.length})
                  </TabsTrigger>
                )}
              </TabsList>
            </div>

            {canViewResources && (
              <TabsContent value="resources">
                <ConstructionResourcesWorkspace key={projectId} projectId={projectId} />
              </TabsContent>
            )}
            {canViewAlerts && (
              <TabsContent value="alerts">
                <MaterialAlertsWorkspace
                  projectId={projectId}
                  users={users}
                  t={t}
                  formatDate={formatDate}
                  formatNumber={(value) => number.format(value)}
                  refreshToken={alertRefreshToken}
                  canManage={canManageAlerts}
                  onTotalChange={setAlertTotal}
                />
              </TabsContent>
            )}

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
                  setRequestSortDirection((current) => (current === "asc" ? "desc" : "asc"));
                  setRequestPage(1);
                }}
                onPage={setRequestPage}
                onExport={() => void exportMaterialRequests()}
              />
            )}

            {canViewBoq && (
              <TabsContent value="boq" className="space-y-4">
                <div className="grid gap-3 border-y py-4 sm:grid-cols-2 lg:grid-cols-[minmax(220px,1fr)_180px_180px_150px_auto]">
                  <div className="space-y-1.5">
                    <Label htmlFor="boq-search">{t("procurement.boq.filter.search")}</Label>
                    <div className="relative">
                      <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                      <Input
                        id="boq-search"
                        className="pl-9"
                        value={boqSearch}
                        onChange={(event) => setBoqSearch(event.target.value)}
                        placeholder={t("procurement.boq.filter.searchPlaceholder")}
                      />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="boq-status">{t("procurement.field.status")}</Label>
                    <Select
                      value={boqStatus}
                      onValueChange={(value) => {
                        setBoqStatus(value);
                        setBoqPage(1);
                      }}
                    >
                      <SelectTrigger id="boq-status">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">
                          {t("procurement.boq.filter.allStatuses")}
                        </SelectItem>
                        {["Draft", "Submitted", "Approved", "Rejected"].map((value) => (
                          <SelectItem value={value} key={value}>
                            {t(`procurement.status.${value}`)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="boq-sort">{t("procurement.boq.filter.sort")}</Label>
                    <Select
                      value={boqSortBy}
                      onValueChange={(value) => {
                        setBoqSortBy(value as ProjectBoqRevisionListParams["sortBy"]);
                        setBoqPage(1);
                      }}
                    >
                      <SelectTrigger id="boq-sort">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {[
                          "revision",
                          "status",
                          "total",
                          "preparedBy",
                          "createdAt",
                          "updatedAt",
                        ].map((value) => (
                          <SelectItem value={value} key={value}>
                            {t(`procurement.boq.sort.${value}`)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="boq-direction">{t("procurement.boq.filter.direction")}</Label>
                    <Select
                      value={boqSortDirection}
                      onValueChange={(value) => {
                        setBoqSortDirection(value as "asc" | "desc");
                        setBoqPage(1);
                      }}
                    >
                      <SelectTrigger id="boq-direction">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="desc">{t("procurement.boq.sort.desc")}</SelectItem>
                        <SelectItem value="asc">{t("procurement.boq.sort.asc")}</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex items-end">
                    <Button
                      variant="outline"
                      onClick={() => void exportBoq()}
                      disabled={boqListLoading || boqList.total === 0}
                    >
                      <Download className="mr-2 h-4 w-4" />
                      {t("procurement.boq.export")}
                    </Button>
                  </div>
                </div>
                {boqListLoading ? (
                  <PageLoading />
                ) : boqListError ? (
                  <PageError message={boqListError} onRetry={() => void loadBoqList()} />
                ) : (
                  <>
                    <BoqPanel
                      rows={boqList.items}
                      canManage={canManageBoq}
                      canApprove={canApproveBoq}
                      status={status}
                      money={money}
                      t={t}
                      onCreate={() => openDialog("boq")}
                      onSubmit={(row) => submit("boq", row)}
                      onDecision={(row, approved) => {
                        setFormError(null);
                        setDecisionReason("");
                        setDecision({
                          kind: "boq",
                          id: row.id,
                          rowVersion: row.rowVersion,
                          approved,
                        });
                      }}
                      busy={busy}
                    />
                    {boqList.total > boqList.pageSize && (
                      <div className="flex items-center justify-between gap-3">
                        <p className="text-sm text-muted-foreground">
                          {t("procurement.boq.pagination", {
                            page: boqList.page,
                            pages: Math.ceil(boqList.total / boqList.pageSize),
                            total: boqList.total,
                          })}
                        </p>
                        <div className="flex gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={boqPage <= 1}
                            onClick={() => setBoqPage((current) => current - 1)}
                          >
                            {t("common.prev")}
                          </Button>
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={boqPage >= Math.ceil(boqList.total / boqList.pageSize)}
                            onClick={() => setBoqPage((current) => current + 1)}
                          >
                            {t("common.next")}
                          </Button>
                        </div>
                      </div>
                    )}
                  </>
                )}
              </TabsContent>
            )}
            {canViewRequests && (
              <TabsContent value="requests">
                {requestListLoading && requestRows.length === 0 ? (
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
            {canViewIfc && (
              <TabsContent value="ifc">
                <ProcurementIfcInbox key={projectId} projectId={projectId} />
              </TabsContent>
            )}
            {canViewContracts && (
              <TabsContent value="contracts">
                {canReadContracts && (
                  <div className="mb-4">
                    <Button asChild variant="outline">
                      <Link
                        to={`/admin/contracts?direction=Downstream&operationalProjectId=${projectId}`}
                      >
                        {t("nav.contracts")}
                      </Link>
                    </Button>
                  </div>
                )}
                <ContractLinesPanel
                  rows={workspace.contractLines}
                  canManage={canManageContracts}
                  money={money}
                  number={number}
                  t={t}
                  onCreate={() => openDialog("contract")}
                  hasDependencies={Boolean(
                    approvedBoqLines.length && (contracts.length || !contractsLookupAvailable),
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
                  onType={(value) => {
                    setWarehouseType(value);
                    setWarehousePage(1);
                  }}
                  onStatus={(value) => {
                    setWarehouseStatus(value);
                    setWarehousePage(1);
                  }}
                  onResponsibleUser={(value) => {
                    setWarehouseResponsibleUser(value);
                    setWarehousePage(1);
                  }}
                  onOccurredFrom={(value) => {
                    setWarehouseOccurredFrom(value);
                    setWarehousePage(1);
                  }}
                  onOccurredTo={(value) => {
                    setWarehouseOccurredTo(value);
                    setWarehousePage(1);
                  }}
                  onToggleSort={() => {
                    setWarehouseSortDirection((current) => (current === "asc" ? "desc" : "asc"));
                    setWarehousePage(1);
                  }}
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
                  hasContracts={ratingContracts.length > 0 || !contractsLookupAvailable}
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
            <DialogTitle>{dialog ? t(`procurement.dialog.${dialog}.title`) : ""}</DialogTitle>
            <DialogDescription>
              {dialog ? t(`procurement.dialog.${dialog}.description`) : ""}
            </DialogDescription>
          </DialogHeader>
          {dialog === "boq" && (
            <BoqForm
              upstreamContractId={boqUpstreamContractId}
              setUpstreamContractId={setBoqUpstreamContractId}
              upstreamContracts={contracts.filter(
                (contract) =>
                  contract.direction === "Upstream" &&
                  contract.signedDate &&
                  !["Draft", "Cancelled"].includes(contract.status),
              )}
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
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>
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
            <DialogDescription>{t("procurement.decision.description")}</DialogDescription>
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
            <Button variant="outline" onClick={() => setDecision(null)} disabled={busy}>
              {t("common.cancel")}
            </Button>
            <Button
              variant={decision?.approved ? "default" : "destructive"}
              onClick={decide}
              disabled={busy}
            >
              {t(decision?.approved ? "procurement.action.approve" : "procurement.action.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default ProcurementControlPage;
