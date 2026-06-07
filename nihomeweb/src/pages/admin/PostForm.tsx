import { useEffect, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { ArrowLeft, Save } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, slugify } from "@/services/adminApi";
import type { UpsertActivityRequest } from "@/services/adminApi";
import { useActivity, useActivityCategories } from "@/hooks/useContentApi";
import { PageLoading, PageError } from "@/components/PageState";
import GalleryEditor from "@/components/admin/GalleryEditor";
import FeaturedImageUploader from "@/components/admin/FeaturedImageUploader";

interface FormData {
  id: number;
  slug: string;
  date: string;
  imageUrl: string;
  gallery: string[];
  category: string;
  title: string;
  excerpt: string;
  content: string[];
  author: string;
}

const toInputDate = (value: string) => {
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;

  const match = value.match(/^(\d{1,2})\.(\d{1,2})\.(\d{4})$/);
  if (!match) return new Date().toISOString().slice(0, 10);

  const day = match[1].padStart(2, "0");
  const month = match[2].padStart(2, "0");
  const year = match[3];
  return `${year}-${month}-${day}`;
};

const toApiDate = (value: string) => {
  const parts = value.split("-");
  if (parts.length !== 3) return value;
  const [year, month, day] = parts;
  return `${day}.${month}.${year}`;
};

const toContentLines = (value: string) => value.replace(/\r\n/g, "\n").split("\n");

const empty: FormData = {
  id: 0,
  slug: "",
  date: new Date().toISOString().slice(0, 10),
  imageUrl: "",
  gallery: [],
  category: "",
  title: "",
  excerpt: "",
  content: [],
  author: "",
};

const PostForm = ({ mode }: { mode: "create" | "edit" }) => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: existing, loading, error, refetch } = useActivity(mode === "edit" ? (slug ?? "") : "");
  const { data: categories } = useActivityCategories();
  const [data, setData] = useState<FormData>(empty);
  const [initialized, setInitialized] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [pendingImageFile, setPendingImageFile] = useState<File | null>(null);
  const [pendingImagePreview, setPendingImagePreview] = useState<string | null>(null);

  // Sync fetched data into form state once
  if (mode === "edit" && existing && !initialized) {
    setData({
      id: existing.id,
      slug: existing.slug,
      date: toInputDate(existing.date),
      imageUrl: existing.imageUrl,
      gallery: existing.gallery ?? [],
      category: existing.category,
      title: existing.title,
      excerpt: existing.excerpt,
      content: existing.content ?? [],
      author: existing.author ?? "",
    });
    setInitialized(true);
  }

  const update = <K extends keyof FormData>(key: K, value: FormData[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  const categoryOptions = Array.from(
    new Set((categories ?? []).map((item) => item.name).filter(Boolean)),
  ).sort((a, b) => a.localeCompare(b, "vi"));

  useEffect(() => {
    if (!pendingImageFile) {
      setPendingImagePreview(null);
      return;
    }

    const objectUrl = URL.createObjectURL(pendingImageFile);
    setPendingImagePreview(objectUrl);

    return () => {
      URL.revokeObjectURL(objectUrl);
    };
  }, [pendingImageFile]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.title.trim()) {
      toast({ title: t("form.required"), description: t("posts.field.title"), variant: "destructive" });
      return;
    }
    let imageUrl = data.imageUrl || "/placeholder.svg";

    setSubmitting(true);
    try {
      if (pendingImageFile) {
        setUploadingImage(true);
        const upload = await adminApi.uploadImage(
          pendingImageFile,
          mode === "edit" ? data.imageUrl : undefined,
        );
        imageUrl = upload.data.imageUrl;
      }

      const payload: UpsertActivityRequest = {
      slug: data.slug || slugify(data.title),
      date: toApiDate(data.date),
      imageUrl,
      gallery: data.gallery.length ? data.gallery : undefined,
      category: data.category,
      author: data.author || undefined,
      title: data.title,
      excerpt: data.excerpt,
      content: data.content,
      };

      if (mode === "create") {
        await adminApi.createActivity(payload);
      } else {
        await adminApi.updateActivity(data.id, payload);
      }
      toast({ title: mode === "create" ? t("form.created") : t("form.updated"), description: data.title });
      navigate("/admin/posts");
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
      <div className="flex items-center gap-3 mb-6">
        <Link
          to="/admin/posts"
          className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <ArrowLeft className="w-4 h-4" />
        </Link>
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {mode === "create" ? t("posts.addTitle") : t("posts.editTitle")}
          </h1>
          <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{mode === "edit" && data.slug}</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.basicInfo")}</h2>
            <div className="space-y-4">
              <Field label={t("posts.field.title") + " *"}>
                <input className="admin-input" value={data.title} onChange={(e) => update("title", e.target.value)} required />
              </Field>
              <Field label={t("posts.field.excerpt")}>
                <textarea className="admin-input min-h-20" value={data.excerpt} onChange={(e) => update("excerpt", e.target.value)} />
              </Field>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Field label={t("posts.field.category")}>
                  <select className="admin-input" value={data.category} onChange={(e) => update("category", e.target.value)}>
                    <option value="">-- Chọn danh mục --</option>
                    {[
                      ...categoryOptions,
                      ...(data.category && !categoryOptions.includes(data.category) ? [data.category] : []),
                    ].map((option) => (
                      <option key={option} value={option}>{option}</option>
                    ))}
                  </select>
                </Field>
                <Field label={t("posts.field.author")}>
                  <input className="admin-input" value={data.author ?? ""} onChange={(e) => update("author", e.target.value)} />
                </Field>
                <Field label={t("posts.field.date")}>
                  <input type="date" className="admin-input" value={data.date} onChange={(e) => update("date", e.target.value)} />
                </Field>
              </div>
            </div>
          </div>

          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.content")}</h2>
            <Field label={t("posts.field.content")}>
              <textarea
                className="admin-input min-h-64"
                value={data.content.join("\n")}
                onChange={(e) => update("content", toContentLines(e.target.value))}
              />
            </Field>
          </div>
        </div>

        <div className="space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.media")}</h2>
            <Field label={t("posts.field.image") + " *"}>
              <FeaturedImageUploader
                imageUrl={data.imageUrl}
                pendingPreview={pendingImagePreview}
                pendingFileName={pendingImageFile?.name}
                onFileSelected={(file) => {
                  setPendingImageFile(file);
                  toast({ title: t("form.updated"), description: file.name });
                }}
                onClearPending={() => setPendingImageFile(null)}
                disabled={uploadingImage}
              />
            </Field>
          </div>

          <div className="admin-card p-6">
            <h2 className="font-bold mb-1">Thư viện ảnh</h2>
            <p className="text-xs mb-4" style={{ color: "hsl(var(--admin-muted))" }}>
              Tải lên nhiều hình ảnh phụ cho bài đăng.
            </p>
            <GalleryEditor items={data.gallery} onChange={(items) => update("gallery", items)} />
          </div>

          <button type="submit" disabled={submitting || uploadingImage} className="admin-btn-primary w-full inline-flex items-center justify-center gap-2 px-5 py-3 text-sm disabled:opacity-50">
            <Save className="w-4 h-4" />
            {mode === "create" ? t("form.create") : t("form.update")}
          </button>
        </div>
      </form>
    </AdminLayout>
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

export default PostForm;
