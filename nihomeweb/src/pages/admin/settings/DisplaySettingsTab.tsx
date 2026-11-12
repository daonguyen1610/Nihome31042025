import { useCallback, useEffect, useMemo, useState } from "react";
import { Clock, Globe2, RefreshCw, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { adminApi } from "@/services/adminApi";
import type {
  DisplaySettingsResponse,
  TimeZoneOption,
  UpdateDisplaySettingsRequest,
} from "@/services/admin/types/content";
import { applyDisplaySettings, localeFor } from "@/lib/displaySettings";

const CURRENCIES: { value: DisplaySettingsResponse["currency"]; labelKey: string }[] = [
  { value: "VND", labelKey: "settings.display.currencyVnd" },
  { value: "USD", labelKey: "settings.display.currencyUsd" },
];

const DisplaySettingsTab = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.settingsManage);
  const [saved, setSaved] = useState<DisplaySettingsResponse | null>(null);
  const [timeZoneId, setTimeZoneId] = useState("");
  const [currency, setCurrency] = useState<DisplaySettingsResponse["currency"]>("VND");
  const [timeZones, setTimeZones] = useState<TimeZoneOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [failed, setFailed] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setFailed(false);
    try {
      const [display, zones] = await Promise.all([
        adminApi.getDisplaySettings(),
        adminApi.getTimeZones(),
      ]);
      setSaved(display.data);
      setTimeZoneId(display.data.timeZoneId);
      setCurrency(display.data.currency);
      setTimeZones(zones.data);
    } catch {
      setFailed(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const options = useMemo(
    () => timeZones.map((zone) => ({ value: zone.id, label: zone.label })),
    [timeZones],
  );

  const dirty = saved !== null && (timeZoneId !== saved.timeZoneId || currency !== saved.currency);

  const preview = useMemo(() => {
    try {
      return new Intl.DateTimeFormat(localeFor(lang), {
        dateStyle: "medium",
        timeStyle: "short",
        timeZone: timeZoneId || undefined,
      }).format(new Date());
    } catch {
      return null;
    }
  }, [lang, timeZoneId]);

  const save = async () => {
    if (!dirty) return;
    setSaving(true);
    try {
      const payload: UpdateDisplaySettingsRequest = { timeZoneId, currency };
      const { data } = await adminApi.updateDisplaySettings(payload);
      setSaved(data);
      setTimeZoneId(data.timeZoneId);
      setCurrency(data.currency);
      applyDisplaySettings(data);
      toast({ title: t("settings.saved") });
    } catch {
      toast({
        title: t("common.error"),
        description: t("settings.display.saveError"),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="max-w-2xl rounded-lg border bg-card p-6">
      <div className="flex items-center gap-2 border-b pb-4">
        <Globe2 className="h-5 w-5 text-primary" />
        <div>
          <h2 className="text-lg font-semibold">{t("settings.display.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t("settings.display.description")}</p>
        </div>
      </div>

      {loading ? (
        <p className="pt-5 text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : failed ? (
        <div className="flex flex-col gap-3 pt-5 sm:flex-row sm:items-center">
          <p className="text-sm text-muted-foreground">{t("settings.display.loadError")}</p>
          <Button type="button" size="sm" variant="outline" onClick={load}>
            <RefreshCw className="mr-1.5 h-4 w-4" />
            {t("common.retry")}
          </Button>
        </div>
      ) : (
        <div className="space-y-6 pt-5">
          <div className="space-y-2">
            <Label htmlFor="display-time-zone">{t("settings.display.timeZone")}</Label>
            <SearchableSelect
              id="display-time-zone"
              value={timeZoneId}
              onChange={setTimeZoneId}
              options={options}
              placeholder={t("settings.display.timeZonePlaceholder")}
              searchPlaceholder={t("settings.display.timeZoneSearch")}
              emptyText={t("settings.display.timeZoneEmpty")}
              disabled={!canManage || saving}
              aria-describedby="display-time-zone-hint"
            />
            <p id="display-time-zone-hint" className="text-xs text-muted-foreground">
              {t("settings.display.timeZoneHint")}
            </p>
            {preview ? (
              <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <Clock className="h-3.5 w-3.5" />
                {t("settings.display.preview", { value: preview })}
              </p>
            ) : null}
          </div>

          <fieldset className="space-y-2" disabled={!canManage || saving}>
            <legend className="text-sm font-medium">{t("settings.display.currency")}</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              {CURRENCIES.map((option) => (
                <label
                  key={option.value}
                  className={
                    "flex cursor-pointer items-start gap-3 rounded-lg border p-4 transition-colors " +
                    (currency === option.value
                      ? "border-primary bg-primary/5"
                      : "hover:bg-muted/40")
                  }
                >
                  <input
                    type="radio"
                    name="display-currency"
                    value={option.value}
                    checked={currency === option.value}
                    onChange={() => setCurrency(option.value)}
                    className="mt-1 h-4 w-4 accent-primary"
                  />
                  <span>
                    <span className="block text-sm font-medium">{t(option.labelKey)}</span>
                    <span className="mt-0.5 block text-xs text-muted-foreground">
                      {t(
                        option.value === "VND"
                          ? "settings.display.currencyVndHint"
                          : "settings.display.currencyUsdHint",
                      )}
                    </span>
                  </span>
                </label>
              ))}
            </div>
            <p className="text-xs text-muted-foreground">
              {t("settings.display.currencyHint", { currency: currency.toUpperCase() })}
            </p>
          </fieldset>

          <div className="flex flex-wrap items-center gap-3 border-t pt-4">
            {canManage ? (
              <Button onClick={() => void save()} disabled={saving || !dirty}>
                <Save className="mr-1.5 h-4 w-4" />
                {saving ? t("common.saving") : t("common.save")}
              </Button>
            ) : (
              <p className="text-xs text-muted-foreground">
                {t("settings.display.managePermissionRequired")}
              </p>
            )}
            {dirty ? (
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  if (!saved) return;
                  setTimeZoneId(saved.timeZoneId);
                  setCurrency(saved.currency);
                }}
                disabled={saving}
              >
                {t("common.cancel")}
              </Button>
            ) : null}
          </div>
        </div>
      )}
    </section>
  );
};

export default DisplaySettingsTab;
