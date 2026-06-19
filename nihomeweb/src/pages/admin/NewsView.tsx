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
        <div className="admin-card p-10 text-center">
          <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{t("adminNews.notFound")}</p>
          <Link to="/admin/news" className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm mt-4">
            <ArrowLeft className="w-4 h-4" /> {t("form.back")}
          </Link>
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
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-6">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            to="/admin/news"
            className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition shrink-0"
            style={{ borderColor: "hsl(var(--admin-border))" }}
          >
            <ArrowLeft className="w-4 h-4" />
          </Link>
          <div className="min-w-0">
            <p className="text-xs uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-primary))" }}>
              {t("adminNews.detail")}
            </p>
            <h1 className="font-display text-xl lg:text-2xl font-extrabold tracking-tight line-clamp-2">{post.title}</h1>
          </div>
        </div>
        <div className="flex gap-2 shrink-0">
          <Link
            to={`/admin/news/${post.slug}/edit`}
            className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold border bg-white hover:bg-muted transition"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-primary))" }}
          >
            <Edit className="w-4 h-4" /> {t("common.edit")}
          </Link>
          <button
            onClick={handleDelete}
            className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold border bg-white hover:bg-muted transition"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}
          >
            <Trash2 className="w-4 h-4" /> {t("common.delete")}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="admin-card overflow-hidden">
            <div className="aspect-[16/9] bg-muted">
              <img src={post.imageUrl} alt={post.title} className="w-full h-full object-cover" />
            </div>
          </div>
          <div className="admin-card p-6">
            <p className="text-sm font-semibold mb-4" style={{ color: "hsl(var(--admin-muted))" }}>{post.excerpt}</p>
            <ContentBlocks items={post.content} className="text-sm leading-relaxed" paragraphClassName="text-[hsl(var(--admin-sidebar-text))]" />
          </div>
          {post.gallery && post.gallery.length > 0 && (
            <div className="admin-card p-6">
              <div className="flex items-center justify-between gap-3 mb-4">
                <h2 className="font-bold">{t("media.gallery.title")}</h2>
                <div className="inline-flex rounded-lg border overflow-hidden" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <button type="button" onClick={() => setGalleryMode("grid")} className="p-2 hover:bg-muted" aria-label={t("gallery.viewGrid")}>
                    <Grid3X3 className="w-4 h-4" />
                  </button>
                  <button type="button" onClick={() => setGalleryMode("list")} className="p-2 hover:bg-muted border-l" style={{ borderColor: "hsl(var(--admin-border))" }} aria-label={t("gallery.viewList")}>
                    <List className="w-4 h-4" />
                  </button>
                </div>
              </div>
              <div className={galleryMode === "grid" ? "grid grid-cols-1 sm:grid-cols-2 gap-3" : "space-y-3"}>
                {post.gallery.map((url, index) => (
                  <img key={`${url}-${index}`} src={url} alt="" className={galleryMode === "grid" ? "w-full aspect-[4/3] rounded-xl object-cover bg-muted" : "w-full aspect-video rounded-xl object-cover bg-muted"} loading="lazy" />
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="admin-card p-6 h-fit space-y-3 text-sm">
          <h2 className="font-bold mb-2">{t("form.basicInfo")}</h2>
          <Info icon={Tag} label={t("adminNews.field.category")} value={post.category} />
          <Info icon={Calendar} label={t("adminNews.field.date")} value={post.date} />
        </div>
      </div>
    </AdminLayout>
  );
};

const Info = ({ icon: Icon, label, value }: { icon: React.ComponentType<React.SVGProps<SVGSVGElement>>; label: string; value: string }) => (
  <div className="flex items-start gap-3">
    <Icon className="w-4 h-4 mt-0.5 shrink-0" style={{ color: "hsl(var(--admin-muted))" }} />
    <div className="min-w-0">
      <p className="text-[10px] uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-muted))" }}>{label}</p>
      <p className="font-semibold break-words">{value}</p>
    </div>
  </div>
);

export default NewsView;
