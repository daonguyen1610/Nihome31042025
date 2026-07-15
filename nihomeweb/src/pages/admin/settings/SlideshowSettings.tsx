import { useCallback, useEffect, useMemo, useState } from "react";
import { Pencil, Plus, Save, Trash2, Upload } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, slugify, type SlideshowAdminResponse, type UpsertSlideshowRequest } from "@/services/adminApi";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useBulkSelection } from "@/hooks/useBulkSelection";

type MediaKind = "image" | "video";

type Draft = UpsertSlideshowRequest & {
  id?: number;
  mediaKind: MediaKind;
};

const isVideoUrl = (url: string) => /\.(mp4|webm|mov|m4v)(\?|#|$)/i.test(url);

const getMediaKind = (url: string): MediaKind => (isVideoUrl(url) ? "video" : "image");

const emptyDraft: Draft = {
  slug: "",
  imageUrl: "",
  title: "",
  subtitle: "",
  linkUrl: "",
  linkText: "",
  isActive: true,
  sortOrder: 0,
  mediaKind: "image",
};

const mapSlideToDraft = (slide: SlideshowAdminResponse): Draft => ({
  id: slide.id,
  slug: slide.slug,
  imageUrl: slide.imageUrl,
  title: slide.title,
  subtitle: slide.subtitle ?? "",
  linkUrl: slide.linkUrl ?? "",
  linkText: slide.linkText ?? "",
  isActive: slide.isActive,
  sortOrder: slide.sortOrder,
  mediaKind: getMediaKind(slide.imageUrl),
});

const getErrorMessage = (error: unknown, fallback: string) => {
  if (typeof error === "object" && error !== null) {
    const withMessage = error as { message?: unknown; response?: { data?: { detail?: unknown; message?: unknown } } };
    if (typeof withMessage.response?.data?.detail === "string") {
      return withMessage.response.data.detail;
    }
    if (typeof withMessage.response?.data?.message === "string") {
      return withMessage.response.data.message;
    }
    if (typeof withMessage.message === "string") {
      return withMessage.message;
    }
  }

  return fallback;
};

const SlideshowSettings = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [slides, setSlides] = useState<SlideshowAdminResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [draft, setDraft] = useState<Draft>(emptyDraft);

  const loadSlides = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getSlideshow("vi", false);
      const sorted = [...res.data].sort((a, b) => a.sortOrder - b.sortOrder);
      setSlides(sorted);
    } catch (error) {
      setError(getErrorMessage(error, t("set.slideshow.loadError")));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadSlides();
  }, [loadSlides]);

  const titleHint = useMemo(() => {
    return draft.id ? t("set.slideshow.editHint") : t("set.slideshow.createHint");
  }, [draft.id, t]);

  const resetDraft = () => setDraft(emptyDraft);

  const editSlide = (slide: SlideshowAdminResponse) => setDraft(mapSlideToDraft(slide));

  const handleUploadFile = async (file: File) => {
    const looksLikeVideo = file.type.startsWith("video/") || isVideoUrl(file.name);
    setUploading(true);
    try {
      if (looksLikeVideo) {
        const res = await adminApi.uploadVideo(file, draft.id ? draft.imageUrl : undefined, "slideshow");
        setDraft((prev) => ({ ...prev, imageUrl: res.data.mediaUrl, mediaKind: "video" }));
      } else {
        const res = await adminApi.uploadImage(file, draft.id ? draft.imageUrl : undefined, "slideshow");
        setDraft((prev) => ({ ...prev, imageUrl: res.data.imageUrl, mediaKind: "image" }));
      }
      toast({ title: t("form.updated"), description: file.name });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setUploading(false);
    }
  };

  const onPickFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    await handleUploadFile(file);
  };

  const saveSlide = async () => {
    if (!draft.title.trim()) {
      toast({ title: t("form.required"), description: t("set.slideshow.fieldTitle"), variant: "destructive" });
      return;
    }
    if (!draft.imageUrl.trim()) {
      toast({ title: t("form.required"), description: t("set.slideshow.fieldMediaUrl"), variant: "destructive" });
      return;
    }

    const payload: UpsertSlideshowRequest = {
      slug: draft.slug.trim() || slugify(draft.title),
      imageUrl: draft.imageUrl.trim(),
      title: draft.title.trim(),
      subtitle: draft.subtitle?.trim() || undefined,
      linkUrl: draft.linkUrl?.trim() || undefined,
      linkText: draft.linkText?.trim() || undefined,
      isActive: draft.isActive,
      sortOrder: Number.isFinite(draft.sortOrder) ? draft.sortOrder : 0,
    };

    setSaving(true);
    try {
      if (draft.id) {
        await adminApi.updateSlideshow(draft.id, payload);
      } else {
        await adminApi.createSlideshow(payload);
      }
      toast({ title: draft.id ? t("form.updated") : t("form.created") });
      resetDraft();
      await loadSlides();
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  };

  const removeSlide = async (slide: SlideshowAdminResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteSlideshow(slide.id);
      toast({ title: t("form.deleted") });
      if (draft.id === slide.id) {
        resetDraft();
      }
      await loadSlides();
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  const visibleIds = useMemo(() => slides.map((s) => s.id), [slides]);
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
    deleteOne: (id) => adminApi.deleteSlideshow(id),
    onAfter: async ({ success }) => {
      if (success > 0 && draft.id && selectedIds.has(draft.id)) resetDraft();
      await loadSlides();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [clearSelection]);

  if (loading) return <PageLoading />;
  if (error) return <PageError message={error} onRetry={loadSlides} />;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-5">
        <section className="rounded-lg border bg-card p-6 xl:col-span-3">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold">{t("set.slideshow.title")}</h2>
              <p className="mt-1 text-xs text-muted-foreground">
                {t("set.slideshow.desc")}
              </p>
            </div>
            <Button type="button" size="sm" variant="outline" onClick={resetDraft}>
              <Plus className="mr-1.5 h-3.5 w-3.5" />
              {t("common.new")}
            </Button>
          </div>

          {slides.length === 0 ? (
            <PageEmpty message={t("common.noData")} />
          ) : (
            <>
              <BulkActionBar
                selectedCount={selectedIds.size}
                bulkDeleting={bulkDeleting}
                onClear={clearSelection}
                onBulkDelete={() => void handleBulkDelete()}
              />
              <div className="mb-2 flex items-center gap-2 px-1 text-xs text-muted-foreground">
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
              <div className="space-y-3">
                {slides.map((slide) => (
                  <div
                    key={slide.id}
                    className="flex flex-col gap-3 rounded-lg border bg-background p-3 sm:flex-row sm:items-center"
                  >
                    <div onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        checked={selectedIds.has(slide.id)}
                        onCheckedChange={(v) => toggleOne(slide.id, v === true)}
                        aria-label={`${t("common.selectAll")} · ${slide.title}`}
                      />
                    </div>
                    <div className="aspect-video w-full shrink-0 overflow-hidden rounded-lg bg-muted sm:w-32">
                      {isVideoUrl(slide.imageUrl) ? (
                        <video
                          src={slide.imageUrl}
                          className="h-full w-full object-cover"
                          muted
                          playsInline
                          preload="metadata"
                        />
                      ) : (
                        <img
                          src={slide.imageUrl}
                          alt={slide.title}
                          className="h-full w-full object-cover"
                          onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
                        />
                      )}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{slide.title}</p>
                      <p className="mt-0.5 truncate text-xs text-muted-foreground">
                        slug: {slide.slug} · sort: {slide.sortOrder} · {slide.isActive ? t("set.slideshow.statusActive") : t("set.slideshow.statusInactive")}
                      </p>
                    </div>
                    <div className="flex items-center gap-1">
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        onClick={() => editSlide(slide)}
                      >
                        <Pencil className="mr-1 h-3.5 w-3.5" />
                        {t("common.edit")}
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="text-destructive hover:text-destructive"
                        onClick={() => removeSlide(slide)}
                      >
                        <Trash2 className="mr-1 h-3.5 w-3.5" />
                        {t("common.delete")}
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </section>

        <section className="rounded-lg border bg-card p-6 xl:col-span-2">
          <h2 className="text-lg font-semibold">{titleHint}</h2>
          <p className="mb-5 mt-1 text-xs text-muted-foreground">
            {t("set.slideshow.editorDesc")}
          </p>

          <div className="space-y-4">
            <FieldRow label={`${t("set.slideshow.fieldTitle")} *`}>
              <Input
                value={draft.title}
                onChange={(e) => setDraft((prev) => ({ ...prev, title: e.target.value }))}
                placeholder={t("set.slideshow.placeholderTitle")}
              />
            </FieldRow>

            <FieldRow label={t("set.slideshow.fieldSlug")}>
              <Input
                value={draft.slug}
                onChange={(e) => setDraft((prev) => ({ ...prev, slug: e.target.value }))}
                placeholder={t("set.slideshow.placeholderSlug")}
              />
            </FieldRow>

            <div className="grid grid-cols-2 gap-3">
              <FieldRow label={t("set.slideshow.fieldMediaType")}>
                <select
                  className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                  value={draft.mediaKind}
                  onChange={(e) => setDraft((prev) => ({ ...prev, mediaKind: e.target.value as MediaKind }))}
                >
                  <option value="image">{t("set.slideshow.mediaTypeImage")}</option>
                  <option value="video">{t("set.slideshow.mediaTypeVideo")}</option>
                </select>
              </FieldRow>
              <FieldRow label={t("set.slideshow.fieldSortOrder")}>
                <Input
                  type="number"
                  value={draft.sortOrder}
                  onChange={(e) => setDraft((prev) => ({ ...prev, sortOrder: Number(e.target.value) || 0 }))}
                />
              </FieldRow>
            </div>

            <FieldRow label={t("set.slideshow.fieldSubtitle")}>
              <Input
                value={draft.subtitle ?? ""}
                onChange={(e) => setDraft((prev) => ({ ...prev, subtitle: e.target.value }))}
                placeholder={t("set.slideshow.placeholderSubtitle")}
              />
            </FieldRow>

            <div className="grid grid-cols-2 gap-3">
              <FieldRow label={t("set.slideshow.fieldLinkText")}>
                <Input
                  value={draft.linkText ?? ""}
                  onChange={(e) => setDraft((prev) => ({ ...prev, linkText: e.target.value }))}
                  placeholder={t("set.slideshow.placeholderLinkText")}
                />
              </FieldRow>
              <FieldRow label={t("set.slideshow.fieldLinkUrl")}>
                <Input
                  value={draft.linkUrl ?? ""}
                  onChange={(e) => setDraft((prev) => ({ ...prev, linkUrl: e.target.value }))}
                  placeholder={t("set.slideshow.placeholderLinkUrl")}
                />
              </FieldRow>
            </div>

            <label className="inline-flex items-center gap-2 text-sm font-medium">
              <Checkbox
                checked={draft.isActive}
                onCheckedChange={(v) => setDraft((prev) => ({ ...prev, isActive: v === true }))}
              />
              {t("set.slideshow.fieldIsActive")}
            </label>

            <div className="space-y-3">
              <label className="inline-flex w-full cursor-pointer items-center justify-center gap-2 rounded-md border border-input bg-background px-4 py-2.5 text-sm font-medium shadow-sm transition hover:bg-accent hover:text-accent-foreground">
                <Upload className="h-4 w-4" />
                {uploading ? t("set.slideshow.uploading") : t("set.slideshow.uploadButton")}
                <input
                  type="file"
                  className="hidden"
                  disabled={uploading}
                  accept="image/*,video/mp4,video/webm,video/quicktime,video/x-m4v"
                  onChange={onPickFile}
                />
              </label>

              <details>
                <summary className="cursor-pointer select-none text-xs text-muted-foreground hover:text-foreground">
                  {t("media.url.toggleWithVideo")}
                </summary>
                <Input
                  className="mt-2"
                  value={draft.imageUrl}
                  onChange={(e) =>
                    setDraft((prev) => ({
                      ...prev,
                      imageUrl: e.target.value,
                      mediaKind: getMediaKind(e.target.value),
                    }))}
                  placeholder={t("set.slideshow.placeholderMediaUrl")}
                />
              </details>

              {draft.imageUrl && (
                <div className="aspect-video overflow-hidden rounded-lg bg-muted">
                  {draft.mediaKind === "video" ? (
                    <video
                      src={draft.imageUrl}
                      className="h-full w-full object-cover"
                      controls
                      preload="metadata"
                    />
                  ) : (
                    <img
                      src={draft.imageUrl}
                      alt={draft.title || t("set.slideshow.previewAlt")}
                      className="h-full w-full object-cover"
                      onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
                    />
                  )}
                </div>
              )}
            </div>

            <Button
              type="button"
              disabled={saving || uploading}
              onClick={saveSlide}
              className="w-full"
            >
              <Save className="mr-1.5 h-4 w-4" />
              {draft.id ? t("form.update") : t("form.create")}
            </Button>
          </div>
        </section>
      </div>
    </div>
  );
};

const FieldRow = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="space-y-1.5">
    <Label className="text-xs">{label}</Label>
    {children}
  </div>
);

export default SlideshowSettings;
