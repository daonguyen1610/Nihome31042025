import { useCallback, useEffect, useRef, useState } from "react";
import { Save, Eye, Maximize2, Minimize2, RotateCcw, Mail, ShieldCheck } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

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
        <div className="p-4 sm:p-6">
          <h1 className="text-2xl font-semibold">Cấu hình Email Template</h1>
          <div className="p-8 text-center text-sm text-muted-foreground">Đang tải...</div>
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h1 className="text-2xl font-semibold">Cấu hình Email Template</h1>
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" onClick={handleReset} disabled={saving}>
              <RotateCcw className="mr-1.5 h-4 w-4" /> Khôi phục
            </Button>
            <Button type="button" onClick={handleSave} disabled={saving}>
              <Save className="mr-1.5 h-4 w-4" /> {saving ? "Đang lưu..." : "Lưu thay đổi"}
            </Button>
          </div>
        </header>

        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          {/* Left: form */}
          <div className="space-y-4">
            {/* Tabs */}
            <div className="flex gap-1 border-b">
              <button
                type="button"
                onClick={() => setActiveTab("application")}
                className={cn(
                  "-mb-px inline-flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium transition",
                  activeTab === "application"
                    ? "border-primary text-primary"
                    : "border-transparent text-muted-foreground hover:text-foreground",
                )}
              >
                <Mail className="h-4 w-4" /> Thông báo ứng tuyển
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("otp")}
                className={cn(
                  "-mb-px inline-flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium transition",
                  activeTab === "otp"
                    ? "border-primary text-primary"
                    : "border-transparent text-muted-foreground hover:text-foreground",
                )}
              >
                <ShieldCheck className="h-4 w-4" /> OTP xác thực
              </button>
            </div>

            {/* Notification email — shown only on application tab */}
            {activeTab === "application" && (
              <div className="space-y-1.5">
                <Label className="text-xs" htmlFor="notification-email">Email nhận thông báo</Label>
                <Input
                  id="notification-email"
                  type="email"
                  value={notificationEmail}
                  onChange={(e) => setNotificationEmail(e.target.value)}
                  placeholder="hr@nihome.vn"
                />
                <p className="text-xs text-muted-foreground">Email sẽ nhận thông báo khi có đơn ứng tuyển mới.</p>
              </div>
            )}

            {/* Subject */}
            <div className="space-y-1.5">
              <Label className="text-xs" htmlFor="email-subject">Tiêu đề email</Label>
              <Input
                id="email-subject"
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
            <div className="space-y-1.5">
              <Label className="text-xs" htmlFor="email-body">Nội dung HTML</Label>
              <Textarea
                id="email-body"
                className="font-mono text-xs"
                rows={18}
                value={activeTab === "application" ? body : otpBody}
                onChange={(e) =>
                  activeTab === "application" ? setBody(e.target.value) : setOtpBody(e.target.value)
                }
                placeholder="<div>...</div>"
              />
            </div>

            {/* Available tokens */}
            <div className="rounded-lg border bg-muted/50 p-4">
              <p className="mb-3 text-xs font-medium uppercase tracking-wide text-muted-foreground">Biến có sẵn (click để chèn)</p>
              <div className="flex flex-wrap gap-2">
                {currentTokens.map(({ token, desc }) => (
                  <button
                    key={token}
                    type="button"
                    onClick={() => insertToken(token)}
                    className="inline-flex items-center gap-1.5 rounded-md border bg-background px-2.5 py-1.5 text-xs transition hover:border-primary hover:text-primary"
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
            <div className="mb-3 flex items-center justify-between">
              <div className="flex items-center gap-2 text-sm font-medium">
                <Eye className="h-4 w-4" /> Xem trước
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setFullscreen(!fullscreen)}
              >
                {fullscreen ? <Minimize2 className="mr-1.5 h-3.5 w-3.5" /> : <Maximize2 className="mr-1.5 h-3.5 w-3.5" />}
                {fullscreen ? "Thu nhỏ" : "Toàn màn hình"}
              </Button>
            </div>
            <div className="overflow-hidden rounded-lg border bg-white">
              <div className="truncate border-b bg-muted/50 px-4 py-2 font-mono text-xs text-muted-foreground">
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
      </div>
    </AdminLayout>
  );
};

export default EmailTemplateConfig;
