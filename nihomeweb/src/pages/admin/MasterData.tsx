import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  type MasterDataCategory,
  type MasterDataOption,
  type UpsertMasterDataOptionRequest,
} from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { PageLoading, PageError } from "@/components/PageState";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

// Mirror the backend regex: code is lowercase, digits, dash/underscore.
const CODE_RE = /^[a-z0-9][a-z0-9_-]{0,79}$/;

type FormData = {
  code: string;
  name: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
};

const emptyForm: FormData = {
  code: "",
  name: "",
  description: "",
  isActive: true,
  sortOrder: 0,
};

const getErrorMessage = (error: unknown): string | undefined => {
  if (
    typeof error === "object" &&
    error !== null &&
    "response" in error &&
    typeof error.response === "object" &&
    error.response !== null &&
    "data" in error.response &&
    typeof error.response.data === "object" &&
    error.response.data !== null
  ) {
    const data = error.response.data as { detail?: unknown; message?: unknown };
    if (typeof data.detail === "string") return data.detail;
    if (typeof data.message === "string") return data.message;
  }
  return undefined;
};

const categoryLabel = (
  t: (key: string) => string,
  category: string,
): string => {
  // Reuse the existing seed convention: masterData.category.<category>.title
  const key = `masterData.category.${category}.title`;
  const translated = t(key);
  // Fallback to the raw category if no translation exists yet.
  return translated === key ? category : translated;
};

const MasterData = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.masterDataManage);

  const [searchParams, setSearchParams] = useSearchParams();

  const [categories, setCategories] = useState<MasterDataCategory[]>([]);
  const [loadingCategories, setLoadingCategories] = useState(true);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);

  const [activeCategory, setActiveCategory] = useState<string>("");
  const [options, setOptions] = useState<MasterDataOption[]>([]);
  const [loadingOptions, setLoadingOptions] = useState(false);
  const [optionsError, setOptionsError] = useState<string | null>(null);

  const [query, setQuery] = useState("");

  const loadCategories = useCallback(async () => {
    setLoadingCategories(true);
    setCategoriesError(null);
    try {
      const { data } = await adminApi.listMasterDataCategories();
      const sorted = [...data].sort((a, b) => a.category.localeCompare(b.category));
      setCategories(sorted);
      // If no category selected yet, prefer the ?category= param or the first one.
      const wanted = searchParams.get("category");
      const initial = wanted && sorted.some((c) => c.category === wanted)
        ? wanted
        : sorted[0]?.category ?? "";
      setActiveCategory((current) => current || initial);
    } catch (err) {
      setCategoriesError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setLoadingCategories(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [t]);

  const loadOptions = useCallback(
    async (category: string) => {
      if (!category) return;
      setLoadingOptions(true);
      setOptionsError(null);
      try {
        // include inactive so admins can see and re-enable them
        const { data } = await adminApi.listMasterDataByCategory(category, true);
        const sorted = [...data].sort((a, b) => {
          if (a.sortOrder !== b.sortOrder) return a.sortOrder - b.sortOrder;
          return a.code.localeCompare(b.code);
        });
        setOptions(sorted);
      } catch (err) {
        setOptionsError(getErrorMessage(err) ?? t("common.error"));
      } finally {
        setLoadingOptions(false);
      }
    },
    [t],
  );

  useEffect(() => {
    void loadCategories();
  }, [loadCategories]);

  useEffect(() => {
    void loadOptions(activeCategory);
  }, [activeCategory, loadOptions]);

  const onChangeCategory = (next: string) => {
    setActiveCategory(next);
    setQuery("");
    setSearchParams((prev) => {
      const clone = new URLSearchParams(prev);
      clone.set("category", next);
      return clone;
    });
  };

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter(
      (o) =>
        o.code.toLowerCase().includes(q) ||
        o.name.toLowerCase().includes(q),
    );
  }, [options, query]);

  const visibleIds = useMemo(() => filtered.map((o) => o.id), [filtered]);
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
    deleteOne: (id) => adminApi.deleteMasterDataOption(id),
    onAfter: async () => {
      await loadOptions(activeCategory);
      await loadCategories();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [activeCategory, query, clearSelection]);

  // -------- form / dialog --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormData>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: options.length });
    setFormError(null);
    setDialogOpen(true);
  };
  const openEdit = (option: MasterDataOption) => {
    setEditingId(option.id);
    setForm({
      code: option.code,
      name: option.name,
      description: option.description ?? "",
      isActive: option.isActive,
      sortOrder: option.sortOrder,
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const submit = async () => {
    setFormError(null);
    const code = form.code.trim();
    const name = form.name.trim();
    if (!code || !name) {
      setFormError(t("form.required"));
      return;
    }
    if (!CODE_RE.test(code)) {
      setFormError(t("masterData.codeInvalid"));
      return;
    }
    const payload: UpsertMasterDataOptionRequest = {
      code,
      name,
      description: form.description.trim() || null,
      isActive: form.isActive,
      sortOrder: Number.isFinite(form.sortOrder) ? Math.max(0, form.sortOrder) : 0,
    };
    setSaving(true);
    try {
      if (editingId != null) {
        await adminApi.updateMasterDataOption(editingId, payload);
        toast({ title: t("form.updated") });
      } else {
        await adminApi.createMasterDataOption(activeCategory, payload);
        toast({ title: t("form.created") });
      }
      setDialogOpen(false);
      await loadOptions(activeCategory);
      await loadCategories();
    } catch (err) {
      setFormError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (option: MasterDataOption) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteMasterDataOption(option.id);
      toast({ title: t("form.deleted") });
      await loadOptions(activeCategory);
      await loadCategories();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err),
        variant: "destructive",
      });
    }
  };

  if (loadingCategories) {
    return (
      <AdminLayout>
        <div className="p-4 sm:p-6">
          <PageLoading />
        </div>
      </AdminLayout>
    );
  }
  if (categoriesError) {
    return (
      <AdminLayout>
        <div className="p-4 sm:p-6">
          <PageError message={categoriesError} onRetry={() => void loadCategories()} />
        </div>
      </AdminLayout>
    );
  }

  const activeMeta = categories.find((c) => c.category === activeCategory);

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <h1 className="text-2xl font-semibold">{t("masterData.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("masterData.subtitle")}</p>
          </div>
          {canManage && activeCategory && (
            <Button onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("masterData.newOption")}
            </Button>
          )}
        </header>

        {/* Category picker + search + count */}
        <section className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
          <div className="min-w-[220px] flex-1 sm:max-w-sm">
            <Label className="text-xs" htmlFor="md-category">
              {t("masterData.category")}
            </Label>
            <Select value={activeCategory} onValueChange={onChangeCategory}>
              <SelectTrigger id="md-category" className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="max-h-72">
                {categories.map((c) => (
                  <SelectItem key={c.category} value={c.category}>
                    {categoryLabel(t, c.category)}
                    <span className="ml-2 text-xs text-muted-foreground">
                      ({c.activeCount}/{c.totalCount})
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-[200px] flex-1 sm:max-w-sm">
            <Label className="text-xs" htmlFor="md-search">
              {t("common.search")}
            </Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="md-search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={t("masterData.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
          {activeMeta && (
            <p className="text-xs italic text-muted-foreground">
              {activeMeta.activeCount} / {activeMeta.totalCount}
            </p>
          )}
        </section>

        {/* List */}
        {loadingOptions ? (
          <PageLoading />
        ) : optionsError ? (
          <PageError message={optionsError} onRetry={() => void loadOptions(activeCategory)} />
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <SearchIcon className="h-5 w-5" aria-hidden />
            </div>
            {options.length === 0 ? (
              <>
                <p>{t("masterData.empty")}</p>
                {canManage && (
                  <Button size="sm" onClick={openCreate}>
                    <Plus className="mr-1.5 h-4 w-4" /> {t("masterData.newOption")}
                  </Button>
                )}
              </>
            ) : (
              <>
                <p>{t("masterData.noMatch")}</p>
                <Button variant="outline" size="sm" onClick={() => setQuery("")}>
                  {t("common.reset")}
                </Button>
              </>
            )}
          </div>
        ) : (
          <div className="space-y-2">
            {canManage && (
              <BulkActionBar
                selectedCount={selectedIds.size}
                bulkDeleting={bulkDeleting}
                onClear={clearSelection}
                onBulkDelete={() => void handleBulkDelete()}
              />
            )}

            {/* Mobile / tablet card view (<lg) */}
            <ul className="grid gap-3 lg:hidden">
              {filtered.map((o) => (
                <li key={o.id} className="rounded-lg border bg-card p-3 shadow-sm">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex min-w-0 items-start gap-2">
                      {canManage && (
                        <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                          <Checkbox
                            checked={selectedIds.has(o.id)}
                            onCheckedChange={(v) => toggleOne(o.id, v === true)}
                            aria-label={`${t("common.selectAll")} · ${o.name}`}
                          />
                        </span>
                      )}
                      <div className="min-w-0">
                        <h3 className="break-words text-sm font-semibold leading-tight">{o.name}</h3>
                        <p className="mt-0.5 break-all font-mono text-xs text-muted-foreground">{o.code}</p>
                      </div>
                    </div>
                    {o.isActive ? (
                      <Check className="h-4 w-4 shrink-0 text-emerald-600" aria-label={t("masterData.field.isActive")} />
                    ) : (
                      <X className="h-4 w-4 shrink-0 text-muted-foreground" aria-label={t("masterData.field.isActive")} />
                    )}
                  </div>
                  <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
                    <dt className="text-muted-foreground">{t("masterData.field.sortOrder")}</dt>
                    <dd className="font-medium">{o.sortOrder}</dd>
                    {o.description && (
                      <>
                        <dt className="text-muted-foreground">{t("masterData.field.description")}</dt>
                        <dd className="break-words">{o.description}</dd>
                      </>
                    )}
                  </dl>
                  {canManage && (
                    <div className="mt-3 flex flex-wrap items-center justify-end gap-1 border-t pt-2">
                      <Button variant="ghost" size="sm" onClick={() => openEdit(o)}>
                        <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => remove(o)}
                        className="text-destructive hover:text-destructive"
                      >
                        <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                      </Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>

            {/* Desktop table (lg+) */}
            <div className="hidden overflow-x-auto rounded-lg border lg:block">
              <table className="w-full min-w-[900px] divide-y text-sm">
                <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    {canManage && (
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
                    )}
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("masterData.field.code")}</th>
                    <th className="min-w-[220px] px-3 py-3 text-left font-medium">{t("masterData.field.name")}</th>
                    <th className="min-w-[240px] px-3 py-3 text-left font-medium">{t("masterData.field.description")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("masterData.field.sortOrder")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("masterData.field.isActive")}</th>
                    {canManage && (
                      <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {filtered.map((o) => (
                    <tr key={o.id} className="hover:bg-muted/40 transition">
                      {canManage && (
                        <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={selectedIds.has(o.id)}
                            onCheckedChange={(v) => toggleOne(o.id, v === true)}
                            aria-label={`${t("common.selectAll")} · ${o.name}`}
                          />
                        </td>
                      )}
                      <td className="whitespace-nowrap px-3 py-3 font-mono text-xs">{o.code}</td>
                      <td className="min-w-[220px] px-3 py-3 font-medium">{o.name}</td>
                      <td className="min-w-[240px] break-words px-3 py-3 text-xs text-muted-foreground">
                        {o.description ?? "—"}
                      </td>
                      <td className="whitespace-nowrap px-3 py-3">{o.sortOrder}</td>
                      <td className="whitespace-nowrap px-3 py-3">
                        {o.isActive ? (
                          <Badge variant="outline" className="border-emerald-200 bg-emerald-50 text-emerald-700">
                            <Check className="mr-1 h-3 w-3" />
                            {t("common.on")}
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="border-slate-200 bg-slate-50 text-slate-600">
                            <X className="mr-1 h-3 w-3" />
                            {t("common.off")}
                          </Badge>
                        )}
                      </td>
                      {canManage && (
                        <td className="whitespace-nowrap px-3 py-3 text-right">
                          <div className="inline-flex items-center gap-1">
                            <Button variant="ghost" size="sm" onClick={() => openEdit(o)}>
                              <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => remove(o)}
                              className="text-destructive hover:text-destructive"
                            >
                              <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                            </Button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {/* Create / edit dialog */}
      <Dialog
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            setEditingId(null);
            setFormError(null);
          }
        }}
      >
        <DialogContent className="w-[95vw] max-w-xl max-h-[90vh] overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle>
              {editingId != null
                ? t("masterData.editModalTitle")
                : t("masterData.createModalTitle")}
            </DialogTitle>
            <DialogDescription>
              {categoryLabel(t, activeCategory)}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="md-code" className="text-xs">
                {t("masterData.field.code")} *
              </Label>
              <Input
                id="md-code"
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value.toLowerCase() })}
                placeholder="lowercase-slug"
                className="h-9 font-mono"
                disabled={editingId != null}
              />
              <p className="text-xs text-muted-foreground">{t("masterData.codeHint")}</p>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="md-name" className="text-xs">
                {t("masterData.field.name")} *
              </Label>
              <Input
                id="md-name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className="h-9"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="md-description" className="text-xs">
                {t("masterData.field.description")}
              </Label>
              <Textarea
                id="md-description"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                rows={2}
              />
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="md-sortOrder" className="text-xs">
                  {t("masterData.field.sortOrder")}
                </Label>
                <Input
                  id="md-sortOrder"
                  type="number"
                  min={0}
                  value={form.sortOrder}
                  onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) || 0 })}
                  className="h-9"
                />
              </div>
              <div className="flex items-end gap-2">
                <Switch
                  id="md-active"
                  checked={form.isActive}
                  onCheckedChange={(checked) => setForm({ ...form, isActive: checked })}
                  aria-label={t("masterData.field.isActive")}
                />
                <Label htmlFor="md-active" className="mb-2 text-sm font-medium">
                  {t("masterData.field.isActive")}
                </Label>
              </div>
            </div>

            {formError && <p className="text-sm text-destructive">{formError}</p>}
          </div>

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={saving}>
              {saving ? t("common.saving") : t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default MasterData;
