import { useEffect, useRef, useState } from "react";
import {
  Eye,
  EyeOff,
  FileText,
  FolderOpen,
  Key,
  Lock,
  Save,
  Shield,
  Trash2,
  Upload,
  User as UserIcon,
} from "lucide-react";
import { isAxiosError } from "axios";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useAppDispatch, useAppSelector } from "@/store";
import { setUser } from "@/store/authSlice";
import { meApi, type UserDocumentResponse } from "@/services/meApi";

interface FormData {
  fullName: string;
  email: string;
  phoneNumber: string;
}

const DOCUMENT_TYPES = [
  { value: "CCCD", labelKey: "profile.docType.cccd", fallback: "CCCD" },
  { value: "PASSPORT", labelKey: "profile.docType.passport", fallback: "Hộ chiếu" },
  { value: "OTHER", labelKey: "profile.docType.other", fallback: "Giấy tờ khác" },
];

const formatBytes = (n: number): string => {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / 1024 / 1024).toFixed(2)} MB`;
};

const extractError = (err: unknown, fallback: string): string => {
  if (isAxiosError(err)) {
    const data = err.response?.data as { message?: string } | undefined;
    if (data?.message) return data.message;
  }
  return fallback;
};

const apiBase = (import.meta.env.VITE_API_URL as string | undefined) ?? "";
const fileBase = apiBase.replace(/\/api\/?$/, "");
const resolveFileUrl = (path: string) =>
  path.startsWith("http") ? path : `${fileBase}${path}`;

const MyProfile = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const dispatch = useAppDispatch();
  const authUser = useAppSelector((s) => s.auth.user);

  const [data, setData] = useState<FormData>({ fullName: "", email: "", phoneNumber: "" });
  const [savingProfile, setSavingProfile] = useState(false);

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

  const handleSubmitProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.fullName.trim()) {
      toast({
        title: t("form.required") || "Vui lòng nhập họ và tên",
        variant: "destructive",
      });
      return;
    }
    setSavingProfile(true);
    try {
      const res = await meApi.updateMe({
        fullName: data.fullName.trim(),
        email: data.email.trim() || undefined,
      });
      if (authUser) {
        dispatch(
          setUser({
            ...authUser,
            fullName: res.data.fullName ?? "",
            email: res.data.email,
          }),
        );
      }
      toast({ title: t("profile.updated") || "Đã cập nhật hồ sơ" });
    } catch (err) {
      toast({
        title: extractError(err, t("profile.updateFailed") || "Cập nhật thất bại"),
        variant: "destructive",
      });
    } finally {
      setSavingProfile(false);
    }
  };

  if (!authUser) return null;

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

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
            <div className="lg:col-span-2 space-y-5">
              <form onSubmit={handleSubmitProfile} className="admin-card overflow-hidden">
                <SectionHeader
                  icon={<UserIcon className="w-4 h-4" />}
                  iconBg="bg-primary/10 text-primary"
                  title={t("profile.personalInfo") || "Thông tin cá nhân"}
                  subtitle={t("profile.personalInfoHint") || "Cập nhật thông tin liên hệ của bạn"}
                />
                <div className="p-6">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Field
                      label={`${t("profile.fullName") || "Họ và tên"} *`}
                      className="md:col-span-2"
                    >
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
                      <input type="tel" className="admin-input" value={data.phoneNumber} disabled />
                    </Field>
                  </div>
                  <p className="text-xs mt-3" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("profile.phoneLocked") ||
                      "Số điện thoại được sử dụng để đăng nhập và không thể thay đổi."}
                  </p>
                  <div
                    className="flex justify-end mt-5 pt-4 border-t"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <button
                      type="submit"
                      disabled={savingProfile}
                      className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm disabled:opacity-50"
                    >
                      <Save className="w-4 h-4" />
                      {savingProfile
                        ? t("form.saving") || "Đang lưu..."
                        : t("form.update") || "Cập nhật"}
                    </button>
                  </div>
                </div>
              </form>

              <DocumentsCard />

              <ChangePasswordCard />
            </div>

            <div className="space-y-5">
              <div className="admin-card overflow-hidden">
                <SectionHeader
                  icon={<Shield className="w-4 h-4" />}
                  iconBg="bg-secondary text-foreground"
                  title={t("profile.account") || "Tài khoản"}
                  subtitle={t("profile.accountHint") || "Quyền và trạng thái"}
                />
                <div className="p-6 space-y-3 text-sm">
                  <Row label={t("profile.role") || "Vai trò"} value={authUser.role} />
                  <Row
                    label={t("profile.status") || "Trạng thái"}
                    value={
                      authUser.isActive
                        ? t("profile.active") || "Đang hoạt động"
                        : t("profile.inactive") || "Tạm khoá"
                    }
                  />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
};

const SectionHeader = ({
  icon,
  iconBg,
  title,
  subtitle,
}: {
  icon: React.ReactNode;
  iconBg: string;
  title: string;
  subtitle?: string;
}) => (
  <div
    className="px-6 py-4 border-b flex items-center gap-3"
    style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--surface))" }}
  >
    <div className={`w-9 h-9 rounded-lg flex items-center justify-center ${iconBg}`}>{icon}</div>
    <div className="min-w-0">
      <h2 className="font-bold text-sm leading-tight">{title}</h2>
      {subtitle && (
        <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
          {subtitle}
        </p>
      )}
    </div>
  </div>
);

const DocumentsCard = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [docs, setDocs] = useState<UserDocumentResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [docType, setDocType] = useState<string>("CCCD");
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    meApi
      .listDocuments()
      .then((r) => {
        if (active) setDocs(r.data);
      })
      .catch(() => {
        /* noop */
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const uploadFiles = async (files: FileList | File[]) => {
    const list = Array.from(files);
    if (list.length === 0) return;
    setUploading(true);
    let okCount = 0;
    for (const f of list) {
      try {
        const res = await meApi.uploadDocument(f, docType);
        setDocs((prev) => [res.data, ...prev]);
        okCount++;
      } catch (err) {
        toast({
          title: extractError(err, t("profile.uploadFailed") || "Tải lên thất bại"),
          variant: "destructive",
        });
      }
    }
    setUploading(false);
    if (okCount > 0) {
      toast({
        title: `${t("profile.uploaded") || "Đã tải lên"} ${okCount} ${
          t("profile.fileSuffix") || "tệp"
        }`,
      });
    }
  };

  const handleSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      void uploadFiles(e.target.files);
      e.target.value = "";
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    if (e.dataTransfer.files) void uploadFiles(e.dataTransfer.files);
  };

  const handleDelete = async (id: number) => {
    try {
      await meApi.deleteDocument(id);
      setDocs((prev) => prev.filter((d) => d.id !== id));
      toast({ title: t("profile.deleted") || "Đã xoá" });
    } catch (err) {
      toast({
        title: extractError(err, t("profile.deleteFailed") || "Xoá thất bại"),
        variant: "destructive",
      });
    }
  };

  return (
    <div className="admin-card overflow-hidden">
      <SectionHeader
        icon={<FolderOpen className="w-4 h-4" />}
        iconBg="bg-primary/10 text-primary"
        title={t("profile.myDocuments") || "Tài liệu của tôi"}
        subtitle={
          t("profile.myDocumentsHint") || "Tải lên các tài liệu cá nhân (CCCD, hộ chiếu, giấy tờ)"
        }
      />
      <div className="p-6 space-y-5">
        <div className="flex flex-wrap items-center gap-3">
          <span
            className="text-xs font-bold uppercase tracking-wider"
            style={{ color: "hsl(var(--admin-muted))" }}
          >
            {t("profile.docType") || "Loại tài liệu"}
          </span>
          <div className="flex flex-wrap gap-2">
            {DOCUMENT_TYPES.map((type) => (
              <button
                key={type.value}
                type="button"
                onClick={() => setDocType(type.value)}
                className={`px-3 py-1.5 text-xs font-semibold rounded-full border transition-colors ${
                  docType === type.value
                    ? "bg-primary text-primary-foreground border-primary"
                    : "bg-white text-foreground hover:bg-secondary"
                }`}
                style={
                  docType !== type.value ? { borderColor: "hsl(var(--admin-border))" } : undefined
                }
              >
                {t(type.labelKey) || type.fallback}
              </button>
            ))}
          </div>
        </div>

        <div
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
          onClick={() => inputRef.current?.click()}
          className={`rounded-xl border-2 border-dashed cursor-pointer transition-colors p-10 text-center ${
            dragOver ? "bg-primary/10 border-primary" : "bg-primary/5 hover:bg-primary/10"
          }`}
          style={{ borderColor: dragOver ? undefined : "hsl(var(--primary) / 0.4)" }}
        >
          <input
            ref={inputRef}
            type="file"
            accept="image/jpeg,image/png,image/gif,image/webp"
            multiple
            className="hidden"
            onChange={handleSelect}
          />
          <div className="w-14 h-14 rounded-full bg-primary text-primary-foreground flex items-center justify-center mx-auto mb-3 shadow-lg">
            <Upload className="w-6 h-6" />
          </div>
          <p className="font-bold text-sm">
            {uploading
              ? t("profile.uploading") || "Đang tải lên..."
              : t("profile.dragDrop") || "Nhấn hoặc kéo thả ảnh vào đây"}
          </p>
          <p className="text-xs mt-1.5" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("profile.formatsHint") || "Hỗ trợ: JPEG, PNG, GIF, WebP (tối đa 10MB)"}
          </p>
        </div>

        <div>
          {loading ? (
            <p
              className="text-sm text-center py-6"
              style={{ color: "hsl(var(--admin-muted))" }}
            >
              {t("common.loading") || "Đang tải..."}
            </p>
          ) : docs.length === 0 ? (
            <div className="text-center py-8">
              <FolderOpen
                className="w-10 h-10 mx-auto mb-2"
                style={{ color: "hsl(var(--admin-muted))" }}
              />
              <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
                {t("profile.noDocuments") || "Bạn chưa tải lên tài liệu nào"}
              </p>
            </div>
          ) : (
            <ul className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {docs.map((doc) => (
                <li
                  key={doc.id}
                  className="flex items-center gap-3 p-3 rounded-lg border bg-white"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <a
                    href={resolveFileUrl(doc.fileUrl)}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-12 h-12 rounded-md bg-secondary overflow-hidden flex items-center justify-center shrink-0"
                  >
                    {doc.contentType.startsWith("image/") ? (
                      <img
                        src={resolveFileUrl(doc.fileUrl)}
                        alt={doc.originalName}
                        className="w-full h-full object-cover"
                      />
                    ) : (
                      <FileText className="w-5 h-5" />
                    )}
                  </a>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold truncate" title={doc.originalName}>
                      {doc.originalName}
                    </p>
                    <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                      {(t(`profile.docType.${doc.documentType.toLowerCase()}`) ||
                        doc.documentType) +
                        " • " +
                        formatBytes(doc.size)}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => handleDelete(doc.id)}
                    className="w-8 h-8 rounded-md flex items-center justify-center text-destructive hover:bg-destructive/10 transition-colors"
                    aria-label={t("common.delete") || "Xoá"}
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
};

const ChangePasswordCard = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (newPassword.length < 6) {
      toast({
        title: t("profile.passwordTooShort") || "Mật khẩu mới phải có ít nhất 6 ký tự",
        variant: "destructive",
      });
      return;
    }
    if (newPassword !== confirmPassword) {
      toast({
        title: t("profile.passwordMismatch") || "Mật khẩu xác nhận không khớp",
        variant: "destructive",
      });
      return;
    }
    setSubmitting(true);
    try {
      await meApi.changePassword({ currentPassword, newPassword });
      toast({ title: t("profile.passwordChanged") || "Đổi mật khẩu thành công" });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err) {
      toast({
        title: extractError(
          err,
          t("profile.passwordChangeFailed") || "Đổi mật khẩu thất bại",
        ),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form onSubmit={submit} className="admin-card overflow-hidden">
      <SectionHeader
        icon={<Shield className="w-4 h-4" />}
        iconBg="bg-orange-100 text-orange-600"
        title={t("profile.security") || "Bảo mật tài khoản"}
        subtitle={t("profile.securityHint") || "Thay đổi mật khẩu để bảo vệ tài khoản"}
      />
      <div className="p-6 space-y-4">
        <PasswordField
          label={t("profile.currentPassword") || "Mật khẩu hiện tại"}
          placeholder={t("profile.currentPasswordPlaceholder") || "Nhập mật khẩu hiện tại"}
          icon={<Lock className="w-3.5 h-3.5 text-destructive" />}
          value={currentPassword}
          onChange={setCurrentPassword}
          show={showCurrent}
          toggleShow={() => setShowCurrent((v) => !v)}
        />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <PasswordField
            label={t("profile.newPassword") || "Mật khẩu mới"}
            placeholder={t("profile.newPasswordPlaceholder") || "Tối thiểu 6 ký tự"}
            icon={<Key className="w-3.5 h-3.5 text-destructive" />}
            value={newPassword}
            onChange={setNewPassword}
            show={showNew}
            toggleShow={() => setShowNew((v) => !v)}
          />
          <PasswordField
            label={t("profile.confirmPassword") || "Xác nhận mật khẩu"}
            placeholder={t("profile.confirmPasswordPlaceholder") || "Nhập lại mật khẩu mới"}
            icon={<Shield className="w-3.5 h-3.5 text-destructive" />}
            value={confirmPassword}
            onChange={setConfirmPassword}
            show={showConfirm}
            toggleShow={() => setShowConfirm((v) => !v)}
          />
        </div>
        <div
          className="flex justify-end pt-4 border-t"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <button
            type="submit"
            disabled={submitting}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-md text-sm font-semibold text-white bg-orange-500 hover:bg-orange-600 transition-colors disabled:opacity-50"
          >
            <Key className="w-4 h-4" />
            {submitting
              ? t("form.saving") || "Đang lưu..."
              : t("profile.changePassword") || "Đổi mật khẩu"}
          </button>
        </div>
      </div>
    </form>
  );
};

const PasswordField = ({
  label,
  placeholder,
  icon,
  value,
  onChange,
  show,
  toggleShow,
}: {
  label: string;
  placeholder: string;
  icon: React.ReactNode;
  value: string;
  onChange: (v: string) => void;
  show: boolean;
  toggleShow: () => void;
}) => (
  <label className="block">
    <span
      className="text-xs font-bold uppercase tracking-wider mb-1.5 flex items-center gap-1.5"
      style={{ color: "hsl(var(--admin-muted))" }}
    >
      {icon}
      {label}
    </span>
    <div className="relative">
      <input
        type={show ? "text" : "password"}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="admin-input pr-10"
        required
      />
      <button
        type="button"
        onClick={toggleShow}
        className="absolute right-2 top-1/2 -translate-y-1/2 w-7 h-7 flex items-center justify-center text-muted-foreground hover:text-foreground"
        tabIndex={-1}
      >
        {show ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
      </button>
    </div>
  </label>
);

const Field = ({
  label,
  children,
  className,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
}) => (
  <label className={["block", className].filter(Boolean).join(" ")}>
    <span
      className="text-xs font-bold uppercase tracking-wider mb-1.5 block"
      style={{ color: "hsl(var(--admin-muted))" }}
    >
      {label}
    </span>
    {children}
  </label>
);

const Row = ({ label, value }: { label: string; value: string }) => (
  <div
    className="flex items-center justify-between gap-3 py-1.5 border-b last:border-0"
    style={{ borderColor: "hsl(var(--admin-border))" }}
  >
    <span style={{ color: "hsl(var(--admin-muted))" }}>{label}</span>
    <span className="font-semibold">{value}</span>
  </div>
);

export default MyProfile;
