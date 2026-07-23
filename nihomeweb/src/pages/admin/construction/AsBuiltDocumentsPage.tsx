import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  FileCheck,
  FolderArchive,
  FolderOpen,
  Plus,
  RefreshCcw,
  Search,
  Trash2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
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
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import {
  adminApi,
  type AsBuiltCategory,
  type AsBuiltDocumentListParams,
  type AsBuiltDocumentResponse,
  type AsBuiltStatus,
  type CreateAsBuiltDocumentRequest,
  type DesignProjectListItemResponse,
  type UpdateAsBuiltDocumentRequest,
} from "@/services/adminApi";

const ASBUILT_STATUSES: AsBuiltStatus[] = ["Draft", "Submitted", "Approved", "Archived", "Cancelled"];
const ASBUILT_CATEGORIES: AsBuiltCategory[] = [
  "Drawing",
  "AcceptanceMinute",
  "TestReport",
  "WarrantyCertificate",
  "Other",
];
const EDITABLE_STATUSES = new Set<AsBuiltStatus>(["Draft", "Submitted"]);
const DELETABLE_STATUSES = new Set<AsBuiltStatus>(["Draft", "Cancelled"]);

const STATUS_BADGE: Record<AsBuiltStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Archived: "border-indigo-200 bg-indigo-50 text-indigo-700",
  Cancelled: "border-zinc-200 bg-zinc-50 text-zinc-600",
};

const CATEGORY_BADGE = "border-violet-200 bg-violet-50 text-violet-700";

const formatDate = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
};

const formatDateTime = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString();
};

export default function AsBuiltDocumentsPage() {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionAsBuiltManage);
  const canApprove = has(ADMIN_PERMS.constructionAsBuiltApprove);

  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const [projectId, setProjectId] = useState<number | undefined>();
  const [category, setCategory] = useState<AsBuiltCategory | "">("");
  const [status, setStatus] = useState<AsBuiltStatus | "">("");
  const [openOnly, setOpenOnly] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);

  const [rows, setRows] = useState<AsBuiltDocumentResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<AsBuiltStatus, number>>>({});
  const [completedRequired, setCompletedRequired] = useState(0);
  const [totalRequired, setTotalRequired] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());

  const [detail, setDetail] = useState<AsBuiltDocumentResponse | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

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

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await adminApi.listDesignProjects({ pageSize: 200 });
        if (!cancelled) setProjects(res.data.items ?? []);
      } catch {
        // Project dropdown is optional.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: AsBuiltDocumentListParams = {
        designProjectId: projectId,
        category: category || undefined,
        status: status || undefined,
        search: search.trim() || undefined,
        openOnly: openOnly || undefined,
        page,
        pageSize,
      };
      const res = await adminApi.listAsBuiltDocuments(params);
      setRows(res.data.items ?? []);
      setTotal(res.data.total ?? 0);
      setStatusCounts(res.data.statusCounts ?? {});
      setCompletedRequired(res.data.completedRequiredCategories ?? 0);
      setTotalRequired(res.data.totalRequiredCategories ?? 0);
      setSelected(new Set());
    } catch (e) {
      setError(extractApiError(e, t("asbuilt.error")));
    } finally {
      setLoading(false);
    }
  }, [projectId, category, status, search, openOnly, page, pageSize, t]);

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
    setForm({ title: "", category: "Drawing", description: "", fileUrl: "", note: "" });
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
    if (!editingId && projectId == null) {
      setFormError(t("asbuilt.form.required.project"));
      return;
    }
    if (!form.category) {
      setFormError(t("asbuilt.form.required.category"));
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
          designProjectId: projectId!,
        } as CreateAsBuiltDocumentRequest);
      }
      toast({ title: t("asbuilt.form.saved") });
      setFormOpen(false);
      await load();
    } catch (e) {
      setFormError(extractApiError(e, t("asbuilt.error")));
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
        title: extractApiError(e, t("asbuilt.error")),
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
        title: extractApiError(e, t("asbuilt.error")),
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
        title: extractApiError(e, t("asbuilt.error")),
      });
    }
  };

  const availableTransitions = (r: AsBuiltDocumentResponse) => {
    const out: Array<{ next: AsBuiltStatus; label: string; testId: string }> = [];
    switch (r.status) {
      case "Draft":
        if (canManage) out.push({ next: "Submitted", label: t("asbuilt.action.submit"), testId: "asbuilt-submit" });
        if (canManage) out.push({ next: "Cancelled", label: t("asbuilt.action.cancel"), testId: "asbuilt-cancel" });
        break;
      case "Submitted":
        if (canApprove) out.push({ next: "Approved", label: t("asbuilt.action.approve"), testId: "asbuilt-approve" });
        if (canManage) out.push({ next: "Draft", label: t("asbuilt.action.revise"), testId: "asbuilt-revise" });
        if (canManage) out.push({ next: "Cancelled", label: t("asbuilt.action.cancel"), testId: "asbuilt-cancel" });
        break;
      case "Approved":
        if (canManage) out.push({ next: "Archived", label: t("asbuilt.action.archive"), testId: "asbuilt-archive" });
        if (canManage) out.push({ next: "Draft", label: t("asbuilt.action.revise"), testId: "asbuilt-revise" });
        if (canManage) out.push({ next: "Cancelled", label: t("asbuilt.action.cancel"), testId: "asbuilt-cancel" });
        break;
      case "Cancelled":
        if (canManage) out.push({ next: "Draft", label: t("asbuilt.action.restore"), testId: "asbuilt-restore" });
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
        gradient: "from-indigo-500 to-violet-500",
        icon: FolderArchive,
      },
      {
        key: "submitted",
        label: t("asbuilt.stats.submitted"),
        value: statusCounts.Submitted ?? 0,
        gradient: "from-sky-500 to-cyan-500",
        icon: FileCheck,
      },
      {
        key: "approved",
        label: t("asbuilt.stats.approved"),
        value: (statusCounts.Approved ?? 0) + (statusCounts.Archived ?? 0),
        gradient: "from-emerald-500 to-teal-500",
        icon: CheckCircle2,
      },
      {
        key: "completeness",
        label: t("asbuilt.stats.completeness"),
        value: totalRequired > 0 ? `${completedRequired}/${totalRequired}` : "—",
        gradient: totalRequired > 0 && completedRequired === totalRequired
          ? "from-emerald-500 to-teal-500"
          : "from-amber-500 to-orange-500",
        icon: FolderOpen,
      },
    ],
    [t, total, statusCounts, completedRequired, totalRequired],
  );

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
    <AdminLayout>
      <div className="space-y-6 p-4 md:p-6" data-testid="asbuilt-page">
        <header className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 className="text-2xl font-bold">{t("asbuilt.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("asbuilt.subtitle")}</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={load} disabled={loading}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} data-testid="asbuilt-new" disabled={projectId == null}>
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
              <div
                key={s.key}
                className={cn(
                  "relative overflow-hidden rounded-xl p-4 text-white shadow-md",
                  `bg-gradient-to-br ${s.gradient}`,
                )}
                data-testid={`asbuilt-stat-${s.key}`}
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium uppercase tracking-wider opacity-90">
                    {s.label}
                  </span>
                  <Icon className="h-5 w-5 opacity-80" />
                </div>
                <div className="mt-2 text-3xl font-bold">{s.value}</div>
                {subLabel && (
                  <div className="mt-1 text-[10px] opacity-90">{subLabel}</div>
                )}
              </div>
            );
          })}
        </div>

        <div className="rounded-lg border bg-card p-3 md:p-4">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-4 [&>div]:min-w-0">
            <div>
              <Label>{t("asbuilt.field.project")}</Label>
              <SearchableSelect
                options={[
                  { value: "", label: t("asbuilt.filter.project.all") },
                  ...projects.map((p) => ({ value: String(p.id), label: p.name })),
                ]}
                value={projectId != null ? String(projectId) : ""}
                onChange={(v) => {
                  setProjectId(v ? Number(v) : undefined);
                  setPage(1);
                }}
                placeholder={t("asbuilt.filter.project.all")}
              />
            </div>
            <div>
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
                  {ASBUILT_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>
                      {t(`asbuilt.category.${c.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
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
            </div>
            <div>
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
            </div>
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
          <PageLoading label={t("asbuilt.loading")} />
        ) : error ? (
          <PageError message={error} onRetry={load} />
        ) : rows.length === 0 ? (
          <div className="rounded-lg border bg-card p-8 text-center text-sm text-muted-foreground">
            {t("asbuilt.empty")}
          </div>
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
                    <th className="w-10 px-3 py-2"></th>
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
                          {t(`asbuilt.category.${r.category.toLowerCase()}`)}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-xs text-muted-foreground">{r.designProjectName}</td>
                      <td className="px-3 py-2">
                        <Badge variant="outline" className={STATUS_BADGE[r.status]}>
                          {t(`asbuilt.status.${r.status.toLowerCase()}`)}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                        {canManage && DELETABLE_STATUSES.has(r.status) && (
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setPendingDelete(r)}
                            data-testid={`asbuilt-row-delete-${r.id}`}
                          >
                            <Trash2 className="h-4 w-4 text-rose-500" />
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile cards */}
            <div className="grid grid-cols-1 gap-3 md:hidden">
              {rows.map((r) => (
                <div
                  key={r.id}
                  className="rounded-lg border bg-card p-3 shadow-sm"
                  data-testid={`asbuilt-card-${r.id}`}
                  {...rowClickable(r)}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="font-mono text-xs text-muted-foreground">{r.documentCode}</div>
                      <div className="truncate font-medium">{r.title}</div>
                      <div className="mt-1 text-xs text-muted-foreground">{r.designProjectName}</div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <Badge variant="outline" className={STATUS_BADGE[r.status]}>
                        {t(`asbuilt.status.${r.status.toLowerCase()}`)}
                      </Badge>
                      <Badge variant="outline" className={CATEGORY_BADGE}>
                        {t(`asbuilt.category.${r.category.toLowerCase()}`)}
                      </Badge>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <div>{t("common.pagination.total").replace("{total}", String(total))}</div>
              <div className="flex gap-1">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  {t("common.pagination.prev")}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page * pageSize >= total}
                  onClick={() => setPage(page + 1)}
                >
                  {t("common.pagination.next")}
                </Button>
              </div>
            </div>
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
                  <span className="mr-2 font-mono text-xs text-muted-foreground">{detail.documentCode}</span>
                  {detail.title}
                </SheetTitle>
                <SheetDescription>{detail.designProjectName}</SheetDescription>
              </SheetHeader>
              <div className="mt-4 space-y-4 text-sm">
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline" className={STATUS_BADGE[detail.status]}>
                    {t(`asbuilt.status.${detail.status.toLowerCase()}`)}
                  </Badge>
                  <Badge variant="outline" className={CATEGORY_BADGE}>
                    {t(`asbuilt.category.${detail.category.toLowerCase()}`)}
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
                      <dd>
                        <a
                          href={detail.fileUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="break-all text-primary underline"
                        >
                          {detail.fileUrl}
                        </a>
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
                  <dt className="text-muted-foreground">Created</dt>
                  <dd>{formatDate(detail.createdAt)}</dd>
                </dl>

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
                    {canManage && DELETABLE_STATUSES.has(detail.status) && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-rose-600"
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
            <DialogDescription>
              {projects.find((p) => p.id === projectId)?.name}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t("asbuilt.field.title")}</Label>
              <Input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                data-testid="asbuilt-form-title"
              />
            </div>
            <div>
              <Label>{t("asbuilt.field.category")}</Label>
              <Select
                value={form.category}
                onValueChange={(v) => setForm({ ...form, category: v as AsBuiltCategory })}
              >
                <SelectTrigger data-testid="asbuilt-form-category">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ASBUILT_CATEGORIES.map((c) => (
                    <SelectItem key={c} value={c}>
                      {t(`asbuilt.category.${c.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t("asbuilt.field.fileUrl")}</Label>
              <Input
                value={form.fileUrl}
                onChange={(e) => setForm({ ...form, fileUrl: e.target.value })}
                placeholder="/files/asbuilt/…"
              />
            </div>
            <div>
              <Label>{t("asbuilt.field.description")}</Label>
              <Textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>
            <div>
              <Label>{t("asbuilt.field.note")}</Label>
              <Input
                value={form.note}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
              />
            </div>
            {formError && <div className="text-sm text-rose-600">{formError}</div>}
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
      <AlertDialog open={!!pendingTransition} onOpenChange={(o) => !o && setPendingTransition(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("asbuilt.confirm.transition.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("asbuilt.confirm.transition.body")
                .replace("{title}", pendingTransition?.title ?? "")
                .replace(
                  "{status}",
                  pendingTransition ? t(`asbuilt.status.${pendingTransition.next.toLowerCase()}`) : "",
                )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div>
            <Textarea
              rows={2}
              placeholder={t("asbuilt.confirm.reasonPlaceholder")}
              value={transitionNote}
              onChange={(e) => setTransitionNote(e.target.value)}
              data-testid="asbuilt-transition-note"
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("asbuilt.action.close")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleTransitionConfirm} data-testid="asbuilt-action-confirm">
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
    </AdminLayout>
  );
}
