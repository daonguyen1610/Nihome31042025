import { useEffect, useState } from "react";
import { Save, User as UserIcon } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useAppSelector } from "@/store";

interface FormData {
  fullName: string;
  email: string;
  phoneNumber: string;
}

const MyProfile = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const authUser = useAppSelector((s) => s.auth.user);
  const [data, setData] = useState<FormData>({ fullName: "", email: "", phoneNumber: "" });
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (authUser) {
      setData({
        fullName: authUser.fullName ?? "",
        email: authUser.email ?? "",
        phoneNumber: authUser.phoneNumber ?? "",
      });
    }
  }, [authUser]);

  const update = <K extends keyof FormData>(key: K, value: FormData[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.fullName.trim()) {
      toast({ title: t("form.required") || "Vui lòng nhập đầy đủ", variant: "destructive" });
      return;
    }
    setSubmitting(true);
    try {
      // TODO: integrate PUT /api/users/me when backend endpoint is available
      await new Promise((r) => setTimeout(r, 400));
      toast({ title: t("profile.updated") || "Đã cập nhật hồ sơ" });
    } finally {
      setSubmitting(false);
    }
  };

  if (!authUser) {
    return null;
  }

  return (
    <Layout>
      <div className="admin-scope">
        <div className="container-custom pt-28 pb-16">
          <div className="flex items-center gap-3 mb-6">
            <div
              className="w-10 h-10 rounded-full bg-white border flex items-center justify-center"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            >
              <UserIcon className="w-4 h-4" />
            </div>
            <div>
              <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
                {t("profile.myProfile") || "Hồ sơ của tôi"}
              </h1>
              <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
                {t("profile.manageInfo") || "Quản lý thông tin tài khoản của bạn"}
              </p>
            </div>
          </div>

          <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-3 gap-5">
            <div className="lg:col-span-2 space-y-5">
              <div className="admin-card p-6">
                <h2 className="font-bold mb-4">{t("profile.personalInfo") || "Thông tin cá nhân"}</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <Field label={`${t("profile.fullName") || "Họ và tên"} *`} className="md:col-span-2">
                    <input
                      className="admin-input"
                      value={data.fullName}
                      onChange={(e) => update("fullName", e.target.value)}
                      required
                    />
                  </Field>
                  <Field label={t("profile.email") || "Email"}>
                    <input
                      type="email"
                      className="admin-input"
                      value={data.email}
                      onChange={(e) => update("email", e.target.value)}
                    />
                  </Field>
                  <Field label={t("profile.phone") || "Số điện thoại"}>
                    <input
                      type="tel"
                      className="admin-input"
                      value={data.phoneNumber}
                      disabled
                    />
                  </Field>
                </div>
                <p className="text-xs mt-3" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("profile.phoneLocked") || "Số điện thoại được sử dụng để đăng nhập và không thể thay đổi."}
                </p>
              </div>
            </div>

            <div className="space-y-5">
              <div className="admin-card p-6">
                <h2 className="font-bold mb-4">{t("profile.account") || "Tài khoản"}</h2>
                <div className="space-y-3 text-sm">
                  <Row label={t("profile.role") || "Vai trò"} value={authUser.role} />
                  <Row
                    label={t("profile.status") || "Trạng thái"}
                    value={authUser.isActive ? (t("profile.active") || "Đang hoạt động") : (t("profile.inactive") || "Tạm khoá")}
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={submitting}
                className="admin-btn-primary w-full inline-flex items-center justify-center gap-2 px-5 py-3 text-sm disabled:opacity-50"
              >
                <Save className="w-4 h-4" />
                {submitting ? (t("form.saving") || "Đang lưu...") : (t("form.update") || "Cập nhật")}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Layout>
  );
};

const Field = ({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) => (
  <label className={["block", className].filter(Boolean).join(" ")}>
    <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

const Row = ({ label, value }: { label: string; value: string }) => (
  <div className="flex items-center justify-between gap-3 py-1.5 border-b last:border-0" style={{ borderColor: "hsl(var(--admin-border))" }}>
    <span style={{ color: "hsl(var(--admin-muted))" }}>{label}</span>
    <span className="font-semibold">{value}</span>
  </div>
);

export default MyProfile;
