import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import { collectLookupPages } from "@/lib/pagination";
import ConstructionStatTile from "./ConstructionStatTile";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  Download,
  Eye,
  FileCheck,
  FolderArchive,
  FolderOpen,
  Pencil,
  Plus,
  RefreshCcw,
  Search,
  Trash2,
} from "lucide-react";
import { type DesignProjectScopeProps } from "./constructionScope";
import AdminDocumentUpload from "@/components/admin/AdminDocumentUpload";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { useI18n } from "@/lib/i18n";
import { localizedName } from "@/lib/category";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { isManagedDocumentPath, resolveSafeLinkUrl } from "@/lib/url";
import { ListPagination } from "@/components/admin/ListPagination";
import { PageEmpty, PageLoading, PageError } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { ASBUILT_STATUS_TONE } from "./constructionStatus";
import { Badge } from "@/components/ui/badge";
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
  type AsBuiltCategory,
  type AsBuiltDocumentCategoryResponse,
  type AsBuiltDocumentListParams,
  type AsBuiltDocumentResponse,
  type AsBuiltStatus,
  type CreateAsBuiltDocumentRequest,
  type DesignProjectListItemResponse,
  type UpdateAsBuiltDocumentRequest,
} from "@/services/adminApi";
import { formatDateTime as formatDateTimeInZone } from "@/lib/displaySettings";

const ASBUILT_STATUSES: AsBuiltStatus[] = [
  "Draft",
  "Submitted",
  "Approved",
  "Archived",
  "Cancelled",
];
const EDITABLE_STATUSES = new Set<AsBuiltStatus>(["Draft", "Submitted"]);

const CATEGORY_BADGE = "border-indigo/40 bg-indigo/10 text-indigo";

const formatDateTime = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const d = new Date(iso);
  return formatDateTimeInZone(iso);
};

export function AsBuiltDocumentsPanel({
  designProjectId,
  scope = "review",
}: DesignProjectScopeProps) {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionAsBuiltManage);
  const canApprove = scope === "review" && has(ADMIN_PERMS.constructionAsBuiltApprove);

  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const projectId = designProjectId ?? undefined;
  const [category, setCategory] = useState<AsBuiltCategory | "">("");
  const [status, setStatus] = useState<AsBuiltStatus | "">("");
  const [openOnly, setOpenOnly] = useState(false);
  const [search, setSearch] = useState("");
  const settledSearch = useDebouncedValue(search);
  const [sort, setSort] = useState("category-asc");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);

  const [categories, setCategories] = useState<AsBuiltDocumentCategoryResponse[]>([]);
  const [rows, setRows] = useState<AsBuiltDocumentResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<AsBuiltStatus, number>>>({});
  const [completedRequired, setCompletedRequired] = useState(0);
  const [totalRequired, setTotalRequired] = useState(0);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());

  const [detail, setDetail] = useState<AsBuiltDocumentResponse | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formProjectId, setFormProjectId] = useState<number | undefined>(); // For create dialog

  const [pendingTransition, setPendingTransition] = useState<{
    id: number;
    title: string;
    next: AsBuiltStatus;
  } | null>(null);
  const [pendingDelete, setPendingDelete] = useState<AsBuiltDocumentResponse | null>(null);
  const [pendingBulk, setPendingBulk] = useState(false);
  const [transitionNote, setTransitionNote] = useState("");

  const [form, setForm] = useState({
    title: "",
    category: "Drawing" as AsBuiltCategory,
    description: "",
    fileUrl: "",
    note: "",
  });

  // Helper to get localized category name
  const getCategoryName = useCallback(
    (code: string) => {
      const cat = categories.find((c) => c.code === code);
      if (!cat) return code;
      return localizedName(cat, lang);
    },
    [categories, lang],
  );

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [projectsRes, categoriesRes] = await Promise.all([
          collectLookupPages((page) => adminApi.listDesignProjects({ pageSize: 200, page })),
          adminApi.getAsBuiltDocumentCategories(true),
        ]);
        if (!cancelled) {
          setProjects(projectsRes.data.items ?? []);
          setCategories(categoriesRes.data ?? []);
        }
      } catch {
        // Initial data load optional - will work without
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const startListRequest = useRequestGuard();
  const load = useCallback(async () => {
    const isCurrent = startListRequest();
    setLoading(true);
    setError(null);
    try {
      const params: AsBuiltDocumentListParams = {
        designProjectId: projectId,
        category: category || undefined,
        status: status || undefined,
        search: settledSearch.trim() || undefined,
        openOnly: openOnly || undefined,
        sortBy: sort.split("-")[0] as AsBuiltDocumentListParams["sortBy"],
        sortDirection: sort.split("-")[1] as AsBuiltDocumentListParams["sortDirection"],
        page,
        pageSize,
      };
      const res = await adminApi.listAsBuiltDocuments(params);
      if (!isCurrent()) return;
      setRows(res.data.items ?? []);
      setTotal(res.data.total ?? 0);
      setStatusCounts(res.data.statusCounts ?? {});
      setCompletedRequired(res.data.completedRequiredCategories ?? 0);
      setTotalRequired(res.data.totalRequiredCategories ?? 0);
      setSelected(new Set());
    } catch (e) {
      if (!isCurrent()) return;
      setError(uiError(e) || t("asbuilt.error"));
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }, [
    startListRequest,
    projectId,
    category,
    status,
    settledSearch,
    openOnly,
    sort,
    page,
    pageSize,
    uiError,
    t,
  ]);

  useEffect(() => {
    load();
  }, [load]);

  // Keep detail sheet in sync with reload.
  useEffect(() => {
    if (!detail) return;
    const refreshed = rows.find((r) => r.id === detail.id);
    if (refreshed && refreshed !== detail) setDetail(refreshed);
  }, [rows, detail]);

  const openCreate = () => {
    setEditingId(null);
    // Pre-select first active category if available
    const firstActiveCategory = categories.find((c) => c.isActive);
    setForm({
      title: "",
      category: firstActiveCategory?.code || "Drawing",
      description: "",
      fileUrl: "",
      note: "",
    });
    // Pre-select current filter project if set, otherwise leave empty for user to choose
    setFormProjectId(projectId);
    setFormError(null);
    setFormOpen(true);
  };

  const openEdit = (r: AsBuiltDocumentResponse) => {
    setEditingId(r.id);
    setForm({
      title: r.title,
      category: r.category,
      description: r.description ?? "",
      fileUrl: r.fileUrl ?? "",
      note: r.note ?? "",
    });
    setFormProjectId(undefined); // Not needed for edit
    setFormError(null);
    setFormOpen(true);
  };

  const handleSave = async () => {
    setFormError(null);
    const title = form.title.trim();
    if (!title) {
      setFormError(t("asbuilt.form.required.title"));
      return;
    }
    // For new documents, require project selection in dialog
    if (!editingId && formProjectId == null) {
      setFormError(t("asbuilt.form.required.project"));
      return;
    }
    if (!form.category) {
      setFormError(t("asbuilt.form.required.category"));
      return;
    }
    if (form.fileUrl.trim() && !resolveSafeLinkUrl(form.fileUrl)) {
      setFormError(t("common.invalidFileLink"));
      return;
    }
    setSaving(true);
    try {
      const payload = {
        title,
        category: form.category,
        description: form.description.trim() || null,
        fileUrl: form.fileUrl.trim() || null,
        note: form.note.trim() || null,
      };
      if (editingId) {
        await adminApi.updateAsBuiltDocument(editingId, payload as UpdateAsBuiltDocumentRequest);
      } else {
        await adminApi.createAsBuiltDocument({
          ...payload,
          designProjectId: formProjectId!,
        } as CreateAsBuiltDocumentRequest);
      }
      toast({ title: t("asbuilt.form.saved") });
      setFormOpen(false);
      await load();
    } catch (e) {
      setFormError(uiError(e) || t("asbuilt.error"));
    } finally {
      setSaving(false);
    }
  };

  const runTransition = async (id: number, next: AsBuiltStatus, note: string) => {
    try {
      if (next === "Approved") {
        await adminApi.approveAsBuiltDocument(id, { status: "Approved", note: note || null });
      } else {
        await adminApi.transitionAsBuiltStatus(id, { status: next, note: note || null });
      }
      toast({ title: t("asbuilt.toast.transition.success") });
      await load();
    } catch (e) {
      toast({
        variant: "destructive",
        title: uiError(e) || t("asbuilt.error"),
      });
    }
  };

  const confirmTransition = (r: AsBuiltDocumentResponse, next: AsBuiltStatus) => {
    setTransitionNote("");
    setPendingTransition({ id: r.id, title: r.title, next });
  };

  const handleTransitionConfirm = async () => {
    if (!pendingTransition) return;
    const { id, next } = pendingTransition;
    setPendingTransition(null);
    await runTransition(id, next, transitionNote.trim());
  };

  const handleDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    try {
      await adminApi.deleteAsBuiltDocument(id);
      toast({ title: t("asbuilt.toast.deleted") });
      if (detail?.id === id) setDetail(null);
      await load();
    } catch (e) {
      toast({
        variant: "destructive",
        title: uiError(e) || t("asbuilt.error"),
      });
    }
  };

  const handleBulkDelete = async () => {
    setPendingBulk(false);
    if (selected.size === 0) return;
    try {
      const res = await adminApi.bulkDeleteAsBuiltDocuments({ ids: Array.from(selected) });
      const deletedCount = res.data.deletedIds?.length ?? 0;
      const skippedCount = res.data.skippedIds?.length ?? 0;
      toast({
        title: t("asbuilt.toast.bulkDeleted").replace("{count}", String(deletedCount)),
        description:
          skippedCount > 0
            ? t("asbuilt.toast.bulkSkipped").replace("{count}", String(skippedCount))
            : undefined,
      });
      await load();
    } catch (e) {
      toast({
        variant: "destructive",
        title: uiError(e) || t("asbuilt.error"),
      });
    }
  };

  const handleExport = async () => {
    setExporting(true);
    try {
      const [sortBy, sortDirection] = sort.split("-");
      const response = await adminApi.exportAsBuiltDocuments({
        designProjectId: projectId,
        category: category || undefined,
        status: status || undefined,
        search: settledSearch.trim() || undefined,
        openOnly: openOnly || undefined,
        sortBy: sortBy as AsBuiltDocumentListParams["sortBy"],
        sortDirection: sortDirection as AsBuiltDocumentListParams["sortDirection"],
      });
      const url = URL.createObjectURL(response.data);
      const link = document.createElement("a");
      link.href = url;
      link.download = `as-built-documents-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      toast({ title: t("asbuilt.toast.exported") });
    } catch (e) {
      toast({
        variant: "destructive",
        title: uiError(e) || t("asbuilt.error"),
      });
    } finally {
      setExporting(false);
    }
  };

  const availableTransitions = (r: AsBuiltDocumentResponse) => {
    const out: Array<{ next: AsBuiltStatus; label: string; testId: string }> = [];
    switch (r.status) {
      case "Draft":
        if (canManage)
          out.push({
            next: "Submitted",
            label: t("asbuilt.action.submit"),
            testId: "asbuilt-submit",
          });
        if (canManage)
          out.push({
            next: "Cancelled",
            label: t("asbuilt.action.cancel"),
            testId: "asbuilt-cancel",
          });
        break;
      case "Submitted":
        if (canApprove)
          out.push({
            next: "Approved",
            label: t("asbuilt.action.approve"),
            testId: "asbuilt-approve",
          });
        if (canManage)
          out.push({ next: "Draft", label: t("asbuilt.action.revise"), testId: "asbuilt-revise" });
        if (canManage)
          out.push({
            next: "Cancelled",
            label: t("asbuilt.action.cancel"),
            testId: "asbuilt-cancel",
          });
        break;
      case "Approved":
        if (canManage)
          out.push({
            next: "Archived",
            label: t("asbuilt.action.archive"),
            testId: "asbuilt-archive",
          });
        if (canManage)
          out.push({ next: "Draft", label: t("asbuilt.action.revise"), testId: "asbuilt-revise" });
        if (canManage)
          out.push({
            next: "Cancelled",
            label: t("asbuilt.action.cancel"),
            testId: "asbuilt-cancel",
          });
        break;
      case "Cancelled":
        if (canManage)
          out.push({
            next: "Draft",
            label: t("asbuilt.action.restore"),
            testId: "asbuilt-restore",
          });
        break;
      case "Archived":
        break;
    }
    return out;
  };

  const stats = useMemo(
    () => [
      {
        key: "total",
        label: t("asbuilt.stats.total"),
        value: total,

        icon: FolderArchive,
      },
      {
        key: "submitted",
        label: t("asbuilt.stats.submitted"),
        value: statusCounts.Submitted ?? 0,

        icon: FileCheck,
      },
      {
        key: "approved",
        label: t("asbuilt.stats.approved"),
        value: (statusCounts.Approved ?? 0) + (statusCounts.Archived ?? 0),

        icon: CheckCircle2,
      },
      {
        key: "completeness",
        label: t("asbuilt.stats.completeness"),
        value: totalRequired > 0 ? `${completedRequired}/${totalRequired}` : "—",
        gradient:
          totalRequired > 0 && completedRequired === totalRequired
            ? "from-success to-info"
            : "from-warning to-accent-orange",
        icon: FolderOpen,
      },
    ],
    [t, total, statusCounts, completedRequired, totalRequired],
  );

  const selectableCategories = useMemo(() => {
    const existingCategoryCode =
      editingId == null ? undefined : rows.find((row) => row.id === editingId)?.category;
    return categories.filter((item) => item.isActive || item.code === existingCategoryCode);
  }, [categories, editingId, rows]);

  const rowClickable = useCallback(
    (r: AsBuiltDocumentResponse) => ({
      role: "button" as const,
      tabIndex: 0,
      onClick: () => setDetail(r),
      onKeyDown: (e: React.KeyboardEvent) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          setDetail(r);
        }
      },
    }),
    [],
  );

  return (
    <>
      <div className="space-y-6" data-testid="asbuilt-page">
        <header className="flex justify-end">
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleExport}
              disabled={exporting || total === 0}
              data-testid="asbuilt-export"
            >
              <Download className="mr-2 h-4 w-4" />
              {exporting ? t("asbuilt.action.exporting") : t("asbuilt.action.export")}
            </Button>
            <Button variant="outline" size="sm" onClick={load} disabled={loading}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} data-testid="asbuilt-new">
                <Plus className="mr-2 h-4 w-4" />
                {t("asbuilt.action.new")}
              </Button>
            )}
          </div>
        </header>

        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          {stats.map((s) => {
            const Icon = s.icon;
            const subLabel =
              s.key === "completeness" && totalRequired > 0
                ? t("asbuilt.stats.completenessValue")
                    .replace("{done}", String(completedRequired))
                    .replace("{total}", String(totalRequired))
                : null;
            return (
              <ConstructionStatTile
                key={s.key}
                label={s.label}
                value={s.value}
                icon={<Icon />}
                testId={`asbuilt-stat-${s.key}`}
                note={subLabel}
              />
            );
          })}
        </div>

        <div className="rounded-lg border bg-card p-3 md:p-4">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-5 [&>div]:min-w-0">
            <FieldScope>
              <Label>{t("asbuilt.field.project")}</Label>
              <Input value={projects.find((p) => p.id === projectId)?.name ?? "—"} readOnly />
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.field.category")}</Label>
              <Select
                value={category || "__all__"}
                onValueChange={(v) => {
                  setCategory(v === "__all__" ? "" : (v as AsBuiltCategory));
                  setPage(1);
                }}
              >
                <SelectTrigger data-testid="asbuilt-filter-category">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("asbuilt.filter.category.all")}</SelectItem>
                  {categories
                    .filter((c) => c.isActive)
                    .map((c) => (
                      <SelectItem key={c.code} value={c.code}>
                        {localizedName(c, lang)}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.field.status")}</Label>
              <Select
                value={status || "__all__"}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as AsBuiltStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger data-testid="asbuilt-filter-status">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("asbuilt.filter.status.all")}</SelectItem>
                  {ASBUILT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`asbuilt.status.${s.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.filter.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("asbuilt.filter.search")}
                  className="pl-8"
                  data-testid="asbuilt-search"
                />
              </div>
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.filter.sort")}</Label>
              <Select
                value={sort}
                onValueChange={(value) => {
                  setSort(value);
                  setPage(1);
                }}
              >
                <SelectTrigger data-testid="asbuilt-sort">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="category-asc">{t("asbuilt.sort.categoryAsc")}</SelectItem>
                  <SelectItem value="code-asc">{t("asbuilt.sort.codeAsc")}</SelectItem>
                  <SelectItem value="code-desc">{t("asbuilt.sort.codeDesc")}</SelectItem>
                  <SelectItem value="title-asc">{t("asbuilt.sort.titleAsc")}</SelectItem>
                  <SelectItem value="title-desc">{t("asbuilt.sort.titleDesc")}</SelectItem>
                  <SelectItem value="project-asc">{t("asbuilt.sort.projectAsc")}</SelectItem>
                  <SelectItem value="status-asc">{t("asbuilt.sort.statusAsc")}</SelectItem>
                  <SelectItem value="updatedAt-desc">{t("asbuilt.sort.updatedDesc")}</SelectItem>
                  <SelectItem value="updatedAt-asc">{t("asbuilt.sort.updatedAsc")}</SelectItem>
                </SelectContent>
              </Select>
            </FieldScope>
          </div>
          <div className="mt-2 flex items-center gap-4">
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={openOnly}
                onCheckedChange={(v) => {
                  setOpenOnly(!!v);
                  setPage(1);
                }}
              />
              {t("asbuilt.filter.openOnly")}
            </label>
          </div>
        </div>

        {canManage && selected.size > 0 && (
          <div className="flex items-center justify-between rounded-lg border bg-muted/40 px-4 py-2">
            <span className="text-sm">
              {selected.size} / {rows.length}
            </span>
            <Button
              variant="destructive"
              size="sm"
              onClick={() => setPendingBulk(true)}
              data-testid="asbuilt-bulk-delete"
            >
              <Trash2 className="mr-2 h-4 w-4" />
              {t("asbuilt.action.bulkDelete").replace("{count}", String(selected.size))}
            </Button>
          </div>
        )}

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={load} />
        ) : rows.length === 0 ? (
          <PageEmpty
            message={t("asbuilt.empty")}
            action={
              canManage && (
                <Button size="sm" onClick={openCreate} data-testid="asbuilt-empty-new">
                  <Plus className="mr-2 h-4 w-4" />
                  {t("asbuilt.action.new")}
                </Button>
              )
            }
          />
        ) : (
          <>
            {/* Desktop table */}
            <div className="hidden overflow-hidden rounded-lg border bg-card md:block">
              <table className="w-full text-sm">
                <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    {canManage && (
                      <th className="w-10 px-3 py-2">
                        <Checkbox
                          checked={selected.size === rows.length && rows.length > 0}
                          onCheckedChange={(v) => {
                            if (v) setSelected(new Set(rows.map((r) => r.id)));
                            else setSelected(new Set());
                          }}
                          aria-label={t("asbuilt.action.selectAll")}
                        />
                      </th>
                    )}
                    <th className="px-3 py-2">{t("asbuilt.field.code")}</th>
                    <th className="px-3 py-2">{t("asbuilt.field.title")}</th>
                    <th className="px-3 py-2">{t("asbuilt.field.category")}</th>
                    <th className="px-3 py-2">{t("asbuilt.field.project")}</th>
                    <th className="px-3 py-2">{t("asbuilt.field.status")}</th>
                    <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      className="cursor-pointer border-t hover:bg-muted/40"
                      data-testid={`asbuilt-row-${r.id}`}
                      {...rowClickable(r)}
                    >
                      {canManage && (
                        <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={selected.has(r.id)}
                            onCheckedChange={(v) => {
                              const s = new Set(selected);
                              if (v) s.add(r.id);
                              else s.delete(r.id);
                              setSelected(s);
                            }}
                            aria-label={`select-${r.id}`}
                          />
                        </td>
                      )}
                      <td className="px-3 py-2 font-mono text-xs">{r.documentCode}</td>
                      <td className="px-3 py-2 font-medium">{r.title}</td>
                      <td className="px-3 py-2">
                        <Badge variant="outline" className={CATEGORY_BADGE}>
                          {getCategoryName(r.category)}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-xs text-muted-foreground">
                        {r.designProjectName}
                      </td>
                      <td className="px-3 py-2">
                        <ConstructionStatusBadge tone={ASBUILT_STATUS_TONE[r.status]}>
                          {t(`asbuilt.status.${r.status.toLowerCase()}`)}
                        </ConstructionStatusBadge>
                      </td>
                      <td className="px-3 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                        <div className="flex justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            title={t("common.view")}
                            aria-label={t("common.view")}
                            onClick={() => setDetail(r)}
                            data-testid={`asbuilt-row-view-${r.id}`}
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
                                disabled={!EDITABLE_STATUSES.has(r.status)}
                                onClick={() => openEdit(r)}
                                data-testid={`asbuilt-row-edit-${r.id}`}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              <Button
                                variant="ghost"
                                size="icon"
                                title={t("common.delete")}
                                aria-label={t("common.delete")}
                                onClick={() => setPendingDelete(r)}
                                data-testid={`asbuilt-row-delete-${r.id}`}
                              >
                                <Trash2 className="h-4 w-4 text-destructive" />
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

            {/* Mobile cards */}
            <div className="grid grid-cols-1 gap-3 md:hidden">
              {rows.map((r) => (
                <article
                  key={r.id}
                  className="rounded-lg border bg-card p-3 shadow-sm"
                  data-testid={`asbuilt-card-${r.id}`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="font-mono text-xs text-muted-foreground">
                        {r.documentCode}
                      </div>
                      <div className="truncate font-medium">{r.title}</div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        {r.designProjectName}
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <ConstructionStatusBadge tone={ASBUILT_STATUS_TONE[r.status]}>
                        {t(`asbuilt.status.${r.status.toLowerCase()}`)}
                      </ConstructionStatusBadge>
                      <Badge variant="outline" className={CATEGORY_BADGE}>
                        {getCategoryName(r.category)}
                      </Badge>
                    </div>
                  </div>
                  <div className="mt-3 flex flex-wrap justify-end gap-1 border-t pt-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setDetail(r)}
                      data-testid={`asbuilt-card-view-${r.id}`}
                    >
                      <Eye className="mr-1 h-4 w-4" />
                      {t("common.view")}
                    </Button>
                    {canManage && (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={!EDITABLE_STATUSES.has(r.status)}
                          onClick={() => openEdit(r)}
                          data-testid={`asbuilt-card-edit-${r.id}`}
                        >
                          <Pencil className="mr-1 h-4 w-4" />
                          {t("common.edit")}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-destructive hover:text-destructive"
                          onClick={() => setPendingDelete(r)}
                          data-testid={`asbuilt-card-delete-${r.id}`}
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

            <ListPagination
              page={page}
              pageSize={pageSize}
              total={total}
              onPageChange={setPage}
              disabled={loading}
            />
          </>
        )}
      </div>

      {/* Detail sheet */}
      <Sheet open={!!detail} onOpenChange={(o) => !o && setDetail(null)}>
        <SheetContent className="w-full overflow-y-auto sm:max-w-lg">
          {detail && (
            <>
              <SheetHeader>
                <SheetTitle className="text-lg">
                  <span className="mr-2 font-mono text-xs text-muted-foreground">
                    {detail.documentCode}
                  </span>
                  {detail.title}
                </SheetTitle>
                <SheetDescription>{detail.designProjectName}</SheetDescription>
              </SheetHeader>
              <div className="mt-4 space-y-4 text-sm">
                <div className="flex flex-wrap gap-2">
                  <ConstructionStatusBadge tone={ASBUILT_STATUS_TONE[detail.status]}>
                    {t(`asbuilt.status.${detail.status.toLowerCase()}`)}
                  </ConstructionStatusBadge>
                  <Badge variant="outline" className={CATEGORY_BADGE}>
                    {getCategoryName(detail.category)}
                  </Badge>
                </div>

                <dl className="grid grid-cols-2 gap-2 text-xs">
                  {detail.description && (
                    <>
                      <dt className="text-muted-foreground">{t("asbuilt.field.description")}</dt>
                      <dd className="whitespace-pre-wrap">{detail.description}</dd>
                    </>
                  )}
                  {detail.fileUrl && (
                    <>
                      <dt className="text-muted-foreground">{t("asbuilt.field.fileUrl")}</dt>
                      <dd className="flex min-w-0 items-center gap-2">
                        <span className="min-w-0 flex-1 break-all">{detail.fileUrl}</span>
                        <AdminFilePreview
                          url={detail.fileUrl}
                          fetchFile={
                            isManagedDocumentPath(
                              detail.fileUrl,
                              "/files/business-documents/as-built",
                            )
                              ? async () =>
                                  (await adminApi.getAsBuiltDocumentContent(detail.id)).data
                              : undefined
                          }
                          testId="asbuilt-detail-file-preview"
                        />
                      </dd>
                    </>
                  )}
                  {detail.note && (
                    <>
                      <dt className="text-muted-foreground">{t("asbuilt.field.note")}</dt>
                      <dd className="whitespace-pre-wrap">{detail.note}</dd>
                    </>
                  )}
                  {detail.submittedAt && (
                    <>
                      <dt className="text-muted-foreground">{t("asbuilt.field.submittedBy")}</dt>
                      <dd>
                        {detail.submittedByName ?? "—"} · {formatDateTime(detail.submittedAt)}
                      </dd>
                    </>
                  )}
                  {detail.approvedAt && (
                    <>
                      <dt className="text-muted-foreground">{t("asbuilt.field.approvedBy")}</dt>
                      <dd>
                        {detail.approvedByName ?? "—"} · {formatDateTime(detail.approvedAt)}
                      </dd>
                    </>
                  )}
                  {detail.archivedAt && (
                    <>
                      <dt className="text-muted-foreground">{t("asbuilt.field.archivedAt")}</dt>
                      <dd>{formatDateTime(detail.archivedAt)}</dd>
                    </>
                  )}
                  <dt className="text-muted-foreground">{t("asbuilt.field.createdAt")}</dt>
                  <dd>{formatDateTime(detail.createdAt)}</dd>
                  <dt className="text-muted-foreground">{t("asbuilt.field.updatedAt")}</dt>
                  <dd>{formatDateTime(detail.updatedAt)}</dd>
                </dl>

                <div className="border-t pt-3">
                  <h3 className="mb-2 text-sm font-semibold">{t("asbuilt.detail.lifecycle")}</h3>
                  <ol className="space-y-2 text-xs">
                    <li className="flex items-start justify-between gap-3">
                      <span>{t("asbuilt.lifecycle.created")}</span>
                      <span className="text-muted-foreground">
                        {formatDateTime(detail.createdAt)}
                      </span>
                    </li>
                    {detail.submittedAt && (
                      <li className="flex items-start justify-between gap-3">
                        <span>
                          {t("asbuilt.lifecycle.submitted").replace(
                            "{user}",
                            detail.submittedByName ?? "—",
                          )}
                        </span>
                        <span className="text-muted-foreground">
                          {formatDateTime(detail.submittedAt)}
                        </span>
                      </li>
                    )}
                    {detail.approvedAt && (
                      <li className="flex items-start justify-between gap-3">
                        <span>
                          {t("asbuilt.lifecycle.approved").replace(
                            "{user}",
                            detail.approvedByName ?? "—",
                          )}
                        </span>
                        <span className="text-muted-foreground">
                          {formatDateTime(detail.approvedAt)}
                        </span>
                      </li>
                    )}
                    {detail.archivedAt && (
                      <li className="flex items-start justify-between gap-3">
                        <span>{t("asbuilt.lifecycle.archived")}</span>
                        <span className="text-muted-foreground">
                          {formatDateTime(detail.archivedAt)}
                        </span>
                      </li>
                    )}
                  </ol>
                </div>

                {(canManage || canApprove) && (
                  <div className="flex flex-wrap gap-2 border-t pt-3">
                    {canManage && EDITABLE_STATUSES.has(detail.status) && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => openEdit(detail)}
                        data-testid="asbuilt-edit"
                      >
                        {t("asbuilt.action.edit")}
                      </Button>
                    )}
                    {availableTransitions(detail).map((tr) => (
                      <Button
                        key={tr.next}
                        variant={tr.next === "Approved" ? "default" : "outline"}
                        size="sm"
                        onClick={() => confirmTransition(detail, tr.next)}
                        data-testid={tr.testId}
                      >
                        {tr.next === "Approved" && <CheckCircle2 className="mr-2 h-4 w-4" />}
                        {tr.label}
                      </Button>
                    ))}
                    {canManage && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive"
                        onClick={() => setPendingDelete(detail)}
                        data-testid="asbuilt-delete"
                      >
                        <Trash2 className="mr-2 h-4 w-4" />
                        {t("asbuilt.action.delete")}
                      </Button>
                    )}
                  </div>
                )}
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>

      {/* Create / edit dialog */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {editingId ? t("asbuilt.form.editTitle") : t("asbuilt.form.newTitle")}
            </DialogTitle>
            {editingId && (
              <DialogDescription>
                {projects.find((p) => p.id === projectId)?.name}
              </DialogDescription>
            )}
          </DialogHeader>
          <div className="space-y-3">
            {/* Project selector - only show when creating new document */}
            {!editingId && (
              <FieldScope>
                <Label>{t("asbuilt.field.project")} *</Label>
                <SearchableSelect
                  options={projects.map((p) => ({ value: String(p.id), label: p.name }))}
                  value={formProjectId != null ? String(formProjectId) : ""}
                  onChange={(v) => setFormProjectId(v ? Number(v) : undefined)}
                  placeholder={t("asbuilt.form.selectProject")}
                  data-testid="asbuilt-form-project"
                />
              </FieldScope>
            )}
            <FieldScope>
              <Label>{t("asbuilt.field.title")} *</Label>
              <Input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                data-testid="asbuilt-form-title"
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.field.category")} *</Label>
              <Select
                value={form.category}
                onValueChange={(v) => setForm({ ...form, category: v as AsBuiltCategory })}
              >
                <SelectTrigger data-testid="asbuilt-form-category">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {selectableCategories.map((c) => (
                    <SelectItem key={c.code} value={c.code}>
                      {localizedName(c, lang)}
                      {c.isActive ? "" : ` (${t("common.inactive")})`}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.field.fileUrl")}</Label>
              <AdminDocumentUpload
                uploadFile={async (file) =>
                  (await adminApi.uploadAsBuiltDocumentFile(file)).data.path
                }
                onUploaded={([path]) => setForm((current) => ({ ...current, fileUrl: path }))}
                disabled={saving}
                testId="asbuilt-document-upload"
              />
              <div className="flex items-center gap-2">
                <Input
                  value={form.fileUrl}
                  onChange={(e) => setForm({ ...form, fileUrl: e.target.value })}
                  placeholder="/files/asbuilt/…"
                  data-testid="asbuilt-form-file-url"
                />
                {form.fileUrl.trim() &&
                  (!isManagedDocumentPath(form.fileUrl, "/files/business-documents/as-built") ||
                    rows.find((row) => row.id === editingId)?.fileUrl === form.fileUrl) && (
                    <AdminFilePreview
                      url={form.fileUrl}
                      fetchFile={
                        isManagedDocumentPath(form.fileUrl, "/files/business-documents/as-built")
                          ? async () => (await adminApi.getAsBuiltDocumentContent(editingId!)).data
                          : undefined
                      }
                    />
                  )}
              </div>
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.field.description")}</Label>
              <Textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </FieldScope>
            <FieldScope>
              <Label>{t("asbuilt.field.note")}</Label>
              <Input
                value={form.note}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
              />
            </FieldScope>
            {formError && <div className="text-sm text-destructive">{formError}</div>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>
              {t("asbuilt.action.close")}
            </Button>
            <Button onClick={handleSave} disabled={saving} data-testid="asbuilt-form-save">
              {saving ? t("asbuilt.form.saving") : t("asbuilt.form.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Confirm transition */}
      <AlertDialog
        open={!!pendingTransition}
        onOpenChange={(o) => !o && setPendingTransition(null)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("asbuilt.confirm.transition.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("asbuilt.confirm.transition.body")
                .replace("{title}", pendingTransition?.title ?? "")
                .replace(
                  "{status}",
                  pendingTransition
                    ? t(`asbuilt.status.${pendingTransition.next.toLowerCase()}`)
                    : "",
                )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div>
            <Textarea
              aria-label={t("a11yUi.reason")}
              rows={2}
              placeholder={t("asbuilt.confirm.reasonPlaceholder")}
              value={transitionNote}
              onChange={(e) => setTransitionNote(e.target.value)}
              data-testid="asbuilt-transition-note"
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("asbuilt.action.close")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleTransitionConfirm}
              data-testid="asbuilt-action-confirm"
            >
              {t("asbuilt.form.save")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Confirm delete */}
      <AlertDialog open={!!pendingDelete} onOpenChange={(o) => !o && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("asbuilt.confirm.delete.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("asbuilt.confirm.delete.body").replace("{title}", pendingDelete?.title ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("asbuilt.action.close")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} data-testid="asbuilt-delete-confirm">
              {t("asbuilt.action.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Confirm bulk */}
      <AlertDialog open={pendingBulk} onOpenChange={setPendingBulk}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {t("asbuilt.confirm.bulkDelete.title").replace("{count}", String(selected.size))}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {t("asbuilt.confirm.bulkDelete.body").replace(
                "{titles}",
                rows
                  .filter((r) => selected.has(r.id))
                  .slice(0, 3)
                  .map((r) => `${r.documentCode} — ${r.title}`)
                  .join(", ") + (selected.size > 3 ? ` … (+${selected.size - 3})` : ""),
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("asbuilt.action.close")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleBulkDelete} data-testid="asbuilt-bulk-confirm">
              {t("asbuilt.action.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
