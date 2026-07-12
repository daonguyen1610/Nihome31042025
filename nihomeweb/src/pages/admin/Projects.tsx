import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, MapPin, Maximize2, Edit, Trash2, Eye, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useProjects, useProjectCategories } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { adminApi } from "@/services/adminApi";
import type { ProjectResponse } from "@/services/contentApi";
import { PageLoading, PageError } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Checkbox } from "@/components/ui/checkbox";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";

const AdminProjects = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [tab, setTab] = useState<"all" | "ongoing" | "completed">("all");
  const [cat, setCat] = useState("all");
  const [q, setQ] = useState("");
  const { data: items, loading, error, refetch } = useProjects();
  const { data: categoryMaster } = useProjectCategories(true);

  const list = useMemo(() => items ?? [], [items]);
  const categoryOptions = useMemo(() => {
    const fromMaster = (categoryMaster ?? []).map((c) => c.name);
    const fromProjects = list.map((p) => p.category ?? "").filter(Boolean);
    return Array.from(new Set([...fromMaster, ...fromProjects])).sort((a, b) =>
      a.localeCompare(b, "vi"),
    );
  }, [categoryMaster, list]);
  const filtered = useMemo(() => {
    return list.filter((p) => {
      if (tab !== "all" && p.status !== tab) return false;
      if (cat !== "all" && (p.category ?? "") !== cat) return false;
      if (!q.trim()) return true;
      return (
        matchesSearch(p.name, q) ||
        matchesSearch(p.slug, q) ||
        matchesSearch(p.location, q) ||
        matchesSearch(p.client, q) ||
        matchesSearch(p.category, q)
      );
    });
  }, [list, tab, cat, q]);

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

  const visibleIds = useMemo(() => filtered.map((p) => p.id), [filtered]);
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
    deleteOne: (id) => adminApi.deleteProject(id),
    onAfter: async () => {
      refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [tab, cat, q, clearSelection]);

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

      <div className="flex flex-col gap-3 mb-6">
        <div className="flex flex-wrap items-center gap-3">
          <div
            className="flex items-center gap-2 rounded-full px-4 py-2 border w-full sm:w-80"
            style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
          >
            <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t("proj.searchPlaceholder")}
              className="bg-transparent outline-none text-sm flex-1 placeholder:opacity-60"
            />
          </div>
          <select
            value={cat}
            onChange={(e) => setCat(e.target.value)}
            className="admin-input w-full sm:w-56 sm:ml-auto"
          >
            <option value="all">{t("common.all")}</option>
            {categoryOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-wrap gap-2">
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
      </div>

      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={refetch} />
      ) : filtered.length === 0 ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("common.noData")}
        </div>
      ) : (
      <div className="space-y-3">
        <BulkActionBar
          selectedCount={selectedIds.size}
          bulkDeleting={bulkDeleting}
          onClear={clearSelection}
          onBulkDelete={() => void handleBulkDelete()}
        />
        <div className="flex items-center gap-2 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
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
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
        {filtered.map((p) => (
          <div key={p.id} className="admin-card overflow-hidden group relative">
            <div
              className="absolute top-3 right-3 z-10 rounded bg-white/90 p-1 shadow"
              onClick={(e) => e.stopPropagation()}
            >
              <Checkbox
                checked={selectedIds.has(p.id)}
                onCheckedChange={(v) => toggleOne(p.id, v === true)}
                aria-label={`${t("common.selectAll")} · ${p.name}`}
              />
            </div>
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
      </div>
      )}
    </AdminLayout>
  );
};

export default AdminProjects;
