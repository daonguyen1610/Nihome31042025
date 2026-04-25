import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { Construction } from "lucide-react";

const SimplePage = ({ titleKey, descKey }: { titleKey: string; descKey?: string }) => {
  const { t } = useI18n();
  return (
    <AdminLayout>
      <div className="mb-6">
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t(titleKey)}</h1>
      </div>
      <div className="admin-card p-10 text-center">
        <div
          className="w-14 h-14 rounded-2xl mx-auto mb-4 flex items-center justify-center"
          style={{ background: "hsl(var(--admin-primary-soft))", color: "hsl(var(--admin-primary))" }}
        >
          <Construction className="w-6 h-6" />
        </div>
        <h2 className="font-bold text-lg">Sắp ra mắt</h2>
        <p className="text-sm mt-2" style={{ color: "hsl(var(--admin-muted))" }}>
          {descKey ? t(descKey) : "Trang này đang được hoàn thiện."}
        </p>
      </div>
    </AdminLayout>
  );
};

export default SimplePage;
