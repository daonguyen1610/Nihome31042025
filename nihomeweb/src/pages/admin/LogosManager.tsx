import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  ImageIcon,
  Plus,
  Pencil,
  Trash2,
  ExternalLink,
  Upload,
  Save,
  Trophy,
  X,
  Search,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useLogos } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { adminApi, type UpsertLogoRequest } from "@/services/adminApi";
import type { LogoResponse } from "@/services/contentApi";
import { PageLoading, PageError } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type Kind = "clients" | "partners" | "suppliers" | "awards";

type LogoFormData = {
  id: number | null;
  name: string;
  imageUrl: string;
  href: string;
  sortOrder: number;
};

const emptyForm: LogoFormData = {
  id: null,
  name: "",
  imageUrl: "",
  href: "",
  sortOrder: 0,
};

const kindMap: Record<Kind, string> = {
  clients: "Client",
  partners: "Partner",
  suppliers: "Supplier",
  awards: "Award",
};

function getErrorMessage(error: unknown) {
  if (typeof error === "object" && error !== null) {
    const withResponse = error as {
      message?: unknown;
      response?: {
        data?: {
          detail?: unknown;
          message?: unknown;
          title?: unknown;
          errors?: Record<string, unknown>;
        };
      };
    };

    const data = withResponse.response?.data;
    if (typeof data?.detail === "string") return data.detail;
    if (typeof data?.message === "string") return data.message;
    if (data?.errors && typeof data.errors === "object") {
      for (const value of Object.values(data.errors)) {
        if (typeof value === "string" && value.trim()) return value;
        if (Array.isArray(value)) {
          const first = value.find(
            (item) => typeof item === "string" && item.trim(),
          );
          if (typeof first === "string") return first;
        }
      }
    }
    if (typeof data?.title === "string") return data.title;
    if (typeof withResponse.message === "string") return withResponse.message;
  }

  return null;
}

const LogosManager = ({ kind, titleKey }: { kind: Kind; titleKey: string }) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: logos, loading, error, refetch } = useLogos();
  const [form, setForm] = useState<LogoFormData>(emptyForm);
  const [submitting, setSubmitting] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [openModal, setOpenModal] = useState(false);
  const [q, setQ] = useState("");

  const items = useMemo(() => {
    const source = logos?.[kind] ?? [];
    const sorted = [...source].sort(
      (a, b) =>
        (a.sortOrder ?? Number.MAX_SAFE_INTEGER) -
        (b.sortOrder ?? Number.MAX_SAFE_INTEGER),
    );
    if (!q.trim()) return sorted;
    return sorted.filter((item) =>
      matchesSearch(item.name, q) ||
      matchesSearch(item.href, q),
    );
  }, [logos, kind, q]);

  const totalCount = (logos?.[kind] ?? []).length;

  const isEditing = form.id != null;
  const isBusy = submitting || uploading;
  const isAwards = kind === "awards";
  const modalTitle = isAwards
    ? isEditing
      ? t("logoAdmin.awardEditTitle")
      : t("logoAdmin.awardCreateTitle")
    : isEditing
      ? t("logoAdmin.editTitle")
      : t("logoAdmin.createTitle");
  const modalDescription = isAwards
    ? isEditing
      ? t("logoAdmin.awardEditDesc")
      : t("logoAdmin.awardCreateDesc")
    : isEditing
      ? t("logoAdmin.logoEditDesc")
      : t("logoAdmin.logoCreateDesc");
  const nameLabel = isAwards
    ? t("logoAdmin.awardFieldName")
    : t("logoAdmin.fieldName");
  const namePlaceholder = isAwards
    ? t("logoAdmin.awardPlaceholderName")
    : t("logoAdmin.placeholderName");
  const previewBadge = isAwards
    ? t("logoAdmin.awardBadge")
    : t("logoAdmin.logoBadge");
  const gridClassName = isAwards
    ? "grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4"
    : "grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3 lg:gap-4";
  const cardClassName = isAwards
    ? "rounded-lg border bg-card p-4 transition hover:shadow-md"
    : "rounded-lg border bg-card p-3 transition hover:shadow-md";
  const imageFrameClassName = isAwards
    ? "aspect-[4/3] rounded-md border bg-white flex items-center justify-center overflow-hidden mb-3"
    : "aspect-[16/10] rounded-md border bg-white flex items-center justify-center overflow-hidden mb-2.5";

  const updateForm = <K extends keyof LogoFormData>(
    key: K,
    value: LogoFormData[K],
  ) => setForm((prev) => ({ ...prev, [key]: value }));

  const startCreate = () => {
    const maxSortOrder = items.reduce(
      (max, item) => Math.max(max, item.sortOrder ?? 0),
      0,
    );
    setForm({
      ...emptyForm,
      sortOrder: maxSortOrder + 1,
    });
    setOpenModal(true);
  };

  const startEdit = (item: LogoResponse) => {
    setForm({
      id: item.id,
      name: item.name,
      imageUrl: item.imageUrl,
      href: item.href ?? "",
      sortOrder: item.sortOrder ?? 0,
    });
    setOpenModal(true);
  };

  const pickImageFile = async () => {
    const file = await new Promise<File | null>((resolve) => {
      const input = document.createElement("input");
      input.type = "file";
      input.accept = "image/*";
      input.onchange = () => resolve(input.files?.[0] ?? null);
      input.click();
    });

    return file;
  };

  const uploadImage = async () => {
    const file = await pickImageFile();
    if (!file) return;

    setUploading(true);
    try {
      const previousImageUrl =
        isEditing && form.imageUrl ? form.imageUrl : undefined;
      const res = await adminApi.uploadImage(file, previousImageUrl, "logos");
      updateForm("imageUrl", res.data.imageUrl);
      toast({ title: t("logoAdmin.uploadSuccess") });
    } catch (error) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error) ?? t("logoAdmin.fallbackError"),
        variant: "destructive",
      });
    } finally {
      setUploading(false);
    }
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.name.trim() || !form.imageUrl.trim()) {
      toast({
        title: t("form.required"),
        description: t("logoAdmin.requiredNameImage"),
        variant: "destructive",
      });
      return;
    }

    const payload: UpsertLogoRequest = {
      name: form.name.trim(),
      imageUrl: form.imageUrl.trim(),
      href: form.href.trim() || undefined,
      kind: kindMap[kind],
      sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
    };

    setSubmitting(true);
    try {
      if (isEditing && form.id != null) {
        await adminApi.updateLogo(form.id, payload);
        toast({ title: t("form.updated"), description: form.name.trim() });
      } else {
        await adminApi.createLogo(payload);
        toast({ title: t("form.created"), description: form.name.trim() });
      }

      setForm(emptyForm);
      setOpenModal(false);
      await refetch();
    } catch (error) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error) ?? t("logoAdmin.fallbackError"),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  const remove = async (item: LogoResponse) => {
    if (!window.confirm(`${t("logoAdmin.confirmDelete")} "${item.name}"?`))
      return;

    try {
      await adminApi.deleteLogo(item.id);
      toast({ title: t("form.deleted"), description: item.name });
      if (form.id === item.id) {
        setForm(emptyForm);
        setOpenModal(false);
      }
      await refetch();
    } catch (error) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error) ?? t("logoAdmin.fallbackError"),
        variant: "destructive",
      });
    }
  };

  const visibleIds = useMemo(() => items.map((i) => i.id), [items]);
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
    deleteOne: (id) => adminApi.deleteLogo(id),
    onAfter: async () => {
      await refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [kind, q, clearSelection]);

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename(`admin-${kind}`),
      columns: [
        { header: "ID", value: "id" },
        { header: t("logoAdmin.fieldName"), value: "name" },
        { header: "Kind", value: "kind" },
        { header: t("logoAdmin.fieldImage"), value: "imageUrl" },
        { header: t("logoAdmin.fieldHref"), value: (row) => row.href ?? "" },
        { header: t("logoAdmin.fieldSortOrder"), value: (row) => row.sortOrder ?? 0 },
      ],
      rows: items,
    });
  };

  if (loading)
    return (
      <AdminLayout>
        <PageLoading />
      </AdminLayout>
    );
  if (error)
    return (
      <AdminLayout>
        <PageError message={error} onRetry={refetch} />
      </AdminLayout>
    );

  return (
    <AdminLayout>
      <div className={`admin-logo-manager space-y-4 p-4 sm:p-6 ${isAwards ? "is-awards" : ""}`}>
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t(titleKey)}</h1>
            <p className="text-xs italic text-muted-foreground">
              {q.trim() ? `${items.length} / ${totalCount}` : totalCount} {t("logoAdmin.logoNoun")}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminExportButton onClick={handleExport} disabled={items.length === 0} />
            <Button type="button" onClick={startCreate}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("logoAdmin.add")}
            </Button>
          </div>
        </header>

        <section className="rounded-lg border bg-card p-3">
          <div className="w-full sm:max-w-sm">
            <Label className="text-xs" htmlFor="logo-search">{t("common.search")}</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="logo-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("logoAdmin.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
        </section>

        {items.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              {isAwards ? <Trophy className="h-5 w-5" aria-hidden /> : <ImageIcon className="h-5 w-5" aria-hidden />}
            </div>
            <p>{t("common.noData")}</p>
            <Button type="button" size="sm" onClick={startCreate}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("logoAdmin.add")}
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
            <div className={gridClassName}>
              {items.map((item) => (
                <div key={item.id} className={`${cardClassName} relative`}>
                  <div
                    className="absolute top-2 right-2 z-10 rounded bg-white/90 p-1 shadow"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <Checkbox
                      checked={selectedIds.has(item.id)}
                      onCheckedChange={(v) => toggleOne(item.id, v === true)}
                      aria-label={`${t("common.selectAll")} · ${item.name}`}
                    />
                  </div>
                  <div className={imageFrameClassName}>
                    <img
                      src={item.imageUrl}
                      alt={item.name}
                      className="max-w-full max-h-full object-contain p-2"
                    />
                  </div>

                  <p
                    className={`text-sm font-medium leading-tight ${isAwards ? "line-clamp-3 min-h-14" : "line-clamp-2 min-h-10"}`}
                  >
                    {item.name}
                  </p>
                  <div className="text-xs mt-1 text-muted-foreground">
                    {t("logoAdmin.sortOrderLabel")}: {item.sortOrder ?? 0}
                  </div>
                  {item.href && (
                    <a
                      href={item.href}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex max-w-full min-w-0 items-center gap-1 text-xs mt-1.5 text-primary hover:underline"
                    >
                      <ExternalLink className="h-3 w-3 shrink-0" />{" "}
                      <span className="truncate">{item.href}</span>
                    </a>
                  )}

                  <div className="mt-3 grid grid-cols-2 gap-1.5">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => startEdit(item)}
                    >
                      <Pencil className="mr-1 h-3.5 w-3.5" />
                      <span className="truncate">{t("common.edit")}</span>
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => remove(item)}
                      className="text-destructive hover:text-destructive"
                    >
                      <Trash2 className="mr-1 h-3.5 w-3.5" />
                      <span className="truncate">{t("common.delete")}</span>
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        <Dialog open={openModal} onOpenChange={setOpenModal}>
          <DialogContent
            className={`admin-scope logo-modal ${isAwards ? "is-awards" : ""} p-0 overflow-hidden gap-0 sm:max-w-3xl`}
          >
            <DialogHeader className="px-5 sm:px-7 pt-5 sm:pt-6 pb-4 border-b">
              <DialogTitle className="flex items-center gap-3 text-xl leading-tight sm:text-2xl">
                <span className="flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary">
                  {isAwards ? <Trophy className="h-5 w-5" /> : <ImageIcon className="h-5 w-5" />}
                </span>
                {modalTitle}
              </DialogTitle>
              <DialogDescription className="mt-2 text-sm">
                {modalDescription}
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={save}>
              <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_320px] gap-0">
                <div className="px-5 sm:px-6 py-5 space-y-4">
                  <Field label={`${nameLabel} *`}>
                    <Input
                      value={form.name}
                      onChange={(e) => updateForm("name", e.target.value)}
                      placeholder={namePlaceholder}
                      required
                    />
                  </Field>

                  <Field label={`${t("logoAdmin.fieldImage")} *`}>
                    <div className="space-y-2">
                      <Button
                        type="button"
                        variant="outline"
                        onClick={uploadImage}
                        disabled={uploading}
                        className="w-full h-11"
                      >
                        <Upload className="mr-2 h-4 w-4" />
                        {uploading ? t("logoAdmin.uploading") : t("logoAdmin.upload")}
                      </Button>
                      <details>
                        <summary className="cursor-pointer text-xs text-muted-foreground hover:text-foreground select-none">
                          {t("media.url.toggle")}
                        </summary>
                        <Input
                          value={form.imageUrl}
                          onChange={(e) => updateForm("imageUrl", e.target.value)}
                          placeholder={t("media.url.placeholder")}
                          className="mt-2"
                        />
                      </details>
                    </div>
                  </Field>

                  <div className="grid grid-cols-1 sm:grid-cols-[minmax(0,1fr)_180px] gap-3">
                    <Field label={t("logoAdmin.fieldHref")}>
                      <Input
                        value={form.href}
                        onChange={(e) => updateForm("href", e.target.value)}
                        placeholder="https://..."
                      />
                    </Field>

                    <Field label={t("logoAdmin.fieldSortOrder")}>
                      <Input
                        type="number"
                        value={form.sortOrder}
                        onChange={(e) => updateForm("sortOrder", Number(e.target.value))}
                        min={0}
                      />
                    </Field>
                  </div>
                </div>

                <aside className="border-t lg:border-t-0 lg:border-l bg-muted/30 px-5 sm:px-6 py-5">
                  <LogoPreview
                    badge={previewBadge}
                    isAward={isAwards}
                    name={form.name}
                    imageUrl={form.imageUrl}
                    sortOrder={form.sortOrder}
                    placeholderName={namePlaceholder}
                    t={t}
                  />
                </aside>
              </div>

              <DialogFooter className="border-t px-5 sm:px-7 py-4 bg-muted/20">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setOpenModal(false)}
                >
                  <X className="mr-1.5 h-4 w-4" /> {t("common.cancel")}
                </Button>
                <Button type="submit" disabled={isBusy} className="min-w-[140px]">
                  <Save className="mr-1.5 h-4 w-4" />
                  {isEditing ? t("form.update") : t("form.create")}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </div>
    </AdminLayout>
  );
};

const Field = ({ label, children }: { label: string; children: ReactNode }) => (
  <div className="space-y-1.5">
    <Label className="text-xs">{label}</Label>
    {children}
  </div>
);

const LogoPreview = ({
  badge,
  isAward,
  name,
  imageUrl,
  sortOrder,
  placeholderName,
  t,
}: {
  badge: string;
  isAward: boolean;
  name: string;
  imageUrl: string;
  sortOrder: number;
  placeholderName: string;
  t: (key: string) => string;
}) => {
  const displayName = name.trim() || placeholderName;
  const displayOrder = Number.isFinite(sortOrder) ? sortOrder : 0;
  const hasImage = imageUrl.trim().length > 0;

  return (
    <>
      <p className="logo-preview-heading">
        {t("logoAdmin.previewTitle")}
      </p>
      <div className="logo-preview-card">
        <div className="logo-preview-topline">
          <span className="logo-preview-badge">
            {isAward ? (
              <Trophy className="w-4 h-4" />
            ) : (
              <ImageIcon className="w-4 h-4" />
            )}
            {badge}
          </span>
        </div>

        <div className="logo-preview-image">
          {hasImage ? (
            <img src={imageUrl} alt={displayName} />
          ) : (
            <div className="logo-preview-empty">
              <ImageIcon className="w-7 h-7" />
              <span>{t("logoAdmin.fieldImage")}</span>
            </div>
          )}
        </div>

        <div className="logo-preview-copy">
          <h3>{displayName}</h3>
          <p>
            #{displayOrder} · {t("logoAdmin.fieldSortOrder")}
          </p>
        </div>
      </div>
    </>
  );
};

export default LogosManager;
