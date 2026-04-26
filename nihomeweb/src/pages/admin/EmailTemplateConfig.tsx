import { useCallback, useEffect, useRef, useState } from "react";
import { Save, Eye, Maximize2, Minimize2, RotateCcw, Mail, ShieldCheck } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";

type TabType = "application" | "otp";

const APPLICATION_TOKENS = [
  { token: "{{siteName}}", desc: "Tên website" },
  { token: "{{positionTitle}}", desc: "Vị trí ứng tuyển" },
  { token: "{{department}}", desc: "Phòng ban" },
  { token: "{{candidateName}}", desc: "Tên ứng viên" },
  { token: "{{email}}", desc: "Email ứng viên" },
  { token: "{{phone}}", desc: "SĐT ứng viên" },
  { token: "{{experienceYears}}", desc: "Số năm kinh nghiệm" },
  { token: "{{coverLetter}}", desc: "Lời giới thiệu" },
  { token: "{{appliedAt}}", desc: "Ngày ứng tuyển" },
];

const OTP_TOKENS = [
  { token: "{{siteName}}", desc: "Tên website" },
  { token: "{{otpCode}}", desc: "Mã OTP" },
  { token: "{{otpExpireMinutes}}", desc: "Thời gian hết hạn (phút)" },
];

const EmailTemplateConfig = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const iframeRef = useRef<HTMLIFrameElement>(null);

  const [activeTab, setActiveTab] = useState<TabType>("application");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [notificationEmail, setNotificationEmail] = useState("");
  const [otpSubject, setOtpSubject] = useState("");
  const [otpBody, setOtpBody] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getEmailTemplates();
      setSubject(res.data.newApplicationEmailSubjectTemplate ?? "");
      setBody(res.data.newApplicationEmailBodyTemplate ?? "");
      setNotificationEmail(res.data.notificationEmail ?? "");
      setOtpSubject(res.data.otpEmailSubjectTemplate ?? "");
      setOtpBody(res.data.otpEmailBodyTemplate ?? "");
    } catch {
      toast({ title: t("common.error"), description: "Không thể tải cấu hình email", variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    loadTemplates();
  }, [loadTemplates]);

  // Update iframe preview whenever body or tab changes
  const activeBody = activeTab === "application" ? body : otpBody;
  const activeSubject = activeTab === "application" ? subject : otpSubject;
  useEffect(() => {
    const iframe = iframeRef.current;
    if (!iframe) return;
    const doc = iframe.contentDocument;
    if (!doc) return;
    doc.open();
    doc.write(activeBody || "<p style='color:#999;padding:20px;font-family:sans-serif;'>Chưa có nội dung template</p>");
    doc.close();
  }, [activeBody]);

  const handleSave = async () => {
    setSaving(true);
    try {
      await adminApi.updateEmailTemplates({
        newApplicationEmailSubjectTemplate: subject || null,
        newApplicationEmailBodyTemplate: body || null,
        notificationEmail: notificationEmail || null,
        otpEmailSubjectTemplate: otpSubject || null,
        otpEmailBodyTemplate: otpBody || null,
      });
      toast({ title: "Đã lưu", description: "Cấu hình email template đã được cập nhật." });
    } catch {
      toast({ title: t("common.error"), description: "Không thể lưu cấu hình", variant: "destructive" });
    } finally {
      setSaving(false);
    }
  };

  const handleReset = async () => {
    await loadTemplates();
    toast({ title: "Đã khôi phục", description: "Đã tải lại cấu hình từ server." });
  };

  const insertToken = (token: string) => {
    if (activeTab === "application") {
      setBody((prev) => prev + token);
    } else {
      setOtpBody((prev) => prev + token);
    }
  };

  const currentTokens = activeTab === "application" ? APPLICATION_TOKENS : OTP_TOKENS;

  if (loading) {
    return (
      <AdminLayout>
        <div className="admin-page-header"><h1 className="admin-page-title">Cấu hình Email Template</h1></div>
        <div className="p-8 text-center text-muted-foreground">Đang tải...</div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="admin-page-header flex items-center justify-between">
        <h1 className="admin-page-title">Cấu hình Email Template</h1>
        <div className="flex items-center gap-2">
          <button onClick={handleReset} className="admin-btn admin-btn-secondary" disabled={saving}>
            <RotateCcw className="w-4 h-4" /> Khôi phục
          </button>
          <button onClick={handleSave} className="admin-btn admin-btn-primary" disabled={saving}>
            <Save className="w-4 h-4" /> {saving ? "Đang lưu..." : "Lưu thay đổi"}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6 p-6">
        {/* Left: form */}
        <div className="space-y-5">
          {/* Tabs */}
          <div className="flex gap-2 border-b border-border pb-3">
            <button
              type="button"
              onClick={() => setActiveTab("application")}
              className={`inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-t-md transition ${
                activeTab === "application"
                  ? "border-b-2 border-primary text-primary bg-primary/5"
                  : "text-muted-foreground hover:text-foreground"
              }`}
            >
              <Mail className="w-4 h-4" /> Thông báo ứng tuyển
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("otp")}
              className={`inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-t-md transition ${
                activeTab === "otp"
                  ? "border-b-2 border-primary text-primary bg-primary/5"
                  : "text-muted-foreground hover:text-foreground"
              }`}
            >
              <ShieldCheck className="w-4 h-4" /> OTP xác thực
            </button>
          </div>

          {/* Notification email — shown only on application tab */}
          {activeTab === "application" && (
            <div>
              <label className="admin-label">Email nhận thông báo</label>
              <input
                className="admin-input w-full"
                type="email"
                value={notificationEmail}
                onChange={(e) => setNotificationEmail(e.target.value)}
                placeholder="hr@nihome.vn"
              />
              <p className="text-xs text-muted-foreground mt-1">Email sẽ nhận thông báo khi có đơn ứng tuyển mới.</p>
            </div>
          )}

          {/* Subject */}
          <div>
            <label className="admin-label">Tiêu đề email</label>
            <input
              className="admin-input w-full"
              value={activeTab === "application" ? subject : otpSubject}
              onChange={(e) =>
                activeTab === "application" ? setSubject(e.target.value) : setOtpSubject(e.target.value)
              }
              placeholder={
                activeTab === "application"
                  ? "[{{siteName}}] Ứng viên mới: {{candidateName}} – {{positionTitle}}"
                  : "[{{siteName}}] Mã xác thực của bạn"
              }
              maxLength={255}
            />
          </div>

          {/* Body */}
          <div>
            <label className="admin-label">Nội dung HTML</label>
            <textarea
              className="admin-input w-full font-mono text-xs"
              rows={18}
              value={activeTab === "application" ? body : otpBody}
              onChange={(e) =>
                activeTab === "application" ? setBody(e.target.value) : setOtpBody(e.target.value)
              }
              placeholder="<div>...</div>"
            />
          </div>

          {/* Available tokens */}
          <div className="bg-muted/50 border border-border rounded-lg p-4">
            <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-3">Biến có sẵn (click để chèn)</p>
            <div className="flex flex-wrap gap-2">
              {currentTokens.map(({ token, desc }) => (
                <button
                  key={token}
                  type="button"
                  onClick={() => insertToken(token)}
                  className="inline-flex items-center gap-1.5 px-2.5 py-1.5 bg-background border border-border rounded-md text-xs hover:border-primary hover:text-primary transition"
                  title={desc}
                >
                  <code className="font-mono">{token}</code>
                  <span className="text-muted-foreground">– {desc}</span>
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Right: preview */}
        <div className={fullscreen ? "fixed inset-0 z-50 bg-background p-6" : ""}>
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <Eye className="w-4 h-4" /> Xem trước
            </div>
            <button
              onClick={() => setFullscreen(!fullscreen)}
              className="admin-btn admin-btn-secondary !py-1 !px-2 text-xs"
            >
              {fullscreen ? <Minimize2 className="w-3.5 h-3.5" /> : <Maximize2 className="w-3.5 h-3.5" />}
              {fullscreen ? "Thu nhỏ" : "Toàn màn hình"}
            </button>
          </div>
          <div className="border border-border rounded-lg overflow-hidden bg-white">
            <div className="bg-muted/50 border-b border-border px-4 py-2 text-xs text-muted-foreground font-mono truncate">
              Subject: {activeSubject || "(trống)"}
            </div>
            <iframe
              ref={iframeRef}
              title="Email preview"
              className="w-full bg-white"
              style={{ height: fullscreen ? "calc(100vh - 140px)" : "500px", border: "none" }}
              sandbox="allow-same-origin"
            />
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default EmailTemplateConfig;
