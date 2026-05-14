import { useState } from "react";
import { Link } from "react-router-dom";
import { Plus, MapPin, Maximize2, Edit, Trash2, Eye } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useProjects } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";
import type { ProjectResponse } from "@/services/contentApi";
import { PageLoading, PageError } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";

const AdminProjects = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [tab, setTab] = useState<"all" | "ongoing" | "completed">("all");
  const { data: items, loading, error, refetch } = useProjects();

  const list = items ?? [];
  const filtered = list.filter((p) => tab === "all" || p.status === tab);

  const handleDelete = async (p: ProjectResponse) => {
    if (!confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteProject(p.id);
      toast({ title: t("form.deleted"), description: p.name });
      refetch();
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename("admin-projects"),
      columns: [
        { header: "ID", value: "id" },
        { header: "Slug", value: "slug" },
        { header: t("proj.title"), value: "name" },
        { header: "Client", value: "client" },
        { header: "Location", value: "location" },
        { header: t("proj.scale"), value: "scale" },
        { header: "Scope", value: "scope" },
        {
          header: t("common.status"),
          value: (row) => (row.status === "ongoing" ? t("proj.ongoing") : t("proj.completed")),
        },
        { header: "Year", value: (row) => row.year ?? "" },
        { header: "Category", value: (row) => row.category ?? "" },
        { header: "Description", value: (row) => row.description ?? "" },
        { header: "Challenges", value: (row) => row.challenges ?? [] },
        { header: "Solutions", value: (row) => row.solutions ?? [] },
        {
          header: "Highlights",
          value: (row) => row.highlights?.map((item) => `${item.label}: ${item.value}`).join("; ") ?? "",
        },
        { header: "Image URL", value: "imageUrl" },
        { header: "Gallery", value: (row) => row.gallery ?? [] },
      ],
      rows: filtered,
    });
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("proj.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} {t("common.showing")}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
          <Link to="/admin/projects/new" className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
            <Plus className="w-4 h-4" /> {t("proj.add")}
          </Link>
        </div>
      </div>

      <div className="flex gap-2 mb-6">
        {[
          { id: "all", label: t("common.all") },
          { id: "ongoing", label: t("proj.ongoing") },
          { id: "completed", label: t("proj.completed") },
        ].map((tb) => (
          <button
            key={tb.id}
            onClick={() => setTab(tb.id as typeof tab)}
            className="px-5 py-2.5 rounded-full text-xs font-bold uppercase tracking-wider transition"
            style={
              tab === tb.id
                ? {
                    background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))",
                    color: "white",
                    boxShadow: "0 8px 18px -6px hsl(var(--admin-primary) / 0.45)",
                  }
                : { background: "white", color: "hsl(var(--admin-sidebar-text))", border: "1px solid hsl(var(--admin-border))" }
            }
          >
            {tb.label}
          </button>
        ))}
      </div>

      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={refetch} />
      ) : (
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
        {filtered.map((p) => (
          <div key={p.id} className="admin-card overflow-hidden group">
            <Link to={`/admin/projects/${p.slug}`} className="block aspect-[16/10] overflow-hidden bg-muted relative">
              <img src={p.imageUrl} alt={p.name} className="w-full h-full object-cover group-hover:scale-105 transition duration-700" />
              <span
                className="admin-chip absolute top-4 left-4"
                style={
                  p.status === "ongoing"
                    ? { background: "hsl(var(--admin-warning-soft))", color: "hsl(var(--admin-warning))" }
                    : { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                }
              >
                {p.status === "ongoing" ? t("proj.ongoing") : t("proj.completed")}
              </span>
            </Link>
            <div className="p-5">
              <p className="text-[10px] uppercase tracking-wider font-bold mb-1.5" style={{ color: "hsl(var(--admin-primary))" }}>
                {p.category}
              </p>
              <h3 className="font-display text-lg font-extrabold mb-2 line-clamp-1">{p.name}</h3>
              <div className="space-y-1 text-xs mb-4" style={{ color: "hsl(var(--admin-muted))" }}>
                <p className="flex items-center gap-1.5"><MapPin className="w-3 h-3" /> {p.location}</p>
                <p className="flex items-center gap-1.5"><Maximize2 className="w-3 h-3" /> {t("proj.scale")}: {p.scale}</p>
              </div>
              <div className="flex items-center gap-1.5 pt-4 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <Link
                  to={`/admin/projects/${p.slug}`}
                  className="flex-1 inline-flex items-center justify-center gap-1.5 py-2 rounded-lg text-xs font-bold hover:bg-muted transition"
                  style={{ color: "hsl(var(--admin-info))" }}
                >
                  <Eye className="w-3.5 h-3.5" /> {t("common.view")}
                </Link>
                <Link
                  to={`/admin/projects/${p.slug}/edit`}
                  className="flex-1 inline-flex items-center justify-center gap-1.5 py-2 rounded-lg text-xs font-bold hover:bg-muted transition"
                  style={{ color: "hsl(var(--admin-primary))" }}
                >
                  <Edit className="w-3.5 h-3.5" /> {t("common.edit")}
                </Link>
                <button
                  onClick={() => handleDelete(p)}
                  className="flex-1 inline-flex items-center justify-center gap-1.5 py-2 rounded-lg text-xs font-bold hover:bg-muted transition"
                  style={{ color: "hsl(var(--admin-danger))" }}
                >
                  <Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
      )}
    </AdminLayout>
  );
};

export default AdminProjects;
