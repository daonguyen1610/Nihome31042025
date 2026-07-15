import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, MapPin, Maximize2, Edit, Trash2, Eye, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useProjects, useProjectCategories } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { adminApi } from "@/services/adminApi";
import type { ProjectResponse } from "@/services/contentApi";
import { PageLoading, PageError } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";
import { resolveCategoryLabel } from "@/lib/category";

const PROJECT_STATUS_STYLES = {
  ongoing: "border-amber-200 bg-amber-50 text-amber-700",
  completed: "border-green-300 bg-green-100 text-green-800",
} as const;

const PROJECT_STATUS_DOT = {
  ongoing: "bg-amber-500",
  completed: "bg-green-600",
} as const;

const AdminProjects = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [tab, setTab] = useState<"all" | "ongoing" | "completed">("all");
  const [catId, setCatId] = useState<number | "all">("all");
  const [q, setQ] = useState("");
  const { data: items, loading, error, refetch } = useProjects();
  const { data: categoryMaster } = useProjectCategories(true);

  const list = useMemo(() => items ?? [], [items]);
  const categoriesById = useMemo(
    () => new Map((categoryMaster ?? []).map((c) => [c.id, c])),
    [categoryMaster],
  );
  const categoryOptions = useMemo(() => {
    const ids = new Set<number>();
    (categoryMaster ?? []).forEach((c) => ids.add(c.id));
    list.forEach((p) => { if (p.categoryId != null) ids.add(p.categoryId); });
    return Array.from(ids)
      .map((id) => ({ id, label: resolveCategoryLabel(id, undefined, categoriesById, lang) }))
      .filter((opt) => opt.label)
      .sort((a, b) => a.label.localeCompare(b.label, lang));
  }, [categoryMaster, list, categoriesById, lang]);
  const filtered = useMemo(() => {
    return list.filter((p) => {
      if (tab !== "all" && p.status !== tab) return false;
      if (catId !== "all" && p.categoryId !== catId) return false;
      if (!q.trim()) return true;
      return (
        matchesSearch(p.name, q) ||
        matchesSearch(p.slug, q) ||
        matchesSearch(p.location, q) ||
        matchesSearch(p.client, q) ||
        matchesSearch(p.category, q)
      );
    });
  }, [list, tab, catId, q]);

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
  }, [tab, catId, q, clearSelection]);

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
        { header: "Category", value: (row) => resolveCategoryLabel(row.categoryId, row.category, categoriesById, lang) },
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
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("proj.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {filtered.length} {t("common.showing")}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
            <Button asChild>
              <Link to="/admin/projects/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("proj.add")}
              </Link>
            </Button>
          </div>
        </header>

        <section className="space-y-3 rounded-lg border bg-card p-3">
          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-[220px] flex-1">
              <Label className="text-xs" htmlFor="projects-search">{t("common.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="projects-search"
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                  placeholder={t("proj.searchPlaceholder")}
                  className="h-9 pl-9"
                />
              </div>
            </div>
            <div className="w-full sm:w-[220px]">
              <Label className="text-xs" htmlFor="projects-category">{t("activities.col.category")}</Label>
              <Select
                value={catId === "all" ? "all" : String(catId)}
                onValueChange={(v) => setCatId(v === "all" ? "all" : Number(v))}
              >
                <SelectTrigger id="projects-category" className="h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("common.all")}</SelectItem>
                  {categoryOptions.map((opt) => (
                    <SelectItem key={opt.id} value={String(opt.id)}>{opt.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="flex flex-wrap gap-1.5">
            {[
              { id: "all", label: t("common.all") },
              { id: "ongoing", label: t("proj.ongoing") },
              { id: "completed", label: t("proj.completed") },
            ].map((tb) => (
              <Button
                key={tb.id}
                type="button"
                variant={tab === tb.id ? "default" : "outline"}
                size="sm"
                onClick={() => setTab(tb.id as typeof tab)}
                className="h-8"
              >
                {tb.label}
              </Button>
            ))}
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={refetch} />
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <Search className="h-5 w-5" aria-hidden />
            </div>
            <p>{t("common.noData")}</p>
            <Button asChild size="sm">
              <Link to="/admin/projects/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("proj.add")}
              </Link>
            </Button>
          </div>
        ) : (
        <div className="space-y-3">
          <BulkActionBar
            selectedCount={selectedIds.size}
            bulkDeleting={bulkDeleting}
            onClear={clearSelection}
            onBulkDelete={() => void handleBulkDelete()}
          />
          <div className="flex items-center gap-2 px-1 text-xs text-muted-foreground">
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
          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            {filtered.map((p) => (
              <div key={p.id} className="group relative overflow-hidden rounded-lg border bg-card transition hover:shadow-md">
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
                  <Badge
                    variant="outline"
                    className={cn(
                      "absolute top-3 left-3 gap-1.5 whitespace-nowrap bg-white/95 font-medium shadow-sm",
                      PROJECT_STATUS_STYLES[p.status],
                    )}
                  >
                    <span className={cn("h-1.5 w-1.5 rounded-full", PROJECT_STATUS_DOT[p.status])} />
                    {p.status === "ongoing" ? t("proj.ongoing") : t("proj.completed")}
                  </Badge>
                </Link>
                <div className="p-4">
                  {(() => {
                    const label = resolveCategoryLabel(p.categoryId, p.category, categoriesById, lang);
                    return label ? (
                      <p className="text-[10px] uppercase tracking-wide font-medium mb-1 text-primary">
                        {label}
                      </p>
                    ) : null;
                  })()}
                  <h3 className="text-base font-semibold mb-2 line-clamp-1">{p.name}</h3>
                  <div className="space-y-1 text-xs mb-3 text-muted-foreground">
                    <p className="flex items-center gap-1.5"><MapPin className="h-3 w-3" /> {p.location}</p>
                    <p className="flex items-center gap-1.5"><Maximize2 className="h-3 w-3" /> {t("proj.scale")}: {p.scale}</p>
                  </div>
                  <div className="flex items-center gap-1 pt-3 border-t">
                    <Button asChild variant="ghost" size="sm" className="flex-1">
                      <Link to={`/admin/projects/${p.slug}`}>
                        <Eye className="mr-1 h-3.5 w-3.5" /> {t("common.view")}
                      </Link>
                    </Button>
                    <Button asChild variant="ghost" size="sm" className="flex-1">
                      <Link to={`/admin/projects/${p.slug}/edit`}>
                        <Edit className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                      </Link>
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => handleDelete(p)} className="flex-1 text-destructive hover:text-destructive">
                      <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminProjects;
