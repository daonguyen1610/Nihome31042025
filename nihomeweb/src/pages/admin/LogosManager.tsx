import { useMemo, useState, type ReactNode } from "react";
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
import { adminApi, type UpsertLogoRequest } from "@/services/adminApi";
import type { LogoResponse } from "@/services/contentApi";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
    ? "logo-grid-card rounded-2xl border p-4 card-hover"
    : "logo-grid-card rounded-xl border p-3 card-hover";
  const imageFrameClassName = isAwards
    ? "aspect-[4/3] rounded-xl border bg-white flex items-center justify-center overflow-hidden mb-3"
    : "aspect-[16/10] rounded-lg border bg-white flex items-center justify-center overflow-hidden mb-2.5";

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
      const res = await adminApi.uploadImage(file, previousImageUrl);
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
      <div className={`admin-logo-manager ${isAwards ? "is-awards" : ""}`}>
        <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
          <div>
            <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
              {t(titleKey)}
            </h1>
            <p
              className="text-sm mt-1"
              style={{ color: "hsl(var(--admin-muted))" }}
            >
              {q.trim() ? `${items.length} / ${totalCount}` : totalCount} {t("logoAdmin.logoNoun")}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div
              className="flex items-center gap-2 rounded-full px-3 py-2 border"
              style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
            >
              <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("logoAdmin.searchPlaceholder")}
                className="bg-transparent outline-none text-sm w-48 placeholder:opacity-60"
              />
            </div>
            <AdminExportButton onClick={handleExport} disabled={items.length === 0} />
            <button
              onClick={startCreate}
              className="admin-btn-primary inline-flex items-center gap-2"
              type="button"
            >
              <Plus className="w-4 h-4" /> {t("logoAdmin.add")}
            </button>
          </div>
        </div>

        <div className="admin-card p-4">
          {items.length === 0 ? (
            <PageEmpty message={t("common.noData")} />
          ) : (
            <div className={gridClassName}>
              {items.map((item) => (
                <div
                  key={item.id}
                  className={cardClassName}
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <div
                    className={imageFrameClassName}
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <img
                      src={item.imageUrl}
                      alt={item.name}
                      className="max-w-full max-h-full object-contain p-2"
                    />
                  </div>

                  <p
                    className={`font-semibold text-sm leading-tight ${isAwards ? "line-clamp-3 min-h-14" : "line-clamp-2 min-h-10"}`}
                  >
                    {item.name}
                  </p>
                  <div
                    className="text-xs mt-1"
                    style={{ color: "hsl(var(--admin-muted))" }}
                  >
                    {t("logoAdmin.sortOrderLabel")}: {item.sortOrder ?? 0}
                  </div>
                  {item.href && (
                    <a
                      href={item.href}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex max-w-full min-w-0 items-center gap-1 text-xs mt-1.5"
                      style={{ color: "hsl(var(--admin-primary))" }}
                    >
                      <ExternalLink className="w-3 h-3 shrink-0" />{" "}
                      <span className="truncate">{item.href}</span>
                    </a>
                  )}

                  <div className="grid grid-cols-2 gap-1.5 mt-3">
                    <button
                      onClick={() => startEdit(item)}
                      className="inline-flex min-w-0 items-center justify-center gap-1.5 text-xs font-bold py-2 px-2 rounded-lg border hover:bg-muted"
                      style={{ borderColor: "hsl(var(--admin-border))" }}
                    >
                      <Pencil className="w-3.5 h-3.5 shrink-0" />{" "}
                      <span className="truncate">{t("common.edit")}</span>
                    </button>
                    <button
                      onClick={() => remove(item)}
                      className="inline-flex min-w-0 items-center justify-center gap-1.5 text-xs font-bold py-2 px-2 rounded-lg border hover:bg-destructive/10"
                      style={{
                        borderColor: "hsl(var(--admin-border))",
                        color: "hsl(var(--admin-danger))",
                      }}
                    >
                      <Trash2 className="w-3.5 h-3.5 shrink-0" />{" "}
                      <span className="truncate">{t("common.delete")}</span>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <Dialog open={openModal} onOpenChange={setOpenModal}>
          <DialogContent
            className={`admin-scope logo-modal ${isAwards ? "is-awards" : ""} p-0 overflow-hidden gap-0 rounded-3xl border shadow-2xl`}
            style={{ borderColor: "hsl(var(--admin-border))" }}
          >
            <DialogHeader
              className="logo-modal-header px-5 sm:px-7 pt-5 sm:pt-6 pb-5"
            >
              <DialogTitle className="font-display text-xl sm:text-2xl flex items-center gap-3 leading-tight">
                <span className="logo-modal-title-icon">
                  {isAwards ? (
                    <Trophy className="w-6 h-6" />
                  ) : (
                    <ImageIcon className="w-6 h-6" />
                  )}
                </span>
                {modalTitle}
              </DialogTitle>
              <DialogDescription
                className="logo-modal-description text-sm sm:text-base mt-2 max-w-3xl"
              >
                {modalDescription}
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={save} className="logo-modal-form">
              <div className="logo-modal-body grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_320px] gap-0">
                <div className="logo-modal-main px-5 sm:px-6 py-5 space-y-4">
                  <Field label={`${nameLabel} *`}>
                    <input
                      className="admin-input logo-name-input logo-styled-input w-full"
                      value={form.name}
                      onChange={(e) => updateForm("name", e.target.value)}
                      placeholder={namePlaceholder}
                      required
                    />
                  </Field>

                  <Field label={`${t("logoAdmin.fieldImage")} *`}>
                    <div className="logo-upload-wrap logo-upload-row space-y-2">
                      <button
                        type="button"
                        onClick={uploadImage}
                        disabled={uploading}
                        className="logo-upload-button w-full inline-flex items-center justify-center gap-2 rounded-xl px-4 h-11 text-sm border bg-white hover:bg-muted disabled:opacity-60"
                        style={{ borderColor: "hsl(var(--admin-border))" }}
                      >
                        <Upload className="w-4 h-4" />{" "}
                        {uploading
                          ? t("logoAdmin.uploading")
                          : t("logoAdmin.upload")}
                      </button>
                      <details>
                        <summary className="text-xs cursor-pointer text-muted-foreground hover:text-foreground select-none">
                          {t("media.url.toggle")}
                        </summary>
                        <input
                          className="admin-input logo-styled-input w-full bg-white mt-2"
                          value={form.imageUrl}
                          onChange={(e) => updateForm("imageUrl", e.target.value)}
                          placeholder={t("media.url.placeholder")}
                        />
                      </details>
                    </div>
                  </Field>

                  <div className="grid grid-cols-1 sm:grid-cols-[minmax(0,1fr)_180px] gap-3">
                    <Field label={t("logoAdmin.fieldHref")}>
                      <input
                        className="admin-input logo-styled-input w-full"
                        value={form.href}
                        onChange={(e) => updateForm("href", e.target.value)}
                        placeholder="https://..."
                      />
                    </Field>

                    <Field label={t("logoAdmin.fieldSortOrder")}>
                      <input
                        type="number"
                        className="admin-input logo-styled-input w-full"
                        value={form.sortOrder}
                        onChange={(e) =>
                          updateForm("sortOrder", Number(e.target.value))
                        }
                        min={0}
                      />
                    </Field>
                  </div>
                </div>

                <aside
                  className="logo-preview-pane border-t lg:border-t-0 lg:border-l px-5 sm:px-6 py-5"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
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

              <div
                className="logo-modal-footer px-5 sm:px-7 py-4 border-t flex flex-col-reverse sm:flex-row sm:items-center sm:justify-end gap-3"
                style={{
                  borderColor: "hsl(var(--admin-border))",
                  background: "hsl(var(--admin-bg))",
                }}
              >
                <button
                  type="button"
                  onClick={() => setOpenModal(false)}
                  className="logo-secondary-btn inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl border hover:bg-muted"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <X className="w-4 h-4" /> {t("common.cancel")}
                </button>
                <button
                  type="submit"
                  disabled={isBusy}
                  className="admin-btn-primary inline-flex items-center justify-center gap-2 px-5 py-2.5 min-w-[140px] disabled:opacity-60"
                >
                  <Save className="w-4 h-4" />{" "}
                  {isEditing ? t("form.update") : t("form.create")}
                </button>
              </div>
            </form>
          </DialogContent>
        </Dialog>
      </div>
    </AdminLayout>
  );
};

const Field = ({ label, children }: { label: string; children: ReactNode }) => (
  <label className="block">
    <span
      className="text-xs font-bold uppercase tracking-wider mb-1.5 block"
      style={{ color: "hsl(var(--admin-muted))" }}
    >
      {label}
    </span>
    {children}
  </label>
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
