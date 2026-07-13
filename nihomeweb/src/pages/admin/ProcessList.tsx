import { useEffect, useMemo, useState, useRef } from "react";
import {
  Search as SearchIcon,
  Pencil,
  ChevronDown,
  ChevronUp,
  Image as ImageIcon,
  LayoutList,
  Grid2x2,
  FileText,
  Download,
  Plus,
  Trash2,
  Save,
  X,
  Upload,
  Loader2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useProcesses } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import type { ProcessResponse, ProcessAssetInfo } from "@/services/contentApi";
import { adminApi, type UpsertProcessRequest, type ProcessAssetInput } from "@/services/adminApi";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type Props = {
  groupKey: string;
  titleKey: string;
};

type DraftAsset = ProcessAssetInput & { fileSizeBytes: number };

type FormState = {
  id: number | null;
  code: string;
  title: string;
  sortOrder: number;
  images: DraftAsset[];
  files: DraftAsset[];
};

const emptyForm: FormState = { id: null, code: "", title: "", sortOrder: 0, images: [], files: [] };

function getErrorMessage(error: unknown): string | null {
  if (typeof error === "object" && error !== null) {
    const e = error as {
      message?: unknown;
      response?: { data?: { detail?: unknown; message?: unknown; title?: unknown; errors?: Record<string, unknown> } };
    };
    const data = e.response?.data;
    if (typeof data?.detail === "string") return data.detail;
    if (typeof data?.message === "string") return data.message;
    if (data?.errors) {
      for (const v of Object.values(data.errors)) {
        if (typeof v === "string" && v.trim()) return v;
        if (Array.isArray(v)) {
          const first = v.find((x) => typeof x === "string" && x.trim());
          if (typeof first === "string") return first;
        }
      }
    }
    if (typeof data?.title === "string") return data.title;
    if (typeof e.message === "string") return e.message;
  }
  return null;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function ImageLightbox({
  images,
  startIndex,
  onClose,
}: {
  images: ProcessAssetInfo[];
  startIndex: number;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [idx, setIdx] = useState(startIndex);
  const img = images[idx];
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "";

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/90"
      onClick={onClose}
    >
      <div
        className="relative max-w-5xl w-full mx-4 flex flex-col items-center"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          onClick={onClose}
          className="absolute -top-10 right-0 text-white/70 hover:text-white p-2"
        >
          ✕
        </button>

        <img
          src={`${apiBase}${img.url}`}
          alt={img.displayName}
          className="max-h-[80vh] max-w-full object-contain rounded-lg shadow-2xl"
        />

        <div className="mt-3 text-white/80 text-sm text-center">
          {img.displayName} ({formatBytes(img.fileSizeBytes)}) — {idx + 1} / {images.length}
        </div>

        {images.length > 1 && (
          <div className="flex gap-3 mt-4">
            <button
              disabled={idx === 0}
              onClick={() => setIdx((i) => i - 1)}
              className="px-4 py-2 rounded-lg bg-white/10 hover:bg-white/20 text-white disabled:opacity-30 text-sm font-bold"
            >
              ← {t("common.prev")}
            </button>
            <button
              disabled={idx === images.length - 1}
              onClick={() => setIdx((i) => i + 1)}
              className="px-4 py-2 rounded-lg bg-white/10 hover:bg-white/20 text-white disabled:opacity-30 text-sm font-bold"
            >
              {t("common.next")} →
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function AssetPanel({ process }: { process: ProcessResponse }) {
  const { t } = useI18n();
  const [lightbox, setLightbox] = useState<number | null>(null);
  const [stackView, setStackView] = useState(false);
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "";
  const hasImages = process.images.length > 0;
  const hasFiles = process.files.length > 0;

  if (!hasImages && !hasFiles) return null;

  return (
    <div className="mt-2 space-y-3 border-t pt-3">
      {hasImages && (
        <div>
          <div className="mb-2 flex items-center gap-2">
            <div className="flex flex-1 items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <ImageIcon className="h-3.5 w-3.5" />
              <span>{t("proc.images")} ({process.images.length})</span>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setStackView((v) => !v)}
              title={stackView ? t("proc.gridView") : t("proc.stackView")}
              className="h-7 gap-1 text-xs"
            >
              {stackView ? <Grid2x2 className="h-3.5 w-3.5" /> : <LayoutList className="h-3.5 w-3.5" />}
              {stackView ? t("proc.gridView") : t("proc.stackView")}
            </Button>
          </div>

          {stackView ? (
            <div className="flex flex-col gap-2">
              {process.images.map((img, i) => (
                <button
                  key={i}
                  onClick={() => setLightbox(i)}
                  className="group relative w-full overflow-hidden rounded-md border transition-all hover:border-primary"
                  title={img.displayName}
                >
                  <img
                    src={`${apiBase}${img.url}`}
                    alt={img.displayName}
                    className="h-auto w-full object-contain transition-opacity group-hover:opacity-90"
                    onError={(e) => {
                      (e.currentTarget as HTMLImageElement).style.display = "none";
                      (e.currentTarget.nextElementSibling as HTMLElement | null)?.classList.remove("hidden");
                    }}
                  />
                  <div className="hidden absolute inset-0 flex items-center justify-center bg-muted" style={{ minHeight: 60 }}>
                    <ImageIcon className="h-5 w-5 text-muted-foreground" />
                  </div>
                </button>
              ))}
            </div>
          ) : (
            <div className="flex flex-wrap gap-2">
              {process.images.map((img, i) => (
                <button
                  key={i}
                  onClick={() => setLightbox(i)}
                  className="group relative overflow-hidden rounded-md border transition-all hover:border-primary"
                  style={{ width: 80, height: 60 }}
                  title={img.displayName}
                >
                  <img
                    src={`${apiBase}${img.url}`}
                    alt={img.displayName}
                    className="h-full w-full object-cover transition-opacity group-hover:opacity-90"
                    onError={(e) => {
                      (e.currentTarget as HTMLImageElement).style.display = "none";
                      (e.currentTarget.nextElementSibling as HTMLElement | null)?.classList.remove("hidden");
                    }}
                  />
                  <div className="hidden absolute inset-0 flex items-center justify-center bg-muted">
                    <ImageIcon className="h-5 w-5 text-muted-foreground" />
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {hasFiles && (
        <div>
          <div className="mb-2 flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
            <FileText className="h-3.5 w-3.5" />
            <span>{t("proc.files")} ({process.files.length})</span>
          </div>
          <div className="space-y-1.5">
            {process.files.map((file, i) => (
              <a
                key={i}
                href={`${apiBase}${file.url}`}
                download={file.originalFileName}
                className="group flex items-center gap-2 rounded-md border px-3 py-2 transition hover:bg-muted/50"
                title={`${t("proc.download")}: ${file.originalFileName}`}
              >
                <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                <span className="flex-1 truncate text-sm">{file.displayName}</span>
                <span className="shrink-0 text-xs text-muted-foreground">
                  {formatBytes(file.fileSizeBytes)}
                </span>
                <Download className="h-4 w-4 shrink-0 text-primary opacity-0 transition-opacity group-hover:opacity-100" />
              </a>
            ))}
          </div>
        </div>
      )}

      {lightbox !== null && (
        <ImageLightbox
          images={process.images}
          startIndex={lightbox}
          onClose={() => setLightbox(null)}
        />
      )}
    </div>
  );
}

function ProcessCard({
  p,
  onEdit,
  onDelete,
  selected,
  onToggleSelect,
}: {
  p: ProcessResponse;
  onEdit: (p: ProcessResponse) => void;
  onDelete: (p: ProcessResponse) => void;
  selected: boolean;
  onToggleSelect: (checked: boolean) => void;
}) {
  const { t } = useI18n();
  const hasAssets = p.images.length > 0 || p.files.length > 0;
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="rounded-lg border bg-card px-5 py-4 transition hover:shadow-md">
      <div className="flex items-center gap-4">
        <div onClick={(e) => e.stopPropagation()}>
          <Checkbox
            checked={selected}
            onCheckedChange={(v) => onToggleSelect(v === true)}
            aria-label={`${t("common.selectAll")} · ${p.title}`}
          />
        </div>
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
          <Pencil className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold text-primary">
            {p.code ? `${p.code} — ${p.title}` : p.title}
          </p>
          {hasAssets && (
            <p className="mt-0.5 text-xs text-muted-foreground">
              {p.images.length > 0 && `${p.images.length} ${t("proc.images")}`}
              {p.images.length > 0 && p.files.length > 0 && " · "}
              {p.files.length > 0 && `${p.files.length} ${t("proc.files")}`}
            </p>
          )}
        </div>
        <div className="flex shrink-0 items-center gap-1">
          {hasAssets && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setExpanded((v) => !v)}
            >
              {expanded ? <ChevronUp className="mr-1 h-3.5 w-3.5" /> : <ChevronDown className="mr-1 h-3.5 w-3.5" />}
              {expanded ? t("proc.hide") : t("common.view")}
            </Button>
          )}
          <Button
            variant="ghost"
            size="icon"
            onClick={() => onEdit(p)}
            title={t("proc.editTitle")}
            aria-label={t("proc.editTitle")}
          >
            <Pencil className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => onDelete(p)}
            title={t("proc.delete")}
            aria-label={t("proc.delete")}
            className="text-destructive hover:text-destructive"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {expanded && <AssetPanel process={p} />}
    </div>
  );
}

const ProcessList = ({ groupKey, titleKey }: Props) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [q, setQ] = useState("");
  const [openModal, setOpenModal] = useState(false);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingImages, setUploadingImages] = useState(false);
  const [uploadingFiles, setUploadingFiles] = useState(false);
  const imageInputRef = useRef<HTMLInputElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "";

  const { data, loading, error, refetch } = useProcesses();
  const items = useMemo(() => (data?.[groupKey] ?? []) as ProcessResponse[], [data, groupKey]);

  const filtered = useMemo(
    () =>
      items.filter(
        (i) =>
          i.title.toLowerCase().includes(q.toLowerCase()) ||
          (i.code ?? "").toLowerCase().includes(q.toLowerCase()),
      ),
    [items, q],
  );

  const isEditing = form.id != null;

  const toDraft = (a: ProcessAssetInfo): DraftAsset => ({
    displayName: a.displayName,
    url: a.url,
    originalFileName: a.originalFileName,
    contentType: a.contentType,
    sortOrder: a.sortOrder,
    fileSizeBytes: a.fileSizeBytes,
  });

  const startCreate = () => {
    const maxSort = items.reduce((m, it) => Math.max(m, it.sortOrder ?? 0), 0);
    setForm({ ...emptyForm, sortOrder: maxSort + 1 });
    setOpenModal(true);
  };

  const startEdit = (p: ProcessResponse) => {
    setForm({
      id: p.id,
      code: p.code ?? "",
      title: p.title,
      sortOrder: p.sortOrder ?? 0,
      images: p.images.map(toDraft),
      files: p.files.map(toDraft),
    });
    setOpenModal(true);
  };

  const remove = async (p: ProcessResponse) => {
    if (!window.confirm(`${t("proc.confirmDelete")}\n\n${p.code ? `${p.code} — ` : ""}${p.title}`)) return;
    try {
      await adminApi.deleteProcess(p.id);
      toast({ title: t("form.deleted"), description: p.title });
      await refetch();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("proc.fallbackError"),
        variant: "destructive",
      });
    }
  };

  const visibleIds = useMemo(() => filtered.map((p) => p.id), [filtered]);
  const {
    selectedIds,
    bulkDeleting,
    allVisibleSelected,
    someVisibleSelected,
    toggleAllVisible,
    toggleOne,
    clearSelection,
    handleBulkDelete,
  } = useBulkSelection<number>({
    visibleIds,
    deleteOne: (id) => adminApi.deleteProcess(id),
    onAfter: async () => {
      await refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [groupKey, q, clearSelection]);

  const handleAssetUpload = async (
    files: FileList | null,
    kind: "images" | "files",
  ) => {
    if (!files || files.length === 0) return;
    const setBusy = kind === "images" ? setUploadingImages : setUploadingFiles;
    setBusy(true);
    try {
      const uploader = kind === "images" ? adminApi.uploadProcessImage : adminApi.uploadProcessFile;
      const uploaded: DraftAsset[] = [];
      for (const f of Array.from(files)) {
        const res = await uploader(f, groupKey);
        uploaded.push({
          displayName: res.data.displayName,
          url: res.data.url,
          originalFileName: res.data.originalFileName,
          contentType: res.data.contentType,
          sortOrder: 0,
          fileSizeBytes: res.data.fileSizeBytes,
        });
      }
      setForm((f) => ({ ...f, [kind]: [...f[kind], ...uploaded] }));
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("proc.fallbackError"),
        variant: "destructive",
      });
    } finally {
      setBusy(false);
    }
  };

  const removeAsset = (kind: "images" | "files", idx: number) => {
    setForm((f) => ({ ...f, [kind]: f[kind].filter((_, i) => i !== idx) }));
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) {
      toast({ title: t("form.required"), description: t("proc.requiredTitle"), variant: "destructive" });
      return;
    }
    const stripFileSize = (a: DraftAsset, i: number): ProcessAssetInput => ({
      displayName: a.displayName,
      url: a.url,
      originalFileName: a.originalFileName,
      contentType: a.contentType,
      sortOrder: i,
    });
    const payload: UpsertProcessRequest = {
      groupKey,
      code: form.code.trim() || undefined,
      title: form.title.trim(),
      sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      images: form.images.map(stripFileSize),
      files: form.files.map(stripFileSize),
    };
    setSubmitting(true);
    try {
      if (isEditing && form.id != null) {
        await adminApi.updateProcess(form.id, payload);
        toast({ title: t("form.updated"), description: form.title.trim() });
      } else {
        await adminApi.createProcess(payload);
        toast({ title: t("form.created"), description: form.title.trim() });
      }
      setOpenModal(false);
      setForm(emptyForm);
      await refetch();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("proc.fallbackError"),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t(titleKey)}</h1>
            <p className="text-xs italic text-muted-foreground">
              {filtered.length} / {items.length}
            </p>
          </div>
          <Button onClick={startCreate}>
            <Plus className="mr-1.5 h-4 w-4" />
            {t("proc.add")}
          </Button>
        </header>

        <section className="rounded-lg border bg-card p-3">
          <div className="w-full sm:max-w-sm">
            <Label className="text-xs" htmlFor="process-search">{t("common.search")}</Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="process-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("proc.searchPh")}
                className="h-9 pl-9"
              />
            </div>
          </div>
        </section>

        {loading ? (
          <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            {t("common.loading")}
          </div>
        ) : error ? (
          <div className="rounded-lg border border-dashed p-10 text-center text-sm text-destructive">
            {error}
          </div>
        ) : (
        <div className="space-y-3">
          <BulkActionBar
            selectedCount={selectedIds.size}
            bulkDeleting={bulkDeleting}
            onClear={clearSelection}
            onBulkDelete={() => void handleBulkDelete()}
          />
          {filtered.length === 0 ? (
            <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
              {t("proc.empty")}
            </div>
          ) : (
            <>
            <div className="flex items-center gap-2 px-2 text-xs text-muted-foreground">
              <Checkbox
                checked={
                  allVisibleSelected
                    ? true
                    : someVisibleSelected
                      ? "indeterminate"
                      : false
                }
                onCheckedChange={(v) => toggleAllVisible(v === true)}
                aria-label={t("common.selectAll")}
              />
              <span>{t("common.selectAll")}</span>
            </div>
            {filtered.map((p) => (
              <ProcessCard
                key={p.id}
                p={p}
                onEdit={startEdit}
                onDelete={remove}
                selected={selectedIds.has(p.id)}
                onToggleSelect={(v) => toggleOne(p.id, v)}
              />
            ))}
            </>
          )}
        </div>
      )}

      <Dialog open={openModal} onOpenChange={setOpenModal}>
        <DialogContent className="admin-scope max-w-2xl max-h-[92vh] flex flex-col p-0 gap-0 overflow-hidden rounded-2xl border-0 shadow-2xl">
          {/* Gradient header */}
          <DialogHeader className="relative px-6 py-5 text-left space-y-1 bg-gradient-to-br from-rose-500 via-rose-500 to-orange-500 text-white">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-white/15 backdrop-blur flex items-center justify-center shrink-0">
                {isEditing ? <Pencil className="w-5 h-5" /> : <Plus className="w-5 h-5" />}
              </div>
              <div className="min-w-0">
                <DialogTitle className="text-lg sm:text-xl font-extrabold tracking-tight">
                  {isEditing ? t("proc.editTitle") : t("proc.createTitle")}
                </DialogTitle>
                <DialogDescription className="text-white/85 text-xs sm:text-sm mt-0.5">
                  {t(titleKey)}
                </DialogDescription>
              </div>
            </div>
          </DialogHeader>

          <form onSubmit={save} className="flex flex-col flex-1 overflow-hidden bg-slate-50">
            <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
              {/* Basic info */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("proc.basicInfo")}
                </h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      {t("proc.fieldTitle")} <span className="text-rose-500">*</span>
                    </label>
                    <input
                      value={form.title}
                      onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                      placeholder={t("proc.titlePh")}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      required
                      autoFocus
                    />
                  </div>
                  <div className="grid grid-cols-3 gap-3">
                    <div className="col-span-2">
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">{t("proc.fieldCode")}</label>
                      <input
                        value={form.code}
                        onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
                        placeholder={t("proc.codePh")}
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                        maxLength={40}
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">{t("proc.fieldSortOrder")}</label>
                      <input
                        type="number"
                        value={form.sortOrder}
                        onChange={(e) => setForm((f) => ({ ...f, sortOrder: Number(e.target.value) }))}
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      />
                    </div>
                  </div>
                </div>
              </section>

              {/* Assets */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("proc.assets")}
                </h3>

                {/* Images */}
                <div className="mb-5">
                  <div className="flex items-center justify-between mb-2">
                    <label className="text-sm font-semibold text-slate-700 flex items-center gap-1.5">
                      <ImageIcon className="w-4 h-4 text-rose-500" />
                      {t("proc.images")}
                      <span className="text-xs font-bold text-slate-400 ml-1">({form.images.length})</span>
                    </label>
                    {form.images.length > 0 && (
                      <button
                        type="button"
                        onClick={() => imageInputRef.current?.click()}
                        disabled={uploadingImages}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 hover:border-rose-300 disabled:opacity-50 transition"
                      >
                        {uploadingImages ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                        {uploadingImages ? t("proc.uploading") : t("proc.addImages")}
                      </button>
                    )}
                    <input
                      ref={imageInputRef}
                      type="file"
                      accept="image/*"
                      multiple
                      className="hidden"
                      onChange={(e) => {
                        handleAssetUpload(e.target.files, "images");
                        e.target.value = "";
                      }}
                    />
                  </div>
                  {form.images.length === 0 ? (
                    <button
                      type="button"
                      onClick={() => !uploadingImages && imageInputRef.current?.click()}
                      disabled={uploadingImages}
                      className="w-full flex flex-col items-center justify-center gap-2 py-7 rounded-xl border-2 border-dashed border-slate-200 bg-slate-50/50 hover:bg-rose-50/40 hover:border-rose-300 transition group disabled:opacity-50"
                    >
                      <div className="w-10 h-10 rounded-full bg-rose-100 text-rose-500 flex items-center justify-center group-hover:scale-110 transition">
                        {uploadingImages ? <Loader2 className="w-5 h-5 animate-spin" /> : <Upload className="w-5 h-5" />}
                      </div>
                      <div className="text-sm font-semibold text-slate-700">
                        {uploadingImages ? t("proc.uploading") : t("proc.addImages")}
                      </div>
                      <div className="text-xs text-slate-500">{t("proc.uploadHint")}</div>
                    </button>
                  ) : (
                    <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
                      {form.images.map((img, i) => (
                        <div key={i} className="relative group rounded-xl overflow-hidden border border-slate-200 bg-white">
                          <img
                            src={`${apiBase}${img.url}`}
                            alt={img.displayName}
                            className="w-full h-24 object-cover"
                          />
                          <button
                            type="button"
                            onClick={() => removeAsset("images", i)}
                            className="absolute top-1.5 right-1.5 w-6 h-6 rounded-full flex items-center justify-center bg-black/70 text-white opacity-0 group-hover:opacity-100 transition-opacity hover:bg-rose-600"
                            title={t("proc.removeAsset")}
                            aria-label={t("proc.removeAsset")}
                          >
                            <X className="w-3.5 h-3.5" />
                          </button>
                          <div className="px-2 py-1 text-[10px] truncate text-slate-600 bg-slate-50 border-t border-slate-100" title={img.displayName}>
                            {img.displayName}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Files */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="text-sm font-semibold text-slate-700 flex items-center gap-1.5">
                      <FileText className="w-4 h-4 text-rose-500" />
                      {t("proc.files")}
                      <span className="text-xs font-bold text-slate-400 ml-1">({form.files.length})</span>
                    </label>
                    {form.files.length > 0 && (
                      <button
                        type="button"
                        onClick={() => fileInputRef.current?.click()}
                        disabled={uploadingFiles}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 hover:border-rose-300 disabled:opacity-50 transition"
                      >
                        {uploadingFiles ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                        {uploadingFiles ? t("proc.uploading") : t("proc.addFiles")}
                      </button>
                    )}
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.zip,.rar,.txt"
                      multiple
                      className="hidden"
                      onChange={(e) => {
                        handleAssetUpload(e.target.files, "files");
                        e.target.value = "";
                      }}
                    />
                  </div>
                  {form.files.length === 0 ? (
                    <button
                      type="button"
                      onClick={() => !uploadingFiles && fileInputRef.current?.click()}
                      disabled={uploadingFiles}
                      className="w-full flex flex-col items-center justify-center gap-2 py-7 rounded-xl border-2 border-dashed border-slate-200 bg-slate-50/50 hover:bg-rose-50/40 hover:border-rose-300 transition group disabled:opacity-50"
                    >
                      <div className="w-10 h-10 rounded-full bg-rose-100 text-rose-500 flex items-center justify-center group-hover:scale-110 transition">
                        {uploadingFiles ? <Loader2 className="w-5 h-5 animate-spin" /> : <Upload className="w-5 h-5" />}
                      </div>
                      <div className="text-sm font-semibold text-slate-700">
                        {uploadingFiles ? t("proc.uploading") : t("proc.addFiles")}
                      </div>
                      <div className="text-xs text-slate-500">{t("proc.uploadHint")}</div>
                    </button>
                  ) : (
                    <div className="space-y-1.5">
                      {form.files.map((file, i) => (
                        <div key={i} className="flex items-center gap-3 px-3 py-2.5 rounded-xl border border-slate-200 bg-white hover:border-rose-200 transition">
                          <div className="w-9 h-9 rounded-lg bg-rose-50 text-rose-500 flex items-center justify-center shrink-0">
                            <FileText className="w-4 h-4" />
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="text-sm font-semibold text-slate-800 truncate" title={file.originalFileName}>
                              {file.displayName}
                            </div>
                            <div className="text-xs text-slate-500">{formatBytes(file.fileSizeBytes)}</div>
                          </div>
                          <button
                            type="button"
                            onClick={() => removeAsset("files", i)}
                            className="shrink-0 w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition"
                            title={t("proc.removeAsset")}
                            aria-label={t("proc.removeAsset")}
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </section>
            </div>

            <div className="px-6 py-4 border-t border-slate-200 bg-white flex items-center justify-end gap-2">
              <button
                type="button"
                onClick={() => setOpenModal(false)}
                disabled={submitting}
                className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-xl font-semibold text-sm border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
              >
                <X className="w-4 h-4" />
                {t("common.cancel")}
              </button>
              <button
                type="submit"
                disabled={submitting || uploadingImages || uploadingFiles}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl font-semibold text-sm text-white bg-gradient-to-br from-rose-500 to-orange-500 shadow-md shadow-rose-500/30 hover:shadow-lg hover:shadow-rose-500/40 hover:brightness-105 disabled:opacity-50 disabled:shadow-none transition"
              >
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {submitting ? t("common.loading") : t("common.save")}
              </button>
            </div>
          </form>
        </DialogContent>
      </Dialog>
      </div>
    </AdminLayout>
  );
};

export default ProcessList;
