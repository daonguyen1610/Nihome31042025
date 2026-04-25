import { useState } from "react";
import { Plus, Pencil, Trash2, ExternalLink } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";
import { useLogos } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";
import type { LogoResponse } from "@/services/contentApi";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";

type Kind = "clients" | "partners" | "suppliers";

const kindMap: Record<Kind, string> = {
  clients: "Client",
  partners: "Partner",
  suppliers: "Supplier",
};

const LogosManager = ({ kind, titleKey }: { kind: Kind; titleKey: string }) => {
  const { t } = useI18n();
  const { data: logos, loading, error, refetch } = useLogos();
  const items: LogoResponse[] = logos?.[kind] ?? [];

  const add = async () => {
    const name = window.prompt("Tên")?.trim();
    if (!name) return;
    const imageUrl = window.prompt("URL hình ảnh")?.trim() ?? "";
    const href = window.prompt("Liên kết (tuỳ chọn)")?.trim() || undefined;
    try {
      await adminApi.createLogo({ name, imageUrl, href, kind: kindMap[kind] });
      toast.success(t("form.created"));
      refetch();
    } catch {
      toast.error(t("common.error"));
    }
  };

  const edit = async (item: LogoResponse) => {
    const name = window.prompt("Tên", item.name)?.trim();
    if (!name) return;
    const imageUrl = window.prompt("URL hình ảnh", item.imageUrl)?.trim() ?? item.imageUrl;
    const href = window.prompt("Liên kết (tuỳ chọn)", item.href ?? "")?.trim() || undefined;
    try {
      await adminApi.updateLogo(item.id, { name, imageUrl, href, kind: kindMap[kind] });
      toast.success(t("form.updated"));
      refetch();
    } catch {
      toast.error(t("common.error"));
    }
  };

  const remove = async (item: LogoResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteLogo(item.id);
      toast.success(t("form.deleted"));
      refetch();
    } catch {
      toast.error(t("common.error"));
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
            {items.length} mục
          </p>
        </div>
        <button onClick={add} className="admin-btn-primary inline-flex items-center gap-2">
          <Plus className="w-4 h-4" /> {t("common.new")}
        </button>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
        {items.length === 0 ? (
          <div className="col-span-full"><PageEmpty message={t("common.noData")} /></div>
        ) : items.map((item) => (
          <div key={item.id} className="admin-card p-4 group">
            <div className="aspect-[4/3] rounded-xl bg-white border flex items-center justify-center overflow-hidden mb-3" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <img src={item.imageUrl} alt={item.name} className="max-w-full max-h-full object-contain" />
            </div>
            <p className="font-semibold text-sm truncate">{item.name}</p>
            {item.href && (
              <a href={item.href} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs mt-1" style={{ color: "hsl(var(--admin-primary))" }}>
                <ExternalLink className="w-3 h-3" /> link
              </a>
            )}
            <div className="flex gap-1 mt-3">
              <button onClick={() => edit(item)} className="flex-1 inline-flex items-center justify-center gap-1 text-xs font-bold px-2 py-1.5 rounded-lg hover:bg-muted">
                <Pencil className="w-3 h-3" /> {t("common.edit")}
              </button>
              <button onClick={() => remove(item)} className="flex-1 inline-flex items-center justify-center gap-1 text-xs font-bold px-2 py-1.5 rounded-lg hover:bg-muted" style={{ color: "hsl(var(--admin-danger))" }}>
                <Trash2 className="w-3 h-3" /> {t("common.delete")}
              </button>
            </div>
          </div>
        ))}
      </div>
    </AdminLayout>
  );
};

export default LogosManager;
