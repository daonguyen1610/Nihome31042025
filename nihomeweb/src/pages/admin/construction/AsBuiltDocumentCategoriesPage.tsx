import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  CircleOff,
  FileCheck2,
  Files,
  Pencil,
  Plus,
  RefreshCcw,
  Search as SearchIcon,
  Trash2,
  X,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { localizedName } from "@/lib/category";
import { extractApiError } from "@/lib/apiError";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import {
  adminApi,
  type AsBuiltDocumentCategoryResponse,
  type UpsertAsBuiltDocumentCategoryRequest,
} from "@/services/adminApi";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";

type StatusFilter = "all" | "active" | "inactive";

interface FormData {
  code: string;
  nameVi: string;
  nameEn: string;
  nameZh: string;
  nameJa: string;
  isRequired: boolean;
  isActive: boolean;
  sortOrder: number;
}

const emptyForm: FormData = {
  code: "",
  nameVi: "",
  nameEn: "",
  nameZh: "",
  nameJa: "",
  isRequired: false,
  isActive: true,
  sortOrder: 0,
};

export default function AsBuiltDocumentCategoriesPage() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionAsBuiltCategoriesManage);

  const [items, setItems] = useState<AsBuiltDocumentCategoryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [q, setQ] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [loadError, setLoadError] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormData>(emptyForm);
  const [formError, setFormError] = useState<string | null>(null);

  const [pendingDelete, setPendingDelete] = useState<AsBuiltDocumentCategoryResponse | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const res = await adminApi.getAsBuiltDocumentCategories(true);
      setItems(res.data ?? []);
    } catch (error) {
      const message = extractApiError(error) || t("asbuiltCat.error.load");
      setLoadError(message);
      toast({ title: t("common.error"), description: message, variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const filtered = useMemo(() => {
    const search = q.trim().toLowerCase();
    return items.filter((item) => {
      if (statusFilter === "active" && !item.isActive) return false;
      if (statusFilter === "inactive" && item.isActive) return false;
      if (!search) return true;
      return (
        item.code.toLowerCase().includes(search) ||
        localizedName(item, lang).toLowerCase().includes(search) ||
        item.nameVi.toLowerCase().includes(search) ||
        item.nameEn.toLowerCase().includes(search) ||
        item.nameZh.toLowerCase().includes(search) ||
        item.nameJa.toLowerCase().includes(search)
      );
    });
  }, [items, q, lang, statusFilter]);

  const summary = useMemo(
    () => ({
      total: items.length,
      active: items.filter((item) => item.isActive).length,
      required: items.filter((item) => item.isActive && item.isRequired).length,
      inUse: items.filter((item) => item.documentCount > 0).length,
    }),
    [items],
  );

  const openCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: items.length + 1 });
    setFormError(null);
    setDialogOpen(true);
  };

  const openEdit = (item: AsBuiltDocumentCategoryResponse) => {
    setEditingId(item.id);
    setForm({
      code: item.code,
      nameVi: item.nameVi || item.name,
      nameEn: item.nameEn || "",
      nameZh: item.nameZh || "",
      nameJa: item.nameJa || "",
      isRequired: item.isRequired,
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingId(null);
    setForm(emptyForm);
    setFormError(null);
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    const code = form.code.trim();
    const localizedNames = [form.nameVi, form.nameEn, form.nameZh, form.nameJa].map((name) => name.trim());
    if (!code) {
      setFormError(t("asbuiltCat.error.codeRequired"));
      return;
    }
    if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(code)) {
      setFormError(t("asbuiltCat.error.codeInvalid"));
      return;
    }
    if (code.length > 50) {
      setFormError(t("asbuiltCat.error.codeTooLong"));
      return;
    }
    if (localizedNames.some((name) => !name)) {
      setFormError(t("asbuiltCat.error.allNamesRequired"));
      return;
    }
    if (localizedNames.some((name) => name.length > 200)) {
      setFormError(t("asbuiltCat.error.nameTooLong"));
      return;
    }

    setSubmitting(true);
    try {
      const payload: UpsertAsBuiltDocumentCategoryRequest = {
        code,
        nameVi: localizedNames[0],
        nameEn: localizedNames[1],
        nameZh: localizedNames[2],
        nameJa: localizedNames[3],
        isRequired: form.isRequired,
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      if (editingId == null) {
        await adminApi.createAsBuiltDocumentCategory(payload);
        toast({ title: t("form.created") });
      } else {
        await adminApi.updateAsBuiltDocumentCategory(editingId, payload);
        toast({ title: t("form.saved") });
      }
      closeDialog();
      await loadData();
    } catch (err) {
      setFormError(extractApiError(err) || t("common.error"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    try {
      await adminApi.deleteAsBuiltDocumentCategory(id);
      toast({ title: t("form.deleted") });
      await loadData();
    } catch (err) {
      toast({
        title: extractApiError(err) || t("common.error"),
        variant: "destructive",
      });
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <h1 className="text-2xl font-semibold tracking-tight">{t("asbuiltCat.title")}</h1>
            <p className="max-w-2xl text-sm text-muted-foreground">{t("asbuiltCat.subtitle")}</p>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void loadData()} disabled={loading}>
              <RefreshCcw className={`mr-2 h-4 w-4 ${loading ? "animate-spin" : ""}`} />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate}>
                <Plus className="mr-2 h-4 w-4" />
                {t("asbuiltCat.action.new")}
              </Button>
            )}
          </div>
        </header>

        <section className="grid grid-cols-2 gap-3 lg:grid-cols-4" aria-label={t("asbuiltCat.summary.label")}>
          {[
            { key: "total", value: summary.total, icon: Files, label: t("asbuiltCat.summary.total") },
            { key: "active", value: summary.active, icon: CheckCircle2, label: t("asbuiltCat.summary.active") },
            { key: "required", value: summary.required, icon: FileCheck2, label: t("asbuiltCat.summary.required") },
            { key: "inUse", value: summary.inUse, icon: CircleOff, label: t("asbuiltCat.summary.inUse") },
          ].map((stat) => {
            const Icon = stat.icon;
            return (
              <div key={stat.key} className="rounded-xl border bg-card p-4 shadow-sm">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="text-2xl font-semibold tabular-nums">{stat.value}</p>
                    <p className="mt-1 text-xs text-muted-foreground">{stat.label}</p>
                  </div>
                  <div className="rounded-lg bg-primary/10 p-2 text-primary">
                    <Icon className="h-5 w-5" aria-hidden="true" />
                  </div>
                </div>
              </div>
            );
          })}
        </section>

        <section className="rounded-xl border bg-card p-3 shadow-sm">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div className="w-full lg:max-w-md">
              <Label className="text-xs" htmlFor="asbuilt-category-search">{t("common.search")}</Label>
              <div className="relative mt-1.5">
                <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="asbuilt-category-search"
                  placeholder={t("asbuiltCat.search.placeholder")}
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                  className="h-9 pl-9 pr-9"
                />
                {q && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="absolute right-0 top-0 h-9 w-9"
                    onClick={() => setQ("")}
                    aria-label={t("common.clearSearch")}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                )}
              </div>
            </div>
            <div className="flex w-full rounded-lg bg-muted p-1 sm:w-auto" role="group" aria-label={t("asbuiltCat.filter.status")}>
              {(["all", "active", "inactive"] as const).map((filter) => (
                <Button
                  key={filter}
                  type="button"
                  variant={statusFilter === filter ? "secondary" : "ghost"}
                  size="sm"
                  className="flex-1 shadow-none sm:flex-none"
                  onClick={() => setStatusFilter(filter)}
                  aria-pressed={statusFilter === filter}
                >
                  {t(`asbuiltCat.filter.${filter}`)}
                </Button>
              ))}
            </div>
          </div>
          <p className="mt-3 text-xs text-muted-foreground">
            {t("asbuiltCat.resultCount")
              .replace("{shown}", String(filtered.length))
              .replace("{total}", String(items.length))}
          </p>
        </section>

        {loadError ? (
          <div className="rounded-xl border border-destructive/30 bg-destructive/5 p-8 text-center">
            <p className="font-medium text-destructive">{t("asbuiltCat.error.loadTitle")}</p>
            <p className="mt-1 text-sm text-muted-foreground">{loadError}</p>
            <Button variant="outline" size="sm" className="mt-4" onClick={() => void loadData()}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.retry")}
            </Button>
          </div>
        ) : loading ? (
          <div className="rounded-xl border border-dashed p-12 text-center text-sm text-muted-foreground">
            {t("common.loading")}
          </div>
        ) : filtered.length === 0 ? (
          <div className="rounded-xl border border-dashed p-12 text-center">
            <p className="font-medium">{t("asbuiltCat.empty.title")}</p>
            <p className="mt-1 text-sm text-muted-foreground">{t("asbuiltCat.empty.description")}</p>
            {(q || statusFilter !== "all") && (
              <Button
                variant="outline"
                size="sm"
                className="mt-4"
                onClick={() => {
                  setQ("");
                  setStatusFilter("all");
                }}
              >
                {t("asbuiltCat.filter.clear")}
              </Button>
            )}
          </div>
        ) : (
          <>
            <ul className="grid gap-3 lg:hidden">
              {filtered.map((item) => (
                <li key={item.id} className="rounded-xl border bg-card p-4 shadow-sm">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <h2 className="break-words text-sm font-semibold">{localizedName(item, lang)}</h2>
                        <Badge variant="outline" className="font-mono font-normal">{item.code}</Badge>
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {t("asbuiltCat.documentCount").replace("{count}", String(item.documentCount))}
                      </p>
                    </div>
                    <Badge variant={item.isActive ? "default" : "secondary"} className="shrink-0">
                      {item.isActive ? t("common.active") : t("common.inactive")}
                    </Badge>
                  </div>
                  <dl className="mt-4 grid grid-cols-2 gap-3 rounded-lg bg-muted/50 p-3 text-xs">
                    <div>
                      <dt className="text-muted-foreground">{t("asbuiltCat.field.isRequired")}</dt>
                      <dd className="mt-1 font-medium">{item.isRequired ? t("common.yes") : t("common.no")}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("asbuiltCat.field.sortOrder")}</dt>
                      <dd className="mt-1 font-medium tabular-nums">{item.sortOrder}</dd>
                    </div>
                  </dl>
                  {canManage && (
                    <div className="mt-3 flex items-center justify-end gap-2 border-t pt-3">
                      <Button variant="ghost" size="sm" onClick={() => openEdit(item)}>
                        <Pencil className="mr-1.5 h-4 w-4" />
                        {t("common.edit")}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
                        onClick={() => item.documentCount === 0 && setPendingDelete(item)}
                        aria-disabled={item.documentCount > 0}
                        title={item.documentCount > 0 ? t("asbuiltCat.delete.inUseHint") : t("common.delete")}
                      >
                        <Trash2 className="mr-1.5 h-4 w-4" />
                        {t("common.delete")}
                      </Button>
                    </div>
                  )}
                  {canManage && item.documentCount > 0 && (
                    <p className="mt-2 text-right text-xs text-muted-foreground">{t("asbuiltCat.delete.inUseHint")}</p>
                  )}
                </li>
              ))}
            </ul>

            <div className="hidden overflow-x-auto rounded-xl border bg-card shadow-sm lg:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-44">{t("asbuiltCat.field.code")}</TableHead>
                  <TableHead>{t("asbuiltCat.field.name")}</TableHead>
                  <TableHead className="w-32 text-center">{t("asbuiltCat.field.documentCount")}</TableHead>
                  <TableHead className="w-28 text-center">{t("asbuiltCat.field.isRequired")}</TableHead>
                  <TableHead className="w-28 text-center">{t("asbuiltCat.field.isActive")}</TableHead>
                  <TableHead className="w-20 text-center">{t("asbuiltCat.field.sortOrder")}</TableHead>
                  {canManage && <TableHead className="w-28 text-right">{t("common.actions")}</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((item) => (
                  <TableRow key={item.id} className={!item.isActive ? "bg-muted/20" : undefined}>
                    <TableCell><Badge variant="outline" className="font-mono font-normal">{item.code}</Badge></TableCell>
                    <TableCell className="font-medium">{localizedName(item, lang)}</TableCell>
                    <TableCell className="text-center tabular-nums">{item.documentCount}</TableCell>
                    <TableCell className="text-center">
                      {item.isRequired ? (
                        <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">{t("common.yes")}</Badge>
                      ) : (
                        <span className="text-sm text-muted-foreground">{t("common.no")}</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.isActive ? (
                        <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100">{t("common.active")}</Badge>
                      ) : (
                        <Badge variant="secondary">{t("common.inactive")}</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-center">{item.sortOrder}</TableCell>
                    {canManage && (
                      <TableCell>
                        <div className="flex items-center justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => openEdit(item)}
                            aria-label={`${t("common.edit")} ${localizedName(item, lang)}`}
                            title={t("common.edit")}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="aria-disabled:cursor-not-allowed aria-disabled:opacity-50"
                            onClick={() => item.documentCount === 0 && setPendingDelete(item)}
                            aria-disabled={item.documentCount > 0}
                            aria-label={`${t("common.delete")} ${localizedName(item, lang)}`}
                            title={item.documentCount > 0 ? t("asbuiltCat.delete.inUseHint") : t("common.delete")}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          </>
        )}
      </div>

      <Dialog open={dialogOpen} onOpenChange={(open) => !open && closeDialog()}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {editingId ? t("asbuiltCat.form.titleEdit") : t("asbuiltCat.form.titleCreate")}
            </DialogTitle>
            <p className="text-sm text-muted-foreground">{t("asbuiltCat.form.description")}</p>
          </DialogHeader>
          <form onSubmit={submitForm} className="space-y-4">
            {formError && (
              <div role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">
                {formError}
              </div>
            )}
            <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_140px]">
            <div className="space-y-1.5">
              <Label htmlFor="code">{t("asbuiltCat.field.code")} *</Label>
              <Input
                id="code"
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value })}
                placeholder={t("asbuiltCat.form.codePlaceholder")}
                disabled={editingId != null}
                maxLength={50}
                autoFocus
                required
                aria-invalid={Boolean(formError && !form.code.trim())}
              />
              <p className="text-xs text-muted-foreground">
                {editingId != null ? t("asbuiltCat.hint.codeReadonly") : t("asbuiltCat.hint.codeFormat")}
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="sortOrder">{t("asbuiltCat.field.sortOrder")}</Label>
              <Input
                id="sortOrder"
                type="number"
                min={0}
                value={form.sortOrder}
                onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) })}
              />
              <p className="text-xs text-muted-foreground">{t("asbuiltCat.hint.sortOrder")}</p>
            </div>
            </div>

            <div className="rounded-lg border p-4">
              <div className="mb-4">
                <p className="text-sm font-medium">{t("asbuiltCat.form.localizedNames")}</p>
                <p className="mt-1 text-xs text-muted-foreground">{t("asbuiltCat.form.localizedNamesHint")}</p>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="nameVi">{t("asbuiltCat.field.nameVi")} *</Label>
              <Input
                id="nameVi"
                value={form.nameVi}
                onChange={(e) => setForm({ ...form, nameVi: e.target.value })}
                placeholder={t("asbuiltCat.form.nameViPlaceholder")}
                maxLength={200}
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="nameEn">{t("asbuiltCat.field.nameEn")} *</Label>
              <Input
                id="nameEn"
                value={form.nameEn}
                onChange={(e) => setForm({ ...form, nameEn: e.target.value })}
                placeholder={t("asbuiltCat.form.nameEnPlaceholder")}
                maxLength={200}
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="nameZh">{t("asbuiltCat.field.nameZh")} *</Label>
              <Input
                id="nameZh"
                value={form.nameZh}
                onChange={(e) => setForm({ ...form, nameZh: e.target.value })}
                placeholder={t("asbuiltCat.form.nameZhPlaceholder")}
                maxLength={200}
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="nameJa">{t("asbuiltCat.field.nameJa")} *</Label>
              <Input
                id="nameJa"
                value={form.nameJa}
                onChange={(e) => setForm({ ...form, nameJa: e.target.value })}
                placeholder={t("asbuiltCat.form.nameJaPlaceholder")}
                maxLength={200}
                required
              />
            </div>
            </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="flex items-start justify-between gap-4 rounded-lg border p-4">
                <div>
                  <Label htmlFor="isRequired">{t("asbuiltCat.field.isRequired")}</Label>
                  <p className="mt-1 text-xs text-muted-foreground">{t("asbuiltCat.hint.isRequired")}</p>
                </div>
                <Switch
                  id="isRequired"
                  checked={form.isRequired}
                  onCheckedChange={(v) => setForm({ ...form, isRequired: v })}
                />
              </div>
              <div className="flex items-start justify-between gap-4 rounded-lg border p-4">
                <div>
                  <Label htmlFor="isActive">{t("asbuiltCat.field.isActive")}</Label>
                  <p className="mt-1 text-xs text-muted-foreground">{t("asbuiltCat.hint.isActive")}</p>
                </div>
                <Switch
                  id="isActive"
                  checked={form.isActive}
                  onCheckedChange={(v) => setForm({ ...form, isActive: v })}
                />
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={closeDialog} disabled={submitting}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={submitting}>
                {submitting ? t("common.saving") : t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <AlertDialog open={!!pendingDelete} onOpenChange={(open) => !open && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("asbuiltCat.delete.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("asbuiltCat.delete.confirm").replace("{name}", pendingDelete?.nameVi || pendingDelete?.name || "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
              {t("common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AdminLayout>
  );
}
