import { useCallback, useEffect, useState } from "react";
import { Save, RotateCcw, Mail, ShieldCheck, FileText } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import WorkPackageApprovalTemplateConfig from "@/components/admin/WorkPackageApprovalTemplateConfig";
import ContractReminderTemplateConfig from "@/components/admin/ContractReminderTemplateConfig";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { adminApi } from "@/services/adminApi";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import EmailTemplateEditor from "@/components/admin/EmailTemplateEditor";

type TabType = "application" | "otp" | "quote" | "contract" | "approval";

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

const QUOTE_TOKENS = [
  "customerName",
  "quoteCode",
  "opportunityName",
  "quoteLines",
  "discountPercent",
  "vatPercent",
  "grandTotal",
  "validUntil",
];

const EmailTemplateConfig = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [actionsContainer, setActionsContainer] = useState<HTMLDivElement | null>(null);

  const [activeTab, setActiveTab] = useState<TabType>("application");
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [notificationEmail, setNotificationEmail] = useState("");
  const [otpSubject, setOtpSubject] = useState("");
  const [otpBody, setOtpBody] = useState("");
  const [quoteSubject, setQuoteSubject] = useState("");
  const [quoteBody, setQuoteBody] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getEmailTemplates();
      setSubject(res.data.newApplicationEmailSubjectTemplate ?? "");
      setBody(res.data.newApplicationEmailBodyTemplate ?? "");
      setNotificationEmail(res.data.notificationEmail ?? "");
      setOtpSubject(res.data.otpEmailSubjectTemplate ?? "");
      setOtpBody(res.data.otpEmailBodyTemplate ?? "");
      setQuoteSubject(res.data.quoteEmailSubjectTemplate ?? "");
      setQuoteBody(res.data.quoteEmailBodyTemplate ?? "");
    } catch {
      toast({
        title: t("common.error"),
        description: "Không thể tải cấu hình email",
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    loadTemplates();
  }, [loadTemplates]);

  const activeBody = activeTab === "application" ? body : activeTab === "otp" ? otpBody : quoteBody;
  const activeSubject =
    activeTab === "application" ? subject : activeTab === "otp" ? otpSubject : quoteSubject;
  const handleSave = async () => {
    const normalizedQuoteBody = quoteBody.toLowerCase();
    if (
      !quoteSubject.trim() ||
      !quoteBody.trim() ||
      !normalizedQuoteBody.includes("{{quotelines}}") ||
      !normalizedQuoteBody.includes("{{grandtotal}}")
    ) {
      toast({
        title: t("common.error"),
        description: t("emailTemplate.quote.required"),
        variant: "destructive",
      });
      return;
    }
    if (!quoteBody.trimStart().startsWith("<")) {
      toast({
        title: t("common.error"),
        description: t("emailTemplate.quote.htmlRequired"),
        variant: "destructive",
      });
      return;
    }
    const quoteTemplateText = `${quoteSubject}\n${quoteBody}`;
    const matchedTokens = [...quoteTemplateText.matchAll(/\{\{(\w+)\}\}/g)];
    const unknownToken = matchedTokens.some(
      (match) => !QUOTE_TOKENS.some((name) => name.toLowerCase() === match[1].toLowerCase()),
    );
    const unmatchedBraces = quoteTemplateText.replace(/\{\{\w+\}\}/g, "");
    if (unknownToken || unmatchedBraces.includes("{{") || unmatchedBraces.includes("}}")) {
      toast({
        title: t("common.error"),
        description: t("emailTemplate.quote.invalidToken"),
        variant: "destructive",
      });
      return;
    }
    setSaving(true);
    try {
      await adminApi.updateEmailTemplates({
        newApplicationEmailSubjectTemplate: subject || null,
        newApplicationEmailBodyTemplate: body || null,
        notificationEmail: notificationEmail || null,
        otpEmailSubjectTemplate: otpSubject || null,
        otpEmailBodyTemplate: otpBody || null,
        quoteEmailSubjectTemplate: quoteSubject,
        quoteEmailBodyTemplate: quoteBody,
      });
      toast({ title: "Đã lưu", description: "Cấu hình email template đã được cập nhật." });
    } catch (error) {
      toast({
        title: t("common.error"),
        description: extractApiError(error),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleReset = async () => {
    await loadTemplates();
    toast({ title: "Đã khôi phục", description: "Đã tải lại cấu hình từ server." });
  };

  const currentTokens =
    activeTab === "application"
      ? APPLICATION_TOKENS
      : activeTab === "otp"
        ? OTP_TOKENS
        : QUOTE_TOKENS.map((name) => ({
            token: `{{${name}}}`,
            desc: t(`emailTemplate.quote.token.${name}`),
          }));

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
          <div
            className={cn(
              "flex items-center gap-2",
              (activeTab === "contract" || activeTab === "approval") && "hidden",
            )}
          >
            <Button type="button" variant="outline" onClick={handleReset} disabled={saving}>
              <RotateCcw className="mr-1.5 h-4 w-4" /> Khôi phục
            </Button>
            <Button type="button" onClick={handleSave} disabled={saving}>
              <Save className="mr-1.5 h-4 w-4" /> {saving ? "Đang lưu..." : "Lưu thay đổi"}
            </Button>
          </div>
          <div
            ref={setActionsContainer}
            className={cn(
              "flex items-center gap-2",
              activeTab !== "contract" && activeTab !== "approval" && "hidden",
            )}
          />
        </header>

        <div className="flex flex-wrap gap-1 border-b">
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
          <button
            type="button"
            onClick={() => setActiveTab("quote")}
            className={cn(
              "-mb-px inline-flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium transition",
              activeTab === "quote"
                ? "border-primary text-primary"
                : "border-transparent text-muted-foreground hover:text-foreground",
            )}
          >
            <FileText className="h-4 w-4" /> {t("emailTemplate.quote.tab")}
          </button>
          {(["contract", "approval"] as const).map((tab) => (
            <button
              key={tab}
              type="button"
              onClick={() => setActiveTab(tab)}
              aria-pressed={activeTab === tab}
              className={cn(
                "-mb-px inline-flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium transition",
                activeTab === tab
                  ? "border-primary text-primary"
                  : "border-transparent text-muted-foreground hover:text-foreground",
              )}
            >
              <FileText className="h-4 w-4 shrink-0" />
              {t(tab === "contract" ? "contractReminder.templates" : "approvalEmail.tab")}
            </button>
          ))}
        </div>

        <div hidden={activeTab !== "contract"}>
          <ContractReminderTemplateConfig
            active={activeTab === "contract"}
            actionsContainer={actionsContainer}
          />
        </div>
        <div hidden={activeTab !== "approval"}>
          <WorkPackageApprovalTemplateConfig
            active={activeTab === "approval"}
            actionsContainer={actionsContainer}
          />
        </div>
        {activeTab !== "contract" && activeTab !== "approval" && (
          <EmailTemplateEditor
            key={activeTab}
            id="email"
            subject={activeSubject}
            body={activeBody}
            onSubjectChange={
              activeTab === "application"
                ? setSubject
                : activeTab === "otp"
                  ? setOtpSubject
                  : setQuoteSubject
            }
            onBodyChange={
              activeTab === "application"
                ? setBody
                : activeTab === "otp"
                  ? setOtpBody
                  : setQuoteBody
            }
            subjectMaxLength={activeTab === "quote" ? 200 : 255}
            bodyMaxLength={activeTab === "quote" ? 8000 : undefined}
            hint={activeTab === "quote" ? t("emailTemplate.quote.hint") : undefined}
            tokens={currentTokens}
            beforeSubject={
              activeTab === "application" ? (
                <div className="space-y-1.5">
                  <Label className="text-xs" htmlFor="notification-email">
                    Email nhận thông báo
                  </Label>
                  <Input
                    id="notification-email"
                    type="email"
                    value={notificationEmail}
                    onChange={(event) => setNotificationEmail(event.target.value)}
                    placeholder="hr@nihome.vn"
                  />
                  <p className="text-xs text-muted-foreground">
                    Email sẽ nhận thông báo khi có đơn ứng tuyển mới.
                  </p>
                </div>
              ) : undefined
            }
          />
        )}
      </div>
    </AdminLayout>
  );
};

export default EmailTemplateConfig;
