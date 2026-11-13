import { useEffect, useState } from "react";
import { Gauge } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { MoneyInput } from "@/components/ui/money-input";
import { PageError, PageLoading } from "@/components/PageState";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { cmScheduleApi } from "@/services/admin/cmScheduleApi";
import SettingsSection from "./SettingsSection";
import { settingsSectionDescriptionId } from "./settingsSectionIds";

export default function CmSchedulePolicy() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const uiError = useUiError();
  const { has } = usePermissions();
  const [value, setValue] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
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
    setError(null);
    try {
      const { data } = await cmScheduleApi.setPolicy(value);
      setValue(data.thresholdPercent);
      toast({ title: t("cmSchedule.policySaved") });
    } catch (cause) {
      setError(uiError(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <SettingsSection
      sectionKey="cm-schedule"
      icon={Gauge}
      title={t("cmSchedule.policyTitle")}
      description={t("cmSchedule.policyHint")}
    >
      {error ? (
        <PageError message={error} onRetry={() => setRevision((x) => x + 1)} />
      ) : value === null ? (
        <PageLoading />
      ) : (
        <div className="max-w-2xl space-y-4">
          <div className="max-w-40 space-y-1.5">
            <Label htmlFor="cm-delay-threshold">{t("cmSchedule.threshold")}</Label>
            <MoneyInput
              showZero
              id="cm-delay-threshold"
              value={value}
              decimalScale={4}
              formatValue={(number) => number.toLocaleString(lang, { maximumFractionDigits: 4 })}
              preferDecimalComma={lang === "vi"}
              aria-describedby={settingsSectionDescriptionId("cm-schedule")}
              disabled={busy || !has(ADMIN_PERMS.settingsManage)}
              onChange={setValue}
            />
          </div>
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
        </div>
      )}
    </SettingsSection>
  );
}
