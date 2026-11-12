import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import StatTile from "./ConstructionStatTile";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { personOption, type PersonIdentity } from "@/lib/people";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  CircleDot,
  Eye,
  MapPin,
  Pencil,
  Play,
  RefreshCcw,
  RotateCcw,
  Search,
  ShieldAlert,
  ShieldCheck,
  Trash2,
  Wrench,
} from "lucide-react";
import { type DesignProjectScopeProps } from "./constructionScope";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { ListPagination } from "@/components/admin/ListPagination";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { PageEmpty, PageLoading, PageError } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { OVERDUE_TONE, PUNCH_SEVERITY_TONE, PUNCH_STATUS_TONE } from "./constructionStatus";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import {
  adminApi,
  type CreatePunchItemRequest,
  type PunchItemListParams,
  type PunchItemResponse,
  type PunchRootCause,
  type PunchSeverity,
  type PunchStatus,
  type UpdatePunchItemRequest,
} from "@/services/adminApi";
import { formatDateTime } from "@/lib/displaySettings";
import { formatDate as formatDateInZone } from "@/lib/displaySettings";

const PUNCH_STATUSES: PunchStatus[] = ["Open", "InProgress", "Fixed", "Verified", "Cancelled"];
const PUNCH_SEVERITIES: PunchSeverity[] = ["Low", "Medium", "High", "Critical"];
const PUNCH_ROOT_CAUSES: PunchRootCause[] = [
  "Design",
  "Construction",
  "Material",
  "ClientChange",
  "Other",
];
const DESIGN_TEAM_ROLES = new Set([
  "DesignLead",
  "Architect",
  "StructuralEngineer",
  "MepEngineer",
  "InteriorDesigner",
]);

interface UserOption extends PersonIdentity {
  id: number;
  fullName: string;
}

type ConfirmKind = "reopen" | "verify" | "delete" | null;

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return formatDateInZone(iso, lang);
  } catch {
    return iso;
  }
};

interface PunchForm {
  title: string;
  description: string;
  location: string;
  severity: PunchSeverity;
  assigneeUserId: number | null;
  deadline: string;
  resolutionNote: string;
  note: string;
}

const emptyForm = (): PunchForm => ({
  title: "",
  description: "",
  location: "",
  severity: "Medium",
  assigneeUserId: null,
  deadline: "",
  resolutionNote: "",
  note: "",
});

const formFromRow = (row: PunchItemResponse): PunchForm => ({
  title: row.title,
  description: row.description ?? "",
  location: row.location ?? "",
  severity: row.severity,
  assigneeUserId: row.assigneeUserId ?? null,
  deadline: row.deadline ?? "",
  resolutionNote: row.resolutionNote ?? "",
  note: row.note ?? "",
});

export const PunchListPanel = ({
  designProjectId,
  operationalProjectId,
  scope = "execute",
}: DesignProjectScopeProps) => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionPunchManage);
  const canVerify = scope === "review" && has(ADMIN_PERMS.constructionPunchVerify);
  const canListUsers = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<PunchItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<PunchStatus, number>>>({});
  const [overdueCount, setOverdueCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const projectId = designProjectId;
  const [status, setStatus] = useState<PunchStatus | "">("");
  const [severity, setSeverity] = useState<PunchSeverity | "">("");
  const [assigneeUserId, setAssigneeUserId] = useState<number | null>(null);
  const [openOnly, setOpenOnly] = useState(false);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [search, setSearch] = useState("");
  const settledSearch = useDebouncedValue(search);
  const [page, setPage] = useState(1);
  const pageSize = 50;

  // selection
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const [bulkDeleting, setBulkDeleting] = useState(false);

  // dialogs
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<PunchForm>(() => emptyForm());
  const [createProjectId, setCreateProjectId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [detail, setDetail] = useState<PunchItemResponse | null>(null);
  const [detailMode, setDetailMode] = useState<"view" | "edit">("view");
  const [detailForm, setDetailForm] = useState<PunchForm | null>(null);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [confirmAction, setConfirmAction] = useState<{
    kind: ConfirmKind;
    row: PunchItemResponse | null;
  }>({
    kind: null,
    row: null,
  });
  const [actionBusy, setActionBusy] = useState(false);
  const [verifyRootCause, setVerifyRootCause] = useState<PunchRootCause>("Construction");
  const [verifyResponsibleUserId, setVerifyResponsibleUserId] = useState<number | null>(null);
  const [verifyRootCauseNote, setVerifyRootCauseNote] = useState("");
  const [verifyCandidates, setVerifyCandidates] = useState<UserOption[]>([]);

  // lookups
  const [users, setUsers] = useState<UserOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (canListUsers) {
        try {
          const { data } = await adminApi.getPeopleDirectory();
          if (!cancelled) {
            setUsers(
              (data.items ?? []).map((u) => ({
                ...u,
                id: u.id,
                fullName: u.fullName ?? u.email ?? `#${u.id}`,
              })),
            );
          }
        } catch {
          /* ignore */
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canListUsers]);

  const startListRequest = useRequestGuard();
  const fetchList = useCallback(async () => {
    const isCurrent = startListRequest();
    setLoading(true);
    setError(null);
    try {
      const params: PunchItemListParams = { page, pageSize };
      if (projectId != null) params.designProjectId = projectId;
      if (status) params.status = status;
      if (severity) params.severity = severity;
      if (assigneeUserId != null) params.assigneeUserId = assigneeUserId;
      if (openOnly) params.openOnly = true;
      if (overdueOnly) params.overdueOnly = true;
      if (settledSearch.trim()) params.search = settledSearch.trim();
      const { data } = await adminApi.listPunchItems(params);
      if (!isCurrent()) return;
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setStatusCounts(data.statusCounts ?? {});
      setOverdueCount(data.overdueCount ?? 0);
      const visible = new Set((data.items ?? []).map((r) => r.id));
      setSelected((prev) => {
        const next = new Set<number>();
        prev.forEach((id) => {
          if (visible.has(id)) next.add(id);
        });
        return next;
      });
    } catch (err) {
      if (!isCurrent()) return;
      setError(uiError(err));
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }, [
    startListRequest,
    page,
    projectId,
    status,
    severity,
    assigneeUserId,
    openOnly,
    overdueOnly,
    settledSearch,
    uiError,
    toast,
    t,
  ]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const statusPill = (s: PunchStatus) => statusCounts[s] ?? 0;

  const userFilterOptions = useMemo(
    () => [
      { value: "", label: t("punch.filter.allAssignees") },
      ...users.map((u) => personOption(u, t)),
    ],
    [users, t],
  );
  const userFormOptions = useMemo(() => users.map((u) => personOption(u, t)), [users, t]);

  const hasActiveFilter =
    status !== "" ||
    severity !== "" ||
    assigneeUserId != null ||
    openOnly ||
    overdueOnly ||
    search.trim() !== "";

  const resetFilters = () => {
    setStatus("");
    setSeverity("");
    setAssigneeUserId(null);
    setOpenOnly(false);
    setOverdueOnly(false);
    setSearch("");
    setPage(1);
  };

  // ---- create ----
  const openCreate = () => {
    setCreateForm(emptyForm());
    setCreateProjectId(projectId);
    setCreateError(null);
    setCreating(true);
  };
  const submitCreate = async () => {
    setCreateError(null);
    if (createProjectId == null) {
      setCreateError(t("punch.form.projectRequired"));
      return;
    }
    if (!createForm.title.trim()) {
      setCreateError(t("punch.form.titleRequired"));
      return;
    }
    setSaving(true);
    try {
      const body: CreatePunchItemRequest = {
        designProjectId: createProjectId,
        title: createForm.title.trim(),
        description: createForm.description.trim() || undefined,
        location: createForm.location.trim() || undefined,
        severity: createForm.severity,
        assigneeUserId: createForm.assigneeUserId ?? undefined,
        deadline: createForm.deadline || undefined,
        note: createForm.note.trim() || undefined,
      };
      await adminApi.createPunchItem(body);
      toast({ title: t("punch.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(uiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---- detail ----
  const openDetail = (row: PunchItemResponse, mode: "view" | "edit" = "view") => {
    setDetail(row);
    setDetailMode(mode);
    setDetailForm(formFromRow(row));
    setDetailError(null);
  };
  const closeDetail = () => {
    setDetail(null);
    setDetailForm(null);
    setDetailError(null);
  };
  useEffect(() => {
    if (!detail) return;
    setDetailForm(formFromRow(detail));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail?.id]);

  const canEditDetail =
    canManage &&
    detailMode === "edit" &&
    detail &&
    detail.status !== "Verified" &&
    detail.status !== "Cancelled";

  const submitDetail = async () => {
    if (!detail || !detailForm) return;
    setDetailError(null);
    if (!detailForm.title.trim()) {
      setDetailError(t("punch.form.titleRequired"));
      return;
    }
    setDetailSaving(true);
    try {
      const body: UpdatePunchItemRequest = {
        title: detailForm.title.trim(),
        description: detailForm.description.trim() || undefined,
        location: detailForm.location.trim() || undefined,
        severity: detailForm.severity,
        assigneeUserId: detailForm.assigneeUserId ?? undefined,
        deadline: detailForm.deadline || undefined,
        resolutionNote: detailForm.resolutionNote.trim() || undefined,
        note: detailForm.note.trim() || undefined,
      };
      const { data: updated } = await adminApi.updatePunchItem(detail.id, body);
      toast({ title: t("punch.updated") });
      setDetail(updated);
      await fetchList();
    } catch (err) {
      setDetailError(uiError(err));
    } finally {
      setDetailSaving(false);
    }
  };

  // ---- transitions ----
  const changeStatus = async (row: PunchItemResponse, next: PunchStatus) => {
    try {
      const { data: updated } = await adminApi.transitionPunchStatus(row.id, { status: next });
      toast({ title: t("punch.statusChanged") });
      if (detail?.id === row.id) setDetail(updated);
      await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    }
  };

  const runConfirmAction = async () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return;
    setActionBusy(true);
    try {
      let updated: PunchItemResponse | null = null;
      if (kind === "verify") {
        if (verifyRootCause === "Design" && verifyResponsibleUserId == null) {
          toast({
            title: t("common.error"),
            description: t("punch.form.responsibleDesignerRequired"),
            variant: "destructive",
          });
          return;
        }
        if (verifyRootCause === "Other" && !verifyRootCauseNote.trim()) {
          toast({
            title: t("common.error"),
            description: t("punch.form.rootCauseNoteRequired"),
            variant: "destructive",
          });
          return;
        }
        const { data } = await adminApi.verifyPunchItem(row.id, {
          rootCause: verifyRootCause,
          responsibleDesignUserId:
            verifyRootCause === "Design" ? verifyResponsibleUserId : undefined,
          rootCauseNote: verifyRootCauseNote.trim() || undefined,
        });
        updated = data;
        toast({ title: t("punch.verified") });
      } else if (kind === "reopen") {
        const { data } = await adminApi.transitionPunchStatus(row.id, { status: "Open" });
        updated = data;
        toast({ title: t("punch.reopened") });
      } else if (kind === "delete") {
        await adminApi.deletePunchItem(row.id);
        toast({ title: t("punch.deleted") });
        if (detail?.id === row.id) closeDetail();
      }
      if (updated && detail?.id === updated.id) setDetail(updated);
      setConfirmAction({ kind: null, row: null });
      await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err, undefined),
        variant: "destructive",
      });
    } finally {
      setActionBusy(false);
    }
  };

  const openVerify = async (row: PunchItemResponse) => {
    setVerifyRootCause(row.rootCause === "Unclassified" ? "Construction" : row.rootCause);
    setVerifyResponsibleUserId(row.responsibleDesignUserId ?? null);
    setVerifyRootCauseNote(row.rootCauseNote ?? "");
    setVerifyCandidates([]);
    if (operationalProjectId) {
      try {
        const { data } = await adminApi.getOperationalProjectTeam(
          operationalProjectId,
          row.designProjectId,
        );
        setVerifyCandidates(
          data.members
            .filter(
              (member) =>
                member.isActive &&
                member.roles.some((role) => !role.endedAt && DESIGN_TEAM_ROLES.has(role.roleCode)),
            )
            .map((member) => ({ ...member, id: member.userId, fullName: member.userName })),
        );
      } catch {
        /* server validation remains authoritative */
      }
    }
    setConfirmAction({ kind: "verify", row });
  };

  // ---- bulk delete ----
  const toggleSelect = (id: number, checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  };
  const toggleSelectAll = (checked: boolean) => {
    if (checked) setSelected(new Set(rows.map((r) => r.id)));
    else setSelected(new Set());
  };
  const submitBulkDelete = async () => {
    if (selected.size === 0) return;
    setBulkDeleting(true);
    try {
      const { data } = await adminApi.bulkDeletePunchItems({ ids: Array.from(selected) });
      toast({
        title: t("punch.bulkDeleted")
          .replace("{deleted}", String(data.deleted))
          .replace("{requested}", String(data.requested)),
      });
      if (data.failures.length > 0) {
        const MAX_SHOWN = 3;
        const shown = data.failures
          .slice(0, MAX_SHOWN)
          .map((f) => `#${f.id}: ${f.message}`)
          .join(" · ");
        const rest = data.failures.length - MAX_SHOWN;
        toast({
          title: t("common.warning"),
          description: rest > 0 ? `${shown} … (+${rest})` : shown,
          variant: "destructive",
        });
      }
      setConfirmBulkDelete(false);
      setSelected(new Set());
      await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setBulkDeleting(false);
    }
  };

  // ---- render helpers ----
  const renderBadges = (row: PunchItemResponse) => (
    <div className="flex flex-wrap items-center gap-1">
      <ConstructionStatusBadge tone={PUNCH_SEVERITY_TONE[row.severity]}>
        {t(`punch.severity.${row.severity}`)}
      </ConstructionStatusBadge>
      <ConstructionStatusBadge tone={PUNCH_STATUS_TONE[row.status]}>
        {t(`punch.status.${row.status}`)}
      </ConstructionStatusBadge>
      {row.isOverdue && (
        <ConstructionStatusBadge tone={OVERDUE_TONE}>
          {t("punch.status.Overdue")}
        </ConstructionStatusBadge>
      )}
    </div>
  );

  const confirmTitle = () => {
    switch (confirmAction.kind) {
      case "verify":
        return t("punch.action.verify");
      case "reopen":
        return t("punch.action.reopen");
      case "delete":
        return t("punch.action.delete");
      default:
        return "";
    }
  };
  const confirmMessage = () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return "";
    const code = row.punchCode;
    switch (kind) {
      case "verify":
        return t("punch.confirmVerify").replace("{code}", code);
      case "reopen":
        return t("punch.confirmReopen").replace("{code}", code);
      case "delete":
        return t("punch.confirmDelete").replace("{code}", code);
      default:
        return "";
    }
  };

  return (
    <>
      <div className="space-y-6" data-testid="punch-page">
        <div className="flex justify-end">
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} data-testid="punch-new">
                <ShieldAlert className="mr-2 h-4 w-4" />
                {t("punch.new")}
              </Button>
            )}
          </div>
        </div>

        {/* stats */}
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <StatTile
            icon={<ShieldAlert className="h-6 w-6" />}
            label={t("punch.stat.total")}
            value={total}
          />
          <StatTile
            icon={<CircleDot className="h-6 w-6" />}
            label={t("punch.stat.open")}
            value={statusPill("Open")}
          />
          <StatTile
            icon={<Wrench className="h-6 w-6" />}
            label={t("punch.stat.inProgress")}
            value={statusPill("InProgress")}
          />
          <StatTile
            icon={<AlertTriangle className="h-6 w-6" />}
            label={t("punch.stat.overdue")}
            value={overdueCount}
          />
        </div>

        {/* filters */}
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <FieldScope>
              <Label className="text-xs text-muted-foreground">{t("punch.field.status")}</Label>
              <Select
                value={status || "__all__"}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as PunchStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("punch.filter.allStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("punch.filter.allStatuses")}</SelectItem>
                  {PUNCH_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`punch.status.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">{t("punch.field.severity")}</Label>
              <Select
                value={severity || "__all__"}
                onValueChange={(v) => {
                  setSeverity(v === "__all__" ? "" : (v as PunchSeverity));
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("punch.filter.allSeverities")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("punch.filter.allSeverities")}</SelectItem>
                  {PUNCH_SEVERITIES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`punch.severity.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            {canListUsers && (
              <FieldScope>
                <Label className="text-xs text-muted-foreground">{t("punch.field.assignee")}</Label>
                <SearchableSelect
                  value={assigneeUserId == null ? "" : String(assigneeUserId)}
                  onChange={(v) => {
                    setAssigneeUserId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={userFilterOptions}
                  placeholder={t("punch.filter.allAssignees")}
                />
              </FieldScope>
            )}
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 [&>div]:min-w-0">
            <FieldScope>
              <Label className="text-xs text-muted-foreground">{t("punch.filter.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("punch.filter.search")}
                  data-testid="punch-search"
                />
              </div>
            </FieldScope>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
            <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground">
              <Checkbox
                checked={openOnly}
                onCheckedChange={(v) => {
                  setOpenOnly(!!v);
                  setPage(1);
                }}
              />
              {t("punch.filter.openOnly")}
            </label>
            <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground">
              <Checkbox
                checked={overdueOnly}
                onCheckedChange={(v) => {
                  setOverdueOnly(!!v);
                  setPage(1);
                }}
              />
              {t("punch.filter.overdueOnly")}
            </label>
            {hasActiveFilter && (
              <Button variant="ghost" size="sm" onClick={resetFilters}>
                {t("common.reset")}
              </Button>
            )}
          </div>
        </div>

        <BulkActionBar
          selectedCount={selected.size}
          bulkDeleting={bulkDeleting}
          onClear={() => setSelected(new Set())}
          onBulkDelete={() => setConfirmBulkDelete(true)}
        />

        {/* body */}
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <PageEmpty
            message={t("punch.empty")}
            action={
              canManage && (
                <Button size="sm" onClick={openCreate} data-testid="punch-empty-new">
                  <ShieldAlert className="mr-2 h-4 w-4" />
                  {t("punch.new")}
                </Button>
              )
            }
          />
        ) : (
          <>
            {/* Mobile cards (< md) */}
            <div className="space-y-3 md:hidden">
              {rows.map((row) => (
                <article
                  key={`m-${row.id}`}
                  className={cn(
                    "relative rounded-xl border border-border bg-card p-4 text-left shadow-sm",
                    row.isOverdue && "border-warning/50 bg-warning-soft/50",
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="font-mono text-xs text-muted-foreground">{row.punchCode}</div>
                      <div className="mt-1 line-clamp-2 text-base font-semibold text-foreground">
                        {row.title}
                      </div>
                    </div>
                    {canManage && (
                      <div onClick={(e) => e.stopPropagation()} role="presentation">
                        <Checkbox
                          checked={selected.has(row.id)}
                          onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                          aria-label={`m-select-${row.punchCode}`}
                          className="mt-1"
                        />
                      </div>
                    )}
                  </div>
                  <div className="mt-2">{renderBadges(row)}</div>
                  {row.location && (
                    <div className="mt-2 inline-flex items-center gap-1 text-xs text-muted-foreground">
                      <MapPin className="h-3 w-3" /> {row.location}
                    </div>
                  )}
                  <div className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-muted-foreground">
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">
                        {t("punch.field.assignee")}
                      </div>
                      <div>{row.assigneeName ?? "—"}</div>
                    </div>
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">
                        {t("punch.field.deadline")}
                      </div>
                      <div>{formatDate(row.deadline, lang)}</div>
                    </div>
                  </div>
                  <div className="mt-3 flex flex-wrap justify-end gap-1 border-t pt-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => openDetail(row)}
                      data-testid={`punch-view-${row.id}`}
                    >
                      <Eye className="mr-1 h-4 w-4" />
                      {t("common.view")}
                    </Button>
                    {canManage && (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={row.status === "Verified" || row.status === "Cancelled"}
                          onClick={() => openDetail(row, "edit")}
                          data-testid={`punch-edit-${row.id}`}
                        >
                          <Pencil className="mr-1 h-4 w-4" />
                          {t("common.edit")}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-destructive hover:text-destructive"
                          onClick={() => setConfirmAction({ kind: "delete", row })}
                          data-testid={`punch-delete-${row.id}`}
                        >
                          <Trash2 className="mr-1 h-4 w-4" />
                          {t("common.delete")}
                        </Button>
                      </>
                    )}
                  </div>
                </article>
              ))}
            </div>

            {/* Desktop table (md+) */}
            <div className="hidden overflow-hidden rounded-lg border border-border bg-card shadow-sm md:block">
              <div className="max-h-[70vh] overflow-auto">
                <table className="min-w-full divide-y divide-border text-sm">
                  <thead className="sticky top-0 z-10 bg-muted/30 text-left text-xs uppercase tracking-wide text-muted-foreground">
                    <tr>
                      {canManage && (
                        <th className="w-8 px-3 py-2">
                          <Checkbox
                            aria-label={t("punch.action.selectAll")}
                            checked={rows.length > 0 && selected.size === rows.length}
                            onCheckedChange={(v) => toggleSelectAll(!!v)}
                            data-testid="punch-select-all"
                          />
                        </th>
                      )}
                      <th className="px-3 py-2">{t("punch.field.code")}</th>
                      <th className="px-3 py-2">{t("punch.field.title")}</th>
                      <th className="px-3 py-2">{t("punch.field.severity")}</th>
                      <th className="px-3 py-2">{t("punch.field.status")}</th>
                      <th className="px-3 py-2">{t("punch.field.assignee")}</th>
                      <th className="px-3 py-2">{t("punch.field.deadline")}</th>
                      <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {rows.map((row) => (
                      <tr
                        key={row.id}
                        className={cn(
                          "cursor-pointer hover:bg-muted/30",
                          row.isOverdue && "bg-warning-soft/50",
                        )}
                        onClick={() => openDetail(row)}
                        data-testid={`punch-row-${row.id}`}
                      >
                        {canManage && (
                          <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selected.has(row.id)}
                              onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                              aria-label={`select-${row.punchCode}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 font-mono text-xs text-muted-foreground">
                          {row.punchCode}
                        </td>
                        <td className="px-3 py-2">
                          <div className="font-medium text-foreground">{row.title}</div>
                          {row.location && (
                            <div className="mt-0.5 inline-flex items-center gap-1 text-[11px] text-muted-foreground">
                              <MapPin className="h-3 w-3" /> {row.location}
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2">
                          <ConstructionStatusBadge tone={PUNCH_SEVERITY_TONE[row.severity]}>
                            {t(`punch.severity.${row.severity}`)}
                          </ConstructionStatusBadge>
                        </td>
                        <td className="px-3 py-2">
                          <div className="flex flex-wrap items-center gap-1">
                            <ConstructionStatusBadge tone={PUNCH_STATUS_TONE[row.status]}>
                              {t(`punch.status.${row.status}`)}
                            </ConstructionStatusBadge>
                            {row.isOverdue && (
                              <ConstructionStatusBadge tone={OVERDUE_TONE}>
                                {t("punch.status.Overdue")}
                              </ConstructionStatusBadge>
                            )}
                          </div>
                        </td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          {row.assigneeName ?? "—"}
                        </td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          {formatDate(row.deadline, lang)}
                        </td>
                        <td className="px-3 py-2" onClick={(event) => event.stopPropagation()}>
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              title={t("common.view")}
                              aria-label={t("common.view")}
                              onClick={() => openDetail(row)}
                              data-testid={`punch-view-${row.id}`}
                            >
                              <Eye className="h-4 w-4" />
                            </Button>
                            {canManage && (
                              <>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  title={t("common.edit")}
                                  aria-label={t("common.edit")}
                                  disabled={row.status === "Verified" || row.status === "Cancelled"}
                                  onClick={() => openDetail(row, "edit")}
                                  data-testid={`punch-edit-${row.id}`}
                                >
                                  <Pencil className="h-4 w-4" />
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  title={t("common.delete")}
                                  aria-label={t("common.delete")}
                                  className="text-destructive hover:text-destructive"
                                  onClick={() => setConfirmAction({ kind: "delete", row })}
                                  data-testid={`punch-delete-${row.id}`}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
            <ListPagination
              page={page}
              pageSize={pageSize}
              total={total}
              onPageChange={setPage}
              disabled={loading}
            />
          </>
        )}

        {/* create dialog */}
        <Dialog open={creating} onOpenChange={setCreating}>
          <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{t("punch.new")}</DialogTitle>
              <DialogDescription>{t("punch.form.hint")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-3">
              <FieldScope>
                <Label>{t("punch.field.title")}</Label>
                <Input
                  value={createForm.title}
                  onChange={(e) => setCreateForm((f) => ({ ...f, title: e.target.value }))}
                  data-testid="punch-new-title"
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("punch.field.description")}</Label>
                <Textarea
                  rows={2}
                  value={createForm.description}
                  onChange={(e) => setCreateForm((f) => ({ ...f, description: e.target.value }))}
                />
              </FieldScope>
              <div className="grid grid-cols-2 gap-3">
                <FieldScope>
                  <Label>{t("punch.field.location")}</Label>
                  <Input
                    value={createForm.location}
                    onChange={(e) => setCreateForm((f) => ({ ...f, location: e.target.value }))}
                  />
                </FieldScope>
                <FieldScope>
                  <Label>{t("punch.field.severity")}</Label>
                  <Select
                    value={createForm.severity}
                    onValueChange={(v) =>
                      setCreateForm((f) => ({ ...f, severity: v as PunchSeverity }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {PUNCH_SEVERITIES.map((s) => (
                        <SelectItem key={s} value={s}>
                          {t(`punch.severity.${s}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldScope>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <FieldScope>
                  <Label>{t("punch.field.assignee")}</Label>
                  {canListUsers ? (
                    <SearchableSelect
                      value={
                        createForm.assigneeUserId == null ? "" : String(createForm.assigneeUserId)
                      }
                      onChange={(v) =>
                        setCreateForm((f) => ({ ...f, assigneeUserId: v ? Number(v) : null }))
                      }
                      options={userFormOptions}
                      placeholder="—"
                    />
                  ) : (
                    <Input value="—" disabled />
                  )}
                </FieldScope>
                <FieldScope>
                  <Label>{t("punch.field.deadline")}</Label>
                  <Input
                    type="date"
                    value={createForm.deadline}
                    onChange={(e) => setCreateForm((f) => ({ ...f, deadline: e.target.value }))}
                  />
                </FieldScope>
              </div>
              <FieldScope>
                <Label>{t("punch.field.note")}</Label>
                <Textarea
                  rows={2}
                  value={createForm.note}
                  onChange={(e) => setCreateForm((f) => ({ ...f, note: e.target.value }))}
                />
              </FieldScope>
              {createError && <div className="text-sm text-destructive">{createError}</div>}
            </div>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setCreating(false)} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button onClick={submitCreate} disabled={saving} data-testid="punch-new-save">
                {saving ? t("common.saving") : t("common.save")}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* detail sheet */}
        <Sheet
          open={detail !== null}
          onOpenChange={(open) => {
            if (!open) closeDetail();
          }}
        >
          <SheetContent side="right" className="w-full max-w-xl overflow-y-auto sm:max-w-xl">
            {detail && detailForm && (
              <>
                <SheetHeader>
                  <SheetTitle>
                    {t(detailMode === "edit" ? "common.edit" : "common.view")} · {detail.punchCode}{" "}
                    — {detail.title}
                  </SheetTitle>
                  <SheetDescription className="text-xs text-muted-foreground">
                    {detail.designProjectCode} · {detail.designProjectName}
                  </SheetDescription>
                </SheetHeader>
                <div className="mt-4 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">{renderBadges(detail)}</div>
                  {detail.reopenCount > 0 && (
                    <div className="text-xs text-muted-foreground">
                      {t("punch.field.reopenCount")}:{" "}
                      <span className="font-semibold">{detail.reopenCount}</span>
                    </div>
                  )}

                  <FieldScope>
                    <Label>{t("punch.field.title")}</Label>
                    <Input
                      value={detailForm.title}
                      onChange={(e) => setDetailForm((f) => f && { ...f, title: e.target.value })}
                      disabled={!canEditDetail}
                      data-testid="punch-detail-title"
                    />
                  </FieldScope>
                  <FieldScope>
                    <Label>{t("punch.field.description")}</Label>
                    <Textarea
                      rows={3}
                      value={detailForm.description}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, description: e.target.value })
                      }
                      disabled={!canEditDetail}
                    />
                  </FieldScope>
                  <div className="grid grid-cols-2 gap-3">
                    <FieldScope>
                      <Label>{t("punch.field.location")}</Label>
                      <Input
                        value={detailForm.location}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, location: e.target.value })
                        }
                        disabled={!canEditDetail}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("punch.field.severity")}</Label>
                      <Select
                        value={detailForm.severity}
                        onValueChange={(v) =>
                          setDetailForm((f) => f && { ...f, severity: v as PunchSeverity })
                        }
                        disabled={!canEditDetail}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {PUNCH_SEVERITIES.map((s) => (
                            <SelectItem key={s} value={s}>
                              {t(`punch.severity.${s}`)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("punch.field.assignee")}</Label>
                      {canListUsers ? (
                        <SearchableSelect
                          value={
                            detailForm.assigneeUserId == null
                              ? ""
                              : String(detailForm.assigneeUserId)
                          }
                          onChange={(v) =>
                            setDetailForm(
                              (f) => f && { ...f, assigneeUserId: v ? Number(v) : null },
                            )
                          }
                          options={userFormOptions}
                          placeholder="—"
                          disabled={!canEditDetail}
                        />
                      ) : (
                        <Input value={detail.assigneeName ?? "—"} disabled />
                      )}
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("punch.field.deadline")}</Label>
                      <Input
                        type="date"
                        value={detailForm.deadline}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, deadline: e.target.value })
                        }
                        disabled={!canEditDetail}
                      />
                    </FieldScope>
                  </div>
                  <FieldScope>
                    <Label>{t("punch.field.resolutionNote")}</Label>
                    <Textarea
                      rows={2}
                      value={detailForm.resolutionNote}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, resolutionNote: e.target.value })
                      }
                      disabled={!canEditDetail}
                    />
                  </FieldScope>
                  <FieldScope>
                    <Label>{t("punch.field.note")}</Label>
                    <Textarea
                      rows={2}
                      value={detailForm.note}
                      onChange={(e) => setDetailForm((f) => f && { ...f, note: e.target.value })}
                      disabled={!canEditDetail}
                    />
                  </FieldScope>

                  {detail.verifiedByName && (
                    <div className="rounded-md border border-success/40 bg-success-soft p-3 text-xs text-success-strong">
                      <div className="flex items-center gap-1 font-semibold">
                        <ShieldCheck className="h-4 w-4" /> {t("punch.field.verifiedBy")}
                      </div>
                      <div>
                        {detail.verifiedByName} ·{" "}
                        {detail.verifiedAt && formatDateTime(detail.verifiedAt, lang)}
                      </div>
                    </div>
                  )}
                  {detail.rootCause !== "Unclassified" && (
                    <div className="rounded-md border border-border bg-muted/30 p-3 text-xs text-foreground">
                      <div className="font-semibold">
                        {t("punch.field.rootCause")}: {t(`punch.rootCause.${detail.rootCause}`)}
                      </div>
                      {detail.responsibleDesignUserName && (
                        <div>
                          {t("punch.field.responsibleDesigner")}: {detail.responsibleDesignUserName}
                        </div>
                      )}
                      {detail.rootCauseNote && <div>{detail.rootCauseNote}</div>}
                    </div>
                  )}

                  {detailError && <div className="text-sm text-destructive">{detailError}</div>}

                  <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
                    <div className="flex flex-wrap gap-2">
                      {canManage && (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => setConfirmAction({ kind: "delete", row: detail })}
                          data-testid="punch-detail-delete"
                        >
                          <Trash2 className="mr-2 h-4 w-4" />
                          {t("punch.action.delete")}
                        </Button>
                      )}
                      {canManage && detail.status === "Open" && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => void changeStatus(detail, "InProgress")}
                          data-testid="punch-start"
                        >
                          <Play className="mr-2 h-4 w-4" />
                          {t("punch.action.startProgress")}
                        </Button>
                      )}
                      {canManage && detail.status === "InProgress" && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => void changeStatus(detail, "Fixed")}
                          data-testid="punch-fix"
                        >
                          <Wrench className="mr-2 h-4 w-4" />
                          {t("punch.action.markFixed")}
                        </Button>
                      )}
                      {canVerify && detail.status === "Fixed" && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => void openVerify(detail)}
                          data-testid="punch-verify"
                        >
                          <ShieldCheck className="mr-2 h-4 w-4" />
                          {t("punch.action.verify")}
                        </Button>
                      )}
                      {canManage &&
                        (detail.status === "Fixed" ||
                          detail.status === "Verified" ||
                          detail.status === "InProgress") && (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setConfirmAction({ kind: "reopen", row: detail })}
                            data-testid="punch-reopen"
                          >
                            <RotateCcw className="mr-2 h-4 w-4" />
                            {t("punch.action.reopen")}
                          </Button>
                        )}
                      {canManage &&
                        (detail.status === "Open" ||
                          detail.status === "InProgress" ||
                          detail.status === "Fixed") && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => void changeStatus(detail, "Cancelled")}
                            data-testid="punch-cancel"
                          >
                            {t("punch.action.cancel")}
                          </Button>
                        )}
                    </div>
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={closeDetail} disabled={detailSaving}>
                        {t("common.close")}
                      </Button>
                      {canManage && detailMode === "view" && (
                        <Button
                          variant="outline"
                          disabled={detail.status === "Verified" || detail.status === "Cancelled"}
                          onClick={() => setDetailMode("edit")}
                          data-testid="punch-detail-edit"
                        >
                          <Pencil className="mr-1 h-4 w-4" />
                          {t("common.edit")}
                        </Button>
                      )}
                      {canEditDetail && (
                        <Button
                          onClick={submitDetail}
                          disabled={detailSaving}
                          data-testid="punch-detail-save"
                        >
                          {detailSaving ? t("common.saving") : t("common.save")}
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              </>
            )}
          </SheetContent>
        </Sheet>

        {/* single-action confirm */}
        <AlertDialog
          open={confirmAction.kind !== null}
          onOpenChange={(v) => !v && setConfirmAction({ kind: null, row: null })}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{confirmTitle()}</AlertDialogTitle>
              <AlertDialogDescription>{confirmMessage()}</AlertDialogDescription>
            </AlertDialogHeader>
            {confirmAction.kind === "verify" && (
              <div className="space-y-3">
                <FieldScope>
                  <Label>{t("punch.field.rootCause")}</Label>
                  <Select
                    value={verifyRootCause}
                    onValueChange={(value) => {
                      setVerifyRootCause(value as PunchRootCause);
                      if (value !== "Design") setVerifyResponsibleUserId(null);
                    }}
                  >
                    <SelectTrigger data-testid="punch-verify-root-cause">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {PUNCH_ROOT_CAUSES.map((cause) => (
                        <SelectItem key={cause} value={cause}>
                          {t(`punch.rootCause.${cause}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldScope>
                {verifyRootCause === "Design" && (
                  <FieldScope>
                    <Label>{t("punch.field.responsibleDesigner")}</Label>
                    <SearchableSelect
                      value={verifyResponsibleUserId == null ? "" : String(verifyResponsibleUserId)}
                      onChange={(value) => setVerifyResponsibleUserId(value ? Number(value) : null)}
                      ariaLabel={t("punch.field.responsibleDesigner")}
                      options={verifyCandidates.map((candidate) => personOption(candidate, t))}
                      placeholder="—"
                    />
                  </FieldScope>
                )}
                {(verifyRootCause === "Design" || verifyRootCause === "Other") && (
                  <FieldScope>
                    <Label>{t("punch.field.rootCauseNote")}</Label>
                    <Textarea
                      value={verifyRootCauseNote}
                      onChange={(event) => setVerifyRootCauseNote(event.target.value)}
                      rows={2}
                    />
                  </FieldScope>
                )}
              </div>
            )}
            <AlertDialogFooter>
              <AlertDialogCancel disabled={actionBusy}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction
                onClick={(event) => {
                  event.preventDefault();
                  void runConfirmAction();
                }}
                disabled={actionBusy}
                data-testid="punch-action-confirm"
              >
                {actionBusy ? t("common.saving") : t("common.confirm")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* bulk delete confirm */}
        <AlertDialog open={confirmBulkDelete} onOpenChange={setConfirmBulkDelete}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t("punch.action.bulkDelete")}</AlertDialogTitle>
              <AlertDialogDescription>
                {t("punch.confirmBulkDelete").replace("{count}", String(selected.size))}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={bulkDeleting}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction
                onClick={submitBulkDelete}
                disabled={bulkDeleting}
                data-testid="punch-bulk-delete-confirm"
              >
                {bulkDeleting ? t("common.saving") : t("common.delete")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </>
  );
};
