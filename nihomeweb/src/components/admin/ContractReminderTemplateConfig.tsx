import { useEffect, useState } from "react";
import { adminApi, type ContractReminderTemplates } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";
import EmailTemplateEditor, { EmailTemplateActions } from "./EmailTemplateEditor";

export default function ContractReminderTemplateConfig({
  active,
  actionsContainer,
}: {
  active: boolean;
  actionsContainer: HTMLDivElement | null;
}) {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [data, setData] = useState<ContractReminderTemplates | null>(null);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [kind, setKind] = useState<"renewal" | "end">("renewal");
  useEffect(() => {
    let active = true;
    setLoading(true);
    adminApi
      .getContractReminderTemplates()
      .then(({ data: result }) => {
        if (active) {
          setData(result);
          setError("");
          setSaved(false);
        }
      })
      .catch((reason) => {
        if (active) setError(extractApiError(reason));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [reload]);

  const save = async () => {
    if (!data) return;
    if (
      Object.values(data).some((value) => !value.trim()) ||
      [data.renewalBody, data.endBody].some(
        (value) =>
          !value.toLowerCase().includes("{{contractnumber}}") ||
          !value.toLowerCase().includes("{{enddate}}"),
      )
    ) {
      setError(t("contractReminder.templateRule"));
      return;
    }
    setBusy(true);
    setError("");
    setSaved(false);
    try {
      const result = await adminApi.updateContractReminderTemplates(data);
      setData(result.data);
      setSaved(true);
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="min-w-0 space-y-4" aria-label={t("contractReminder.templates")}>
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
        <Button variant="outline" onClick={() => setReload((value) => value + 1)}>
          {t("common.retry")}
        </Button>
      ) : (
        <>
          <div
            className="flex flex-wrap gap-2"
            role="group"
            aria-label={t("contractReminder.kind")}
          >
            {(["renewal", "end"] as const).map((value) => (
              <Button
                key={value}
                type="button"
                variant={kind === value ? "default" : "outline"}
                aria-pressed={kind === value}
                onClick={() => setKind(value)}
              >
                {t(`contractReminder.${value === "renewal" ? "Renewal" : "End"}`)}
              </Button>
            ))}
          </div>
          <EmailTemplateEditor
            key={kind}
            id={kind}
            previewTitle={`${t("contractReminder.preview")} ${t(`contractReminder.${kind === "renewal" ? "Renewal" : "End"}`)}`}
            subject={data[`${kind}Subject`]}
            body={data[`${kind}Body`]}
            bodyMaxLength={8000}
            disabled={busy || !has("system.settings.manage")}
            onSubjectChange={(value) => {
              setData({ ...data, [`${kind}Subject`]: value });
              setSaved(false);
            }}
            onBodyChange={(value) => {
              setData({ ...data, [`${kind}Body`]: value });
              setSaved(false);
            }}
            tokens={["customerName", "contractNumber", "endDate"].map((name) => ({
              token: `{{${name}}}`,
              desc: t(`emailTemplate.editor.token.${name}`),
            }))}
            hint={t("contractReminder.tokens")}
          />
          {saved && <p role="status">{t("contractReminder.saved")}</p>}
        </>
      )}
    </section>
  );
}
