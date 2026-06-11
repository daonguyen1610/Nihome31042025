import { useEffect, useState } from "react";
import { useAppSelector } from "@/store";
import { useI18n } from "@/lib/i18n";
import { Mail, Phone, MapPin, Camera } from "lucide-react";
import Layout from "@/components/layout/Layout";

interface UserProfileData {
  fullName: string;
  email: string;
  phone?: string;
  address?: string;
  avatar?: string;
}

const UserProfile = () => {
  const { t } = useI18n();
  const authUser = useAppSelector((s) => s.auth.user);
  const [profileData, setProfileData] = useState<UserProfileData | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [formData, setFormData] = useState<UserProfileData>({
    fullName: "",
    email: "",
    phone: "",
    address: "",
  });

  useEffect(() => {
    if (authUser) {
      const initialData: UserProfileData = {
        fullName: authUser.fullName,
        email: authUser.email || "",
        phone: "",
        address: "",
        avatar: authUser.avatar,
      };
      setProfileData(initialData);
      setFormData(initialData);
    }
  }, [authUser]);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSave = async () => {
    // TODO: Implement save to backend
    setProfileData(formData);
    setIsEditing(false);
  };

  if (!authUser || !profileData) {
    return (
      <Layout>
        <div className="container-custom mt-20 pt-8">
          <p className="text-center text-foreground/60">{t("common.loading") || "Đang tải..."}</p>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="container-custom mt-20 pt-8 pb-16">
        {/* Page Header */}
        <div className="mb-12">
          <h1 className="text-4xl font-bold text-foreground mb-2">{t("profile.myProfile") || "Hồ sơ của tôi"}</h1>
          <p className="text-foreground/60">{t("profile.manageYourInfo") || "Quản lý thông tin cá nhân"}</p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Left: Avatar */}
          <div className="lg:col-span-1">
            <div className="bg-secondary rounded-lg p-6 sticky top-24">
              <div className="aspect-square bg-primary/10 rounded-lg mb-4 flex items-center justify-center overflow-hidden">
                {profileData.avatar ? (
                  <img src={profileData.avatar} alt={profileData.fullName} className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full bg-gradient-to-br from-primary/20 to-accent/20 flex items-center justify-center">
                    <Camera className="w-16 h-16 text-primary/40" />
                  </div>
                )}
              </div>
              {isEditing && (
                <button className="w-full px-4 py-2 bg-primary text-white rounded-lg font-semibold hover:bg-primary/90 transition-colors mb-2">
                  {t("profile.uploadAvatar") || "Tải ảnh lên"}
                </button>
              )}
            </div>
          </div>

          {/* Right: Profile Info */}
          <div className="lg:col-span-2">
            <div className="bg-secondary rounded-lg p-8">
              <div className="flex items-center justify-between mb-6">
                <h2 className="text-2xl font-bold text-foreground">{t("profile.personalInfo") || "Thông tin cá nhân"}</h2>
                {!isEditing ? (
                  <button
                    onClick={() => setIsEditing(true)}
                    className="px-4 py-2 bg-primary text-white rounded-lg font-semibold hover:bg-primary/90 transition-colors"
                  >
                    {t("common.edit") || "Chỉnh sửa"}
                  </button>
                ) : null}
              </div>

              {isEditing ? (
                <div className="space-y-6">
                  {/* Full Name */}
                  <div>
                    <label className="block text-sm font-medium text-foreground mb-2">
                      {t("profile.fullName") || "Họ và tên"} <span className="text-accent">*</span>
                    </label>
                    <input
                      type="text"
                      name="fullName"
                      value={formData.fullName}
                      onChange={handleInputChange}
                      className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
                      required
                    />
                  </div>

                  {/* Email */}
                  <div>
                    <label className="block text-sm font-medium text-foreground mb-2">
                      {t("profile.email") || "Email"} <span className="text-accent">*</span>
                    </label>
                    <input
                      type="email"
                      name="email"
                      value={formData.email}
                      onChange={handleInputChange}
                      className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
                      required
                    />
                  </div>

                  {/* Phone */}
                  <div>
                    <label className="block text-sm font-medium text-foreground mb-2">
                      {t("profile.phone") || "Số điện thoại"}
                    </label>
                    <input
                      type="tel"
                      name="phone"
                      value={formData.phone}
                      onChange={handleInputChange}
                      className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
                    />
                  </div>

                  {/* Address */}
                  <div>
                    <label className="block text-sm font-medium text-foreground mb-2">
                      {t("profile.address") || "Địa chỉ"}
                    </label>
                    <textarea
                      name="address"
                      value={formData.address}
                      onChange={handleInputChange}
                      rows={3}
                      className="w-full px-4 py-2 bg-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary resize-none"
                    />
                  </div>

                  {/* Action Buttons */}
                  <div className="flex gap-3 pt-4 border-t border-border">
                    <button
                      onClick={handleSave}
                      className="px-6 py-2 bg-primary text-white rounded-lg font-semibold hover:bg-primary/90 transition-colors"
                    >
                      {t("common.save") || "Lưu"}
                    </button>
                    <button
                      onClick={() => {
                        setIsEditing(false);
                        setFormData(profileData);
                      }}
                      className="px-6 py-2 bg-foreground/10 text-foreground rounded-lg font-semibold hover:bg-foreground/20 transition-colors"
                    >
                      {t("common.cancel") || "Hủy"}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="space-y-4">
                  {/* Full Name */}
                  <div className="flex items-center gap-4 pb-4 border-b border-border/30">
                    <div className="flex-1">
                      <p className="text-sm text-foreground/60">{t("profile.fullName") || "Họ và tên"}</p>
                      <p className="text-lg font-semibold text-foreground">{profileData.fullName}</p>
                    </div>
                  </div>

                  {/* Email */}
                  <div className="flex items-center gap-4 pb-4 border-b border-border/30">
                    <Mail className="w-5 h-5 text-primary flex-shrink-0" />
                    <div className="flex-1">
                      <p className="text-sm text-foreground/60">{t("profile.email") || "Email"}</p>
                      <p className="text-lg font-semibold text-foreground">{profileData.email}</p>
                    </div>
                  </div>

                  {/* Phone */}
                  {profileData.phone && (
                    <div className="flex items-center gap-4 pb-4 border-b border-border/30">
                      <Phone className="w-5 h-5 text-primary flex-shrink-0" />
                      <div className="flex-1">
                        <p className="text-sm text-foreground/60">{t("profile.phone") || "Số điện thoại"}</p>
                        <p className="text-lg font-semibold text-foreground">{profileData.phone}</p>
                      </div>
                    </div>
                  )}

                  {/* Address */}
                  {profileData.address && (
                    <div className="flex items-center gap-4 pb-4 border-b border-border/30">
                      <MapPin className="w-5 h-5 text-primary flex-shrink-0" />
                      <div className="flex-1">
                        <p className="text-sm text-foreground/60">{t("profile.address") || "Địa chỉ"}</p>
                        <p className="text-lg font-semibold text-foreground">{profileData.address}</p>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
};

export default UserProfile;
