import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Edit, Trash2, Calendar, Tag, User } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { deletePost, getPost } from "@/lib/adminStore";

const PostView = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const post = id ? getPost(id) : undefined;

  if (!post) {
    return (
      <AdminLayout>
        <div className="admin-card p-10 text-center">
          <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>Không tìm thấy bài đăng.</p>
          <Link to="/admin/posts" className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm mt-4">
            <ArrowLeft className="w-4 h-4" /> {t("form.back")}
          </Link>
        </div>
      </AdminLayout>
    );
  }

  const handleDelete = () => {
    if (!confirm(t("form.confirmDelete"))) return;
    deletePost(post.id);
    toast({ title: t("form.deleted"), description: post.title });
    navigate("/admin/posts");
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-6">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            to="/admin/posts"
            className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition shrink-0"
            style={{ borderColor: "hsl(var(--admin-border))" }}
          >
            <ArrowLeft className="w-4 h-4" />
          </Link>
          <div className="min-w-0">
            <p className="text-xs uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-primary))" }}>
              {t("posts.detail")}
            </p>
            <h1 className="font-display text-xl lg:text-2xl font-extrabold tracking-tight line-clamp-2">{post.title}</h1>
          </div>
        </div>
        <div className="flex gap-2 shrink-0">
          <Link
            to={`/admin/posts/${post.id}/edit`}
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
              <img src={post.img} alt={post.title} className="w-full h-full object-cover" />
            </div>
          </div>
          <div className="admin-card p-6">
            <p className="text-sm font-semibold mb-4" style={{ color: "hsl(var(--admin-muted))" }}>{post.excerpt}</p>
            <div className="space-y-4 text-sm leading-relaxed" style={{ color: "hsl(var(--admin-sidebar-text))" }}>
              {post.content.map((p, i) => <p key={i}>{p}</p>)}
            </div>
          </div>
        </div>

        <div className="admin-card p-6 h-fit space-y-3 text-sm">
          <h2 className="font-bold mb-2">{t("form.basicInfo")}</h2>
          <Info icon={Tag} label={t("posts.field.category")} value={post.category} />
          <Info icon={User} label={t("posts.field.author")} value={post.author ?? "—"} />
          <Info icon={Calendar} label={t("posts.field.date")} value={post.date} />
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

export default PostView;
