import { useMemo, useState, type ReactNode } from "react";
import { Plus, Pencil, Trash2, ExternalLink, Upload, Save, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useLogos } from "@/hooks/useContentApi";
import { adminApi, type UpsertLogoRequest } from "@/services/adminApi";
import type { LogoResponse } from "@/services/contentApi";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";

type Kind = "clients" | "partners" | "suppliers";

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
};

function getErrorMessage(error: unknown) {
  if (typeof error === "object" && error !== null && "response" in error) {
    const response = (error as { response?: { data?: { message?: string } } }).response;
    if (response?.data?.message) return response.data.message;
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

  const items = useMemo(() => {
    const source = logos?.[kind] ?? [];
    return [...source].sort(
      (a, b) => (a.sortOrder ?? Number.MAX_SAFE_INTEGER) - (b.sortOrder ?? Number.MAX_SAFE_INTEGER),
    );
  }, [logos, kind]);

  const isEditing = form.id != null;

  const updateForm = <K extends keyof LogoFormData>(key: K, value: LogoFormData[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }));

  const startCreate = () => {
    const maxSortOrder = items.reduce((max, item) => Math.max(max, item.sortOrder ?? 0), 0);
    setForm({
      ...emptyForm,
      sortOrder: maxSortOrder + 1,
    });
  };

  const startEdit = (item: LogoResponse) => {
    setForm({
      id: item.id,
      name: item.name,
      imageUrl: item.imageUrl,
      href: item.href ?? "",
      sortOrder: item.sortOrder ?? 0,
    });
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

      <div className="grid grid-cols-1 xl:grid-cols-[380px_minmax(0,1fr)] gap-5">
        <div className="admin-card p-5 h-fit">
          <h2 className="font-display text-lg font-extrabold mb-4">
            {isEditing ? t("logoAdmin.editTitle") : t("logoAdmin.createTitle")}
          </h2>

          <form onSubmit={save} className="space-y-4">
            <Field label={`${t("logoAdmin.fieldName")} *`}>
              <input
                className="admin-input"
                value={form.name}
                onChange={(e) => updateForm("name", e.target.value)}
                placeholder={t("logoAdmin.placeholderName")}
                required
              />
            </Field>

            <Field label={`${t("logoAdmin.fieldImage")} *`}>
              <div className="space-y-2">
                <div className="flex gap-2">
                  <input
                    className="admin-input flex-1"
                    value={form.imageUrl}
                    onChange={(e) => updateForm("imageUrl", e.target.value)}
                    placeholder="/images/upload/..."
                    required
                  />
                  <button
                    type="button"
                    onClick={uploadImage}
                    disabled={uploading}
                    className="inline-flex items-center justify-center gap-1 rounded-xl px-3 text-sm border hover:bg-muted disabled:opacity-60"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <Upload className="w-4 h-4" /> {uploading ? t("logoAdmin.uploading") : t("logoAdmin.upload")}
                  </button>
                </div>
                {form.imageUrl && (
                  <div
                    className="h-32 rounded-xl border bg-white flex items-center justify-center overflow-hidden p-3"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <img src={form.imageUrl} alt={form.name || t("logoAdmin.previewAlt")} className="max-w-full max-h-full object-contain" />
                  </div>
                )}
              </div>
            </Field>

            <Field label={t("logoAdmin.fieldHref")}>
              <input
                className="admin-input"
                value={form.href}
                onChange={(e) => updateForm("href", e.target.value)}
                placeholder="https://..."
              />
            </Field>

            <Field label={t("logoAdmin.fieldSortOrder")}>
              <input
                type="number"
                className="admin-input"
                value={form.sortOrder}
                onChange={(e) => updateForm("sortOrder", Number(e.target.value))}
                min={0}
              />
            </Field>

            <div className="flex gap-2 pt-1">
              <button
                type="submit"
                disabled={submitting}
                className="admin-btn-primary inline-flex items-center justify-center gap-2 flex-1 disabled:opacity-60"
              >
                <Save className="w-4 h-4" /> {isEditing ? t("form.update") : t("form.create")}
              </button>
              {isEditing && (
                <button
                  type="button"
                  onClick={() => setForm(emptyForm)}
                  className="inline-flex items-center justify-center gap-2 px-3 rounded-xl border hover:bg-muted"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <X className="w-4 h-4" /> {t("common.cancel")}
                </button>
              )}
            </div>
          </form>
        </div>

        <div className="admin-card p-4">
          {items.length === 0 ? (
            <PageEmpty message={t("common.noData")} />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
              {items.map((item) => (
                <div key={item.id} className="rounded-2xl border bg-white p-4" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <div
                    className="aspect-[4/3] rounded-xl border bg-white flex items-center justify-center overflow-hidden mb-3"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <img src={item.imageUrl} alt={item.name} className="max-w-full max-h-full object-contain p-2" />
                  </div>

                  <p className="font-semibold text-sm leading-tight line-clamp-2 min-h-10">{item.name}</p>
                  <div className="text-xs mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("logoAdmin.sortOrderLabel")}: {item.sortOrder ?? 0}
                  </div>
                  {item.href && (
                    <a
                      href={item.href}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex items-center gap-1 text-xs mt-1.5"
                      style={{ color: "hsl(var(--admin-primary))" }}
                    >
                      <ExternalLink className="w-3 h-3" /> {item.href}
                    </a>
                  )}

                  <div className="grid grid-cols-2 gap-2 mt-3">
                    <button
                      onClick={() => startEdit(item)}
                      className="inline-flex items-center justify-center gap-1.5 text-xs font-bold py-2 rounded-lg border hover:bg-muted"
                      style={{ borderColor: "hsl(var(--admin-border))" }}
                    >
                      <Pencil className="w-3.5 h-3.5" /> {t("common.edit")}
                    </button>
                    <button
                      onClick={() => remove(item)}
                      className="inline-flex items-center justify-center gap-1.5 text-xs font-bold py-2 rounded-lg border hover:bg-destructive/10"
                      style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}
                    >
                      <Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
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
