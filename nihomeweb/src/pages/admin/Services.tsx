import { useEffect, useMemo, useRef, useState } from "react";
import { Plus, Search, Pencil, Trash2, ExternalLink, Loader2, Save, X, ImagePlus } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useServices } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import type { ServiceResponse } from "@/services/contentApi";
import { adminApi, slugify, type UpsertServiceAdminRequest } from "@/services/adminApi";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

// ─── Types ───────────────────────────────────────────────────────

type SectionDraft = { heading: string; body: string[] };
type IntroBlockDraft = { text: string; imageUrl: string; uploading: boolean };

type FormState = {
  id: number | null;
  slug: string;
  title: string;
  shortTitle: string;
  tagline: string;
  intro: string;
  highlights: string[];
  sections: SectionDraft[];
  introBlocks: IntroBlockDraft[];
  sortOrder: number;
};

const emptyForm: FormState = {
  id: null,
  slug: "",
  title: "",
  shortTitle: "",
  tagline: "",
  intro: "",
  highlights: [],
  sections: [],
  introBlocks: [],
  sortOrder: 0,
};

// ─── Helpers ─────────────────────────────────────────────────────

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

// ─── SectionsEditor ──────────────────────────────────────────────

function SectionsEditor({
  form,
  setForm,
  t,
}: {
  form: FormState;
  setForm: React.Dispatch<React.SetStateAction<FormState>>;
  t: (key: string) => string;
}) {
  const addSection = () => {
    setForm((f) => ({
      ...f,
      sections: [...f.sections, { heading: "", body: [""] }],
    }));
  };

  const removeSection = (si: number) => {
    setForm((f) => ({ ...f, sections: f.sections.filter((_, i) => i !== si) }));
  };

  const updateSectionHeading = (si: number, heading: string) => {
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) => (i === si ? { ...s, heading } : s)),
    }));
  };

  const addBullet = (si: number) => {
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) =>
        i === si ? { ...s, body: [...s.body, ""] } : s
      ),
    }));
  };

  const removeBullet = (si: number, bi: number) => {
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) =>
        i === si ? { ...s, body: s.body.filter((_, j) => j !== bi) } : s
      ),
    }));
  };

  const updateBullet = (si: number, bi: number, value: string) => {
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) =>
        i === si
          ? { ...s, body: s.body.map((b, j) => (j === bi ? value : b)) }
          : s
      ),
    }));
  };

  return (
    <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
        <span className="w-1 h-4 rounded-full bg-rose-500" />
        {t("svc.admin.sections")}
        <span className="ml-auto text-slate-400 font-normal normal-case tracking-normal text-xs">
          {form.sections.length}
        </span>
      </h3>

      <div className="space-y-4">
        {form.sections.map((sec, si) => (
          <div
            key={si}
            className="rounded-xl border border-slate-200 bg-slate-50 p-4"
          >
            {/* Section header row */}
            <div className="flex items-center gap-2 mb-3">
              <span className="w-6 h-6 rounded-full bg-rose-100 text-rose-600 text-xs font-bold flex items-center justify-center shrink-0">
                {si + 1}
              </span>
              <input
                value={sec.heading}
                onChange={(e) => updateSectionHeading(si, e.target.value)}
                placeholder={t("svc.admin.sectionHeadingPh")}
                className="flex-1 px-3 py-2 rounded-lg border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
              />
              <button
                type="button"
                onClick={() => removeSection(si)}
                className="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition shrink-0"
                aria-label={t("common.delete")}
              >
                <Trash2 className="w-4 h-4" />
              </button>
            </div>

            {/* Bullets */}
            <div className="ml-8 space-y-2">
              {sec.body.map((bullet, bi) => (
                <div key={bi} className="flex items-center gap-2">
                  <span className="w-1.5 h-1.5 rounded-full bg-rose-400 shrink-0" />
                  <input
                    value={bullet}
                    onChange={(e) => updateBullet(si, bi, e.target.value)}
                    placeholder={t("svc.admin.bulletPh")}
                    className="flex-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                  />
                  <button
                    type="button"
                    onClick={() => removeBullet(si, bi)}
                    className="w-7 h-7 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition shrink-0"
                    aria-label={t("common.delete")}
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}

              <button
                type="button"
                onClick={() => addBullet(si)}
                className="w-full flex items-center justify-center gap-1.5 py-1.5 rounded-lg border border-dashed border-slate-300 text-xs font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/40 transition"
              >
                {t("svc.admin.addBullet")}
              </button>
            </div>
          </div>
        ))}

        <button
          type="button"
          onClick={addSection}
          className="w-full flex items-center justify-center gap-2 py-3 rounded-xl border-2 border-dashed border-slate-200 text-sm font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/40 transition"
        >
          {t("svc.admin.addSection")}
        </button>
      </div>
    </section>
  );
}

// ─── IntroBlocksEditor ───────────────────────────────────────────

function IntroBlocksEditor({
  form,
  setForm,
  t,
}: {
  form: FormState;
  setForm: React.Dispatch<React.SetStateAction<FormState>>;
  t: (key: string) => string;
}) {
  const fileInputRefs = useRef<(HTMLInputElement | null)[]>([]);

  const addBlock = () => {
    setForm((f) => ({
      ...f,
      introBlocks: [...f.introBlocks, { text: "", imageUrl: "", uploading: false }],
    }));
  };

  const removeBlock = (i: number) => {
    setForm((f) => ({ ...f, introBlocks: f.introBlocks.filter((_, j) => j !== i) }));
  };

  const updateText = (i: number, text: string) => {
    setForm((f) => ({
      ...f,
      introBlocks: f.introBlocks.map((b, j) => (j === i ? { ...b, text } : b)),
    }));
  };

  const handleImageUpload = async (i: number, file: File) => {
    setForm((f) => ({
      ...f,
      introBlocks: f.introBlocks.map((b, j) => (j === i ? { ...b, uploading: true } : b)),
    }));
    try {
      const res = await adminApi.uploadImage(file, undefined, "services");
      setForm((f) => ({
        ...f,
        introBlocks: f.introBlocks.map((b, j) =>
          j === i ? { ...b, imageUrl: res.data.imageUrl, uploading: false } : b
        ),
      }));
    } catch {
      setForm((f) => ({
        ...f,
        introBlocks: f.introBlocks.map((b, j) => (j === i ? { ...b, uploading: false } : b)),
      }));
    }
  };

  const clearImage = (i: number) => {
    setForm((f) => ({
      ...f,
      introBlocks: f.introBlocks.map((b, j) => (j === i ? { ...b, imageUrl: "" } : b)),
    }));
    if (fileInputRefs.current[i]) fileInputRefs.current[i]!.value = "";
  };

  return (
    <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
        <span className="w-1 h-4 rounded-full bg-rose-500" />
        {t("svc.admin.introBlocks")}
        <span className="ml-auto text-slate-400 font-normal normal-case tracking-normal text-xs">
          {form.introBlocks.length}
        </span>
      </h3>

      <div className="space-y-4">
        {form.introBlocks.map((block, i) => (
          <div key={i} className="rounded-xl border border-slate-200 bg-slate-50 p-4 space-y-3">
            <div className="flex items-center gap-2">
              <span className="w-6 h-6 rounded-full bg-rose-100 text-rose-600 text-xs font-bold flex items-center justify-center shrink-0">
                {i + 1}
              </span>
              <span className="text-xs font-semibold text-slate-500 flex-1">
                {t("svc.admin.introBlock")} {i + 1}
              </span>
              <button
                type="button"
                onClick={() => removeBlock(i)}
                className="w-7 h-7 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition"
                aria-label={t("common.delete")}
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Text */}
            <textarea
              value={block.text}
              onChange={(e) => updateText(i, e.target.value)}
              placeholder={t("svc.admin.introBlockTextPh")}
              rows={3}
              className="w-full px-3 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition resize-none"
            />

            {/* Image */}
            {block.imageUrl ? (
              <div className="space-y-2">
                <div className="relative w-full max-h-48 overflow-hidden rounded-xl border border-slate-200 bg-slate-100">
                  <img
                    src={block.imageUrl}
                    alt=""
                    className="w-full object-cover max-h-48"
                    onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = "none"; }}
                  />
                  <button
                    type="button"
                    onClick={() => clearImage(i)}
                    className="absolute top-2 right-2 w-7 h-7 rounded-lg bg-white/80 backdrop-blur flex items-center justify-center text-slate-600 hover:text-rose-600 hover:bg-white shadow transition"
                    aria-label={t("common.delete")}
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
                <button
                  type="button"
                  disabled={block.uploading}
                  onClick={() => fileInputRefs.current[i]?.click()}
                  className="w-full flex items-center justify-center gap-2 py-2 rounded-xl border border-dashed border-slate-300 text-xs font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/40 disabled:opacity-50 transition"
                >
                  {block.uploading ? (
                    <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <ImagePlus className="w-3.5 h-3.5" />
                  )}
                  {block.uploading ? t("common.loading") : t("svc.admin.introBlockReplace")}
                </button>
              </div>
            ) : (
              <button
                type="button"
                disabled={block.uploading}
                onClick={() => fileInputRefs.current[i]?.click()}
                className="w-full flex items-center justify-center gap-2 py-3 rounded-xl border-2 border-dashed border-slate-200 text-xs font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/40 disabled:opacity-50 transition"
              >
                {block.uploading ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <ImagePlus className="w-4 h-4" />
                )}
                {block.uploading ? t("common.loading") : t("svc.admin.introBlockImage")}
              </button>
            )}

            <input
              ref={(el) => { fileInputRefs.current[i] = el; }}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) handleImageUpload(i, file);
              }}
            />
          </div>
        ))}

        <button
          type="button"
          onClick={addBlock}
          className="w-full flex items-center justify-center gap-2 py-3 rounded-xl border-2 border-dashed border-slate-200 text-sm font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/40 transition"
        >
          {t("svc.admin.addIntroBlock")}
        </button>
      </div>
    </section>
  );
}

// ─── AdminServices ────────────────────────────────────────────────

const AdminServices = () => {
  const { t } = useI18n();
  const { toast } = useToast();

  const [q, setQ] = useState("");
  const [openModal, setOpenModal] = useState(false);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);

  const { data, loading, error, refetch } = useServices();
  const items = useMemo(() => data ?? [], [data]);

  const filtered = useMemo(
    () =>
      items.filter(
        (s) =>
          s.title.toLowerCase().includes(q.toLowerCase()) ||
          s.slug.toLowerCase().includes(q.toLowerCase()) ||
          s.shortTitle.toLowerCase().includes(q.toLowerCase()),
      ),
    [items, q],
  );

  const isEditing = form.id != null;

  const startCreate = () => {
    setForm({ ...emptyForm, sortOrder: items.length + 1 });
    setOpenModal(true);
  };

  const startEdit = (s: ServiceResponse) => {
    setForm({
      id: s.id,
      slug: s.slug,
      title: s.title,
      shortTitle: s.shortTitle,
      tagline: s.tagline,
      intro: s.intro,
      highlights: s.highlights ?? [],
      sections: (s.sections ?? []).map((sec) => ({
        heading: sec.heading,
        body: Array.isArray(sec.body) ? sec.body : sec.body ? [sec.body] : [],
      })),
      introBlocks: (s.introBlocks ?? []).map((b) => ({
        text: b.text,
        imageUrl: b.imageUrl ?? "",
        uploading: false,
      })),
      sortOrder: s.sortOrder,
    });
    setOpenModal(true);
  };

  const remove = async (s: ServiceResponse) => {
    if (!window.confirm(`${t("svc.admin.confirmDel")}\n\n${s.title}`)) return;
    try {
      await adminApi.deleteService(s.id);
      toast({ title: t("form.deleted"), description: s.title });
      await refetch();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("svc.admin.fallbackError"),
        variant: "destructive",
      });
    }
  };

  const visibleIds = useMemo(() => filtered.map((s) => s.id), [filtered]);
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
    deleteOne: (id) => adminApi.deleteService(id),
    onAfter: async () => {
      await refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [q, clearSelection]);

  // Row / card click opens a read-only preview dialog. All fields we show
  // are already on the list row so no extra fetch is needed.
  const [previewRow, setPreviewRow] = useState<ServiceResponse | null>(null);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim() || !form.slug.trim() || !form.shortTitle.trim() || !form.tagline.trim() || !form.intro.trim()) {
      toast({ title: t("form.required"), variant: "destructive" });
      return;
    }
    const payload: UpsertServiceAdminRequest = {
      slug: form.slug.trim(),
      title: form.title.trim(),
      shortTitle: form.shortTitle.trim(),
      tagline: form.tagline.trim(),
      intro: form.intro.trim(),
      highlights: form.highlights.filter((h) => h.trim()),
      sections: form.sections
        .filter((s) => s.heading.trim())
        .map((s) => ({
          heading: s.heading.trim(),
          body: s.body.filter((b) => b.trim()).map((b) => b.trim()),
        }))
        .filter((s) => s.body.length > 0),
      introBlocks: form.introBlocks
        .filter((b) => b.text.trim() || b.imageUrl)
        .map((b) => ({ text: b.text.trim(), imageUrl: b.imageUrl || undefined })),
      sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
    };
    setSubmitting(true);
    try {
      if (isEditing && form.id != null) {
        await adminApi.updateService(form.id, payload);
        toast({ title: t("form.updated"), description: form.title.trim() });
      } else {
        await adminApi.createService(payload);
        toast({ title: t("form.created"), description: form.title.trim() });
      }
      setOpenModal(false);
      setForm(emptyForm);
      await refetch();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("svc.admin.fallbackError"),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        {/* Page header */}
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("svc.admin.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {filtered.length} / {items.length}
            </p>
          </div>
          <Button onClick={startCreate}>
            <Plus className="mr-1.5 h-4 w-4" />
            {t("svc.admin.add")}
          </Button>
        </header>

        {/* Search bar */}
        <section className="rounded-lg border bg-card p-3">
          <div className="w-full sm:max-w-sm">
            <Label className="text-xs" htmlFor="service-search">{t("common.search")}</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="service-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("svc.admin.searchPh")}
                className="h-9 pl-9"
              />
            </div>
          </div>
        </section>

        {/* Content */}
        {loading ? (
          <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            {t("common.loading")}
          </div>
        ) : error ? (
          <div className="rounded-lg border border-dashed p-10 text-center text-sm text-destructive">
            {error}
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <Search className="h-5 w-5" aria-hidden />
            </div>
            <p>{t("svc.admin.empty")}</p>
            <Button size="sm" onClick={startCreate}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("svc.admin.add")}
            </Button>
          </div>
        ) : (
          <div className="space-y-2">
            <BulkActionBar
              selectedCount={selectedIds.size}
              bulkDeleting={bulkDeleting}
              onClear={clearSelection}
              onBulkDelete={() => void handleBulkDelete()}
            />

            {/* Mobile / tablet card view (<lg). Full row is clickable so
                users can tap the card to open the preview dialog; per-row
                buttons and the checkbox stopPropagation. */}
            <ul className="grid gap-3 lg:hidden">
              {filtered.map((s) => (
                <li
                  key={s.id}
                  className="cursor-pointer rounded-lg border bg-card p-3 shadow-sm hover:bg-muted/40"
                  onClick={() => setPreviewRow(s)}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex min-w-0 items-start gap-2">
                      <span
                        onClick={(e) => e.stopPropagation()}
                        className="pt-0.5"
                      >
                        <Checkbox
                          checked={selectedIds.has(s.id)}
                          onCheckedChange={(v) => toggleOne(s.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${s.title}`}
                        />
                      </span>
                      <div className="min-w-0">
                        <h3 className="break-words text-sm font-semibold leading-tight">{s.title}</h3>
                        <p className="mt-0.5 break-all font-mono text-xs text-muted-foreground">
                          /{s.slug}
                        </p>
                      </div>
                    </div>
                    <Badge
                      variant="outline"
                      className="shrink-0 whitespace-nowrap border-indigo-200 bg-indigo-50 font-medium text-indigo-700"
                    >
                      {s.shortTitle}
                    </Badge>
                  </div>
                  {s.tagline && (
                    <p className="mt-2 line-clamp-2 text-xs text-muted-foreground">
                      {s.tagline}
                    </p>
                  )}
                  <div
                    className="mt-3 flex items-center justify-end gap-1 border-t pt-2"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <Button asChild variant="ghost" size="icon" title={t("common.view")} aria-label={t("common.view")}>
                      <a href={`/services/${s.slug}`} target="_blank" rel="noopener noreferrer">
                        <ExternalLink className="h-4 w-4" />
                      </a>
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => startEdit(s)}
                      title={t("svc.admin.editTitle")}
                      aria-label={t("svc.admin.editTitle")}
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => remove(s)}
                      title={t("common.delete")}
                      aria-label={t("common.delete")}
                      className="text-destructive hover:text-destructive"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </li>
              ))}
            </ul>

            {/* Desktop table (lg+) */}
            <div className="hidden overflow-x-auto rounded-lg border lg:block">
              <table className="min-w-[800px] w-full divide-y text-sm">
                <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="w-10 px-3 py-3 text-left">
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
                    </th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">#</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("svc.admin.shortTitle")}</th>
                    <th className="min-w-[220px] px-3 py-3 text-left font-medium">{t("form.title")}</th>
                    <th className="hidden px-3 py-3 text-left font-medium md:table-cell">{t("svc.admin.tagline")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {filtered.map((s) => (
                    <tr
                      key={s.id}
                      className="cursor-pointer hover:bg-muted/40 transition"
                      onClick={() => setPreviewRow(s)}
                    >
                      <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={selectedIds.has(s.id)}
                          onCheckedChange={(v) => toggleOne(s.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${s.title}`}
                        />
                      </td>
                      <td className="whitespace-nowrap px-3 py-3">
                        <span className="font-mono text-xs text-muted-foreground">{s.id}</span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-3">
                        <Badge variant="outline" className="whitespace-nowrap border-indigo-200 bg-indigo-50 font-medium text-indigo-700">
                          {s.shortTitle}
                        </Badge>
                      </td>
                      <td className="min-w-[220px] px-3 py-3">
                        <div className="font-medium">{s.title}</div>
                        <div className="mt-0.5 font-mono text-xs text-muted-foreground">/{s.slug}</div>
                      </td>
                      <td className="hidden max-w-xs px-3 py-3 md:table-cell">
                        <span className="block truncate text-xs text-muted-foreground">{s.tagline}</span>
                      </td>
                      <td
                        className="whitespace-nowrap px-3 py-3 text-right"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div className="inline-flex items-center gap-1">
                          <Button asChild variant="ghost" size="icon" title={t("common.view")} aria-label={t("common.view")}>
                            <a href={`/services/${s.slug}`} target="_blank" rel="noopener noreferrer">
                              <ExternalLink className="h-4 w-4" />
                            </a>
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => startEdit(s)}
                            title={t("svc.admin.editTitle")}
                            aria-label={t("svc.admin.editTitle")}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => remove(s)}
                            title={t("common.delete")}
                            aria-label={t("common.delete")}
                            className="text-destructive hover:text-destructive"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

      {/* Dialog */}
      <Dialog open={openModal} onOpenChange={(open) => { setOpenModal(open); if (!open) setForm(emptyForm); }}>
        <DialogContent className="admin-scope max-w-2xl max-h-[92vh] flex flex-col p-0 gap-0 overflow-hidden rounded-2xl border-0 shadow-2xl">
          {/* Gradient header */}
          <DialogHeader className="relative px-6 py-5 text-left space-y-1 bg-gradient-to-br from-rose-500 via-rose-500 to-orange-500 text-white">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-white/15 backdrop-blur flex items-center justify-center shrink-0">
                {isEditing ? <Pencil className="w-5 h-5" /> : <Plus className="w-5 h-5" />}
              </div>
              <div className="min-w-0">
                <DialogTitle className="text-lg sm:text-xl font-extrabold tracking-tight">
                  {isEditing ? t("svc.admin.editTitle") : t("svc.admin.createTitle")}
                </DialogTitle>
                <DialogDescription className="text-white/85 text-xs sm:text-sm mt-0.5">
                  {t("svc.admin.title")}
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
                  {t("svc.admin.basicInfo")}
                </h3>
                <div className="space-y-4">
                  {/* Title */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      {t("form.title")} <span className="text-rose-500">*</span>
                    </label>
                    <input
                      value={form.title}
                      onChange={(e) => {
                        const title = e.target.value;
                        setForm((f) => ({
                          ...f,
                          title,
                          slug: f.id == null ? slugify(title) : f.slug,
                        }));
                      }}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      required
                      autoFocus
                    />
                  </div>

                  {/* Short title */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      {t("svc.admin.shortTitle")} <span className="text-rose-500">*</span>
                    </label>
                    <input
                      value={form.shortTitle}
                      onChange={(e) => setForm((f) => ({ ...f, shortTitle: e.target.value }))}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      required
                    />
                  </div>

                  {/* Slug */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      {t("svc.admin.slug")} <span className="text-rose-500">*</span>
                    </label>
                    <input
                      value={form.slug}
                      onChange={(e) => setForm((f) => ({ ...f, slug: e.target.value }))}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 font-mono placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      required
                    />
                  </div>

                  {/* Tagline + Sort Order */}
                  <div className="grid grid-cols-3 gap-3">
                    <div className="col-span-2">
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        {t("svc.admin.tagline")} <span className="text-rose-500">*</span>
                      </label>
                      <input
                        value={form.tagline}
                        onChange={(e) => setForm((f) => ({ ...f, tagline: e.target.value }))}
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        {t("proc.fieldSortOrder")}
                      </label>
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

              {/* Intro */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("svc.admin.intro")}
                </h3>
                <textarea
                  value={form.intro}
                  onChange={(e) => setForm((f) => ({ ...f, intro: e.target.value }))}
                  rows={4}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition resize-none"
                  required
                />
              </section>

              {/* Highlights */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("svc.admin.highlights")}
                  <span className="ml-auto text-slate-400 font-normal normal-case tracking-normal text-xs">
                    {form.highlights.length}
                  </span>
                </h3>
                <div className="space-y-2">
                  {form.highlights.map((hl, i) => (
                    <div key={i} className="flex items-center gap-2">
                      <input
                        value={hl}
                        onChange={(e) => {
                          const val = e.target.value;
                          setForm((f) => ({
                            ...f,
                            highlights: f.highlights.map((h, j) => (j === i ? val : h)),
                          }));
                        }}
                        placeholder={t("svc.admin.highlightPh")}
                        className="flex-1 px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      />
                      <button
                        type="button"
                        onClick={() =>
                          setForm((f) => ({
                            ...f,
                            highlights: f.highlights.filter((_, j) => j !== i),
                          }))
                        }
                        className="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition shrink-0"
                        aria-label={t("common.delete")}
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  ))}
                  <button
                    type="button"
                    onClick={() => setForm((f) => ({ ...f, highlights: [...f.highlights, ""] }))}
                    className="w-full flex items-center justify-center gap-2 py-2.5 rounded-xl border-2 border-dashed border-slate-200 text-sm font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/40 transition"
                  >
                    {t("svc.admin.addHighlight")}
                  </button>
                </div>
              </section>

              {/* Sections editor */}
              <SectionsEditor form={form} setForm={setForm} t={t} />

              {/* Intro blocks editor */}
              <IntroBlocksEditor form={form} setForm={setForm} t={t} />
            </div>

            {/* Footer */}
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
                disabled={submitting}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl font-semibold text-sm text-white bg-gradient-to-br from-rose-500 to-orange-500 shadow-md shadow-rose-500/30 hover:shadow-lg hover:shadow-rose-500/40 hover:brightness-105 disabled:opacity-50 disabled:shadow-none transition"
              >
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {submitting ? t("common.loading") : t("common.save")}
              </button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      {/* Quick-view preview dialog. Read-only summary — full editing is
          via the pencil button which opens the heavy edit dialog above. */}
      <Dialog
        open={previewRow !== null}
        onOpenChange={(o) => !o && setPreviewRow(null)}
      >
        <DialogContent className="w-[95vw] max-w-2xl max-h-[90vh] overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle className="break-words text-base md:text-lg">
              {previewRow?.title}
            </DialogTitle>
            <DialogDescription className="break-all text-xs md:text-sm">
              /{previewRow?.slug}
            </DialogDescription>
          </DialogHeader>

          {previewRow && (
            <div className="space-y-4 text-sm">
              <div className="flex flex-wrap items-center gap-2">
                <Badge
                  variant="outline"
                  className="border-indigo-200 bg-indigo-50 font-medium text-indigo-700"
                >
                  {previewRow.shortTitle}
                </Badge>
              </div>

              {previewRow.tagline && (
                <div>
                  <div className="text-xs text-muted-foreground">
                    {t("svc.admin.tagline")}
                  </div>
                  <p className="mt-0.5">{previewRow.tagline}</p>
                </div>
              )}

              {previewRow.intro && (
                <div>
                  <div className="text-xs text-muted-foreground">
                    {t("svc.admin.intro")}
                  </div>
                  <p className="mt-0.5 whitespace-pre-wrap break-words">
                    {previewRow.intro}
                  </p>
                </div>
              )}

              {previewRow.highlights.length > 0 && (
                <div>
                  <div className="text-xs text-muted-foreground">
                    {t("svc.admin.highlights")}
                    <span className="ml-1">({previewRow.highlights.length})</span>
                  </div>
                  <ul className="mt-1 list-disc space-y-0.5 pl-5">
                    {previewRow.highlights.map((h, i) => (
                      <li key={i}>{h}</li>
                    ))}
                  </ul>
                </div>
              )}

              {previewRow.sections.length > 0 && (
                <div>
                  <div className="text-xs text-muted-foreground">
                    {t("svc.admin.sections")}
                    <span className="ml-1">({previewRow.sections.length})</span>
                  </div>
                  <ul className="mt-1 space-y-1 pl-1">
                    {previewRow.sections.map((sec, i) => (
                      <li key={i} className="font-medium">
                        {sec.heading || `#${i + 1}`}
                      </li>
                    ))}
                  </ul>
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
                  href={`/services/${previewRow.slug}`}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <ExternalLink className="mr-1.5 h-3.5 w-3.5" />
                  {t("common.view")}
                </a>
              </Button>
            )}
            {previewRow && (
              <Button
                onClick={() => {
                  const row = previewRow;
                  setPreviewRow(null);
                  startEdit(row);
                }}
                className="w-full sm:w-auto"
              >
                <Pencil className="mr-1.5 h-3.5 w-3.5" />
                {t("svc.admin.editTitle")}
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
      </div>
    </AdminLayout>
  );
};

export default AdminServices;
