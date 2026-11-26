import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useCallback, useEffect, useState } from "react";
import { Save, RotateCcw, ChevronDown } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import CmScheduleTemplateConfig from "@/components/admin/CmScheduleTemplateConfig";
import WorkPackageApprovalTemplateConfig from "@/components/admin/WorkPackageApprovalTemplateConfig";
import ContractReminderTemplateConfig from "@/components/admin/ContractReminderTemplateConfig";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { adminApi } from "@/services/adminApi";
import type { ProjectScheduleEmailReviewItem } from "@/services/adminApi";
import { Link, useSearchParams } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import EmailTemplateEditor from "@/components/admin/EmailTemplateEditor";
import { formatDateTime } from "@/lib/displaySettings";

type TabType =
  | "cmSchedule"
  | "application"
  | "otp"
  | "quote"
  | "projectSchedule"
  | "quoteApproval"
  | "quoteDecision"
  | "contract"
  | "approval";

const CUSTOM_TEMPLATE_TABS = [
  "cmSchedule",
  "contract",
  "approval",
  "quoteApproval",
  "quoteDecision",
];
const usesCustomEditor = (tab: TabType) => CUSTOM_TEMPLATE_TABS.includes(tab);

/**
 * Templates grouped by the business area that sends them. Each keeps its
 * existing name (the accessible name) and gains one line on when it is sent.
 */
const TEMPLATE_GROUPS: { labelKey: string; tabs: { tab: TabType; nameKey: string }[] }[] = [
  {
    labelKey: "emailTemplate.group.accounts",
    tabs: [
      { tab: "application", nameKey: "a11yUi.applicationTab" },
      { tab: "otp", nameKey: "a11yUi.otpTab" },
    ],
  },
  {
    labelKey: "emailTemplate.group.sales",
    tabs: [
      { tab: "quote", nameKey: "emailTemplate.quote.tab" },
      { tab: "quoteApproval", nameKey: "quotes.approval.templateTab" },
      { tab: "quoteDecision", nameKey: "quotes.approval.decisionTemplateTab" },
    ],
  },
  {
    labelKey: "emailTemplate.group.contracts",
    tabs: [{ tab: "contract", nameKey: "contractReminder.templates" }],
  },
  {
    labelKey: "emailTemplate.group.projects",
    tabs: [
      { tab: "projectSchedule", nameKey: "emailTemplate.schedule.tab" },
      { tab: "cmSchedule", nameKey: "cmSchedule.emailTemplate" },
    ],
  },
  {
    labelKey: "emailTemplate.group.workPackages",
    tabs: [{ tab: "approval", nameKey: "approvalEmail.tab" }],
  },
];
const ALL_TEMPLATES = TEMPLATE_GROUPS.flatMap((group) => group.tabs);
const isTab = (value: string | null): value is TabType =>
  ALL_TEMPLATES.some((item) => item.tab === value);

const APPLICATION_TOKENS = [
  { token: "{{siteName}}", desc: "a11yUi.siteName" },
  { token: "{{positionTitle}}", desc: "a11yUi.positionTitle" },
  { token: "{{department}}", desc: "a11yUi.department" },
  { token: "{{candidateName}}", desc: "a11yUi.candidateName" },
  { token: "{{email}}", desc: "a11yUi.email" },
  { token: "{{phone}}", desc: "a11yUi.phone" },
  { token: "{{experienceYears}}", desc: "a11yUi.experienceYears" },
  { token: "{{coverLetter}}", desc: "a11yUi.coverLetter" },
  { token: "{{appliedAt}}", desc: "a11yUi.appliedAt" },
];

const OTP_TOKENS = [
  { token: "{{siteName}}", desc: "a11yUi.siteName" },
  { token: "{{otpCode}}", desc: "a11yUi.otpCode" },
  { token: "{{otpExpireMinutes}}", desc: "a11yUi.otpExpireMinutes" },
];

const QUOTE_TOKENS = [
  "customerName",
  "quoteCode",
  "opportunityName",
  "quoteLines",
  "discountPercent",
  "vatPercent",
  "grandTotal",
  "currency",
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

const EmailTemplateConfig = () => {
  const uiError = useUiError();
  const { t } = useI18n();
  const { toast } = useToast();
  const [actionsContainer, setActionsContainer] = useState<HTMLDivElement | null>(null);

  // The chosen template lives in the URL, so a reload or shared link reopens it.
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedTab = searchParams.get("tab");
  const activeTab: TabType = isTab(requestedTab) ? requestedTab : "application";
  const [pickerOpen, setPickerOpen] = useState(false);
  const setActiveTab = (tab: TabType) => {
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current);
        next.set("tab", tab);
        return next;
      },
      { replace: true },
    );
    setPickerOpen(false);
  };
  const activeTemplate = ALL_TEMPLATES.find((item) => item.tab === activeTab)!;
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
    } catch {
      toast({
        title: t("common.error"),
        description: t("emailTemplate.editor.loadFailed"),
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
      setScheduleDeliveryError(uiError(error));
    } finally {
      setScheduleDeliveryLoading(false);
    }
  }, [uiError]);

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
        if (!stale) setSchedulePreviewError(uiError(error));
      } finally {
        if (!stale) setSchedulePreviewLoading(false);
      }
    }, 300);
    return () => {
      stale = true;
      window.clearTimeout(timer);
    };
  }, [activeTab, loading, projectScheduleSubject, projectScheduleBody, t, uiError]);

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
      });
      toast({ title: t("form.saved"), description: t("a11yUi.templatesUpdated") });
    } catch (error) {
      toast({
        title: t("common.error"),
        description: uiError(error),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleReset = async () => {
    await loadTemplates();
    toast({ title: t("a11yUi.restored"), description: t("a11yUi.templatesReloaded") });
  };

  const currentTokens =
    activeTab === "application"
      ? APPLICATION_TOKENS.map((item) => ({ ...item, desc: t(item.desc) }))
      : activeTab === "otp"
        ? OTP_TOKENS.map((item) => ({ ...item, desc: t(item.desc) }))
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
          <h1 className="text-2xl font-semibold">{t("a11yUi.emailTitle")}</h1>
          <div className="p-8 text-center text-sm text-muted-foreground">{t("common.loading")}</div>
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h1 className="text-2xl font-semibold">{t("a11yUi.emailTitle")}</h1>
          <div className={cn("flex items-center gap-2", usesCustomEditor(activeTab) && "hidden")}>
            <Button type="button" variant="outline" onClick={handleReset} disabled={saving}>
              <RotateCcw className="mr-1.5 h-4 w-4" /> {t("emailTemplate.editor.reset")}
            </Button>
            <Button type="button" onClick={handleSave} disabled={saving}>
              <Save className="mr-1.5 h-4 w-4" /> {saving ? t("common.saving") : t("common.save")}
            </Button>
          </div>
          <div
            ref={setActionsContainer}
            className={cn("flex items-center gap-2", !usesCustomEditor(activeTab) && "hidden")}
          />
        </header>

        <div className="lg:grid lg:grid-cols-[17rem_minmax(0,1fr)] lg:items-start lg:gap-8">
          {/* Phones: one picker naming the template being edited. */}
          <button
            type="button"
            data-testid="email-template-picker"
            aria-expanded={pickerOpen}
            aria-controls="email-template-list"
            onClick={() => setPickerOpen((open) => !open)}
            className="mb-4 flex w-full items-center justify-between gap-3 rounded-md border bg-card px-4 py-3 text-left lg:hidden"
          >
            <span className="min-w-0">
              <span className="block text-xs text-muted-foreground">
                {t("emailTemplate.picker.editing")}
              </span>
              <span className="block font-medium text-foreground">{t(activeTemplate.nameKey)}</span>
            </span>
            <ChevronDown
              className={cn(
                "h-4 w-4 shrink-0 text-muted-foreground transition-transform",
                pickerOpen && "rotate-180",
              )}
              aria-hidden
            />
          </button>
          <nav
            id="email-template-list"
            aria-label={t("emailTemplate.picker.listLabel")}
            className={cn(
              "mb-5 space-y-4 rounded-md border bg-card p-3 lg:sticky lg:top-20 lg:mb-0 lg:block",
              pickerOpen ? "block" : "hidden",
            )}
          >
            {TEMPLATE_GROUPS.map((group) => (
              <div key={group.labelKey} className="space-y-1">
                <h2 className="px-3 text-xs font-medium text-muted-foreground">
                  {t(group.labelKey)}
                </h2>
                <ul className="space-y-0.5">
                  {group.tabs.map(({ tab, nameKey }) => {
                    const active = activeTab === tab;
                    return (
                      <li key={tab}>
                        <button
                          type="button"
                          onClick={() => setActiveTab(tab)}
                          aria-current={active ? "true" : undefined}
                          aria-describedby={`email-template-when-${tab}`}
                          className={cn(
                            "w-full rounded-md px-3 py-2 text-left text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                            active
                              ? "bg-muted font-medium text-foreground"
                              : "text-foreground/80 hover:bg-muted/60 hover:text-foreground",
                          )}
                        >
                          {t(nameKey)}
                        </button>
                        <p id={`email-template-when-${tab}`} className="sr-only">
                          {t(`emailTemplate.when.${tab}`)}
                        </p>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}
          </nav>

          <section aria-labelledby="email-template-current" className="min-w-0 space-y-4">
            <div>
              {/* On phones the picker above already names the template. */}
              <h2 id="email-template-current" className="hidden text-lg font-semibold lg:block">
                {t(activeTemplate.nameKey)}
              </h2>
              <p className="text-sm text-muted-foreground">
                {t(`emailTemplate.when.${activeTab}`)}
              </p>
            </div>
            <div hidden={activeTab !== "cmSchedule"}>
              <CmScheduleTemplateConfig
                active={activeTab === "cmSchedule"}
                actionsContainer={actionsContainer}
              />
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
            <div hidden={activeTab !== "quoteDecision"}>
              <WorkPackageApprovalTemplateConfig
                kind="quoteDecision"
                active={activeTab === "quoteDecision"}
                actionsContainer={actionsContainer}
              />
            </div>
            {!usesCustomEditor(activeTab) && (
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
                        {t("a11yUi.notificationEmail")}
                      </Label>
                      <Input
                        id="notification-email"
                        type="email"
                        value={notificationEmail}
                        onChange={(event) => setNotificationEmail(event.target.value)}
                        placeholder="hr@nihome.vn"
                      />
                      <p className="text-xs text-muted-foreground">
                        {t("a11yUi.notificationEmailHelp")}
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
                            {email.lastAttemptAt && ` · ${formatDateTime(email.lastAttemptAt)}`}
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
          </section>
        </div>
      </div>
    </AdminLayout>
  );
};

export default EmailTemplateConfig;
