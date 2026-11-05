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
import type { ProjectScheduleEmailReviewItem } from "@/services/adminApi";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import EmailTemplateEditor from "@/components/admin/EmailTemplateEditor";

type TabType =
  | "application"
  | "otp"
  | "quote"
  | "projectSchedule"
  | "quoteApproval"
  | "contract"
  | "approval"
  | "layout";

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
const PROJECT_SCHEDULE_TOKENS = [
  "eventName",
  "recipientName",
  "projectCode",
  "projectName",
  "version",
  "changeReason",
  "changeSummary",
];

const escapePreviewText = (value: string) =>
  value.replace(
    /[&<>"']/g,
    (character) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[character],
  );

const EmailTemplateConfig = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [actionsContainer, setActionsContainer] = useState<HTMLDivElement | null>(null);

  const [activeTab, setActiveTab] = useState<TabType>(
    new URLSearchParams(window.location.search).get("tab") === "projectSchedule"
      ? "projectSchedule"
      : "application",
  );
  const [scheduleEmailsNeedingReview, setScheduleEmailsNeedingReview] = useState<
    ProjectScheduleEmailReviewItem[]
  >([]);
  const [scheduleDeliveryLoading, setScheduleDeliveryLoading] = useState(false);
  const [scheduleDeliveryError, setScheduleDeliveryError] = useState<string | null>(null);
  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [notificationEmail, setNotificationEmail] = useState("");
  const [otpSubject, setOtpSubject] = useState("");
  const [otpBody, setOtpBody] = useState("");
  const [quoteSubject, setQuoteSubject] = useState("");
  const [quoteBody, setQuoteBody] = useState("");
  const [projectScheduleSubject, setProjectScheduleSubject] = useState("");
  const [projectScheduleBody, setProjectScheduleBody] = useState("");
  const [sharedLayout, setSharedLayout] = useState("");
  const [schedulePreview, setSchedulePreview] = useState<{ subject: string; body: string } | null>(
    null,
  );
  const [schedulePreviewError, setSchedulePreviewError] = useState<string | null>(null);
  const [schedulePreviewLoading, setSchedulePreviewLoading] = useState(false);
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
      setProjectScheduleSubject(res.data.projectScheduleEmailSubjectTemplate);
      setProjectScheduleBody(res.data.projectScheduleEmailBodyTemplate);
      setSharedLayout(res.data.sharedBusinessEmailLayoutTemplate);
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

  const loadScheduleDelivery = useCallback(async () => {
    setScheduleDeliveryLoading(true);
    setScheduleDeliveryError(null);
    try {
      const response = await adminApi.getProjectScheduleEmailsNeedingReview();
      setScheduleEmailsNeedingReview(response.data);
    } catch (error) {
      setScheduleDeliveryError(extractApiError(error));
    } finally {
      setScheduleDeliveryLoading(false);
    }
  }, []);

  useEffect(() => {
    if (activeTab === "projectSchedule") void loadScheduleDelivery();
  }, [activeTab, loadScheduleDelivery]);

  useEffect(() => {
    if (activeTab !== "projectSchedule" || loading) return;
    let stale = false;
    setSchedulePreview(null);
    setSchedulePreviewError(null);
    setSchedulePreviewLoading(true);
    const timer = window.setTimeout(async () => {
      try {
        const response = await adminApi.previewProjectScheduleEmail({
          subject: projectScheduleSubject,
          body: projectScheduleBody,
        });
        if (!stale) setSchedulePreview(response.data);
      } catch (error) {
        if (!stale) setSchedulePreviewError(extractApiError(error));
      } finally {
        if (!stale) setSchedulePreviewLoading(false);
      }
    }, 300);
    return () => {
      stale = true;
      window.clearTimeout(timer);
    };
  }, [activeTab, loading, projectScheduleSubject, projectScheduleBody]);

  const activeBody =
    activeTab === "application"
      ? body
      : activeTab === "otp"
        ? otpBody
        : activeTab === "projectSchedule"
          ? projectScheduleBody
          : quoteBody;
  const activeSubject =
    activeTab === "application"
      ? subject
      : activeTab === "otp"
        ? otpSubject
        : activeTab === "projectSchedule"
          ? projectScheduleSubject
          : quoteSubject;
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
        projectScheduleEmailSubjectTemplate: projectScheduleSubject,
        projectScheduleEmailBodyTemplate: projectScheduleBody,
        sharedBusinessEmailLayoutTemplate: sharedLayout,
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
        : activeTab === "projectSchedule"
          ? PROJECT_SCHEDULE_TOKENS.map((name) => ({
              token: `{{${name}}}`,
              desc: t(`emailTemplate.schedule.token.${name}`),
            }))
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
              (activeTab === "contract" ||
                activeTab === "approval" ||
                activeTab === "quoteApproval") &&
                "hidden",
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
              activeTab !== "contract" &&
                activeTab !== "approval" &&
                activeTab !== "quoteApproval" &&
                "hidden",
            )}
          />
        </header>

        <div className="flex flex-wrap gap-1 border-b">
          <button
            type="button"
            onClick={() => setActiveTab("layout")}
            aria-pressed={activeTab === "layout"}
            className={cn(
              "-mb-px border-b-2 px-4 py-2 text-sm font-medium",
              activeTab === "layout"
                ? "border-primary text-primary"
                : "border-transparent text-muted-foreground hover:text-foreground",
            )}
          >
            {t("emailTemplate.layout.tab")}
          </button>
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
          <button
            type="button"
            onClick={() => setActiveTab("projectSchedule")}
            aria-pressed={activeTab === "projectSchedule"}
            className={cn(
              "-mb-px inline-flex items-center gap-2 border-b-2 px-4 py-2 text-sm font-medium transition",
              activeTab === "projectSchedule"
                ? "border-primary text-primary"
                : "border-transparent text-muted-foreground hover:text-foreground",
            )}
          >
            <FileText className="h-4 w-4" /> {t("emailTemplate.schedule.tab")}
          </button>
          {(["contract", "approval", "quoteApproval"] as const).map((tab) => (
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
              {t(
                tab === "contract"
                  ? "contractReminder.templates"
                  : tab === "approval"
                    ? "approvalEmail.tab"
                    : "quotes.approval.templateTab",
              )}
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
        <div hidden={activeTab !== "quoteApproval"}>
          <WorkPackageApprovalTemplateConfig
            kind="quote"
            active={activeTab === "quoteApproval"}
            actionsContainer={actionsContainer}
          />
        </div>
        {activeTab === "layout" && (
          <section className="grid gap-5 lg:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="shared-email-layout">{t("emailTemplate.layout.label")}</Label>
              <p className="text-sm text-muted-foreground">{t("emailTemplate.layout.guidance")}</p>
              <Textarea
                id="shared-email-layout"
                value={sharedLayout}
                maxLength={8000}
                onChange={(event) => setSharedLayout(event.target.value)}
                className="min-h-96 font-mono text-xs"
              />
            </div>
            <div className="space-y-2">
              <h2 className="text-sm font-medium">{t("emailTemplate.layout.preview")}</h2>
              <iframe
                title={t("emailTemplate.layout.preview")}
                sandbox=""
                className="h-[520px] w-full rounded-md border bg-white"
                srcDoc={sharedLayout
                  .replace(
                    "{{heading}}",
                    escapePreviewText(t("emailTemplate.layout.sample.heading")),
                  )
                  .replace(
                    "{{content}}",
                    t("emailTemplate.layout.sample.content")
                      .split("|")
                      .map((line) => `<p>${escapePreviewText(line)}</p>`)
                      .join(""),
                  )
                  .replace(
                    "{{footer}}",
                    escapePreviewText(t("emailTemplate.layout.sample.footer")),
                  )}
              />
            </div>
          </section>
        )}
        {activeTab !== "layout" &&
          activeTab !== "contract" &&
          activeTab !== "approval" &&
          activeTab !== "quoteApproval" && (
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
                    : activeTab === "projectSchedule"
                      ? setProjectScheduleSubject
                      : setQuoteSubject
              }
              onBodyChange={
                activeTab === "application"
                  ? setBody
                  : activeTab === "otp"
                    ? setOtpBody
                    : activeTab === "projectSchedule"
                      ? setProjectScheduleBody
                      : setQuoteBody
              }
              subjectMaxLength={
                activeTab === "quote" || activeTab === "projectSchedule" ? 200 : 255
              }
              bodyMaxLength={
                activeTab === "quote" || activeTab === "projectSchedule" ? 8000 : undefined
              }
              hint={
                activeTab === "quote"
                  ? t("emailTemplate.quote.hint")
                  : activeTab === "projectSchedule"
                    ? t("emailTemplate.schedule.hint")
                    : undefined
              }
              tokens={currentTokens}
              renderedPreview={activeTab === "projectSchedule" ? schedulePreview : undefined}
              previewError={activeTab === "projectSchedule" ? schedulePreviewError : undefined}
              previewLoading={activeTab === "projectSchedule" && schedulePreviewLoading}
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
        {activeTab === "projectSchedule" && (
          <section id="schedule-delivery" className="space-y-3 border-t pt-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold">
                  {t("emailTemplate.schedule.delivery.title")}
                </h2>
                <p className="text-sm text-muted-foreground">
                  {t("emailTemplate.schedule.delivery.guidance")}
                </p>
              </div>
              <Button type="button" variant="outline" onClick={loadScheduleDelivery}>
                {t("emailTemplate.schedule.delivery.refresh")}
              </Button>
            </div>
            {scheduleDeliveryLoading ? (
              <p className="text-sm text-muted-foreground">
                {t("emailTemplate.schedule.delivery.loading")}
              </p>
            ) : scheduleDeliveryError ? (
              <p role="alert" className="text-sm text-destructive">
                {scheduleDeliveryError}
              </p>
            ) : scheduleEmailsNeedingReview.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {t("emailTemplate.schedule.delivery.empty")}
              </p>
            ) : (
              <div className="divide-y rounded-md border">
                {scheduleEmailsNeedingReview.map((email) => (
                  <div
                    key={email.id}
                    className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
                  >
                    <div>
                      <div className="font-medium">
                        {email.projectCode} · v{email.version} · #{email.id}
                      </div>
                      <div className="text-muted-foreground">
                        {t("emailTemplate.schedule.delivery.attempts")}: {email.attemptCount}
                        {email.lastAttemptAt &&
                          ` · ${new Date(email.lastAttemptAt).toLocaleString()}`}
                      </div>
                    </div>
                    <Link
                      className="font-medium text-primary underline-offset-4 hover:underline"
                      to={`/admin/operational-projects/${email.projectId}?tab=schedule`}
                    >
                      {t("emailTemplate.schedule.delivery.openProject")}
                    </Link>
                  </div>
                ))}
              </div>
            )}
          </section>
        )}
      </div>
    </AdminLayout>
  );
};

export default EmailTemplateConfig;
