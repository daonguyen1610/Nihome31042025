import { useEffect, useState } from "react";
import { adminApi } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";
import EmailTemplateEditor, { EmailTemplateActions } from "./EmailTemplateEditor";

type ApprovalTemplateKind = "workPackage" | "quote" | "quoteDecision";

const token = (name: string, descKey = `emailTemplate.editor.token.${name}`) => ({ name, descKey });

const TOKENS: Record<ApprovalTemplateKind, { name: string; descKey: string }[]> = {
  workPackage: [
    "recipientName",
    "workPackageCode",
    "workPackageName",
    "recordType",
    "recordReference",
    "senderName",
    "workPackageUrl",
  ].map((name) => token(name)),
  quote: [
    "recipientName",
    "senderName",
    "quoteCode",
    "quoteVersion",
    "grandTotal",
    "validUntil",
    "quoteUrl",
  ].map((name) => token(name)),
  quoteDecision: [
    token("recipientName", "quotes.approval.token.authorName"),
    token("approverName"),
    token("quoteCode"),
    token("quoteVersion"),
    token("grandTotal"),
    token("validUntil"),
    token("decisionLabel"),
    token("decisionNote"),
    token("quoteUrl"),
  ],
};

function pickTemplate(kind: ApprovalTemplateKind, templates: adminApi.QuoteApprovalTemplates) {
  return kind === "quoteDecision"
    ? { subject: templates.decisionSubject, body: templates.decisionBody }
    : { subject: templates.subject, body: templates.body };
}

export default function WorkPackageApprovalTemplateConfig({
  active,
  actionsContainer,
  kind = "workPackage",
}: {
  active: boolean;
  actionsContainer: HTMLDivElement | null;
  kind?: ApprovalTemplateKind;
}) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [data, setData] = useState<{ subject: string; body: string } | null>(null);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let active = true;
    setLoading(true);
    (kind === "quote" || kind === "quoteDecision"
      ? adminApi
          .getQuoteApprovalTemplate()
          .then(({ data: result }) => ({ data: pickTemplate(kind, result) }))
      : adminApi.getWorkPackageApprovalTemplate()
    )
      .then(({ data: result }) => {
        if (active) {
          setData(result);
          setError("");
          setSaved(false);
        }
      })
      .catch((cause) => {
        if (active) setError(extractApiError(cause));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [reload, kind]);
  const save = async () => {
    if (!data) return;
    setBusy(true);
    setError("");
    setSaved(false);
    try {
      const result =
        kind === "quote" || kind === "quoteDecision"
          ? await adminApi.updateQuoteApprovalTemplate(
              kind === "quoteDecision"
                ? { decisionSubject: data.subject, decisionBody: data.body }
                : { subject: data.subject, body: data.body },
            )
          : await adminApi.updateWorkPackageApprovalTemplate(data);
      setData(pickTemplate(kind, result.data));
      setSaved(true);
    } catch (cause) {
      setError(extractApiError(cause));
    } finally {
      setBusy(false);
    }
  };
  return (
    <section
      className="min-w-0 space-y-4"
      aria-label={t(
        kind === "quote"
          ? "quotes.approval.templateTab"
          : kind === "quoteDecision"
            ? "quotes.approval.decisionTemplateTab"
            : "approvalEmail.tab",
      )}
    >
      {active && (
        <EmailTemplateActions
          container={actionsContainer}
          busy={busy || loading}
          saving={busy}
          canSave={!!data && has("system.settings.manage")}
          onReset={() => setReload((value) => value + 1)}
          onSave={save}
        />
      )}
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {loading ? (
        <p role="status" className="p-8 text-center text-sm text-muted-foreground">
          {t("common.loading")}
        </p>
      ) : !data ? (
        <Button onClick={() => setReload((value) => value + 1)}>{t("common.retry")}</Button>
      ) : (
        <>
          <EmailTemplateEditor
            id={
              kind === "quote"
                ? "quote-approval-email"
                : kind === "quoteDecision"
                  ? "quote-approval-decision-email"
                  : "approval-email"
            }
            previewTitle={t("approvalEmail.templatePreview")}
            subject={data.subject}
            body={data.body}
            bodyMaxLength={8000}
            disabled={busy || !has("system.settings.manage")}
            onSubjectChange={(subject) => {
              setData({ ...data, subject });
              setSaved(false);
            }}
            onBodyChange={(body) => {
              setData({ ...data, body });
              setSaved(false);
            }}
            tokens={TOKENS[kind].map(({ name, descKey }) => ({
              token: `{{${name}}}`,
              desc: t(descKey),
            }))}
            hint={t(
              kind === "quote"
                ? "quotes.approval.templateHint"
                : kind === "quoteDecision"
                  ? "quotes.approval.decisionTemplateHint"
                  : "approvalEmail.tokens",
            )}
          />
          {saved && <p role="status">{t("form.saved")}</p>}
        </>
      )}
    </section>
  );
}
