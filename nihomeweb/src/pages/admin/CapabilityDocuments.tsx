import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Download,
  FileArchive,
  Loader2,
  Pencil,
  Plus,
  RefreshCcw,
  Search,
  Trash2,
  Upload,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { resolveAssetUrl } from "@/lib/url";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Textarea } from "@/components/ui/textarea";
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  adminApi,
  CAPABILITY_DOCUMENT_EXPIRY_STATES,
  type CapabilityDocumentDetailResponse,
  type CapabilityDocumentExpiryState,
  type CapabilityDocumentListParams,
  type CapabilityDocumentResponse,
  type MasterDataOption,
  type UpsertCapabilityDocumentRequest,
} from "@/services/adminApi";

const ALL_VALUE = "__all__";

const EXPIRY_BADGE_STYLES: Record<CapabilityDocumentExpiryState, string> = {
  none: "border-slate-200 bg-slate-50 text-slate-600",
  ok: "border-emerald-200 bg-emerald-50 text-emerald-700",
  warning: "border-amber-200 bg-amber-50 text-amber-800",
  critical: "border-orange-300 bg-orange-100 text-orange-800",
  expired: "border-rose-300 bg-rose-100 text-rose-800",
};

const formatBytes = (n: number) => {
  if (!Number.isFinite(n) || n <= 0) return "—";
  const units = ["B", "KB", "MB", "GB"];
  let size = n;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024;
    unit++;
  }
  return `${size.toFixed(size < 10 && unit > 0 ? 1 : 0)} ${units[unit]}`;
};

const formatDate = (iso?: string | null, lang: string = "vi") => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

const CapabilityDocuments = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.capabilityDocsManage);

  // -------- state --------
  const [rows, setRows] = useState<CapabilityDocumentResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [tagFilter, setTagFilter] = useState<string>("");
  const [expiryFilter, setExpiryFilter] = useState<CapabilityDocumentExpiryState | "">("");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");

  const [tags, setTags] = useState<MasterDataOption[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const h = window.setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 350);
    return () => window.clearTimeout(h);
  }, [searchInput]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: CapabilityDocumentListParams = { page, pageSize };
      if (tagFilter) params.tagCode = tagFilter;
      if (expiryFilter) params.expiryState = expiryFilter;
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listCapabilityDocuments(params);
      setRows(data.items);
      setTotal(data.total);
      // Drop any selection that fell off the current page.
      setSelectedIds((prev) => {
        const next = new Set<number>();
        for (const r of data.items) if (prev.has(r.id)) next.add(r.id);
        return next;
      });
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [page, tagFilter, expiryFilter, search]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("capability_document_tag");
        if (!cancelled) setTags(data);
      } catch {
        /* non-fatal — filters will just be empty */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // -------- upload flow --------
  const [uploadTag, setUploadTag] = useState<string>("");
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [dragging, setDragging] = useState(false);

  const doUpload = useCallback(
    async (files: FileList | File[]) => {
      if (!canManage) return;
      if (!uploadTag) {
        toast({ title: t("common.error"), description: t("capDocs.form.selectTagFirst"), variant: "destructive" });
        return;
      }
      const list = Array.from(files);
      if (list.length === 0) return;
      setBusy(true);
      let successCount = 0;
      for (const file of list) {
        try {
          const uploaded = await adminApi.uploadCapabilityDocument(file);
          await adminApi.createCapabilityDocument({
            name: file.name.replace(/\.[^.]+$/, ""),
            tagCode: uploadTag,
            filePath: uploaded.data.filePath,
            originalFileName: uploaded.data.originalFileName,
            fileSize: uploaded.data.fileSize,
            contentType: uploaded.data.contentType,
          });
          successCount++;
        } catch (err) {
          toast({
            title: t("common.error"),
            description: t("capDocs.form.uploadFailed").replace("{message}", extractApiError(err)),
            variant: "destructive",
          });
        }
      }
      setBusy(false);
      if (successCount > 0) {
        toast({
          title: t("capDocs.uploaded").replace("{count}", String(successCount)),
        });
        await fetchList();
      }
    },
    [canManage, uploadTag, toast, t, fetchList],
  );

  const onDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragging(false);
    if (e.dataTransfer.files?.length) void doUpload(e.dataTransfer.files);
  };

  // -------- edit dialog --------
  const [editing, setEditing] = useState<CapabilityDocumentDetailResponse | null>(null);
  const [editForm, setEditForm] = useState<UpsertCapabilityDocumentRequest | null>(null);
  const [editError, setEditError] = useState<string | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);

  const openEdit = async (id: number) => {
    try {
      const { data } = await adminApi.getCapabilityDocument(id);
      setEditing(data);
      setEditForm({
        name: data.name,
        tagCode: data.tagCode,
        issuedDate: data.issuedDate ?? null,
        expiryDate: data.expiryDate ?? null,
        description: data.description ?? "",
      });
      setEditError(null);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const submitEdit = async () => {
    if (!editing || !editForm) return;
    if (!editForm.name.trim() || !editForm.tagCode) {
      setEditError(t("capDocs.validation.missingFields"));
      return;
    }
    setSavingEdit(true);
    try {
      await adminApi.updateCapabilityDocument(editing.id, {
        name: editForm.name.trim(),
        tagCode: editForm.tagCode,
        issuedDate: editForm.issuedDate || null,
        expiryDate: editForm.expiryDate || null,
        description: editForm.description || null,
      });
      toast({ title: t("capDocs.saved") });
      setEditing(null);
      setEditForm(null);
      await fetchList();
    } catch (err) {
      setEditError(extractApiError(err));
    } finally {
      setSavingEdit(false);
    }
  };

  const replaceFileForEditing = async (file: File) => {
    if (!editing) return;
    setSavingEdit(true);
    try {
      const uploaded = await adminApi.uploadCapabilityDocument(file);
      const { data } = await adminApi.replaceCapabilityDocumentFile(editing.id, {
        filePath: uploaded.data.filePath,
        originalFileName: uploaded.data.originalFileName,
        fileSize: uploaded.data.fileSize,
        contentType: uploaded.data.contentType,
      });
      toast({ title: t("capDocs.replaced") });
      // Refresh detail so version list shows the new snapshot.
      const detail = await adminApi.getCapabilityDocument(data.id);
      setEditing(detail.data);
      await fetchList();
    } catch (err) {
      setEditError(extractApiError(err));
    } finally {
      setSavingEdit(false);
    }
  };

  // -------- delete --------
  const [deleting, setDeleting] = useState<CapabilityDocumentResponse | null>(null);
  const confirmDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await adminApi.deleteCapabilityDocument(deleting.id);
      toast({ title: t("capDocs.deleted") });
      setDeleting(null);
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  // -------- quick-view preview --------
  // The list row already contains every field we want to show, so no extra
  // fetch is needed. Full editing still happens through openEdit(), which the
  // preview footer can invoke as a shortcut.
  const [previewRow, setPreviewRow] = useState<CapabilityDocumentResponse | null>(null);

  // -------- ZIP download --------
  const downloadZip = async () => {
    if (selectedIds.size === 0) return;
    setBusy(true);
    try {
      const { data } = await adminApi.downloadCapabilityDocumentsZip(Array.from(selectedIds));
      const url = URL.createObjectURL(data as unknown as Blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `capability-documents-${Date.now()}.zip`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  // -------- helpers --------
  const toggleAll = (checked: boolean) => {
    if (!checked) return setSelectedIds(new Set());
    setSelectedIds(new Set(rows.map((r) => r.id)));
  };
  const toggleOne = (id: number, checked: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  };
  const allSelected = rows.length > 0 && rows.every((r) => selectedIds.has(r.id));

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const expiryLabel = (state: CapabilityDocumentExpiryState) => t(`capDocs.expiry.${state}`);
  const tagsById = useMemo(() => new Map(tags.map((tag) => [tag.code, tag])), [tags]);
  const localTagLabel = (code: string, fallback?: string | null) =>
    tagsById.get(code)?.name ?? fallback ?? code;

  const renderRowActions = (r: CapabilityDocumentResponse) => (
    <>
      <Button asChild size="icon" variant="ghost" title={t("capDocs.action.download")}>
        <a href={resolveAssetUrl(r.filePath)} target="_blank" rel="noreferrer" aria-label={t("capDocs.action.download")}>
          <Download className="h-4 w-4" />
        </a>
      </Button>
      {canManage && (
        <>
          <Button
            size="icon"
            variant="ghost"
            onClick={() => void openEdit(r.id)}
            title={t("capDocs.action.edit")}
            aria-label={t("capDocs.action.edit")}
          >
            <Pencil className="h-4 w-4" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            className="text-rose-600 hover:bg-rose-50 hover:text-rose-700"
            onClick={() => setDeleting(r)}
            title={t("capDocs.action.delete")}
            aria-label={t("capDocs.action.delete")}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </>
      )}
    </>
  );

  // -------- render --------
  return (
    <AdminLayout>
      <div className="space-y-4 md:space-y-6">
        {/* Header */}
        <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold tracking-tight md:text-2xl">{t("capDocs.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("capDocs.subtitle")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => void fetchList()}
              disabled={loading}
              className="flex-1 md:flex-none"
            >
              <RefreshCcw className={cn("mr-2 h-4 w-4", loading && "animate-spin")} />
              {t("common.refresh") ?? "Refresh"}
            </Button>
            {canManage && (
              <Button
                size="sm"
                onClick={() => fileInputRef.current?.click()}
                disabled={busy || !uploadTag}
                className="flex-1 md:flex-none"
              >
                <Plus className="mr-2 h-4 w-4" />
                {t("capDocs.new")}
              </Button>
            )}
            <input
              ref={fileInputRef}
              type="file"
              multiple
              accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
              className="hidden"
              onChange={(e) => {
                if (e.target.files) void doUpload(e.target.files);
                if (fileInputRef.current) fileInputRef.current.value = "";
              }}
            />
          </div>
        </header>

        {/* Upload area */}
        {canManage && (
          <div
            className={cn(
              "rounded-lg border-2 border-dashed p-3 transition-colors md:p-4",
              dragging ? "border-primary bg-primary/5" : "border-slate-200",
            )}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
          >
            <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div className="flex items-start gap-2 text-sm text-muted-foreground md:items-center">
                <Upload className="mt-0.5 h-5 w-5 shrink-0 md:mt-0" />
                <span className="text-left">{t("capDocs.form.dropHint")}</span>
              </div>
              <div className="flex items-center gap-2 md:shrink-0">
                <Label className="text-xs uppercase tracking-wide text-muted-foreground">
                  {t("capDocs.field.tag")}
                </Label>
                <Select value={uploadTag} onValueChange={setUploadTag}>
                  <SelectTrigger className="w-full min-w-[160px] md:w-[200px]">
                    <SelectValue placeholder={t("capDocs.field.tag")} />
                  </SelectTrigger>
                  <SelectContent>
                    {tags.map((tag) => (
                      <SelectItem key={tag.code} value={tag.code}>
                        {tag.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {busy && (
              <p className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                <Loader2 className="h-3 w-3 animate-spin" />
                {t("capDocs.form.uploading")}
              </p>
            )}
          </div>
        )}

        {/* Filter bar */}
        <div className="grid gap-2 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder={t("capDocs.filter.search")}
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>
          <Select
            value={tagFilter || ALL_VALUE}
            onValueChange={(v) => {
              setTagFilter(v === ALL_VALUE ? "" : v);
              setPage(1);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder={t("capDocs.filter.allTags")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_VALUE}>{t("capDocs.filter.allTags")}</SelectItem>
              {tags.map((tag) => (
                <SelectItem key={tag.code} value={tag.code}>{tag.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            value={expiryFilter || ALL_VALUE}
            onValueChange={(v) => {
              setExpiryFilter(v === ALL_VALUE ? "" : (v as CapabilityDocumentExpiryState));
              setPage(1);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder={t("capDocs.filter.allExpiry")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_VALUE}>{t("capDocs.filter.allExpiry")}</SelectItem>
              {CAPABILITY_DOCUMENT_EXPIRY_STATES.map((state) => (
                <SelectItem key={state} value={state}>{expiryLabel(state)}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Bulk actions */}
        {selectedIds.size > 0 && (
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-slate-50 px-3 py-2 text-sm">
            <span className="text-muted-foreground">
              {selectedIds.size} / {rows.length}
            </span>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" onClick={downloadZip} disabled={busy}>
                <FileArchive className="mr-2 h-4 w-4" />
                {t("capDocs.action.downloadZip")}
              </Button>
            </div>
          </div>
        )}

        {/* List */}
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
            {t("capDocs.empty")}
          </div>
        ) : (
          <>
            {/* Mobile / tablet card view (<lg) */}
            <div className="grid gap-3 lg:hidden">
              {rows.length > 1 && (
                <label className="flex items-center gap-2 rounded-md border bg-white px-3 py-2 text-sm text-muted-foreground">
                  <Checkbox checked={allSelected} onCheckedChange={(v) => toggleAll(Boolean(v))} />
                  <span>{allSelected ? t("capDocs.deselectAll") : t("capDocs.selectAll")}</span>
                </label>
              )}
              {rows.map((r) => (
                <article
                  key={r.id}
                  className="cursor-pointer rounded-lg border bg-white p-3 shadow-sm hover:bg-slate-50/70"
                  onClick={() => setPreviewRow(r)}
                >
                  <header className="flex items-start gap-2">
                    <span onClick={(e) => e.stopPropagation()} className="mt-1 shrink-0">
                      <Checkbox
                        checked={selectedIds.has(r.id)}
                        onCheckedChange={(v) => toggleOne(r.id, Boolean(v))}
                      />
                    </span>
                    <div className="min-w-0 flex-1">
                      <h3 className="break-words text-sm font-semibold leading-tight">{r.name}</h3>
                      <p className="mt-0.5 break-all text-xs text-muted-foreground">{r.originalFileName}</p>
                    </div>
                    <Badge variant="secondary" className="shrink-0 whitespace-nowrap">V{r.currentVersion}</Badge>
                  </header>

                  <div className="mt-2 flex flex-wrap items-center gap-1.5">
                    <Badge variant="outline">{localTagLabel(r.tagCode, r.tagLabel)}</Badge>
                    {r.expiryState !== "none" && r.expiryState !== "ok" && (
                      <Badge variant="outline" className={EXPIRY_BADGE_STYLES[r.expiryState]}>
                        {expiryLabel(r.expiryState)}
                      </Badge>
                    )}
                  </div>

                  <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-xs">
                    <div>
                      <dt className="text-muted-foreground">{t("capDocs.field.issuedDate")}</dt>
                      <dd className="font-medium">{formatDate(r.issuedDate, lang)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("capDocs.field.expiryDate")}</dt>
                      <dd className="font-medium">{formatDate(r.expiryDate, lang)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("capDocs.field.fileSize")}</dt>
                      <dd className="font-medium">{formatBytes(r.fileSize)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("capDocs.field.updatedAt")}</dt>
                      <dd className="font-medium">{formatDate(r.updatedAt, lang)}</dd>
                    </div>
                  </dl>

                  <footer
                    className="mt-3 flex items-center justify-end gap-1 border-t pt-2"
                    onClick={(e) => e.stopPropagation()}
                  >
                    {renderRowActions(r)}
                  </footer>
                </article>
              ))}
            </div>

            {/* Desktop table view (lg+) */}
            <div className="hidden overflow-x-auto rounded-md border lg:block">
              <table className="w-full min-w-[1000px] text-sm">
                <thead className="bg-slate-50 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="w-10 px-3 py-2">
                      <Checkbox checked={allSelected} onCheckedChange={(v) => toggleAll(Boolean(v))} />
                    </th>
                    <th className="min-w-[240px] px-3 py-2">{t("capDocs.field.name")}</th>
                    <th className="min-w-[140px] px-3 py-2">{t("capDocs.field.tag")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("capDocs.field.issuedDate")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("capDocs.field.expiryDate")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("capDocs.field.version")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("capDocs.field.fileSize")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("capDocs.field.updatedAt")}</th>
                    <th className="w-32 px-3 py-2 text-right"> </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      className="cursor-pointer border-t align-top hover:bg-slate-50/50"
                      onClick={() => setPreviewRow(r)}
                    >
                      <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                        <Checkbox checked={selectedIds.has(r.id)} onCheckedChange={(v) => toggleOne(r.id, Boolean(v))} />
                      </td>
                      <td className="min-w-[240px] px-3 py-2">
                        <div className="font-medium">{r.name}</div>
                        <div className="break-all text-xs text-muted-foreground">{r.originalFileName}</div>
                      </td>
                      <td className="px-3 py-2">
                        <Badge variant="outline" className="whitespace-nowrap">
                          {localTagLabel(r.tagCode, r.tagLabel)}
                        </Badge>
                      </td>
                      <td className="whitespace-nowrap px-3 py-2">{formatDate(r.issuedDate, lang)}</td>
                      <td className="whitespace-nowrap px-3 py-2">
                        <div>{formatDate(r.expiryDate, lang)}</div>
                        {r.expiryState !== "none" && r.expiryState !== "ok" && (
                          <Badge className={cn("mt-1", EXPIRY_BADGE_STYLES[r.expiryState])} variant="outline">
                            {expiryLabel(r.expiryState)}
                          </Badge>
                        )}
                      </td>
                      <td className="whitespace-nowrap px-3 py-2">V{r.currentVersion}</td>
                      <td className="whitespace-nowrap px-3 py-2 text-muted-foreground">{formatBytes(r.fileSize)}</td>
                      <td className="whitespace-nowrap px-3 py-2 text-xs text-muted-foreground">{formatDate(r.updatedAt, lang)}</td>
                      <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                        <div className="flex justify-end gap-1">{renderRowActions(r)}</div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}

        {/* Pagination */}
        {total > pageSize && (
          <div className="flex flex-col items-center justify-between gap-2 text-sm sm:flex-row">
            <span className="text-muted-foreground">
              {page} / {totalPages} · {total}
            </span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                ←
              </Button>
              <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                →
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Edit dialog */}
      <Dialog open={!!editing} onOpenChange={(v) => !v && (setEditing(null), setEditForm(null))}>
        <DialogContent className="max-h-[90vh] w-[95vw] max-w-2xl overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle className="break-words text-base md:text-lg">{editing?.name}</DialogTitle>
            <DialogDescription className="break-all text-xs md:text-sm">
              V{editing?.currentVersion} · {editing?.originalFileName}
            </DialogDescription>
          </DialogHeader>
          {editForm && (
            <div className="space-y-3">
              <div className="grid gap-3 md:grid-cols-2">
                <div className="space-y-1">
                  <Label>{t("capDocs.field.name")}</Label>
                  <Input value={editForm.name} onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} />
                </div>
                <div className="space-y-1">
                  <Label>{t("capDocs.field.tag")}</Label>
                  <Select value={editForm.tagCode} onValueChange={(v) => setEditForm({ ...editForm, tagCode: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {tags.map((tag) => (
                        <SelectItem key={tag.code} value={tag.code}>{tag.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label>{t("capDocs.field.issuedDate")}</Label>
                  <Input
                    type="date"
                    value={editForm.issuedDate ? editForm.issuedDate.slice(0, 10) : ""}
                    onChange={(e) => setEditForm({ ...editForm, issuedDate: e.target.value || null })}
                  />
                </div>
                <div className="space-y-1">
                  <Label>{t("capDocs.field.expiryDate")}</Label>
                  <Input
                    type="date"
                    value={editForm.expiryDate ? editForm.expiryDate.slice(0, 10) : ""}
                    onChange={(e) => setEditForm({ ...editForm, expiryDate: e.target.value || null })}
                  />
                </div>
              </div>
              <div className="space-y-1">
                <Label>{t("capDocs.field.description")}</Label>
                <Textarea
                  value={editForm.description ?? ""}
                  onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                  rows={2}
                />
              </div>

              {/* Replace file */}
              {canManage && (
                <div className="rounded-md border bg-slate-50 p-3">
                  <Label className="text-xs uppercase tracking-wide text-muted-foreground">
                    {t("capDocs.action.replace")}
                  </Label>
                  <Input
                    type="file"
                    accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg"
                    className="mt-1"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) void replaceFileForEditing(file);
                      e.target.value = "";
                    }}
                  />
                </div>
              )}

              {/* Version history */}
              <div>
                <Label className="text-xs uppercase tracking-wide text-muted-foreground">
                  {t("capDocs.versions.title")}
                </Label>
                {editing && editing.versions.length === 0 ? (
                  <p className="mt-1 text-xs text-muted-foreground">{t("capDocs.versions.empty")}</p>
                ) : (
                  <ul className="mt-1 divide-y rounded-md border">
                    {editing?.versions.map((v) => (
                      <li key={v.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                        <div className="min-w-0 flex-1">
                          <div className="break-words font-medium">V{v.versionNumber} · {v.originalFileName}</div>
                          <div className="text-xs text-muted-foreground">
                            {formatDate(v.createdAt, lang)} · {formatBytes(v.fileSize)}
                          </div>
                        </div>
                        <Button asChild size="sm" variant="ghost">
                          <a href={resolveAssetUrl(v.filePath)} target="_blank" rel="noreferrer">
                            <Download className="h-4 w-4" />
                          </a>
                        </Button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {editError && <p className="text-sm text-rose-600">{editError}</p>}
            </div>
          )}
          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button
              variant="ghost"
              onClick={() => (setEditing(null), setEditForm(null))}
              disabled={savingEdit}
              className="w-full sm:w-auto"
            >
              {t("common.cancel") ?? "Cancel"}
            </Button>
            <Button
              onClick={() => void submitEdit()}
              disabled={savingEdit || !canManage}
              className="w-full sm:w-auto"
            >
              {savingEdit && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t("common.save") ?? "Save"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirm (2-step per AC) */}
      <AlertDialog open={!!deleting} onOpenChange={(v) => !v && setDeleting(null)}>
        <AlertDialogContent className="w-[95vw] max-w-md sm:w-full">
          <AlertDialogHeader>
            <AlertDialogTitle>{t("capDocs.delete.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription className="break-words">
              {t("capDocs.delete.confirmBody").replace("{name}", deleting?.name ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <AlertDialogCancel disabled={busy} className="w-full sm:w-auto">
              {t("capDocs.delete.cancel")}
            </AlertDialogCancel>
            <AlertDialogAction
              className="w-full bg-rose-600 hover:bg-rose-700 sm:w-auto"
              onClick={(e) => {
                e.preventDefault();
                void confirmDelete();
              }}
              disabled={busy}
            >
              {t("capDocs.delete.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Quick-view preview dialog — read-only summary of the row.
          The list already contains every field the preview shows, so
          no extra API call is needed. Full editing still happens in
          the Edit dialog above. */}
      <Dialog
        open={previewRow !== null}
        onOpenChange={(o) => !o && setPreviewRow(null)}
      >
        <DialogContent className="max-h-[90vh] w-[95vw] max-w-2xl overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle className="break-words text-base md:text-lg">
              {previewRow?.name}
            </DialogTitle>
            <DialogDescription className="break-all text-xs md:text-sm">
              V{previewRow?.currentVersion} · {previewRow?.originalFileName}
            </DialogDescription>
          </DialogHeader>

          {previewRow && (
            <div className="space-y-4 text-sm">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline">
                  {localTagLabel(previewRow.tagCode, previewRow.tagLabel)}
                </Badge>
                {previewRow.expiryState !== "none" && previewRow.expiryState !== "ok" && (
                  <Badge
                    variant="outline"
                    className={EXPIRY_BADGE_STYLES[previewRow.expiryState]}
                  >
                    {expiryLabel(previewRow.expiryState)}
                  </Badge>
                )}
              </div>

              <dl className="grid grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2">
                <div>
                  <dt className="text-xs text-muted-foreground">{t("capDocs.field.issuedDate")}</dt>
                  <dd className="font-medium">{formatDate(previewRow.issuedDate, lang)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">{t("capDocs.field.expiryDate")}</dt>
                  <dd className="font-medium">{formatDate(previewRow.expiryDate, lang)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">{t("capDocs.field.fileSize")}</dt>
                  <dd className="font-medium">{formatBytes(previewRow.fileSize)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">{t("capDocs.field.version")}</dt>
                  <dd className="font-medium">
                    V{previewRow.currentVersion}
                    {previewRow.previousVersionCount > 0 && (
                      <span className="ml-1 text-xs text-muted-foreground">
                        (+{previewRow.previousVersionCount})
                      </span>
                    )}
                  </dd>
                </div>
                {previewRow.uploadedByName && (
                  <div>
                    <dt className="text-xs text-muted-foreground">{t("capDocs.field.uploadedBy")}</dt>
                    <dd className="font-medium">{previewRow.uploadedByName}</dd>
                  </div>
                )}
                <div>
                  <dt className="text-xs text-muted-foreground">{t("capDocs.field.updatedAt")}</dt>
                  <dd className="font-medium">{formatDate(previewRow.updatedAt, lang)}</dd>
                </div>
              </dl>

              {previewRow.description && (
                <div>
                  <div className="text-xs text-muted-foreground">
                    {t("capDocs.field.description")}
                  </div>
                  <p className="whitespace-pre-wrap break-words">{previewRow.description}</p>
                </div>
              )}
            </div>
          )}

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button
              variant="outline"
              onClick={() => setPreviewRow(null)}
              className="w-full sm:w-auto"
            >
              {t("common.close")}
            </Button>
            {previewRow && (
              <Button asChild variant="outline" className="w-full sm:w-auto">
                <a
                  href={resolveAssetUrl(previewRow.filePath)}
                  target="_blank"
                  rel="noreferrer"
                >
                  <Download className="mr-1.5 h-3.5 w-3.5" />
                  {t("capDocs.action.download")}
                </a>
              </Button>
            )}
            {previewRow && canManage && (
              <Button
                onClick={() => {
                  const id = previewRow.id;
                  setPreviewRow(null);
                  void openEdit(id);
                }}
                className="w-full sm:w-auto"
              >
                <Pencil className="mr-1.5 h-3.5 w-3.5" />
                {t("capDocs.action.edit")}
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default CapabilityDocuments;
