import { useEffect, useState } from "react";
import { useAppSelector } from "@/store";
import { useI18n } from "@/lib/i18n";
import { Lock, Eye, EyeOff } from "lucide-react";
import Layout from "@/components/layout/Layout";

interface PasswordForm {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

const ProfileSettings = () => {
  const { t } = useI18n();
  const authUser = useAppSelector((s) => s.auth.user);
  const [passwordForm, setPasswordForm] = useState<PasswordForm>({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [showPasswords, setShowPasswords] = useState({
    current: false,
    new: false,
    confirm: false,
  });
  const [passwordStatus, setPasswordStatus] = useState<{
    type: "success" | "error" | null;
    message: string;
  }>({
    type: null,
    message: "",
  });

  useEffect(() => {
    if (!authUser) {
      // Redirect to login
      window.location.href = "/login";
    }
  }, [authUser]);

  const handlePasswordChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setPasswordForm((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validation
    if (!passwordForm.currentPassword) {
      setPasswordStatus({ type: "error", message: t("password.currentRequired") || "Vui lòng nhập mật khẩu hiện tại" });
      return;
    }

    if (!passwordForm.newPassword) {
      setPasswordStatus({ type: "error", message: t("password.newRequired") || "Vui lòng nhập mật khẩu mới" });
      return;
    }

    if (passwordForm.newPassword.length < 6) {
      setPasswordStatus({ type: "error", message: t("password.minLength") || "Mật khẩu phải ít nhất 6 ký tự" });
      return;
    }

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordStatus({ type: "error", message: t("password.notMatch") || "Mật khẩu xác nhận không khớp" });
      return;
    }

    if (passwordForm.newPassword === passwordForm.currentPassword) {
      setPasswordStatus({
        type: "error",
        message: t("password.sameCurrent") || "Mật khẩu mới không được trùng với hiện tại",
      });
      return;
    }

    try {
      // TODO: Call API to change password
      setPasswordStatus({ type: "success", message: t("password.changeSucess") || "Đổi mật khẩu thành công!" });
      setPasswordForm({
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
      });
    } catch (error) {
      setPasswordStatus({ type: "error", message: t("password.changeError") || "Có lỗi khi đổi mật khẩu" });
    }
  };

  return (
    <Layout>
      <div className="container-custom mt-20 pt-8 pb-16">
        {/* Page Header */}
        <div className="mb-12">
          <h1 className="text-4xl font-bold text-foreground mb-2">{t("profile.settings") || "Cài đặt"}</h1>
          <p className="text-foreground/60">{t("profile.manageAccount") || "Quản lý tài khoản của bạn"}</p>
        </div>

        <div className="max-w-2xl">
          {/* Change Password Section */}
          <div className="bg-secondary rounded-lg p-8 mb-8">
            <div className="flex items-center gap-3 mb-6">
              <Lock className="w-6 h-6 text-primary" />
              <h2 className="text-2xl font-bold text-foreground">{t("password.change") || "Đổi mật khẩu"}</h2>
            </div>

            {passwordStatus.type && (
              <div
                className={`p-4 rounded-lg mb-6 ${
                  passwordStatus.type === "success"
                    ? "bg-green-50 border border-green-200 text-green-700"
                    : "bg-red-50 border border-red-200 text-red-700"
                }`}
              >
                {passwordStatus.message}
              </div>
            )}

            <form onSubmit={handlePasswordSubmit} className="space-y-6">
              {/* Current Password */}
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  {t("password.current") || "Mật khẩu hiện tại"} <span className="text-accent">*</span>
                </label>
                <div className="relative">
                  <input
                    type={showPasswords.current ? "text" : "password"}
                    name="currentPassword"
                    value={passwordForm.currentPassword}
                    onChange={handlePasswordChange}
                    className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary pr-10"
                    required
                  />
                  <button
                    type="button"
                    onClick={() =>
                      setShowPasswords((prev) => ({
                        ...prev,
                        current: !prev.current,
                      }))
                    }
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-foreground/60 hover:text-foreground"
                  >
                    {showPasswords.current ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              {/* New Password */}
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  {t("password.new") || "Mật khẩu mới"} <span className="text-accent">*</span>
                </label>
                <div className="relative">
                  <input
                    type={showPasswords.new ? "text" : "password"}
                    name="newPassword"
                    value={passwordForm.newPassword}
                    onChange={handlePasswordChange}
                    className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary pr-10"
                    required
                  />
                  <button
                    type="button"
                    onClick={() =>
                      setShowPasswords((prev) => ({
                        ...prev,
                        new: !prev.new,
                      }))
                    }
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-foreground/60 hover:text-foreground"
                  >
                    {showPasswords.new ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                <p className="text-xs text-foreground/60 mt-1">{t("password.minLength") || "Ít nhất 6 ký tự"}</p>
              </div>

              {/* Confirm Password */}
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  {t("password.confirm") || "Xác nhận mật khẩu"} <span className="text-accent">*</span>
                </label>
                <div className="relative">
                  <input
                    type={showPasswords.confirm ? "text" : "password"}
                    name="confirmPassword"
                    value={passwordForm.confirmPassword}
                    onChange={handlePasswordChange}
                    className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary pr-10"
                    required
                  />
                  <button
                    type="button"
                    onClick={() =>
                      setShowPasswords((prev) => ({
                        ...prev,
                        confirm: !prev.confirm,
                      }))
                    }
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-foreground/60 hover:text-foreground"
                  >
                    {showPasswords.confirm ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              {/* Submit Button */}
              <div className="pt-4 border-t border-border/30">
                <button
                  type="submit"
                  className="px-6 py-2 bg-primary text-white rounded-lg font-semibold hover:bg-primary/90 transition-colors"
                >
                  {t("password.update") || "Cập nhật mật khẩu"}
                </button>
              </div>
            </form>
          </div>

          {/* Account Info */}
          <div className="bg-secondary rounded-lg p-8">
            <h2 className="text-2xl font-bold text-foreground mb-6">{t("profile.accountInfo") || "Thông tin tài khoản"}</h2>
            <div className="space-y-4">
              <div className="flex justify-between items-center pb-4 border-b border-border/30">
                <span className="text-foreground/60">{t("profile.email") || "Email"}</span>
                <span className="font-semibold text-foreground">{authUser?.email}</span>
              </div>
              <div className="flex justify-between items-center pb-4 border-b border-border/30">
                <span className="text-foreground/60">{t("profile.role") || "Vai trò"}</span>
                <span className="font-semibold text-foreground capitalize">{authUser?.role}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-foreground/60">{t("profile.memberSince") || "Thành viên kể từ"}</span>
                <span className="font-semibold text-foreground">{new Date().toLocaleDateString("vi-VN")}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
};

export default ProfileSettings;
