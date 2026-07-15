import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { ArrowLeft, Save, Plus, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, slugify } from "@/services/adminApi";
import type { UpsertProjectRequest } from "@/services/adminApi";
import { useProject, useProjectCategories } from "@/hooks/useContentApi";
import { localizedName } from "@/lib/category";
import { PageLoading, PageError } from "@/components/PageState";
import GalleryEditor from "@/components/admin/GalleryEditor";
import FeaturedImageUploader from "@/components/admin/FeaturedImageUploader";
import ContentBlockEditor from "@/components/admin/ContentBlockEditor";
import type { ContentItem } from "@/services/contentApi";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";

interface FormData {
  id: number;
  slug: string;
  imageUrl: string;
  gallery: string[];
  name: string;
  client: string;
  location: string;
  scale: string;
  scope: string;
  status: string;
  year: string;
  category: string;
  categoryId: number | null;
  description: string;
  challenges: string[];
  solutions: string[];
  highlights: { label: string; value: string }[];
  content: ContentItem[];
}

const empty: FormData = {
  id: 0,
  slug: "",
  imageUrl: "",
  gallery: [],
  name: "",
  client: "",
  location: "",
  scale: "",
  scope: "",
  status: "ongoing",
  year: "",
  category: "",
  categoryId: null,
  description: "",
  challenges: [],
  solutions: [],
  highlights: [],
  content: [],
};

const ProjectForm = ({ mode }: { mode: "create" | "edit" }) => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { data: existing, loading, error, refetch } = useProject(mode === "edit" ? (slug ?? "") : "");
  const { data: categories } = useProjectCategories(true);
  const [data, setData] = useState<FormData>(empty);
  const [initialized, setInitialized] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [pendingImageFile, setPendingImageFile] = useState<File | null>(null);
  const [pendingImagePreview, setPendingImagePreview] = useState<string | null>(null);

  const categoryOptions = useMemo(() => {
    return (categories ?? [])
      .map((item) => ({ id: item.id, name: item.name, label: localizedName(item, lang) }))
      .sort((a, b) => a.label.localeCompare(b.label, lang));
  }, [categories, lang]);

  if (mode === "edit" && existing && !initialized) {
    setData({
      id: existing.id,
      slug: existing.slug,
      imageUrl: existing.imageUrl,
      gallery: existing.gallery ?? [],
      name: existing.name,
      client: existing.client,
      location: existing.location,
      scale: existing.scale,
      scope: existing.scope,
      status: existing.status,
      year: existing.year ?? "",
      category: existing.category ?? "",
      categoryId: existing.categoryId ?? null,
      description: existing.description ?? "",
      challenges: existing.challenges ?? [],
      solutions: existing.solutions ?? [],
      highlights: existing.highlights ?? [],
      content: existing.content ?? [],
    });
    setInitialized(true);
  }

  const update = <K extends keyof FormData>(key: K, value: FormData[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  useEffect(() => {
    if (!pendingImageFile) {
      setPendingImagePreview(null);
      return;
    }
    const objectUrl = URL.createObjectURL(pendingImageFile);
    setPendingImagePreview(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [pendingImageFile]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.name.trim()) {
      toast({ title: t("form.required"), description: t("proj.field.name"), variant: "destructive" });
      return;
    }
    let imageUrl = data.imageUrl || "/placeholder.svg";

    setSubmitting(true);
    try {
      if (pendingImageFile) {
        setUploadingImage(true);
        const folder = `projects/${data.slug || slugify(data.name)}`;
        const upload = await adminApi.uploadImage(
          pendingImageFile,
          mode === "edit" ? data.imageUrl : undefined,
          folder,
        );
        imageUrl = upload.data.imageUrl;
      }

      const payload: UpsertProjectRequest = {
        slug: data.slug || slugify(data.name),
        imageUrl,
        gallery: data.gallery.length ? data.gallery : undefined,
        name: data.name,
        client: data.client,
        location: data.location,
        scale: data.scale,
        scope: data.scope,
        status: data.status,
        year: data.year || undefined,
        category: data.category || undefined,
        categoryId: data.categoryId,
        description: data.description || undefined,
        challenges: data.challenges.length ? data.challenges : undefined,
        solutions: data.solutions.length ? data.solutions : undefined,
        highlights: data.highlights.length
          ? data.highlights.filter((item) => item.label.trim() || item.value.trim())
          : undefined,
        content: data.content.length ? data.content : undefined,
      };
      if (mode === "create") {
        await adminApi.createProject(payload);
      } else {
        await adminApi.updateProject(data.id, payload);
      }
      toast({ title: mode === "create" ? t("form.created") : t("form.updated"), description: data.name });
      navigate("/admin/projects");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setUploadingImage(false);
      setSubmitting(false);
    }
  };

  if (mode === "edit" && loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (mode === "edit" && error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex items-center gap-3">
          <Button asChild variant="outline" size="icon" className="rounded-full shrink-0">
            <Link to="/admin/projects">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight lg:text-3xl">
              {mode === "create" ? t("proj.addTitle") : t("proj.editTitle")}
            </h1>
            <p className="text-sm text-muted-foreground">
              {mode === "edit" && data.slug}
            </p>
          </div>
        </header>

        <form onSubmit={handleSubmit} className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2 space-y-5">

          {/* ── Thông tin cơ bản ── */}
          <div className="rounded-lg border bg-card p-6">
            <h2 className="font-bold mb-4">{t("form.basicInfo")}</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Field label={t("proj.field.name") + " *"} className="md:col-span-2">
                <input
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.name}
                  onChange={(e) => update("name", e.target.value)}
                  placeholder="Dự án nhà máy ABC..."
                  required
                />
              </Field>
              <Field label={t("proj.field.client")}>
                <input
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.client}
                  onChange={(e) => update("client", e.target.value)}
                  placeholder="Tên chủ đầu tư"
                />
              </Field>
              <Field label={t("proj.field.location")}>
                <input
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.location}
                  onChange={(e) => update("location", e.target.value)}
                  placeholder="Tỉnh / thành phố"
                />
              </Field>
              <Field label={t("proj.scale")}>
                <input
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.scale}
                  onChange={(e) => update("scale", e.target.value)}
                  placeholder="15.000 m²"
                />
              </Field>
              <Field label={t("proj.field.year")}>
                <input
                  type="number"
                  min="1990"
                  max="2100"
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.year ?? ""}
                  onChange={(e) => update("year", e.target.value)}
                  placeholder={String(new Date().getFullYear())}
                />
              </Field>
              <Field label={t("proj.field.category")}>
                <select
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.categoryId != null ? String(data.categoryId) : ""}
                  onChange={(e) => {
                    const v = e.target.value;
                    const found = categoryOptions.find((opt) => String(opt.id) === v);
                    setData((d) => ({ ...d, categoryId: found?.id ?? null, category: found?.name ?? d.category }));
                  }}
                >
                  <option value="">-- Chọn danh mục --</option>
                  {[
                    ...categoryOptions,
                    ...(data.categoryId == null && data.category && !categoryOptions.some((opt) => opt.name === data.category)
                      ? [{ id: -1, name: data.category, label: data.category }]
                      : []),
                  ].map((opt) => (
                    <option key={opt.id} value={opt.id}>{opt.label}</option>
                  ))}
                </select>
              </Field>
              <Field label={t("proj.field.status")}>
                <select
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.status}
                  onChange={(e) => update("status", e.target.value)}
                >
                  <option value="ongoing">{t("proj.ongoing")}</option>
                  <option value="completed">{t("proj.completed")}</option>
                </select>
              </Field>
              <Field label={t("proj.field.scope")} className="md:col-span-2">
                <input
                  className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  value={data.scope}
                  onChange={(e) => update("scope", e.target.value)}
                  placeholder="Thiết kế, thi công, hoàn thiện..."
                />
              </Field>
            </div>
          </div>

          {/* ── Mô tả ── */}
          <div className="rounded-lg border bg-card p-6">
            <h2 className="font-bold mb-4">{t("form.content")}</h2>
            <Field label={t("proj.field.description")}>
              <textarea
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 min-h-24"
                value={data.description ?? ""}
                onChange={(e) => update("description", e.target.value)}
                placeholder="Mô tả tổng quan về dự án..."
              />
            </Field>
          </div>

          {/* ── Rich content blocks ── */}
          <div className="rounded-lg border bg-card p-6">
            <h2 className="font-bold mb-4">{t("form.content")}</h2>
            <ContentBlockEditor
              value={data.content}
              onChange={(items) => update("content", items)}
              folder={`projects/${data.slug || slugify(data.name)}`}
            />
          </div>

          {/* ── Thách thức & Giải pháp ── */}
          <div className="rounded-lg border bg-card p-6">
            <h2 className="font-bold mb-4">{t("proj.field.challenges")} & {t("proj.field.solutions")}</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <ListEditor
                label={t("proj.field.challenges")}
                items={data.challenges}
                onChange={(items) => update("challenges", items)}
                placeholder="Thêm thách thức..."
              />
              <ListEditor
                label={t("proj.field.solutions")}
                items={data.solutions}
                onChange={(items) => update("solutions", items)}
                placeholder="Thêm giải pháp..."
              />
            </div>
          </div>

          <div className="rounded-lg border bg-card p-6">
            <h2 className="font-bold mb-4">{t("projDetail.highlights")}</h2>
            <HighlightsEditor
              items={data.highlights}
              onChange={(items) => update("highlights", items)}
            />
          </div>
        </div>

        {/* ── Sidebar ── */}
        <div className="space-y-5">
          <div className="rounded-lg border bg-card p-6">
            <h2 className="font-bold mb-4">{t("form.media")}</h2>
            <Field label={t("proj.field.image") + " *"}>
              <FeaturedImageUploader
                imageUrl={data.imageUrl}
                pendingPreview={pendingImagePreview}
                pendingFileName={pendingImageFile?.name}
                onUrlChange={(url) => update("imageUrl", url)}
                onFileSelected={(file) => {
                  setPendingImageFile(file);
                  toast({ title: t("form.updated"), description: file.name });
                }}
                onClearPending={() => setPendingImageFile(null)}
                disabled={uploadingImage}
              />
            </Field>
          </div>

          <div className="rounded-lg border bg-card p-6">
            <h2 className="mb-1 text-base font-semibold">{t("media.gallery.title")}</h2>
            <p className="mb-4 text-xs text-muted-foreground">
              {t("media.gallery.descProject")}
            </p>
            <GalleryEditor items={data.gallery} onChange={(items) => update("gallery", items)} folder={`projects/${data.slug || slugify(data.name)}`} />
          </div>

          <Button
            type="submit"
            disabled={submitting || uploadingImage}
            className="w-full"
          >
            <Save className="mr-1.5 h-4 w-4" />
            {mode === "create" ? t("form.create") : t("form.update")}
          </Button>
        </div>
      </form>
      </div>
    </AdminLayout>
  );
};

const Field = ({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) => (
  <div className={["space-y-1.5", className].filter(Boolean).join(" ")}>
    <Label className="text-xs">{label}</Label>
    {children}
  </div>
);

const ListEditor = ({
  label,
  items,
  onChange,
  placeholder,
}: {
  label: string;
  items: string[];
  onChange: (items: string[]) => void;
  placeholder?: string;
}) => {
  const addItem = () => onChange([...items, ""]);
  const updateItem = (i: number, val: string) => {
    const next = [...items];
    next[i] = val;
    onChange(next);
  };
  const removeItem = (i: number) => onChange(items.filter((_, idx) => idx !== i));

  return (
    <div>
      <span className="mb-2 block text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {label}
      </span>
      <div className="space-y-2">
        {items.map((item, i) => (
          <div key={i} className="flex gap-2">
            <input
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 flex-1"
              value={item}
              onChange={(e) => updateItem(i, e.target.value)}
              placeholder={placeholder}
            />
            <button
              type="button"
              onClick={() => removeItem(i)}
              className="flex h-9 w-9 items-center justify-center rounded-md border text-destructive transition hover:bg-destructive/10"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        ))}
        <button
          type="button"
          onClick={addItem}
          className="flex w-full items-center justify-center gap-1.5 rounded-md border border-dashed py-2 text-sm text-muted-foreground transition hover:bg-muted"
        >
          <Plus className="h-3.5 w-3.5" />
          {placeholder ? `+ ${placeholder}` : "+ Thêm"}
        </button>
      </div>
    </div>
  );
};

const HighlightsEditor = ({
  items,
  onChange,
}: {
  items: { label: string; value: string }[];
  onChange: (items: { label: string; value: string }[]) => void;
}) => {
  const addItem = () => onChange([...items, { label: "", value: "" }]);
  const updateItem = (index: number, key: "label" | "value", value: string) => {
    const next = [...items];
    next[index] = { ...next[index], [key]: value };
    onChange(next);
  };
  const removeItem = (index: number) => onChange(items.filter((_, idx) => idx !== index));

  return (
    <div className="space-y-2">
      {items.map((item, index) => (
        <div key={index} className="grid grid-cols-1 md:grid-cols-[1fr_1fr_auto] gap-2 items-center">
          <input
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
            value={item.label}
            onChange={(e) => updateItem(index, "label", e.target.value)}
            placeholder="Nhãn (ví dụ: Diện tích)"
          />
          <input
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
            value={item.value}
            onChange={(e) => updateItem(index, "value", e.target.value)}
            placeholder="Giá trị (ví dụ: 250.000 m²)"
          />
          <button
            type="button"
            onClick={() => removeItem(index)}
            className="flex h-9 w-9 items-center justify-center rounded-md border text-destructive transition hover:bg-destructive/10"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </button>
        </div>
      ))}
      <button
        type="button"
        onClick={addItem}
        className="flex w-full items-center justify-center gap-1.5 rounded-md border border-dashed py-2 text-sm text-muted-foreground transition hover:bg-muted"
      >
        <Plus className="h-3.5 w-3.5" />
        + Thêm điểm nổi bật
      </button>
    </div>
  );
};

export default ProjectForm;
