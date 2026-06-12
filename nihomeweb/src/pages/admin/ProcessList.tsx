import { useMemo, useState } from "react";
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
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useProcesses } from "@/hooks/useContentApi";
import type { ProcessResponse, ProcessAssetInfo } from "@/services/contentApi";
import { adminApi, type UpsertProcessRequest } from "@/services/adminApi";
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

type FormState = {
  id: number | null;
  code: string;
  title: string;
  sortOrder: number;
};

const emptyForm: FormState = { id: null, code: "", title: "", sortOrder: 0 };

function getErrorMessage(error: unknown): string | null {
  if (typeof error === "object" && error !== null) {
    const e = error as {
      message?: unknown;
      response?: { data?: { message?: unknown; title?: unknown; errors?: Record<string, unknown> } };
    };
    const data = e.response?.data;
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
    <div className="border-t mt-2 pt-3 space-y-3">
      {hasImages && (
        <div>
          <div className="flex items-center gap-2 mb-2">
            <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wide flex-1" style={{ color: "hsl(var(--admin-muted))" }}>
              <ImageIcon className="w-3.5 h-3.5" />
              <span>{t("proc.images")} ({process.images.length})</span>
            </div>
            <button
              onClick={() => setStackView((v) => !v)}
              className="inline-flex items-center gap-1 text-xs font-bold px-2.5 py-1 rounded-lg border border-border hover:bg-muted transition"
              style={{ color: "hsl(var(--admin-muted))" }}
              title={stackView ? t("proc.gridView") : t("proc.stackView")}
            >
              {stackView ? <Grid2x2 className="w-3.5 h-3.5" /> : <LayoutList className="w-3.5 h-3.5" />}
              {stackView ? t("proc.gridView") : t("proc.stackView")}
            </button>
          </div>

          {stackView ? (
            <div className="flex flex-col gap-2">
              {process.images.map((img, i) => (
                <button
                  key={i}
                  onClick={() => setLightbox(i)}
                  className="relative group rounded-lg overflow-hidden border border-border hover:border-primary transition-all w-full"
                  title={img.displayName}
                >
                  <img
                    src={`${apiBase}${img.url}`}
                    alt={img.displayName}
                    className="w-full h-auto object-contain group-hover:opacity-90 transition-opacity"
                    onError={(e) => {
                      (e.currentTarget as HTMLImageElement).style.display = "none";
                      (e.currentTarget.nextElementSibling as HTMLElement | null)?.classList.remove("hidden");
                    }}
                  />
                  <div className="hidden absolute inset-0 flex items-center justify-center bg-muted" style={{ minHeight: 60 }}>
                    <ImageIcon className="w-5 h-5" style={{ color: "hsl(var(--admin-muted))" }} />
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
                  className="relative group rounded-lg overflow-hidden border border-border hover:border-primary transition-all"
                  style={{ width: 80, height: 60 }}
                  title={img.displayName}
                >
                  <img
                    src={`${apiBase}${img.url}`}
                    alt={img.displayName}
                    className="w-full h-full object-cover group-hover:opacity-90 transition-opacity"
                    onError={(e) => {
                      (e.currentTarget as HTMLImageElement).style.display = "none";
                      (e.currentTarget.nextElementSibling as HTMLElement | null)?.classList.remove("hidden");
                    }}
                  />
                  <div className="hidden absolute inset-0 flex items-center justify-center bg-muted">
                    <ImageIcon className="w-5 h-5" style={{ color: "hsl(var(--admin-muted))" }} />
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {hasFiles && (
        <div>
          <div className="flex items-center gap-1.5 mb-2 text-xs font-bold uppercase tracking-wide" style={{ color: "hsl(var(--admin-muted))" }}>
            <FileText className="w-3.5 h-3.5" />
            <span>{t("proc.files")} ({process.files.length})</span>
          </div>
          <div className="space-y-1.5">
            {process.files.map((file, i) => (
              <a
                key={i}
                href={`${apiBase}${file.url}`}
                download={file.originalFileName}
                className="flex items-center gap-2 px-3 py-2 rounded-lg border border-border hover:bg-muted/50 transition group"
                title={`${t("proc.download")}: ${file.originalFileName}`}
              >
                <FileText className="w-4 h-4 shrink-0" style={{ color: "hsl(var(--admin-muted))" }} />
                <span className="text-sm truncate flex-1">{file.displayName}</span>
                <span className="text-xs shrink-0" style={{ color: "hsl(var(--admin-muted))" }}>
                  {formatBytes(file.fileSizeBytes)}
                </span>
                <Download className="w-4 h-4 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity" style={{ color: "hsl(var(--admin-primary))" }} />
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
}: {
  p: ProcessResponse;
  onEdit: (p: ProcessResponse) => void;
  onDelete: (p: ProcessResponse) => void;
}) {
  const { t } = useI18n();
  const hasAssets = p.images.length > 0 || p.files.length > 0;
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="admin-card px-5 py-4 hover:shadow-md transition">
      <div className="flex items-center gap-4">
        <div
          className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
          style={{
            background:
              "linear-gradient(135deg, hsl(var(--admin-primary) / 0.12), hsl(22 95% 58% / 0.1))",
            color: "hsl(var(--admin-primary))",
          }}
        >
          <Pencil className="w-5 h-5" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="font-semibold truncate" style={{ color: "hsl(var(--admin-primary))" }}>
            {p.code ? `${p.code} — ${p.title}` : p.title}
          </p>
          {hasAssets && (
            <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
              {p.images.length > 0 && `${p.images.length} ${t("proc.images")}`}
              {p.images.length > 0 && p.files.length > 0 && " · "}
              {p.files.length > 0 && `${p.files.length} ${t("proc.files")}`}
            </p>
          )}
        </div>
        <div className="flex items-center gap-1 shrink-0">
          {hasAssets && (
            <button
              onClick={() => setExpanded((v) => !v)}
              className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
              style={{ color: "hsl(var(--admin-muted))" }}
            >
              {expanded ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
              {expanded ? t("proc.hide") : t("common.view")}
            </button>
          )}
          <button
            onClick={() => onEdit(p)}
            className="inline-flex items-center justify-center w-8 h-8 rounded-lg hover:bg-muted"
            style={{ color: "hsl(var(--admin-muted))" }}
            title={t("proc.editTitle")}
            aria-label={t("proc.editTitle")}
          >
            <Pencil className="w-4 h-4" />
          </button>
          <button
            onClick={() => onDelete(p)}
            className="inline-flex items-center justify-center w-8 h-8 rounded-lg hover:bg-muted"
            style={{ color: "hsl(var(--admin-danger))" }}
            title={t("proc.delete")}
            aria-label={t("proc.delete")}
          >
            <Trash2 className="w-4 h-4" />
          </button>
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

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) {
      toast({ title: t("form.required"), description: t("proc.requiredTitle"), variant: "destructive" });
      return;
    }
    const payload: UpsertProcessRequest = {
      groupKey,
      code: form.code.trim() || undefined,
      title: form.title.trim(),
      sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
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
      <div className="mb-6 flex items-start gap-3">
        <div className="flex-1">
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {t(titleKey)}
          </h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button
          onClick={startCreate}
          className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg font-bold text-sm shrink-0"
          style={{
            background: "hsl(var(--admin-primary))",
            color: "white",
          }}
        >
          <Plus className="w-4 h-4" />
          {t("proc.add")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="flex items-center gap-2 max-w-md">
          <SearchIcon className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t("proc.searchPh")}
            className="admin-input flex-1"
          />
        </div>
      </div>

      {loading ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("common.loading")}
        </div>
      ) : error ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-danger))" }}>
          {error}
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.length === 0 ? (
            <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("proc.empty")}
            </div>
          ) : (
            filtered.map((p) => (
              <ProcessCard key={p.id} p={p} onEdit={startEdit} onDelete={remove} />
            ))
          )}
        </div>
      )}

      <Dialog open={openModal} onOpenChange={setOpenModal}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{isEditing ? t("proc.editTitle") : t("proc.createTitle")}</DialogTitle>
            <DialogDescription>{t(titleKey)}</DialogDescription>
          </DialogHeader>
          <form onSubmit={save} className="space-y-4">
            <div>
              <label className="block text-sm font-bold mb-1.5">{t("proc.fieldCode")}</label>
              <input
                value={form.code}
                onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
                placeholder={t("proc.codePh")}
                className="admin-input w-full"
                maxLength={40}
              />
            </div>
            <div>
              <label className="block text-sm font-bold mb-1.5">
                {t("proc.fieldTitle")} <span style={{ color: "hsl(var(--admin-danger))" }}>*</span>
              </label>
              <input
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                placeholder={t("proc.titlePh")}
                className="admin-input w-full"
                required
              />
            </div>
            <div>
              <label className="block text-sm font-bold mb-1.5">{t("proc.fieldSortOrder")}</label>
              <input
                type="number"
                value={form.sortOrder}
                onChange={(e) => setForm((f) => ({ ...f, sortOrder: Number(e.target.value) }))}
                className="admin-input w-32"
              />
            </div>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setOpenModal(false)}
                disabled={submitting}
                className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg font-bold text-sm border border-border hover:bg-muted disabled:opacity-50"
              >
                <X className="w-4 h-4" />
                {t("common.cancel")}
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg font-bold text-sm disabled:opacity-50"
                style={{ background: "hsl(var(--admin-primary))", color: "white" }}
              >
                <Save className="w-4 h-4" />
                {submitting ? t("common.loading") : t("common.save")}
              </button>
            </div>
          </form>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default ProcessList;
