import { Link, useNavigate, useParams } from "react-router-dom";
import { useState } from "react";
import { ArrowLeft, Edit, Trash2, Calendar, Grid3X3, List, Tag } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useNewsItem } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";
import { PageLoading, PageError } from "@/components/PageState";
import ContentBlocks from "@/components/ContentBlocks";
import { Button } from "@/components/ui/button";

const NewsView = () => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: post, loading, error, refetch } = useNewsItem(slug ?? "");
  const [galleryMode, setGalleryMode] = useState<"grid" | "list">("grid");

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;

  if (!post) {
    return (
      <AdminLayout>
        <div className="rounded-lg border bg-card p-10 text-center">
          <p className="text-sm text-muted-foreground">{t("adminNews.notFound")}</p>
          <Button asChild className="mt-4">
            <Link to="/admin/news">
              <ArrowLeft className="mr-1.5 h-4 w-4" /> {t("form.back")}
            </Link>
          </Button>
        </div>
      </AdminLayout>
    );
  }

  const handleDelete = async () => {
    if (!confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteNews(post.id);
      toast({ title: t("form.deleted"), description: post.title });
      navigate("/admin/news");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3 min-w-0">
            <Button asChild variant="outline" size="icon" className="rounded-full shrink-0">
              <Link to="/admin/news">
                <ArrowLeft className="h-4 w-4" />
              </Link>
            </Button>
            <div className="min-w-0">
              <p className="text-xs font-medium uppercase tracking-wide text-primary">
                {t("adminNews.detail")}
              </p>
              <h1 className="text-xl font-semibold tracking-tight lg:text-2xl line-clamp-2">{post.title}</h1>
            </div>
          </div>
          <div className="flex flex-wrap gap-2 shrink-0">
            <Button asChild variant="outline">
              <Link to={`/admin/news/${post.slug}/edit`}>
                <Edit className="mr-1.5 h-4 w-4" /> {t("common.edit")}
              </Link>
            </Button>
            <Button variant="outline" onClick={handleDelete} className="text-destructive hover:text-destructive">
              <Trash2 className="mr-1.5 h-4 w-4" /> {t("common.delete")}
            </Button>
          </div>
        </header>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-4">
            <div className="overflow-hidden rounded-lg border bg-card">
              <div className="aspect-[16/9] bg-muted">
                <img src={post.imageUrl} alt={post.title} className="w-full h-full object-cover" />
              </div>
            </div>
            <div className="rounded-lg border bg-card p-6">
              <p className="mb-4 text-sm font-medium text-muted-foreground">{post.excerpt}</p>
              <ContentBlocks items={post.content} className="text-sm leading-relaxed" />
            </div>
            {post.gallery && post.gallery.length > 0 && (
              <div className="rounded-lg border bg-card p-6">
                <div className="mb-4 flex items-center justify-between gap-3">
                  <h2 className="text-base font-semibold">{t("media.gallery.title")}</h2>
                  <div className="inline-flex overflow-hidden rounded-md border">
                    <Button type="button" variant="ghost" size="icon" onClick={() => setGalleryMode("grid")} aria-label={t("gallery.viewGrid")} className="rounded-none">
                      <Grid3X3 className="h-4 w-4" />
                    </Button>
                    <Button type="button" variant="ghost" size="icon" onClick={() => setGalleryMode("list")} aria-label={t("gallery.viewList")} className="rounded-none border-l">
                      <List className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
                <div className={galleryMode === "grid" ? "grid grid-cols-1 gap-3 sm:grid-cols-2" : "space-y-3"}>
                  {post.gallery.map((url, index) => (
                    <img
                      key={`${url}-${index}`}
                      src={url}
                      alt=""
                      className={
                        galleryMode === "grid"
                          ? "w-full aspect-[4/3] rounded-md object-cover bg-muted"
                          : "w-full aspect-video rounded-md object-cover bg-muted"
                      }
                      loading="lazy"
                    />
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="h-fit space-y-3 rounded-lg border bg-card p-6 text-sm">
            <h2 className="mb-2 text-base font-semibold">{t("form.basicInfo")}</h2>
            <Info icon={Tag} label={t("adminNews.field.category")} value={post.category} />
            <Info icon={Calendar} label={t("adminNews.field.date")} value={post.date} />
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

const Info = ({ icon: Icon, label, value }: { icon: React.ComponentType<React.SVGProps<SVGSVGElement>>; label: string; value: string }) => (
  <div className="flex items-start gap-3">
    <Icon className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
    <div className="min-w-0">
      <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="font-medium break-words">{value}</p>
    </div>
  </div>
);

export default NewsView;
