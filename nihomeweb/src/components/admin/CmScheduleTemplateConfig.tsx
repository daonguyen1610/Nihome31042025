import { useEffect, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { usePermissions } from "@/hooks/usePermissions";
import { cmScheduleApi, type CmEmailTemplate } from "@/services/admin/cmScheduleApi";
import EmailTemplateEditor, { EmailTemplateActions } from "./EmailTemplateEditor";
import { Button } from "@/components/ui/button";

export default function CmScheduleTemplateConfig({
  active,
  actionsContainer,
}: {
  active: boolean;
  actionsContainer: HTMLDivElement | null;
}) {
  const { t } = useI18n();
  const uiError = useUiError();
  const { has } = usePermissions();
  const [data, setData] = useState<CmEmailTemplate | null>(null);
  const [preview, setPreview] = useState<CmEmailTemplate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let current = true;
    setError(null);
    void cmScheduleApi
      .template()
      .then(({ data }) => {
        if (current) setData(data);
      })
      .catch((cause) => {
        if (current) setError(uiError(cause));
      });
    return () => {
      current = false;
    };
  }, [revision, uiError]);
  useEffect(() => {
    if (!active || !data || !has("system.settings.manage")) return;
    let current = true;
    setPreview(null);
    setPreviewError(null);
    const timer = setTimeout(() => {
      void cmScheduleApi
        .templatePreview(data)
        .then(({ data }) => {
          if (current) setPreview(data);
        })
        .catch((cause) => {
          if (current) setPreviewError(uiError(cause));
        });
    }, 300);
    return () => {
      current = false;
      clearTimeout(timer);
    };
  }, [active, data, has, uiError]);
  async function save() {
    if (!data) return;
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      const result = await cmScheduleApi.setTemplate(data);
      setData(result.data);
      setSaved(true);
    } catch (cause) {
      setError(uiError(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="min-w-0 space-y-4">
      {active && (
        <EmailTemplateActions
          container={actionsContainer}
          busy={busy || !data}
          saving={busy}
          canSave={!!data && has("system.settings.manage")}
          onReset={() => setRevision((x) => x + 1)}
          onSave={() => void save()}
        />
      )}
      {error && (
        <p role="alert" className="text-destructive">
          {error}
          <Button onClick={() => setRevision((x) => x + 1)}>{t("common.retry")}</Button>
        </p>
      )}
      {data && (
        <EmailTemplateEditor
          id="cm-schedule-email"
          subject={data.subject}
          body={data.body}
          onSubjectChange={(subject) => {
            setData({ ...data, subject });
            setSaved(false);
          }}
          onBodyChange={(body) => {
            setData({ ...data, body });
            setSaved(false);
          }}
          disabled={busy || !has("system.settings.manage")}
          subjectMaxLength={200}
          bodyMaxLength={8000}
          tokens={[
            "eventName",
            "recipientName",
            "projectCode",
            "projectName",
            "version",
            "changeReason",
            "changeSummary",
          ].map((name) => ({
            token: `{{${name}}}`,
            desc: t(`emailTemplate.schedule.token.${name}`),
          }))}
          renderedPreview={preview}
          previewError={previewError}
          previewLoading={has("system.settings.manage") && !preview && !previewError}
        />
      )}
      {saved && <p role="status">{t("cmSchedule.templateSaved")}</p>}
    </section>
  );
}
