import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Search, Edit, Trash2, Eye } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useActivities, useActivityCategories } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { adminApi } from "@/services/adminApi";
import type { ActivityResponse } from "@/services/contentApi";
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

const AdminActivities = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [q, setQ] = useState("");
  const [cat, setCat] = useState("all");
  const { data: items, loading, error, refetch } = useActivities();
  const { data: categoryMaster } = useActivityCategories(true);

  const list = useMemo(() => items ?? [], [items]);
  const categories = useMemo(() => {
    const fromMaster = (categoryMaster ?? []).map((c) => c.name);
    const fromPosts = list.map((a) => a.category).filter(Boolean);
    const unique = Array.from(new Set([...fromMaster, ...fromPosts])).sort((a, b) =>
      a.localeCompare(b, "vi"),
    );
    return ["all", ...unique];
  }, [categoryMaster, list]);
  const filtered = useMemo(
    () =>
      list.filter((a) => {
        const matchCat = cat === "all" || a.category === cat;
        if (!matchCat) return false;
        if (!q.trim()) return true;
        return matchesSearch(a.title, q) || matchesSearch(a.category, q) || matchesSearch(a.slug, q);
      }),
    [list, cat, q],
  );

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

  const visibleIds = useMemo(() => filtered.map((a) => a.id), [filtered]);
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
    deleteOne: (id) => adminApi.deleteActivity(id),
    onAfter: async () => {
      refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [cat, q, clearSelection]);

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename("admin-activities"),
      columns: [
        { header: "ID", value: "id" },
        { header: "Slug", value: "slug" },
        { header: t("activities.col.post"), value: "title" },
        { header: t("activities.col.category"), value: "category" },
        { header: t("activities.col.author"), value: (row) => row.author ?? "" },
        { header: t("common.date"), value: "date" },
        { header: "Excerpt", value: "excerpt" },
        { header: "Content", value: (row) => row.content.map((item) => (typeof item === "string" ? item : JSON.stringify(item))).join("\n\n") },
        { header: "Image URL", value: "imageUrl" },
      ],
      rows: filtered,
    });
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("activities.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {filtered.length} {t("common.showing")}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
            <Button asChild>
              <Link to="/admin/activities/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("activities.create")}
              </Link>
            </Button>
          </div>
        </header>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={refetch} />
        ) : (
        <>
        <section className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
          <div className="min-w-[220px] flex-1">
            <Label className="text-xs" htmlFor="activities-search">{t("common.search")}</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="activities-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("activities.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
          <div className="w-full sm:w-[200px]">
            <Label className="text-xs" htmlFor="activities-category">{t("activities.col.category")}</Label>
            <Select value={cat} onValueChange={setCat}>
              <SelectTrigger id="activities-category" className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {categories.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c === "all" ? t("activities.allCategories") : c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </section>

        {filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <Search className="h-5 w-5" aria-hidden />
            </div>
            <p>{t("activities.empty")}</p>
            <Button asChild size="sm">
              <Link to="/admin/activities/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("activities.create")}
              </Link>
            </Button>
          </div>
        ) : (
        <div className="space-y-2">
          <BulkActionBar
            selectedCount={selectedIds.size}
            bulkDeleting={bulkDeleting}
            onClear={clearSelection}
            onBulkDelete={() => void handleBulkDelete()}
          />
          <div className="overflow-x-auto rounded-lg border">
            <table className="min-w-[800px] w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="w-10 px-3 py-3 text-left">
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
                  </th>
                  <th className="px-3 py-3 text-left font-medium">{t("activities.col.post")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("activities.col.category")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("activities.col.author")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("common.date")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {filtered.map((a) => (
                  <tr key={a.id} className="hover:bg-muted/40 transition">
                    <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        checked={selectedIds.has(a.id)}
                        onCheckedChange={(v) => toggleOne(a.id, v === true)}
                        aria-label={`${t("common.selectAll")} · ${a.title}`}
                      />
                    </td>
                    <td className="px-3 py-3">
                      <Link to={`/admin/activities/${a.slug}`} className="flex items-center gap-3 hover:opacity-80 transition">
                        <img src={a.imageUrl} alt="" className="h-12 w-12 rounded-lg object-cover" />
                        <p className="font-medium line-clamp-2 max-w-md">{a.title}</p>
                      </Link>
                    </td>
                    <td className="whitespace-nowrap px-3 py-3">
                      {a.category ? (
                        <Badge variant="outline" className={cn("whitespace-nowrap font-medium border-indigo-200 bg-indigo-50 text-indigo-700")}>
                          {a.category}
                        </Badge>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{a.author ?? "—"}</td>
                    <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{a.date}</td>
                    <td className="whitespace-nowrap px-3 py-3 text-right">
                      <div className="inline-flex items-center gap-1">
                        <Button asChild variant="ghost" size="icon" title={t("common.view")} aria-label={t("common.view")}>
                          <Link to={`/admin/activities/${a.slug}`}>
                            <Eye className="h-4 w-4" />
                          </Link>
                        </Button>
                        <Button asChild variant="ghost" size="icon" title={t("common.edit")} aria-label={t("common.edit")}>
                          <Link to={`/admin/activities/${a.slug}/edit`}>
                            <Edit className="h-4 w-4" />
                          </Link>
                        </Button>
                        <Button variant="ghost" size="icon" onClick={() => handleDelete(a)} title={t("common.delete")} aria-label={t("common.delete")}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
        )}
        </>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminActivities;
