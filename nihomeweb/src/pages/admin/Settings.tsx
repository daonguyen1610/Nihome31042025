import { useState } from "react";
import { Save, Building2, Mail, Phone, MapPin, Globe } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

const Toggle = ({ on, onChange }: { on: boolean; onChange: (v: boolean) => void }) => (
  <button
    onClick={() => onChange(!on)}
    className="w-11 h-6 rounded-full relative transition"
    style={{ background: on ? "hsl(var(--admin-primary))" : "hsl(var(--admin-border))" }}
  >
    <span
      className="absolute top-0.5 w-5 h-5 rounded-full bg-white transition shadow"
      style={{ left: on ? "calc(100% - 22px)" : "2px" }}
    />
  </button>
);

const AdminSettings = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [company, setCompany] = useState({
    name: "Công ty NICON",
    email: "info@nicon.vn",
    phone: "+84 28 7300 1234",
    address: "Đường Mai Chí Thọ, Thủ Đức, TP.HCM",
    website: "https://nicon.vn",
  });
  const [features, setFeatures] = useState({
    newsletter: true,
    recruitment: true,
    multilang: true,
    chat: false,
    analytics: true,
  });

  const save = () => toast({ title: t("settings.saved") });

  const fields = [
    { key: "name", label: "Tên công ty", icon: Building2 },
    { key: "email", label: "Email", icon: Mail },
    { key: "phone", label: "Điện thoại", icon: Phone },
    { key: "address", label: "Địa chỉ", icon: MapPin },
    { key: "website", label: "Website", icon: Globe },
  ] as const;

  const featureList = [
    { key: "newsletter", label: "Nhận tin newsletter", desc: "Cho phép khách đăng ký nhận bản tin." },
    { key: "recruitment", label: "Trang tuyển dụng", desc: "Hiển thị các vị trí tuyển dụng công khai." },
    { key: "multilang", label: "Đa ngôn ngữ (VI/EN)", desc: "Bật chuyển đổi tiếng Việt và tiếng Anh." },
    { key: "chat", label: "Live chat", desc: "Tích hợp widget chat trực tuyến." },
    { key: "analytics", label: "Theo dõi analytics", desc: "Bật theo dõi hành vi người dùng." },
  ] as const;

  return (
    <AdminLayout>
      <div className="mb-7">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("settings.title")}</h1>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-5">
        {/* Company info */}
        <div className="admin-card p-7">
          <h2 className="font-display text-lg font-extrabold mb-1">{t("settings.company")}</h2>
          <p className="text-xs mb-6" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("settings.companyDesc")}
          </p>
          <div className="space-y-4">
            {fields.map((f) => (
              <div key={f.key}>
                <label className="text-xs uppercase tracking-wider font-bold mb-2 block" style={{ color: "hsl(var(--admin-muted))" }}>
                  {f.label}
                </label>
                <div
                  className="flex items-center gap-3 rounded-xl px-4 py-3 border"
                  style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
                >
                  <f.icon className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
                  <input
                    value={company[f.key]}
                    onChange={(e) => setCompany({ ...company, [f.key]: e.target.value })}
                    className="bg-transparent text-sm outline-none flex-1 font-semibold"
                  />
                </div>
              </div>
            ))}
            <button onClick={save} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm mt-3">
              <Save className="w-4 h-4" /> {t("common.save")}
            </button>
          </div>
        </div>

        {/* Features */}
        <div className="admin-card p-7">
          <h2 className="font-display text-lg font-extrabold mb-1">{t("settings.system")}</h2>
          <p className="text-xs mb-6" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("settings.systemDesc")}
          </p>
          <div className="space-y-3">
            {featureList.map((f) => (
              <div
                key={f.key}
                className="flex items-center justify-between gap-4 p-4 rounded-2xl"
                style={{ background: "hsl(var(--admin-bg))" }}
              >
                <div className="min-w-0">
                  <p className="font-bold text-sm">{f.label}</p>
                  <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
                    {f.desc}
                  </p>
                </div>
                <Toggle
                  on={features[f.key]}
                  onChange={(v) => setFeatures({ ...features, [f.key]: v })}
                />
              </div>
            ))}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminSettings;
