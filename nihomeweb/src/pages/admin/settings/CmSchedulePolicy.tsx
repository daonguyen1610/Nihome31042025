import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { MoneyInput } from "@/components/ui/money-input";
import { PageLoading } from "@/components/PageState";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { cmScheduleApi } from "@/services/admin/cmScheduleApi";

export default function CmSchedulePolicy() {
  const { t, lang } = useI18n();
  const uiError = useUiError();
  const { has } = usePermissions();
  const [value, setValue] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setError(null);
    void cmScheduleApi
      .policy()
      .then(({ data }) => {
        if (active) setValue(data.thresholdPercent);
      })
      .catch((cause) => {
        if (active) setError(uiError(cause));
      });
    return () => {
      active = false;
    };
  }, [revision, uiError]);
  async function save() {
    if (value === null) return;
    setBusy(true);
    setSaved(false);
    setError(null);
    try {
      const { data } = await cmScheduleApi.setPolicy(value);
      setValue(data.thresholdPercent);
      setSaved(true);
    } catch (cause) {
      setError(uiError(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="max-w-2xl space-y-4 rounded-lg border p-5">
      <h2 className="text-lg font-semibold">{t("cmSchedule.policyTitle")}</h2>
      <p className="text-sm text-muted-foreground" id="cm-policy-hint">
        {t("cmSchedule.policyHint")}
      </p>
      {error && (
        <div role="alert" className="text-destructive">
          {error}
          <Button variant="outline" onClick={() => setRevision((x) => x + 1)}>
            {t("common.retry")}
          </Button>
        </div>
      )}
      {value === null ? (
        !error && <PageLoading />
      ) : (
        <>
          <Label htmlFor="cm-delay-threshold">{t("cmSchedule.threshold")}</Label>
          <MoneyInput
            showZero
            id="cm-delay-threshold"
            value={value}
            decimalScale={4}
            formatValue={(number) => number.toLocaleString(lang, { maximumFractionDigits: 4 })}
            preferDecimalComma={lang === "vi"}
            aria-describedby="cm-policy-hint"
            disabled={busy || !has(ADMIN_PERMS.settingsManage)}
            onChange={(next) => {
              setValue(next);
              setSaved(false);
            }}
          />
          <Button
            disabled={
              busy ||
              !has(ADMIN_PERMS.settingsManage) ||
              !Number.isFinite(value) ||
              value < 0 ||
              value > 100
            }
            onClick={() => void save()}
          >
            {t("common.save")}
          </Button>
          {saved && <p role="status">{t("cmSchedule.policySaved")}</p>}
        </>
      )}
    </section>
  );
}
