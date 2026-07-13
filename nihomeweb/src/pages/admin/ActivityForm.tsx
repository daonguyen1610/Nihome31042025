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
import ContentBlockEditor from "@/components/admin/ContentBlockEditor";
import type { ContentItem } from "@/services/contentApi";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

interface FormData {
  id: number;
  slug: string;
  date: string;
  imageUrl: string;
  gallery: string[];
  category: string;
  title: string;
  excerpt: string;
  content: ContentItem[];
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

const ActivityForm = ({ mode }: { mode: "create" | "edit" }) => {
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
      toast({ title: t("form.required"), description: t("activities.field.title"), variant: "destructive" });
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
      categoryId:
        (categories ?? []).find(
          (c) => c.name.toLowerCase() === data.category.trim().toLowerCase(),
        )?.id ?? null,
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
      navigate("/admin/activities");
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
            <Link to="/admin/activities">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight lg:text-3xl">
              {mode === "create" ? t("activities.addTitle") : t("activities.editTitle")}
            </h1>
            <p className="text-sm text-muted-foreground">{mode === "edit" && data.slug}</p>
          </div>
        </header>

        <form onSubmit={handleSubmit} className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-4">
            <div className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("form.basicInfo")}</h2>
              <div className="space-y-4">
                <Field label={t("activities.field.title") + " *"}>
                  <Input value={data.title} onChange={(e) => update("title", e.target.value)} required />
                </Field>
                <Field label={t("activities.field.excerpt")}>
                  <Textarea className="min-h-20" value={data.excerpt} onChange={(e) => update("excerpt", e.target.value)} />
                </Field>
                <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                  <Field label={t("activities.field.category")}>
                    <Select value={data.category || undefined} onValueChange={(v) => update("category", v)}>
                      <SelectTrigger>
                        <SelectValue placeholder={t("form.selectCategory")} />
                      </SelectTrigger>
                      <SelectContent>
                        {[
                          ...categoryOptions,
                          ...(data.category && !categoryOptions.includes(data.category) ? [data.category] : []),
                        ].map((option) => (
                          <SelectItem key={option} value={option}>{option}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </Field>
                  <Field label={t("activities.field.author")}>
                    <Input value={data.author ?? ""} onChange={(e) => update("author", e.target.value)} />
                  </Field>
                  <Field label={t("activities.field.date")}>
                    <Input type="date" value={data.date} onChange={(e) => update("date", e.target.value)} />
                  </Field>
                </div>
              </div>
            </div>

            <div className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("form.content")}</h2>
              <Field label={t("activities.field.content")}>
                <ContentBlockEditor value={data.content} onChange={(items) => update("content", items)} />
              </Field>
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("form.media")}</h2>
              <Field label={t("activities.field.image") + " *"}>
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
                {t("media.gallery.descPost")}
              </p>
              <GalleryEditor items={data.gallery} onChange={(items) => update("gallery", items)} />
            </div>

            <Button type="submit" disabled={submitting || uploadingImage} className="w-full">
              <Save className="mr-1.5 h-4 w-4" />
              {mode === "create" ? t("form.create") : t("form.update")}
            </Button>
          </div>
        </form>
      </div>
    </AdminLayout>
  );
};

const Field = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="space-y-1.5">
    <Label className="text-xs">{label}</Label>
    {children}
  </div>
);

export default ActivityForm;
