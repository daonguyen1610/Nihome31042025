import { useEffect, useState } from "react";
import { adminApi, type ContractReminderTemplates } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { usePermissions } from "@/hooks/usePermissions";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

export default function ContractReminderTemplateConfig() {
  const { t } = useI18n();
  const { has } = usePermissions();
  const [data, setData] = useState<ContractReminderTemplates | null>(null);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    let active = true;
    adminApi
      .getContractReminderTemplates()
      .then(({ data: result }) => {
        if (active) {
          setData(result);
          setError("");
        }
      })
      .catch((reason) => {
        if (active) setError(extractApiError(reason));
      });
    return () => {
      active = false;
    };
  }, [reload]);
  return (
    <section className="space-y-4 rounded-lg border p-4">
      <h2 className="text-lg font-semibold">{t("contractReminder.templates")}</h2>
      <p className="text-sm text-muted-foreground">{t("contractReminder.tokens")}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {!data ? (
        <Button variant="outline" onClick={() => setReload((value) => value + 1)}>
          {t("common.retry")}
        </Button>
      ) : (
        <>
          {(["renewal", "end"] as const).map((kind) => (
            <div key={kind} className="space-y-2">
              <h3 className="font-medium">
                {t(`contractReminder.${kind === "renewal" ? "Renewal" : "End"}`)}
              </h3>
              <Label htmlFor={`${kind}-subject`}>{t("contractReminder.subject")}</Label>
              <Input
                id={`${kind}-subject`}
                maxLength={200}
                disabled={!has("system.settings.manage")}
                value={data[`${kind}Subject`]}
                onChange={(e) => {
                  setData({ ...data, [`${kind}Subject`]: e.target.value });
                  setSaved(false);
                }}
              />
              <Label htmlFor={`${kind}-body`}>{t("contractReminder.body")}</Label>
              <Textarea
                id={`${kind}-body`}
                rows={8}
                maxLength={8000}
                disabled={!has("system.settings.manage")}
                value={data[`${kind}Body`]}
                onChange={(e) => {
                  setData({ ...data, [`${kind}Body`]: e.target.value });
                  setSaved(false);
                }}
              />
              <iframe
                title={`${t("contractReminder.preview")} ${kind}`}
                srcDoc={data[`${kind}Body`]}
                sandbox=""
                className="h-56 w-full rounded border bg-white"
              />
            </div>
          ))}
          <Button
            disabled={busy || !has("system.settings.manage")}
            onClick={async () => {
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
            }}
          >
            {t("common.save")}
          </Button>
          {saved && <p role="status">{t("contractReminder.saved")}</p>}
        </>
      )}
    </section>
  );
}
