import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Search, Edit, Trash2, Eye } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useActivities } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";
import type { ActivityResponse } from "@/services/contentApi";
import { PageLoading, PageError } from "@/components/PageState";

const AdminPosts = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [q, setQ] = useState("");
  const [cat, setCat] = useState("all");
  const { data: items, loading, error, refetch } = useActivities();

  const list = useMemo(() => items ?? [], [items]);
  const categories = useMemo(() => ["all", ...Array.from(new Set(list.map((a) => a.category)))], [list]);
  const filtered = list.filter((a) => {
    const matchQ = !q || a.title.toLowerCase().includes(q.toLowerCase());
    const matchCat = cat === "all" || a.category === cat;
    return matchQ && matchCat;
  });

  const handleDelete = async (a: ActivityResponse) => {
    if (!confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteActivity(a.id);
      toast({ title: t("form.deleted"), description: a.title });
      refetch();
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("posts.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} {t("common.showing")}
          </p>
        </div>
        <Link to="/admin/posts/new" className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Plus className="w-4 h-4" /> {t("posts.create")}
        </Link>
      </div>

      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={refetch} />
      ) : (
      <>
      <div className="admin-card p-5 mb-5 flex flex-col lg:flex-row gap-3 lg:items-center justify-between">
        <div
          className="flex items-center gap-2 rounded-full px-4 py-2 border w-full lg:w-80"
          style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
        >
          <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t("posts.searchPlaceholder")}
            className="bg-transparent outline-none text-sm flex-1"
          />
        </div>
        <div className="flex flex-wrap gap-2">
          {categories.map((c) => (
            <button
              key={c}
              onClick={() => setCat(c)}
              className="px-4 py-2 rounded-full text-xs font-bold uppercase tracking-wider transition"
              style={
                cat === c
                  ? { background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))", color: "white" }
                  : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-sidebar-text))" }
              }
            >
              {c === "all" ? t("posts.allCategories") : c}
            </button>
          ))}
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("posts.col.post")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("posts.col.category")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("posts.col.author")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("common.date")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-6 py-12 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("posts.empty")}
                  </td>
                </tr>
              ) : (
                filtered.map((a) => (
                  <tr key={a.id} className="border-t hover:bg-muted/30 transition" style={{ borderColor: "hsl(var(--admin-border))" }}>
                    <td className="px-6 py-4">
                      <Link to={`/admin/posts/${a.slug}`} className="flex items-center gap-3 hover:opacity-80 transition">
                        <img src={a.imageUrl} alt="" className="w-12 h-12 rounded-xl object-cover" />
                        <p className="font-semibold line-clamp-2 max-w-md">{a.title}</p>
                      </Link>
                    </td>
                    <td className="px-6 py-4">
                      <span className="admin-chip" style={{ background: "hsl(var(--admin-primary-soft))", color: "hsl(var(--admin-primary))" }}>
                        {a.category}
                      </span>
                    </td>
                    <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>{a.author ?? "—"}</td>
                    <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>{a.date}</td>
                    <td className="px-6 py-4 text-right">
                      <div className="inline-flex items-center gap-1">
                        <Link
                          to={`/admin/posts/${a.slug}`}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center transition"
                          style={{ color: "hsl(var(--admin-info))" }}
                        >
                          <Eye className="w-4 h-4" />
                        </Link>
                        <Link
                          to={`/admin/posts/${a.slug}/edit`}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center transition"
                          style={{ color: "hsl(var(--admin-primary))" }}
                        >
                          <Edit className="w-4 h-4" />
                        </Link>
                        <button
                          onClick={() => handleDelete(a)}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center transition"
                          style={{ color: "hsl(var(--admin-danger))" }}
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
      </>
      )}
    </AdminLayout>
  );
};

export default AdminPosts;
