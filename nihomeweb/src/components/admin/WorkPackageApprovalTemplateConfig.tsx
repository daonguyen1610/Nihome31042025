import { useEffect, useState } from "react";
import { adminApi } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";
import EmailTemplateEditor, { EmailTemplateActions } from "./EmailTemplateEditor";

export default function WorkPackageApprovalTemplateConfig({
  active,
  actionsContainer,
  kind = "workPackage",
}: {
  active: boolean;
  actionsContainer: HTMLDivElement | null;
  kind?: "workPackage" | "quote";
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
    (kind === "quote"
      ? adminApi.getQuoteApprovalTemplate()
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
      const result = await (kind === "quote"
        ? adminApi.updateQuoteApprovalTemplate(data)
        : adminApi.updateWorkPackageApprovalTemplate(data));
      setData(result.data);
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
      aria-label={t(kind === "quote" ? "quotes.approval.templateTab" : "approvalEmail.tab")}
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
            id={kind === "quote" ? "quote-approval-email" : "approval-email"}
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
            tokens={(kind === "quote"
              ? [
                  "recipientName",
                  "senderName",
                  "quoteCode",
                  "quoteVersion",
                  "grandTotal",
                  "validUntil",
                  "quoteUrl",
                ]
              : [
                  "recipientName",
                  "workPackageCode",
                  "workPackageName",
                  "recordType",
                  "recordReference",
                  "senderName",
                  "workPackageUrl",
                ]
            ).map((name) => ({
              token: `{{${name}}}`,
              desc: t(`emailTemplate.editor.token.${name}`),
            }))}
            hint={t(kind === "quote" ? "quotes.approval.templateHint" : "approvalEmail.tokens")}
          />
          {saved && <p role="status">{t("form.saved")}</p>}
        </>
      )}
    </section>
  );
}
