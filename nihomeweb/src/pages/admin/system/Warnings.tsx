import { AlertTriangle, Check } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";

type Warn = { ok: boolean; text: string };

const data: Warn[] = [
  { ok: true, text: "Database connection is active" },
  { ok: true, text: "All directory permissions are OK" },
  { ok: true, text: "All file permissions are OK" },
  { ok: true, text: "Email settings are configured" },
  { ok: true, text: "SSL certificate is valid" },
];

const WarningsPage = () => {
  const { t } = useI18n();
  return (
    <AdminLayout>
      <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight mb-6">{t("sys.warn.title")}</h1>
      <div className="admin-card p-6 space-y-3">
        {data.map((w, i) => (
          <div key={i} className="flex items-start gap-3 text-sm">
            {w.ok ? (
              <Check className="w-5 h-5 shrink-0 mt-0.5" style={{ color: "hsl(142 71% 40%)" }} />
            ) : (
              <AlertTriangle className="w-5 h-5 shrink-0 mt-0.5" style={{ color: "hsl(38 92% 50%)" }} />
            )}
            <span style={{ color: w.ok ? "hsl(142 71% 35%)" : "hsl(38 92% 35%)" }}>{w.text}</span>
          </div>
        ))}
      </div>
    </AdminLayout>
  );
};

export default WarningsPage;
