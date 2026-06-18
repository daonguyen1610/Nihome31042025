import { useCallback, useEffect, useMemo, useState } from "react";
import { Pencil, Plus, Save, Trash2, Upload } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, slugify, type SlideshowAdminResponse, type UpsertSlideshowRequest } from "@/services/adminApi";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";

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
      // Always load base Vietnamese content in editor to avoid overriding base fields with translated values.
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
        const res = await adminApi.uploadVideo(file, draft.id ? draft.imageUrl : undefined);
        setDraft((prev) => ({ ...prev, imageUrl: res.data.mediaUrl, mediaKind: "video" }));
      } else {
        const res = await adminApi.uploadImage(file, draft.id ? draft.imageUrl : undefined);
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

  if (loading) return <PageLoading />;
  if (error) return <PageError message={error} onRetry={loadSlides} />;

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 xl:grid-cols-5 gap-5">
        <div className="xl:col-span-3 admin-card p-6">
          <div className="flex items-center justify-between gap-3 mb-4">
            <div>
              <h2 className="font-display text-lg font-extrabold">{t("set.slideshow.title")}</h2>
              <p className="text-xs mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                {t("set.slideshow.desc")}
              </p>
            </div>
            <button
              type="button"
              onClick={resetDraft}
              className="inline-flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-bold border"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            >
              <Plus className="w-3.5 h-3.5" />
              {t("common.new")}
            </button>
          </div>

          {slides.length === 0 ? (
            <PageEmpty message={t("common.noData")} />
          ) : (
            <div className="space-y-3">
              {slides.map((slide) => (
                <div
                  key={slide.id}
                  className="rounded-xl border p-3 flex flex-col sm:flex-row gap-3 sm:items-center"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <div className="w-full sm:w-32 aspect-video rounded-lg overflow-hidden bg-muted shrink-0">
                    {isVideoUrl(slide.imageUrl) ? (
                      <video
                        src={slide.imageUrl}
                        className="w-full h-full object-cover"
                        muted
                        playsInline
                        preload="metadata"
                      />
                    ) : (
                      <img
                        src={slide.imageUrl}
                        alt={slide.title}
                        className="w-full h-full object-cover"
                        onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
                      />
                    )}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-bold truncate">{slide.title}</p>
                    <p className="text-xs mt-0.5 truncate" style={{ color: "hsl(var(--admin-muted))" }}>
                      slug: {slide.slug} · sort: {slide.sortOrder} · {slide.isActive ? t("set.slideshow.statusActive") : t("set.slideshow.statusInactive")}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => editSlide(slide)}
                      className="inline-flex items-center gap-1.5 px-2.5 py-2 text-xs font-bold rounded-lg hover:bg-muted"
                    >
                      <Pencil className="w-3.5 h-3.5" />
                      {t("common.edit")}
                    </button>
                    <button
                      type="button"
                      onClick={() => removeSlide(slide)}
                      className="inline-flex items-center gap-1.5 px-2.5 py-2 text-xs font-bold rounded-lg hover:bg-muted"
                      style={{ color: "hsl(var(--admin-danger))" }}
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                      {t("common.delete")}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="xl:col-span-2 admin-card p-6">
          <h2 className="font-display text-lg font-extrabold mb-1">{titleHint}</h2>
          <p className="text-xs mb-5" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("set.slideshow.editorDesc")}
          </p>

          <div className="space-y-4">
            <Field label={`${t("set.slideshow.fieldTitle")} *`}>
              <input
                className="admin-input"
                value={draft.title}
                onChange={(e) => setDraft((prev) => ({ ...prev, title: e.target.value }))}
                placeholder={t("set.slideshow.placeholderTitle")}
              />
            </Field>

            <Field label={t("set.slideshow.fieldSlug")}>
              <input
                className="admin-input"
                value={draft.slug}
                onChange={(e) => setDraft((prev) => ({ ...prev, slug: e.target.value }))}
                placeholder={t("set.slideshow.placeholderSlug")}
              />
            </Field>

            <div className="grid grid-cols-2 gap-3">
              <Field label={t("set.slideshow.fieldMediaType")}>
                <select
                  className="admin-input"
                  value={draft.mediaKind}
                  onChange={(e) => setDraft((prev) => ({ ...prev, mediaKind: e.target.value as MediaKind }))}
                >
                  <option value="image">{t("set.slideshow.mediaTypeImage")}</option>
                  <option value="video">{t("set.slideshow.mediaTypeVideo")}</option>
                </select>
              </Field>
              <Field label={t("set.slideshow.fieldSortOrder")}>
                <input
                  type="number"
                  className="admin-input"
                  value={draft.sortOrder}
                  onChange={(e) => setDraft((prev) => ({ ...prev, sortOrder: Number(e.target.value) || 0 }))}
                />
              </Field>
            </div>

            <Field label={t("set.slideshow.fieldSubtitle")}>
              <input
                className="admin-input"
                value={draft.subtitle ?? ""}
                onChange={(e) => setDraft((prev) => ({ ...prev, subtitle: e.target.value }))}
                placeholder={t("set.slideshow.placeholderSubtitle")}
              />
            </Field>

            <div className="grid grid-cols-2 gap-3">
              <Field label={t("set.slideshow.fieldLinkText")}>
                <input
                  className="admin-input"
                  value={draft.linkText ?? ""}
                  onChange={(e) => setDraft((prev) => ({ ...prev, linkText: e.target.value }))}
                  placeholder={t("set.slideshow.placeholderLinkText")}
                />
              </Field>
              <Field label={t("set.slideshow.fieldLinkUrl")}>
                <input
                  className="admin-input"
                  value={draft.linkUrl ?? ""}
                  onChange={(e) => setDraft((prev) => ({ ...prev, linkUrl: e.target.value }))}
                  placeholder={t("set.slideshow.placeholderLinkUrl")}
                />
              </Field>
            </div>

            <label className="inline-flex items-center gap-2 text-sm font-semibold">
              <input
                type="checkbox"
                checked={draft.isActive}
                onChange={(e) => setDraft((prev) => ({ ...prev, isActive: e.target.checked }))}
              />
              {t("set.slideshow.fieldIsActive")}
            </label>

            <div className="space-y-3">
              <label
                className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-sm font-bold border cursor-pointer hover:bg-muted"
                style={{ borderColor: "hsl(var(--admin-border))" }}
              >
                <Upload className="w-4 h-4" />
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
                <summary className="text-xs cursor-pointer text-muted-foreground hover:text-foreground select-none">
                  {t("media.url.toggleWithVideo")}
                </summary>
                <input
                  className="admin-input mt-2"
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
                <div className="rounded-xl overflow-hidden bg-muted aspect-video">
                  {draft.mediaKind === "video" ? (
                    <video
                      src={draft.imageUrl}
                      className="w-full h-full object-cover"
                      controls
                      preload="metadata"
                    />
                  ) : (
                    <img
                      src={draft.imageUrl}
                      alt={draft.title || t("set.slideshow.previewAlt")}
                      className="w-full h-full object-cover"
                      onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")}
                    />
                  )}
                </div>
              )}
            </div>

            <button
              type="button"
              disabled={saving || uploading}
              onClick={saveSlide}
              className="admin-btn-primary w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 text-sm disabled:opacity-50"
            >
              <Save className="w-4 h-4" />
              {draft.id ? t("form.update") : t("form.create")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

const Field = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <label className="block">
    <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

export default SlideshowSettings;
