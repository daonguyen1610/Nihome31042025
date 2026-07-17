import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Search, Edit, Trash2, Eye } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useNews, useNewsCategories } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { adminApi } from "@/services/adminApi";
import type { NewsResponse } from "@/services/contentApi";
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

const AdminNews = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [q, setQ] = useState("");
  const [catId, setCatId] = useState<number | "all">("all");
  const { data: items, loading, error, refetch } = useNews();
  const { data: categoryMaster } = useNewsCategories(true);

  const list = useMemo(() => items ?? [], [items]);
  const categoriesById = useMemo(
    () => new Map((categoryMaster ?? []).map((c) => [c.id, c])),
    [categoryMaster],
  );
  const filterOptions = useMemo(() => {
    const ids = new Set<number>();
    (categoryMaster ?? []).forEach((c) => ids.add(c.id));
    list.forEach((a) => { if (a.newsCategoryId != null) ids.add(a.newsCategoryId); });
    return Array.from(ids)
      .map((id) => ({ id, label: resolveCategoryLabel(id, undefined, categoriesById, lang) }))
      .filter((opt) => opt.label)
      .sort((a, b) => a.label.localeCompare(b.label, lang));
  }, [categoryMaster, list, categoriesById, lang]);
  const filtered = useMemo(
    () =>
      list.filter((a) => {
        const matchCat = catId === "all" || a.newsCategoryId === catId;
        if (!matchCat) return false;
        if (!q.trim()) return true;
        return matchesSearch(a.title, q) || matchesSearch(a.category, q) || matchesSearch(a.slug, q);
      }),
    [list, catId, q],
  );

  const handleDelete = async (a: NewsResponse) => {
    if (!confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteNews(a.id);
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
    deleteOne: (id) => adminApi.deleteNews(id),
    onAfter: async () => {
      refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [catId, q, clearSelection]);

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename("admin-news"),
      columns: [
        { header: "ID", value: "id" },
        { header: "Slug", value: "slug" },
        { header: t("adminNews.col.post"), value: "title" },
        { header: t("adminNews.col.category"), value: (row) => resolveCategoryLabel(row.newsCategoryId, row.category, categoriesById, lang) },
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
            <h1 className="text-2xl font-semibold">{t("adminNews.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {filtered.length} {t("common.showing")}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
            <Button asChild>
              <Link to="/admin/news/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("adminNews.create")}
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
            <Label className="text-xs" htmlFor="news-search">{t("common.search")}</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="news-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("adminNews.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
          <div className="w-full sm:w-[200px]">
            <Label className="text-xs" htmlFor="news-category">{t("adminNews.col.category")}</Label>
            <Select
              value={catId === "all" ? "all" : String(catId)}
              onValueChange={(v) => setCatId(v === "all" ? "all" : Number(v))}
            >
              <SelectTrigger id="news-category" className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t("adminNews.allCategories")}</SelectItem>
                {filterOptions.map((opt) => (
                  <SelectItem key={opt.id} value={String(opt.id)}>
                    {opt.label}
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
            <p>{t("adminNews.empty")}</p>
            <Button asChild size="sm">
              <Link to="/admin/news/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("adminNews.create")}
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

          {/* Mobile / tablet card view (<lg) */}
          <ul className="grid gap-3 lg:hidden">
            {filtered.map((a) => {
              const label = resolveCategoryLabel(a.newsCategoryId, a.category, categoriesById, lang);
              return (
                <li key={a.id} className="rounded-lg border bg-card p-3 shadow-sm">
                  <div className="flex items-start gap-2">
                    <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                      <Checkbox
                        checked={selectedIds.has(a.id)}
                        onCheckedChange={(v) => toggleOne(a.id, v === true)}
                        aria-label={`${t("common.selectAll")} · ${a.title}`}
                      />
                    </span>
                    <Link
                      to={`/admin/news/${a.slug}`}
                      className="flex min-w-0 flex-1 items-start gap-2 hover:opacity-80 transition"
                    >
                      <img src={a.imageUrl} alt="" className="h-12 w-12 shrink-0 rounded-lg object-cover" />
                      <div className="min-w-0">
                        <h3 className="line-clamp-2 break-words text-sm font-semibold leading-tight">{a.title}</h3>
                        {label && (
                          <Badge
                            variant="outline"
                            className="mt-1 whitespace-normal border-indigo-200 bg-indigo-50 font-medium text-indigo-700"
                          >
                            {label}
                          </Badge>
                        )}
                      </div>
                    </Link>
                  </div>
                  <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
                    <dt className="text-muted-foreground">{t("common.date")}</dt>
                    <dd className="font-medium">{a.date}</dd>
                  </dl>
                  <div className="mt-3 flex items-center justify-end gap-1 border-t pt-2">
                    <Button asChild variant="ghost" size="icon" title={t("common.view")} aria-label={t("common.view")}>
                      <Link to={`/admin/news/${a.slug}`}>
                        <Eye className="h-4 w-4" />
                      </Link>
                    </Button>
                    <Button asChild variant="ghost" size="icon" title={t("common.edit")} aria-label={t("common.edit")}>
                      <Link to={`/admin/news/${a.slug}/edit`}>
                        <Edit className="h-4 w-4" />
                      </Link>
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => handleDelete(a)}
                      title={t("common.delete")}
                      aria-label={t("common.delete")}
                      className="text-destructive hover:text-destructive"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </li>
              );
            })}
          </ul>

          {/* Desktop table (lg+) */}
          <div className="hidden overflow-x-auto rounded-lg border lg:block">
            <table className="min-w-[720px] w-full divide-y text-sm">
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
                  <th className="px-3 py-3 text-left font-medium">{t("adminNews.col.post")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("adminNews.col.category")}</th>
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
                      <Link to={`/admin/news/${a.slug}`} className="flex items-center gap-3 hover:opacity-80 transition">
                        <img src={a.imageUrl} alt="" className="h-12 w-12 rounded-lg object-cover" />
                        <p className="font-medium line-clamp-2 max-w-md">{a.title}</p>
                      </Link>
                    </td>
                    <td className="whitespace-nowrap px-3 py-3">
                      {(() => {
                        const label = resolveCategoryLabel(a.newsCategoryId, a.category, categoriesById, lang);
                        return label ? (
                          <Badge variant="outline" className={cn("whitespace-nowrap font-medium border-indigo-200 bg-indigo-50 text-indigo-700")}>
                            {label}
                          </Badge>
                        ) : (
                          <span className="text-xs text-muted-foreground">—</span>
                        );
                      })()}
                    </td>
                    <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{a.date}</td>
                    <td className="whitespace-nowrap px-3 py-3 text-right">
                      <div className="inline-flex items-center gap-1">
                        <Button asChild variant="ghost" size="icon" title={t("common.view")} aria-label={t("common.view")}>
                          <Link to={`/admin/news/${a.slug}`}>
                            <Eye className="h-4 w-4" />
                          </Link>
                        </Button>
                        <Button asChild variant="ghost" size="icon" title={t("common.edit")} aria-label={t("common.edit")}>
                          <Link to={`/admin/news/${a.slug}/edit`}>
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

export default AdminNews;
