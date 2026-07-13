import { useState } from "react";
import { Save, Building2, Mail, Phone, MapPin, Globe } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";

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
      <div className="space-y-4 p-4 sm:p-6">
        <header>
          <h1 className="text-2xl font-semibold">{t("settings.title")}</h1>
        </header>

        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          {/* Company info */}
          <div className="rounded-lg border bg-card p-6">
            <h2 className="text-lg font-semibold">{t("settings.company")}</h2>
            <p className="mb-6 text-xs text-muted-foreground">{t("settings.companyDesc")}</p>
            <div className="space-y-4">
              {fields.map((f) => (
                <div key={f.key} className="space-y-1.5">
                  <Label className="text-xs" htmlFor={`settings-${f.key}`}>{f.label}</Label>
                  <div className="relative">
                    <f.icon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      id={`settings-${f.key}`}
                      value={company[f.key]}
                      onChange={(e) => setCompany({ ...company, [f.key]: e.target.value })}
                      className="pl-9"
                    />
                  </div>
                </div>
              ))}
              <Button onClick={save} className="mt-3">
                <Save className="mr-1.5 h-4 w-4" /> {t("common.save")}
              </Button>
            </div>
          </div>

          {/* Features */}
          <div className="rounded-lg border bg-card p-6">
            <h2 className="text-lg font-semibold">{t("settings.system")}</h2>
            <p className="mb-6 text-xs text-muted-foreground">{t("settings.systemDesc")}</p>
            <div className="space-y-3">
              {featureList.map((f) => (
                <div
                  key={f.key}
                  className="flex items-center justify-between gap-4 rounded-md border p-4"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-medium">{f.label}</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">{f.desc}</p>
                  </div>
                  <Switch
                    checked={features[f.key]}
                    onCheckedChange={(v) => setFeatures({ ...features, [f.key]: v })}
                    aria-label={f.label}
                  />
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminSettings;
