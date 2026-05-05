import { useMemo, useState, type ReactNode } from "react";
import { Plus, Pencil, Trash2, ExternalLink, Upload, Save, Trophy } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useLogos } from "@/hooks/useContentApi";
import { adminApi, type UpsertLogoRequest } from "@/services/adminApi";
import type { LogoResponse } from "@/services/contentApi";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";

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
          message?: unknown;
          title?: unknown;
          errors?: Record<string, unknown>;
        };
      };
    };

    const data = withResponse.response?.data;
    if (typeof data?.message === "string") return data.message;
    if (data?.errors && typeof data.errors === "object") {
      for (const value of Object.values(data.errors)) {
        if (typeof value === "string" && value.trim()) return value;
        if (Array.isArray(value)) {
          const first = value.find((item) => typeof item === "string" && item.trim());
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

  const items = useMemo(() => {
    const source = logos?.[kind] ?? [];
    return [...source].sort(
      (a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER),
    );
  }, [logos, kind]);

  const isEditing = form.id != null;
  const hasImage = form.imageUrl.trim().length > 0;
  const isBusy = submitting || uploading;
  const isAwards = kind === "awards";
  const gridClassName = isAwards
    ? "grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4"
    : "grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3 lg:gap-4";
  const cardClassName = isAwards
    ? "logo-grid-card rounded-2xl border p-4 card-hover"
    : "logo-grid-card rounded-xl border p-3 card-hover";
  const imageFrameClassName = isAwards
    ? "aspect-[4/3] rounded-xl border bg-white flex items-center justify-center overflow-hidden mb-3"
    : "aspect-[16/10] rounded-lg border bg-white flex items-center justify-center overflow-hidden mb-2.5";

  const updateForm = <K extends keyof LogoFormData>(key: K, value: LogoFormData[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }));

  const startCreate = () => {
    const maxSortOrder = items.reduce((max, item) => Math.max(max, item.sortOrder ?? 0), 0);
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
      const previousImageUrl = isEditing && form.imageUrl ? form.imageUrl : undefined;
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
      toast({ title: t("form.required"), description: t("logoAdmin.requiredNameImage"), variant: "destructive" });
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
    if (!window.confirm(`${t("logoAdmin.confirmDelete")} "${item.name}"?`)) return;

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

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;

  return (
    <AdminLayout>
      <div className={`admin-logo-manager ${isAwards ? "is-awards" : ""}`}>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t(titleKey)}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {items.length} {t("logoAdmin.logoNoun")}
          </p>
        </div>
        <button onClick={startCreate} className="admin-btn-primary inline-flex items-center gap-2" type="button">
          <Plus className="w-4 h-4" /> {t("logoAdmin.add")}
        </button>
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
                  <img src={item.imageUrl} alt={item.name} className="max-w-full max-h-full object-contain p-2" />
                </div>

                <p className={`font-semibold text-sm leading-tight ${isAwards ? "line-clamp-3 min-h-14" : "line-clamp-2 min-h-10"}`}>
                  {item.name}
                </p>
                <div className="text-xs mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("logoAdmin.sortOrderLabel")}: {item.sortOrder ?? 0}
                </div>
                {item.href && (
                  <a
                    href={item.href}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex max-w-full min-w-0 items-center gap-1 text-xs mt-1.5"
                    style={{ color: "hsl(var(--admin-primary))" }}
                  >
                    <ExternalLink className="w-3 h-3 shrink-0" /> <span className="truncate">{item.href}</span>
                  </a>
                )}

                <div className="grid grid-cols-2 gap-1.5 mt-3">
                  <button
                    onClick={() => startEdit(item)}
                    className="inline-flex min-w-0 items-center justify-center gap-1.5 text-xs font-bold py-2 px-2 rounded-lg border hover:bg-muted"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <Pencil className="w-3.5 h-3.5 shrink-0" /> <span className="truncate">{t("common.edit")}</span>
                  </button>
                  <button
                    onClick={() => remove(item)}
                    className="inline-flex min-w-0 items-center justify-center gap-1.5 text-xs font-bold py-2 px-2 rounded-lg border hover:bg-destructive/10"
                    style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}
                  >
                    <Trash2 className="w-3.5 h-3.5 shrink-0" /> <span className="truncate">{t("common.delete")}</span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <Dialog open={openModal} onOpenChange={setOpenModal}>
        <DialogContent
          className="logo-modal max-w-5xl p-0 overflow-hidden gap-0 rounded-3xl border shadow-2xl"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <DialogHeader
            className="logo-modal-header px-6 pt-5 pb-4"
            style={{ background: "hsl(var(--admin-bg))" }}
          >
            <DialogTitle className="font-display text-xl lg:text-2xl flex items-center gap-2">
              {isAwards && <Trophy className="w-5 h-5 text-primary" />}
              {isEditing ? t("logoAdmin.editTitle") : t("logoAdmin.createTitle")}
            </DialogTitle>
            <DialogDescription className="sr-only">
              {t("logoAdmin.requiredNameImage")}
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={save} className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_360px] gap-0">
            <div className="px-6 py-5 space-y-5">
              <Field label={`${t("logoAdmin.fieldName")} *`}>
                <input
                  className="admin-input logo-name-input w-full"
                  value={form.name}
                  onChange={(e) => updateForm("name", e.target.value)}
                  placeholder={t("logoAdmin.placeholderName")}
                  required
                />
              </Field>

              <Field label={`${t("logoAdmin.fieldImage")} *`}>
                <div
                  className="logo-upload-wrap rounded-2xl border p-3.5 space-y-2.5"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <div className="flex flex-col sm:flex-row gap-2">
                    <input
                      className="admin-input w-full flex-1 bg-white"
                      value={form.imageUrl}
                      onChange={(e) => updateForm("imageUrl", e.target.value)}
                      placeholder="/images/upload/..."
                      required
                    />
                    <button
                      type="button"
                      onClick={uploadImage}
                      disabled={uploading}
                      className="inline-flex items-center justify-center gap-1.5 rounded-xl px-4 h-11 text-sm border bg-white hover:bg-muted disabled:opacity-60 sm:min-w-[148px]"
                      style={{ borderColor: "hsl(var(--admin-border))" }}
                    >
                      <Upload className="w-4 h-4" /> {uploading ? t("logoAdmin.uploading") : t("logoAdmin.upload")}
                    </button>
                  </div>
                </div>
              </Field>

              <div className="grid grid-cols-1 sm:grid-cols-[minmax(0,1fr)_150px] gap-3">
                <Field label={t("logoAdmin.fieldHref")}>
                  <input
                    className="admin-input w-full"
                    value={form.href}
                    onChange={(e) => updateForm("href", e.target.value)}
                    placeholder="https://..."
                  />
                </Field>

                <Field label={t("logoAdmin.fieldSortOrder")}>
                  <input
                    type="number"
                    className="admin-input w-full"
                    value={form.sortOrder}
                    onChange={(e) => updateForm("sortOrder", Number(e.target.value))}
                    min={0}
                  />
                </Field>
              </div>
            </div>

            <aside
              className="logo-preview-pane border-t lg:border-t-0 lg:border-l px-6 py-5"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            >
              <p className="text-xs font-bold uppercase tracking-wider mb-2 flex items-center gap-1.5" style={{ color: "hsl(var(--admin-muted))" }}>
                {isAwards && <Trophy className="w-3.5 h-3.5" />}
                {t("logoAdmin.previewAlt")}
              </p>
              <div
                className="h-[240px] rounded-2xl border bg-white flex items-center justify-center overflow-hidden p-3"
                style={{ borderColor: "hsl(var(--admin-border))" }}
              >
                {hasImage ? (
                  <img src={form.imageUrl} alt={form.name || t("logoAdmin.previewAlt")} className="max-w-full max-h-full object-contain" />
                ) : (
                  <div className="text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                    <Upload className="w-6 h-6 mx-auto mb-1" />
                    <span className="text-sm">{t("logoAdmin.fieldImage")}</span>
                  </div>
                )}
              </div>

              <div
                className="logo-preview-meta mt-3 rounded-xl border p-3 space-y-1 text-sm"
                style={{ borderColor: "hsl(var(--admin-border))" }}
              >
                <p className="font-semibold whitespace-normal break-words leading-snug">{form.name.trim() || "-"}</p>
                <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("logoAdmin.sortOrderLabel")}: {Number.isFinite(form.sortOrder) ? form.sortOrder : 0}
                </p>
              </div>
            </aside>

            <div
              className="lg:col-span-2 px-6 py-4 border-t flex items-center justify-end gap-2"
              style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-bg))" }}
            >
              <button
                type="button"
                onClick={() => setOpenModal(false)}
                className="inline-flex items-center justify-center gap-2 px-4 py-2 rounded-xl border hover:bg-muted"
                style={{ borderColor: "hsl(var(--admin-border))" }}
              >
                {t("common.cancel")}
              </button>
              <button
                type="submit"
                disabled={isBusy}
                className="admin-btn-primary inline-flex items-center justify-center gap-2 px-5 py-2.5 min-w-[138px] disabled:opacity-60"
              >
                <Save className="w-4 h-4" /> {isEditing ? t("form.update") : t("form.create")}
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
    <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

export default LogosManager;
