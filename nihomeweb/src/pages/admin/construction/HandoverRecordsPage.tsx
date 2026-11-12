import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import { collectLookupPages } from "@/lib/pagination";
import ConstructionStatTile from "./ConstructionStatTile";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { useCallback, useEffect, useMemo, useState } from "react";
import { personOption } from "@/lib/people";
import type { SearchableSelectOption } from "@/components/ui/searchable-select";
import {
  CalendarDays,
  Check,
  CheckCircle2,
  ClipboardCheck,
  Eye,
  PackageCheck,
  Pencil,
  Plus,
  RefreshCcw,
  Search,
  Trash2,
  UserRound,
  X,
} from "lucide-react";
import AdminExportButton from "@/components/admin/AdminExportButton";
import AdminDocumentUpload from "@/components/admin/AdminDocumentUpload";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { type DesignProjectScopeProps } from "./constructionScope";
import { ListPagination } from "@/components/admin/ListPagination";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { HANDOVER_STATUS_TONE } from "./constructionStatus";
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
import { LocalizedDateInput } from "@/components/ui/localized-inputs";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { isManagedDocumentPath, resolveSafeLinkUrl } from "@/lib/url";
import {
  adminApi,
  type CreateHandoverRecordRequest,
  type DesignProjectListItemResponse,
  type HandoverChecklistItem,
  type HandoverRecordListParams,
  type HandoverRecordResponse,
  type HandoverStatus,
  type TransitionHandoverStatusRequest,
  type UpdateHandoverRecordRequest,
  type UserListItemResponse,
} from "@/services/adminApi";
import {
  formatDate as formatDateInZone,
  formatDateTime as formatDateTimeInZone,
} from "@/lib/displaySettings";

const HANDOVER_STATUSES: HandoverStatus[] = [
  "Draft",
  "ReadyForHandover",
  "HandedOver",
  "Reopened",
  "Cancelled",
];

const EDITABLE_STATUSES = new Set<HandoverStatus>(["Draft", "Reopened"]);

interface HandoverFormState {
  designProjectId: string;
  title: string;
  description: string;
  plannedHandoverDate: string;
  location: string;
  responsibleUserId: string;
  commissioningCompleted: boolean;
  commissioningNotes: string;
  checklistItems: HandoverChecklistItem[];
  documents: string[];
  signatories: string[];
}

const emptyForm = (): HandoverFormState => ({
  designProjectId: "",
  title: "",
  description: "",
  plannedHandoverDate: new Date().toISOString().slice(0, 10),
  location: "",
  responsibleUserId: "",
  commissioningCompleted: false,
  commissioningNotes: "",
  checklistItems: [],
  documents: [],
  signatories: [],
});

const formatDate = (value: string | null | undefined, fallback: string) => {
  if (!value) return fallback;
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? value : formatDateInZone(value);
};

const formatDateTime = (value: string | null | undefined, fallback: string) => {
  if (!value) return fallback;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : formatDateTimeInZone(value);
};

export function HandoverRecordsPanel({
  designProjectId,
  scope = "review",
}: DesignProjectScopeProps) {
  const uiError = useUiError();
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionHandoverManage);
  const canComplete = scope === "review" && has(ADMIN_PERMS.constructionHandoverComplete);
  const emptyValue = t("common.noData");

  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const [users, setUsers] = useState<UserListItemResponse[]>([]);
  const projectId = designProjectId ?? undefined;
  const [responsibleUserId, setResponsibleUserId] = useState<number | undefined>();
  const [status, setStatus] = useState<HandoverStatus | "">("");
  const [plannedFrom, setPlannedFrom] = useState("");
  const [plannedTo, setPlannedTo] = useState("");
  const [readyOnly, setReadyOnly] = useState(false);
  const [search, setSearch] = useState("");
  const settledSearch = useDebouncedValue(search);
  const [sort, setSort] = useState("date-desc");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [rows, setRows] = useState<HandoverRecordResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [readyCount, setReadyCount] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<HandoverStatus, number>>>({});
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<HandoverRecordResponse | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<HandoverFormState>(emptyForm);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [pendingTransition, setPendingTransition] = useState<{
    record: HandoverRecordResponse;
    next: HandoverStatus;
    complete: boolean;
  } | null>(null);
  const [transitionNote, setTransitionNote] = useState("");
  const [pendingDelete, setPendingDelete] = useState<HandoverRecordResponse | null>(null);
  const [acting, setActing] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void Promise.allSettled([
      collectLookupPages((page) => adminApi.listDesignProjects({ pageSize: 200, page })),
      adminApi.getPeopleDirectory(),
    ]).then(([projectResult, userResult]) => {
      if (cancelled) return;
      if (projectResult.status === "fulfilled") setProjects(projectResult.value.data.items ?? []);
      if (userResult.status === "fulfilled") {
        setUsers((userResult.value.data.items ?? []).filter((user) => user.isActive));
      }
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const projectOptions = useMemo(
    () => projects.map((project) => ({ value: String(project.id), label: project.name })),
    [projects],
  );
  const formProjectOptions = useMemo(
    () =>
      projectId
        ? projectOptions.filter((option) => option.value === String(projectId))
        : projectOptions,
    [projectId, projectOptions],
  );

  const userOptions = useMemo(() => {
    const options = new Map<number, SearchableSelectOption>();
    users.forEach((user) => options.set(user.id, personOption(user, t)));
    projects.forEach((project) => {
      if (
        project.projectManagerUserId &&
        project.projectManagerName &&
        !options.has(project.projectManagerUserId)
      )
        options.set(
          project.projectManagerUserId,
          personOption(
            {
              id: project.projectManagerUserId,
              name: project.projectManagerName,
              roleLabelKey: "designProjects.team.role.ProjectManager",
            },
            t,
          ),
        );
      if (
        project.designLeadUserId &&
        project.designLeadName &&
        !options.has(project.designLeadUserId)
      )
        options.set(
          project.designLeadUserId,
          personOption(
            {
              id: project.designLeadUserId,
              name: project.designLeadName,
              roleLabelKey: "designProjects.team.role.DesignLead",
            },
            t,
          ),
        );
    });
    rows.forEach((row) => {
      if (!options.has(row.responsibleUserId))
        options.set(
          row.responsibleUserId,
          personOption({ id: row.responsibleUserId, name: row.responsibleUserName }, t),
        );
    });
    return Array.from(options.values());
  }, [projects, rows, users, t]);

  const currentParams = useCallback(
    (includePage = true): HandoverRecordListParams => {
      const [sortBy, sortDirection] = sort.split("-") as [
        NonNullable<HandoverRecordListParams["sortBy"]>,
        "asc" | "desc",
      ];
      return {
        designProjectId: projectId,
        responsibleUserId,
        status: status || undefined,
        plannedFrom: plannedFrom || undefined,
        plannedTo: plannedTo || undefined,
        search: settledSearch.trim() || undefined,
        readyOnly: readyOnly || undefined,
        sortBy,
        sortDirection,
        page: includePage ? page : undefined,
        pageSize: includePage ? pageSize : undefined,
      };
    },
    [
      page,
      plannedFrom,
      plannedTo,
      projectId,
      readyOnly,
      responsibleUserId,
      settledSearch,
      sort,
      status,
    ],
  );

  const startListRequest = useRequestGuard();
  const load = useCallback(async () => {
    const isCurrent = startListRequest();
    setLoading(true);
    setError(null);
    try {
      const response = await adminApi.listHandoverRecords(currentParams());
      if (!isCurrent()) return;
      setRows(response.data.items ?? []);
      setTotal(response.data.total ?? 0);
      setReadyCount(response.data.readyCount ?? 0);
      setStatusCounts(response.data.statusCounts ?? {});
    } catch (loadError) {
      if (!isCurrent()) return;
      setError(uiError(loadError) || t("handover.error"));
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }, [startListRequest, currentParams, uiError, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const openDetail = useCallback(
    async (id: number) => {
      setDetailOpen(true);
      setDetail(null);
      setDetailError(null);
      setDetailLoading(true);
      try {
        const response = await adminApi.getHandoverRecord(id);
        setDetail(response.data);
      } catch (loadError) {
        setDetailError(uiError(loadError) || t("handover.detail.error"));
      } finally {
        setDetailLoading(false);
      }
    },
    [t, uiError],
  );

  const openCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm(), designProjectId: projectId ? String(projectId) : "" });
    setFormError(null);
    setFormOpen(true);
  };

  const openEdit = (record: HandoverRecordResponse) => {
    setEditingId(record.id);
    setForm({
      designProjectId: String(record.designProjectId),
      title: record.title,
      description: record.description ?? "",
      plannedHandoverDate: record.plannedHandoverDate,
      location: record.location ?? "",
      responsibleUserId: String(record.responsibleUserId),
      commissioningCompleted: record.commissioningCompleted,
      commissioningNotes: record.commissioningNotes ?? "",
      checklistItems: record.checklistItems.map((item) => ({ ...item })),
      documents: [...record.documents],
      signatories: [...record.signatories],
    });
    setFormError(null);
    setFormOpen(true);
  };

  const updateChecklistItem = (index: number, patch: Partial<HandoverChecklistItem>) => {
    setForm((current) => ({
      ...current,
      checklistItems: current.checklistItems.map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item,
      ),
    }));
  };

  const updateStringRow = (field: "documents" | "signatories", index: number, value: string) => {
    setForm((current) => ({
      ...current,
      [field]: current[field].map((item, itemIndex) => (itemIndex === index ? value : item)),
    }));
  };

  const removeRow = (field: "checklistItems" | "documents" | "signatories", index: number) => {
    setForm((current) => ({
      ...current,
      [field]: current[field].filter((_, itemIndex) => itemIndex !== index),
    }));
  };

  const validateForm = () => {
    const checklistItems = form.checklistItems.map((item) => ({
      name: item.name.trim(),
      isCompleted: item.isCompleted,
      note: item.note?.trim() || null,
    }));
    const documents = form.documents.map((item) => item.trim()).filter(Boolean);
    const signatories = form.signatories.map((item) => item.trim()).filter(Boolean);
    if (!form.designProjectId) return t("handover.form.required.project");
    if (!form.title.trim()) return t("handover.form.required.title");
    if (!form.plannedHandoverDate) return t("handover.form.required.date");
    if (!form.responsibleUserId) return t("handover.form.required.responsible");
    if (
      checklistItems.length > 50 ||
      checklistItems.some(
        (item) => !item.name || item.name.length > 300 || (item.note?.length ?? 0) > 1000,
      )
    )
      return t("handover.form.checklistInvalid");
    if (
      new Set(checklistItems.map((item) => item.name.toLocaleLowerCase())).size !==
      checklistItems.length
    )
      return t("handover.form.checklistDuplicate");
    if (
      documents.length > 20 ||
      documents.some((item) => item.length > 500 || !resolveSafeLinkUrl(item)) ||
      JSON.stringify(documents).length > 4000
    )
      return t("handover.form.documentsInvalid");
    if (signatories.length > 20 || signatories.some((item) => item.length > 200))
      return t("handover.form.signatoriesInvalid");
    return null;
  };

  const handleSave = async () => {
    const validationError = validateForm();
    setFormError(validationError);
    if (validationError) return;
    const payload: UpdateHandoverRecordRequest = {
      title: form.title.trim(),
      description: form.description.trim() || null,
      plannedHandoverDate: form.plannedHandoverDate,
      location: form.location.trim() || null,
      responsibleUserId: Number(form.responsibleUserId),
      commissioningCompleted: form.commissioningCompleted,
      commissioningNotes: form.commissioningNotes.trim() || null,
      checklistItems: form.checklistItems.map((item) => ({
        name: item.name.trim(),
        isCompleted: item.isCompleted,
        note: item.note?.trim() || null,
      })),
      documents: form.documents.map((item) => item.trim()).filter(Boolean),
      signatories: form.signatories.map((item) => item.trim()).filter(Boolean),
    };
    setSaving(true);
    try {
      if (editingId) {
        await adminApi.updateHandoverRecord(editingId, payload);
      } else {
        await adminApi.createHandoverRecord({
          ...payload,
          designProjectId: Number(form.designProjectId),
        } as CreateHandoverRecordRequest);
      }
      toast({ title: t("handover.toast.saved") });
      setFormOpen(false);
      await load();
      if (detail?.id === editingId && editingId) await openDetail(editingId);
    } catch (saveError) {
      setFormError(uiError(saveError) || t("handover.error"));
    } finally {
      setSaving(false);
    }
  };

  const availableActions = (record: HandoverRecordResponse) => {
    const actions: Array<{
      next: HandoverStatus;
      complete?: boolean;
      label: string;
      disabled?: boolean;
    }> = [];
    if ((record.status === "Draft" || record.status === "Reopened") && canManage) {
      actions.push({
        next: "ReadyForHandover",
        label: t("handover.action.markReady"),
        disabled: !record.readiness.isReady,
      });
      actions.push({ next: "Cancelled", label: t("handover.action.cancel") });
    }
    if (record.status === "ReadyForHandover") {
      if (canComplete)
        actions.push({
          next: "HandedOver",
          complete: true,
          label: t("handover.action.complete"),
          disabled: record.signatories.length === 0,
        });
      if (canManage) {
        actions.push({ next: "Draft", label: t("handover.action.revise") });
        actions.push({ next: "Cancelled", label: t("handover.action.cancel") });
      }
    }
    if (record.status === "HandedOver" && canManage)
      actions.push({ next: "Reopened", label: t("handover.action.reopen") });
    return actions;
  };

  const handleTransition = async () => {
    if (!pendingTransition) return;
    const { record, next, complete } = pendingTransition;
    const body: TransitionHandoverStatusRequest = {
      status: next,
      note: transitionNote.trim() || null,
    };
    setActing(true);
    try {
      if (complete) await adminApi.completeHandoverRecord(record.id, body);
      else await adminApi.transitionHandoverStatus(record.id, body);
      toast({ title: t("handover.toast.transitioned") });
      setPendingTransition(null);
      await load();
      if (detailOpen) await openDetail(record.id);
    } catch (transitionError) {
      toast({
        variant: "destructive",
        title: uiError(transitionError) || t("handover.error"),
      });
    } finally {
      setActing(false);
    }
  };

  const handleDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setActing(true);
    try {
      await adminApi.deleteHandoverRecord(id);
      toast({ title: t("handover.toast.deleted") });
      setPendingDelete(null);
      if (detail?.id === id) setDetailOpen(false);
      await load();
    } catch (deleteError) {
      toast({ variant: "destructive", title: uiError(deleteError) || t("handover.error") });
    } finally {
      setActing(false);
    }
  };

  const handleExport = async () => {
    setExporting(true);
    try {
      const response = await adminApi.exportHandoverRecords(currentParams(false));
      const url = URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = `handover-records-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      toast({ title: t("handover.toast.exported") });
    } catch (exportError) {
      toast({ variant: "destructive", title: uiError(exportError) || t("handover.error") });
    } finally {
      setExporting(false);
    }
  };

  const stats = [
    {
      label: t("handover.stats.total"),
      value: total,
      icon: PackageCheck,
    },
    {
      label: t("handover.stats.ready"),
      value: readyCount,
      icon: ClipboardCheck,
    },
    {
      label: t("handover.stats.pending"),
      value: (statusCounts.Draft ?? 0) + (statusCounts.Reopened ?? 0),
      icon: CalendarDays,
    },
    {
      label: t("handover.stats.completed"),
      value: statusCounts.HandedOver ?? 0,
      icon: CheckCircle2,
    },
  ];

  const ReadinessIndicator = ({
    ready,
    label,
    value,
  }: {
    ready: boolean;
    label: string;
    value?: string;
  }) => (
    <div
      className={cn(
        "flex items-start gap-2 rounded-lg border p-3",
        ready ? "border-success/40 bg-success-soft" : "border-warning/40 bg-warning-soft",
      )}
    >
      {ready ? (
        <Check className="mt-0.5 h-4 w-4 shrink-0 text-success-strong" />
      ) : (
        <X className="mt-0.5 h-4 w-4 shrink-0 text-warning-strong" />
      )}
      <div className="min-w-0">
        <p className="text-sm font-medium">{label}</p>
        {value && <p className="text-xs text-muted-foreground">{value}</p>}
      </div>
    </div>
  );

  const ActionButtons = ({ record }: { record: HandoverRecordResponse }) => (
    <div className="flex flex-wrap gap-2">
      {canManage && EDITABLE_STATUSES.has(record.status) && (
        <Button
          size="sm"
          variant="outline"
          onClick={() => openEdit(record)}
          data-testid="handover-detail-edit"
        >
          {t("handover.action.edit")}
        </Button>
      )}
      {availableActions(record).map((action) => (
        <Button
          key={`${record.id}-${action.next}`}
          size="sm"
          variant={action.complete ? "default" : "outline"}
          disabled={action.disabled}
          title={
            action.disabled
              ? action.complete
                ? t("handover.action.signatoryRequired")
                : t("handover.action.notReady")
              : undefined
          }
          onClick={() => {
            setTransitionNote("");
            setPendingTransition({ record, next: action.next, complete: Boolean(action.complete) });
          }}
        >
          {action.label}
        </Button>
      ))}
      {canManage && (
        <Button
          size="sm"
          variant="destructive"
          onClick={() => setPendingDelete(record)}
          data-testid="handover-detail-delete"
        >
          <Trash2 className="mr-2 h-4 w-4" />
          {t("handover.action.delete")}
        </Button>
      )}
    </div>
  );

  return (
    <>
      <div className="space-y-6" data-testid="handover-page">
        <header className="flex justify-end">
          <div className="flex flex-wrap gap-2">
            <AdminExportButton
              onClick={handleExport}
              disabled={exporting || total === 0}
              label={exporting ? t("handover.action.exporting") : t("handover.action.export")}
            />
            <Button variant="outline" onClick={load} disabled={loading}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button onClick={openCreate} data-testid="handover-new">
                <Plus className="mr-2 h-4 w-4" />
                {t("handover.action.new")}
              </Button>
            )}
          </div>
        </header>

        <section className="grid grid-cols-2 gap-3 xl:grid-cols-4">
          {stats.map((stat) => (
            <ConstructionStatTile
              key={stat.label}
              label={stat.label}
              value={stat.value}
              icon={<stat.icon />}
            />
          ))}
        </section>

        <section className="space-y-4 rounded-xl border bg-card p-4 shadow-sm">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.project")}</Label>
              <Input
                value={projects.find((project) => project.id === projectId)?.name ?? emptyValue}
                readOnly
              />
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.responsible")}</Label>
              <SearchableSelect
                ariaLabel={t("handover.field.responsible")}
                value={responsibleUserId ? String(responsibleUserId) : null}
                onChange={(value) => {
                  setResponsibleUserId(value ? Number(value) : undefined);
                  setPage(1);
                }}
                options={[
                  { value: "", label: t("handover.filter.allResponsible") },
                  ...userOptions,
                ]}
                placeholder={t("handover.filter.allResponsible")}
                searchPlaceholder={t("handover.filter.searchResponsible")}
              />
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.status")}</Label>
              <Select
                value={status || "all"}
                onValueChange={(value) => {
                  setStatus(value === "all" ? "" : (value as HandoverStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("handover.filter.allStatuses")}</SelectItem>
                  {HANDOVER_STATUSES.map((item) => (
                    <SelectItem key={item} value={item}>
                      {t(`handover.status.${item.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.filter.sort")}</Label>
              <Select
                value={sort}
                onValueChange={(value) => {
                  setSort(value);
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="date-desc">{t("handover.sort.dateDesc")}</SelectItem>
                  <SelectItem value="date-asc">{t("handover.sort.dateAsc")}</SelectItem>
                  <SelectItem value="code-asc">{t("handover.sort.codeAsc")}</SelectItem>
                  <SelectItem value="project-asc">{t("handover.sort.projectAsc")}</SelectItem>
                  <SelectItem value="status-asc">{t("handover.sort.statusAsc")}</SelectItem>
                  <SelectItem value="updatedAt-desc">{t("handover.sort.updatedDesc")}</SelectItem>
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.filter.plannedFrom")}</Label>
              <LocalizedDateInput
                value={plannedFrom}
                onChange={(event) => {
                  setPlannedFrom(event.target.value);
                  setPage(1);
                }}
              />
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.filter.plannedTo")}</Label>
              <LocalizedDateInput
                value={plannedTo}
                onChange={(event) => {
                  setPlannedTo(event.target.value);
                  setPage(1);
                }}
              />
            </FieldScope>
            <FieldScope className="space-y-1.5 md:col-span-2">
              <Label>{t("handover.filter.search")}</Label>
              <div className="relative">
                <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  className="pl-9"
                  value={search}
                  onChange={(event) => {
                    setSearch(event.target.value);
                    setPage(1);
                  }}
                  placeholder={t("handover.filter.searchPlaceholder")}
                  data-testid="handover-search"
                />
              </div>
            </FieldScope>
          </div>
          <label className="flex cursor-pointer items-center gap-2 text-sm">
            <Checkbox
              checked={readyOnly}
              onCheckedChange={(checked) => {
                setReadyOnly(Boolean(checked));
                setPage(1);
              }}
            />
            {t("handover.filter.readyOnly")}
          </label>
        </section>

        {error ? (
          <PageError message={error} onRetry={load} />
        ) : loading ? (
          <PageLoading />
        ) : rows.length === 0 ? (
          <PageEmpty
            message={t("handover.empty")}
            action={
              canManage && (
                <Button onClick={openCreate} data-testid="handover-empty-new">
                  <Plus className="mr-2 h-4 w-4" />
                  {t("handover.action.new")}
                </Button>
              )
            }
          />
        ) : (
          <>
            <div className="hidden overflow-x-auto rounded-xl border bg-card shadow-sm md:block">
              <table className="w-full text-sm">
                <thead className="bg-muted/50 text-left">
                  <tr>
                    <th className="px-4 py-3">{t("handover.field.code")}</th>
                    <th className="px-4 py-3">{t("handover.field.project")}</th>
                    <th className="px-4 py-3">{t("handover.field.plannedDate")}</th>
                    <th className="px-4 py-3">{t("handover.field.responsible")}</th>
                    <th className="px-4 py-3">{t("handover.field.readiness")}</th>
                    <th className="px-4 py-3">{t("handover.field.status")}</th>
                    <th className="px-4 py-3 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((record) => (
                    <tr
                      key={record.id}
                      className="cursor-pointer border-t hover:bg-muted/30"
                      tabIndex={0}
                      role="button"
                      onClick={() => openDetail(record.id)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") void openDetail(record.id);
                      }}
                      data-testid={`handover-row-${record.id}`}
                    >
                      <td className="px-4 py-3">
                        <p className="font-semibold">{record.handoverCode}</p>
                        <p className="max-w-52 truncate text-xs text-muted-foreground">
                          {record.title}
                        </p>
                      </td>
                      <td className="px-4 py-3">{record.designProjectName}</td>
                      <td className="whitespace-nowrap px-4 py-3">
                        {formatDate(record.plannedHandoverDate, emptyValue)}
                      </td>
                      <td className="px-4 py-3">{record.responsibleUserName}</td>
                      <td className="px-4 py-3">
                        <ConstructionStatusBadge
                          tone={record.readiness.isReady ? "success" : "warning"}
                        >
                          {record.readiness.isReady
                            ? t("handover.readiness.ready")
                            : t("handover.readiness.notReady")}
                        </ConstructionStatusBadge>
                      </td>
                      <td className="px-4 py-3">
                        <ConstructionStatusBadge tone={HANDOVER_STATUS_TONE[record.status]}>
                          {t(`handover.status.${record.status.toLowerCase()}`)}
                        </ConstructionStatusBadge>
                      </td>
                      <td className="px-4 py-3" onClick={(event) => event.stopPropagation()}>
                        <div className="flex justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            title={t("common.view")}
                            aria-label={t("common.view")}
                            onClick={() => void openDetail(record.id)}
                            data-testid={`handover-row-view-${record.id}`}
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                          {canManage && (
                            <Button
                              variant="ghost"
                              size="icon"
                              title={t("common.edit")}
                              aria-label={t("common.edit")}
                              disabled={!EDITABLE_STATUSES.has(record.status)}
                              onClick={() => openEdit(record)}
                              data-testid={`handover-row-edit-${record.id}`}
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                          )}
                          {canManage && (
                            <Button
                              variant="ghost"
                              size="icon"
                              title={t("common.delete")}
                              aria-label={t("common.delete")}
                              className="text-destructive hover:text-destructive"
                              onClick={() => setPendingDelete(record)}
                              data-testid={`handover-row-delete-${record.id}`}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="grid gap-3 md:hidden">
              {rows.map((record) => (
                <article
                  key={record.id}
                  className="rounded-xl border bg-card p-4 text-left shadow-sm"
                  data-testid={`handover-card-${record.id}`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="font-semibold">{record.handoverCode}</p>
                      <p className="text-sm">{record.title}</p>
                    </div>
                    <ConstructionStatusBadge tone={HANDOVER_STATUS_TONE[record.status]}>
                      {t(`handover.status.${record.status.toLowerCase()}`)}
                    </ConstructionStatusBadge>
                  </div>
                  <p className="mt-3 text-sm text-muted-foreground">{record.designProjectName}</p>
                  <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                    <span className="flex items-center gap-1">
                      <CalendarDays className="h-3.5 w-3.5" />
                      {formatDate(record.plannedHandoverDate, emptyValue)}
                    </span>
                    <span className="flex items-center gap-1">
                      <UserRound className="h-3.5 w-3.5" />
                      {record.responsibleUserName}
                    </span>
                  </div>
                  <p
                    className={cn(
                      "mt-3 text-xs font-medium",
                      record.readiness.isReady ? "text-success-strong" : "text-warning-strong",
                    )}
                  >
                    {record.readiness.isReady
                      ? t("handover.readiness.ready")
                      : t("handover.readiness.notReady")}
                  </p>
                  <div className="mt-3 flex flex-wrap justify-end gap-1 border-t pt-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => void openDetail(record.id)}
                      data-testid={`handover-card-view-${record.id}`}
                    >
                      <Eye className="mr-1 h-4 w-4" />
                      {t("common.view")}
                    </Button>
                    {canManage && (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled={!EDITABLE_STATUSES.has(record.status)}
                        onClick={() => openEdit(record)}
                        data-testid={`handover-card-edit-${record.id}`}
                      >
                        <Pencil className="mr-1 h-4 w-4" />
                        {t("common.edit")}
                      </Button>
                    )}
                    {canManage && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive"
                        onClick={() => setPendingDelete(record)}
                        data-testid={`handover-card-delete-${record.id}`}
                      >
                        <Trash2 className="mr-1 h-4 w-4" />
                        {t("common.delete")}
                      </Button>
                    )}
                  </div>
                </article>
              ))}
            </div>
          </>
        )}

        {total > 0 && (
          <ListPagination
            page={page}
            pageSize={pageSize}
            total={total}
            onPageChange={setPage}
            disabled={loading}
          />
        )}
      </div>

      <Sheet open={detailOpen} onOpenChange={setDetailOpen}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-2xl" data-testid="handover-detail">
          <SheetHeader>
            <SheetTitle>{detail?.handoverCode ?? t("handover.detail.title")}</SheetTitle>
            <SheetDescription>{detail?.title ?? t("handover.detail.description")}</SheetDescription>
          </SheetHeader>
          {detailLoading ? (
            <PageLoading />
          ) : detailError ? (
            <PageError message={detailError} />
          ) : (
            detail && (
              <div className="mt-6 space-y-6">
                <div className="flex flex-wrap gap-2">
                  <ConstructionStatusBadge tone={HANDOVER_STATUS_TONE[detail.status]}>
                    {t(`handover.status.${detail.status.toLowerCase()}`)}
                  </ConstructionStatusBadge>
                  <ConstructionStatusBadge tone={detail.readiness.isReady ? "success" : "warning"}>
                    {detail.readiness.isReady
                      ? t("handover.readiness.ready")
                      : t("handover.readiness.notReady")}
                  </ConstructionStatusBadge>
                </div>
                <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
                  <div>
                    <dt className="text-muted-foreground">{t("handover.field.project")}</dt>
                    <dd className="font-medium">{detail.designProjectName}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("handover.field.responsible")}</dt>
                    <dd className="font-medium">{detail.responsibleUserName}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("handover.field.plannedDate")}</dt>
                    <dd>{formatDate(detail.plannedHandoverDate, emptyValue)}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("handover.field.actualDate")}</dt>
                    <dd>{formatDate(detail.actualHandoverDate, emptyValue)}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("handover.field.location")}</dt>
                    <dd>{detail.location || emptyValue}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("handover.field.reopenCount")}</dt>
                    <dd>{detail.reopenCount}</dd>
                  </div>
                </dl>
                {detail.description && (
                  <section>
                    <h3 className="text-sm font-semibold">{t("handover.field.description")}</h3>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                      {detail.description}
                    </p>
                  </section>
                )}
                <section>
                  <h3 className="mb-3 text-sm font-semibold">{t("handover.detail.readiness")}</h3>
                  <div className="grid gap-2 sm:grid-cols-2">
                    <ReadinessIndicator
                      ready={
                        detail.readiness.approvedRequiredAsBuiltCategories ===
                        detail.readiness.requiredAsBuiltCategories
                      }
                      label={t("handover.readiness.asBuilt")}
                      value={t("handover.readiness.asBuiltValue")
                        .replace(
                          "{done}",
                          String(detail.readiness.approvedRequiredAsBuiltCategories),
                        )
                        .replace("{total}", String(detail.readiness.requiredAsBuiltCategories))}
                    />
                    <ReadinessIndicator
                      ready={detail.readiness.unresolvedPunchItems === 0}
                      label={t("handover.readiness.punch")}
                      value={t("handover.readiness.punchValue").replace(
                        "{count}",
                        String(detail.readiness.unresolvedPunchItems),
                      )}
                    />
                    <ReadinessIndicator
                      ready={detail.readiness.approvedAcceptanceRecords > 0}
                      label={t("handover.readiness.acceptance")}
                      value={t("handover.readiness.acceptanceValue").replace(
                        "{count}",
                        String(detail.readiness.approvedAcceptanceRecords),
                      )}
                    />
                    <ReadinessIndicator
                      ready={detail.readiness.commissioningCompleted}
                      label={t("handover.readiness.commissioning")}
                    />
                    <ReadinessIndicator
                      ready={detail.readiness.checklistCompleted}
                      label={t("handover.readiness.checklist")}
                    />
                  </div>
                </section>
                {detail.commissioningNotes && (
                  <section>
                    <h3 className="text-sm font-semibold">
                      {t("handover.field.commissioningNotes")}
                    </h3>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                      {detail.commissioningNotes}
                    </p>
                  </section>
                )}
                <section>
                  <h3 className="mb-2 text-sm font-semibold">{t("handover.field.checklist")}</h3>
                  {detail.checklistItems.length === 0 ? (
                    <p className="text-sm text-muted-foreground">{emptyValue}</p>
                  ) : (
                    <div className="space-y-2">
                      {detail.checklistItems.map((item, index) => (
                        <div key={`${item.name}-${index}`} className="rounded-lg border p-3">
                          <p className="flex items-center gap-2 text-sm font-medium">
                            {item.isCompleted ? (
                              <CheckCircle2 className="h-4 w-4 text-success-strong" />
                            ) : (
                              <X className="h-4 w-4 text-warning-strong" />
                            )}
                            {item.name}
                          </p>
                          {item.note && (
                            <p className="mt-1 pl-6 text-xs text-muted-foreground">{item.note}</p>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </section>
                <section>
                  <h3 className="mb-2 text-sm font-semibold">{t("handover.field.documents")}</h3>
                  {detail.documents.length === 0 ? (
                    <p className="text-sm text-muted-foreground">{emptyValue}</p>
                  ) : (
                    <div className="space-y-2">
                      {detail.documents.map((documentUrl, index) => (
                        <div
                          key={`${documentUrl}-${index}`}
                          className="flex items-center gap-2 rounded-md border px-2 py-1.5"
                        >
                          <span className="min-w-0 flex-1 break-all text-sm">{documentUrl}</span>
                          <AdminFilePreview
                            url={documentUrl}
                            fileName={documentUrl.split("/").pop()}
                            fetchFile={
                              isManagedDocumentPath(
                                documentUrl,
                                "/files/business-documents/handover",
                              )
                                ? async () =>
                                    (
                                      await adminApi.getHandoverDocumentContent(
                                        detail.id,
                                        documentUrl,
                                      )
                                    ).data
                                : undefined
                            }
                            testId={`handover-document-preview-${index}`}
                          />
                        </div>
                      ))}
                    </div>
                  )}
                </section>
                <section>
                  <h3 className="mb-2 text-sm font-semibold">{t("handover.field.signatories")}</h3>
                  {detail.signatories.length === 0 ? (
                    <p className="text-sm text-muted-foreground">{emptyValue}</p>
                  ) : (
                    <ul className="space-y-1 text-sm">
                      {detail.signatories.map((item, index) => (
                        <li key={`${item}-${index}`}>{item}</li>
                      ))}
                    </ul>
                  )}
                </section>
                <section>
                  <h3 className="mb-2 text-sm font-semibold">{t("handover.detail.metadata")}</h3>
                  <dl className="grid gap-2 text-sm sm:grid-cols-2">
                    <div>
                      <dt className="text-muted-foreground">{t("handover.field.createdAt")}</dt>
                      <dd>{formatDateTime(detail.createdAt, emptyValue)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("handover.field.updatedAt")}</dt>
                      <dd>{formatDateTime(detail.updatedAt, emptyValue)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("handover.field.submittedBy")}</dt>
                      <dd>{detail.submittedByName || emptyValue}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("handover.field.handedOverBy")}</dt>
                      <dd>{detail.handedOverByName || emptyValue}</dd>
                    </div>
                  </dl>
                </section>
                <section>
                  <h3 className="mb-2 text-sm font-semibold">{t("handover.detail.history")}</h3>
                  {detail.statusHistory.length === 0 ? (
                    <p className="text-sm text-muted-foreground">{emptyValue}</p>
                  ) : (
                    <ol className="space-y-3 border-l pl-4">
                      {detail.statusHistory.map((item, index) => (
                        <li key={`${item.changedAt}-${index}`} className="text-sm">
                          <p className="font-medium">
                            {t(`handover.status.${item.toStatus.toLowerCase()}`)}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {item.changedByName} · {formatDateTime(item.changedAt, emptyValue)}
                          </p>
                          {item.note && <p className="mt-1 text-muted-foreground">{item.note}</p>}
                        </li>
                      ))}
                    </ol>
                  )}
                </section>
                <ActionButtons record={detail} />
              </div>
            )
          )}
        </SheetContent>
      </Sheet>

      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {editingId ? t("handover.form.editTitle") : t("handover.form.newTitle")}
            </DialogTitle>
            <DialogDescription>{t("handover.form.description")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.project")}</Label>
              <SearchableSelect
                disabled={Boolean(editingId)}
                value={form.designProjectId || null}
                onChange={(value) => setForm((current) => ({ ...current, designProjectId: value }))}
                options={formProjectOptions}
                placeholder={t("handover.form.selectProject")}
                searchPlaceholder={t("handover.filter.searchProject")}
              />
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.responsible")}</Label>
              <SearchableSelect
                ariaLabel={t("handover.field.responsible")}
                value={form.responsibleUserId || null}
                onChange={(value) =>
                  setForm((current) => ({ ...current, responsibleUserId: value }))
                }
                options={userOptions}
                placeholder={t("handover.form.selectResponsible")}
                searchPlaceholder={t("handover.filter.searchResponsible")}
              />
            </FieldScope>
            <FieldScope className="space-y-1.5 sm:col-span-2">
              <Label>{t("handover.field.title")}</Label>
              <Input
                maxLength={300}
                value={form.title}
                onChange={(event) =>
                  setForm((current) => ({ ...current, title: event.target.value }))
                }
                data-testid="handover-form-title"
              />
            </FieldScope>
            <FieldScope className="space-y-1.5 sm:col-span-2">
              <Label>{t("handover.field.description")}</Label>
              <Textarea
                maxLength={4000}
                value={form.description}
                onChange={(event) =>
                  setForm((current) => ({ ...current, description: event.target.value }))
                }
              />
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.plannedDate")}</Label>
              <LocalizedDateInput
                value={form.plannedHandoverDate}
                onChange={(event) =>
                  setForm((current) => ({ ...current, plannedHandoverDate: event.target.value }))
                }
              />
            </FieldScope>
            <FieldScope className="space-y-1.5">
              <Label>{t("handover.field.location")}</Label>
              <Input
                maxLength={300}
                value={form.location}
                onChange={(event) =>
                  setForm((current) => ({ ...current, location: event.target.value }))
                }
              />
            </FieldScope>
            <label className="flex items-center gap-2 sm:col-span-2">
              <Checkbox
                checked={form.commissioningCompleted}
                onCheckedChange={(checked) =>
                  setForm((current) => ({ ...current, commissioningCompleted: Boolean(checked) }))
                }
              />
              {t("handover.field.commissioningCompleted")}
            </label>
            <FieldScope className="space-y-1.5 sm:col-span-2">
              <Label>{t("handover.field.commissioningNotes")}</Label>
              <Textarea
                maxLength={4000}
                value={form.commissioningNotes}
                onChange={(event) =>
                  setForm((current) => ({ ...current, commissioningNotes: event.target.value }))
                }
              />
            </FieldScope>
          </div>
          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <span>{t("handover.field.checklist")}</span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={form.checklistItems.length >= 50}
                onClick={() =>
                  setForm((current) => ({
                    ...current,
                    checklistItems: [
                      ...current.checklistItems,
                      { name: "", isCompleted: false, note: "" },
                    ],
                  }))
                }
              >
                <Plus className="mr-2 h-4 w-4" />
                {t("handover.form.addChecklist")}
              </Button>
            </div>
            {form.checklistItems.map((item, index) => (
              <div
                key={index}
                className="grid gap-2 rounded-lg border p-3 sm:grid-cols-[auto_1fr_1fr_auto]"
              >
                <Checkbox
                  className="mt-2"
                  checked={item.isCompleted}
                  onCheckedChange={(checked) =>
                    updateChecklistItem(index, { isCompleted: Boolean(checked) })
                  }
                  aria-label={t("handover.form.checklistCompleted")}
                />
                <Input
                  aria-label={t("handover.form.checklistName")}
                  maxLength={300}
                  value={item.name}
                  onChange={(event) => updateChecklistItem(index, { name: event.target.value })}
                  placeholder={t("handover.form.checklistName")}
                />
                <Input
                  aria-label={t("handover.form.checklistNote")}
                  maxLength={1000}
                  value={item.note ?? ""}
                  onChange={(event) => updateChecklistItem(index, { note: event.target.value })}
                  placeholder={t("handover.form.checklistNote")}
                />
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  onClick={() => removeRow("checklistItems", index)}
                  aria-label={t("handover.action.remove")}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </section>
          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <span>{t("handover.field.documents")}</span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={form.documents.length >= 20}
                onClick={() =>
                  setForm((current) => ({ ...current, documents: [...current.documents, ""] }))
                }
              >
                <Plus className="mr-2 h-4 w-4" />
                {t("handover.form.addDocument")}
              </Button>
            </div>
            <AdminDocumentUpload
              multiple
              maxFiles={Math.max(0, 20 - form.documents.filter((item) => item.trim()).length)}
              uploadFile={async (file) => (await adminApi.uploadHandoverDocument(file)).data.path}
              onUploaded={(paths) =>
                setForm((current) => ({ ...current, documents: [...current.documents, ...paths] }))
              }
              disabled={saving}
              testId="handover-document-upload"
            />
            {form.documents.map((item, index) => (
              <div key={index} className="flex gap-2">
                <Input
                  aria-label={t("handover.field.documents")}
                  maxLength={500}
                  value={item}
                  onChange={(event) => updateStringRow("documents", index, event.target.value)}
                  placeholder={t("handover.form.documentPlaceholder")}
                />
                {item.trim() &&
                  (!isManagedDocumentPath(item, "/files/business-documents/handover") ||
                    rows.find((row) => row.id === editingId)?.documents.includes(item)) && (
                    <AdminFilePreview
                      url={item}
                      fileName={item.split("/").pop()}
                      fetchFile={
                        isManagedDocumentPath(item, "/files/business-documents/handover")
                          ? async () =>
                              (await adminApi.getHandoverDocumentContent(editingId!, item)).data
                          : undefined
                      }
                    />
                  )}
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  onClick={() => removeRow("documents", index)}
                  aria-label={t("handover.action.remove")}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </section>
          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <span>{t("handover.field.signatories")}</span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={form.signatories.length >= 20}
                onClick={() =>
                  setForm((current) => ({ ...current, signatories: [...current.signatories, ""] }))
                }
              >
                <Plus className="mr-2 h-4 w-4" />
                {t("handover.form.addSignatory")}
              </Button>
            </div>
            {form.signatories.map((item, index) => (
              <div key={index} className="flex gap-2">
                <Input
                  aria-label={t("handover.field.signatories")}
                  maxLength={200}
                  value={item}
                  onChange={(event) => updateStringRow("signatories", index, event.target.value)}
                  placeholder={t("handover.form.signatoryPlaceholder")}
                />
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  onClick={() => removeRow("signatories", index)}
                  aria-label={t("handover.action.remove")}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </section>
          {formError && <p className="text-sm font-medium text-destructive">{formError}</p>}
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={handleSave} disabled={saving} data-testid="handover-form-save">
              {saving ? t("handover.form.saving") : t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(pendingTransition)}
        onOpenChange={(open) => {
          if (!open && !acting) setPendingTransition(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("handover.confirm.transitionTitle")}</DialogTitle>
            <DialogDescription>
              {t("handover.confirm.transitionBody")
                .replace("{title}", pendingTransition?.record.title ?? "")
                .replace(
                  "{status}",
                  pendingTransition
                    ? t(`handover.status.${pendingTransition.next.toLowerCase()}`)
                    : "",
                )}
            </DialogDescription>
          </DialogHeader>
          <Textarea
            aria-label={t("a11yUi.reason")}
            value={transitionNote}
            onChange={(event) => setTransitionNote(event.target.value)}
            placeholder={t("handover.confirm.notePlaceholder")}
          />
          <DialogFooter>
            <Button variant="outline" disabled={acting} onClick={() => setPendingTransition(null)}>
              {t("common.cancel")}
            </Button>
            <Button disabled={acting} onClick={handleTransition}>
              {acting ? t("handover.action.processing") : t("handover.action.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(pendingDelete)}
        onOpenChange={(open) => {
          if (!open && !acting) setPendingDelete(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("handover.confirm.deleteTitle")}</DialogTitle>
            <DialogDescription>
              {t("handover.confirm.deleteBody").replace("{title}", pendingDelete?.title ?? "")}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" disabled={acting} onClick={() => setPendingDelete(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="destructive"
              disabled={acting}
              onClick={handleDelete}
              data-testid="handover-delete-confirm"
            >
              {acting ? t("handover.action.processing") : t("handover.action.delete")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
