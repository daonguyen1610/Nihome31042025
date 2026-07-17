import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Save, ShieldCheck, Map as MapIcon } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, type OtpSettingsResponse } from "@/services/adminApi";
import SlideshowSettings from "./settings/SlideshowSettings";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

// Tabs are limited to features that actually persist through a real backend
// endpoint. Company info, feature-flag toggles, generic social/SEO/security
// settings and thumbnail media settings used to live here but were
// localStorage-only mocks; they were removed because they violated the
// "no dead code / no hardcode" rule.
type Tab = "security" | "slideshow" | "map";
type OtpSettingsKey = keyof OtpSettingsResponse;

const tabs: { key: Tab; labelKey: string }[] = [
  { key: "security", labelKey: "settings.tab.security" },
  { key: "slideshow", labelKey: "set.slideshow" },
  { key: "map", labelKey: "settings.map.tab" },
];

const OtpToggleControl = ({
  label,
  description,
  enabled,
  disabled,
  saving,
  savingLabel,
  onToggle,
}: {
  label: string;
  description: string;
  enabled: boolean;
  disabled: boolean;
  saving: boolean;
  savingLabel: string;
  onToggle: (value: boolean) => void;
}) => (
  <div className="flex items-start gap-4 rounded-lg border bg-muted/40 px-4 py-4">
    <div className="pt-0.5">
      <Switch
        checked={enabled}
        onCheckedChange={onToggle}
        disabled={disabled}
        aria-label={label}
      />
    </div>
    <div className="min-w-0">
      <p className="text-sm font-medium">{label}</p>
      <p className="mt-1 text-xs text-muted-foreground">{description}</p>
      {saving && (
        <p className="mt-2 text-xs font-medium text-primary">{savingLabel}</p>
      )}
    </div>
  </div>
);

/* ─── Security tab: OTP toggles (the only real feature that was in
   the old "General" tab). Everything else on the old tab was
   localStorage-only. ─── */
const SecurityTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [otpSettings, setOtpSettings] = useState<OtpSettingsResponse | null>(null);
  const [otpLoading, setOtpLoading] = useState(true);
  const [otpLoadFailed, setOtpLoadFailed] = useState(false);
  const [otpSavingKey, setOtpSavingKey] = useState<OtpSettingsKey | null>(null);

  const loadOtpSettings = useCallback(async () => {
    setOtpLoading(true);
    setOtpLoadFailed(false);
    try {
      const { data } = await adminApi.getOtpSettings();
      setOtpSettings(data);
    } catch {
      setOtpLoadFailed(true);
      setOtpSettings(null);
    } finally {
      setOtpLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadOtpSettings();
  }, [loadOtpSettings]);

  const updateOtpSetting = async (key: OtpSettingsKey, value: boolean) => {
    if (!otpSettings || otpSavingKey) return;

    const previous = otpSettings;
    const next = { ...previous, [key]: value };

    setOtpSettings(next);
    setOtpSavingKey(key);
    setOtpLoadFailed(false);

    try {
      const { data } = await adminApi.updateOtpSettings(next);
      setOtpSettings(data);
      toast({ title: t("settings.saved") });
    } catch {
      setOtpSettings(previous);
      toast({
        title: t("common.error"),
        description: t("set.otp.saveError"),
        variant: "destructive",
      });
    } finally {
      setOtpSavingKey(null);
    }
  };

  return (
    <section className="rounded-lg border bg-card p-6">
      <div className="flex items-center gap-2 border-b pb-4">
        <ShieldCheck className="h-5 w-5 text-primary" />
        <h2 className="text-lg font-semibold">{t("set.otp.securityTitle")}</h2>
      </div>

      {otpLoading ? (
        <p className="pt-5 text-sm text-muted-foreground">{t("set.otp.loading")}</p>
      ) : otpLoadFailed || !otpSettings ? (
        <div className="flex flex-col gap-3 pt-5 sm:flex-row sm:items-center">
          <p className="text-sm text-muted-foreground">{t("set.otp.loadError")}</p>
          <Button type="button" size="sm" variant="outline" onClick={loadOtpSettings}>
            {t("common.retry")}
          </Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 pt-5 xl:grid-cols-2">
          <OtpToggleControl
            label={t("set.otp.registrationLabel")}
            description={t("set.otp.registrationDesc")}
            enabled={otpSettings.enableOtpForRegistration}
            disabled={otpSavingKey !== null}
            saving={otpSavingKey === "enableOtpForRegistration"}
            savingLabel={t("set.otp.saving")}
            onToggle={(value) => updateOtpSetting("enableOtpForRegistration", value)}
          />
          <OtpToggleControl
            label={t("set.otp.forgotLabel")}
            description={t("set.otp.forgotDesc")}
            enabled={otpSettings.enableOtpForForgotPassword}
            disabled={otpSavingKey !== null}
            saving={otpSavingKey === "enableOtpForForgotPassword"}
            savingLabel={t("set.otp.saving")}
            onToggle={(value) => updateOtpSetting("enableOtpForForgotPassword", value)}
          />
        </div>
      )}
    </section>
  );
};

/* ─── Map tab: Google Maps embed URL (real backend). ─── */
const MapTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [url, setUrl] = useState<string>("");
  const [savedUrl, setSavedUrl] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMapEmbed();
        if (cancelled) return;
        const value = data.mapEmbedUrl ?? "";
        setUrl(value);
        setSavedUrl(value);
      } catch {
        if (!cancelled) {
          toast({
            title: t("common.error"),
            description: t("settings.map.loadError"),
            variant: "destructive",
          });
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [toast, t]);

  const save = async () => {
    const trimmed = url.trim();
    setSaving(true);
    try {
      const { data } = await adminApi.updateMapEmbed({ mapEmbedUrl: trimmed ? trimmed : null });
      const value = data.mapEmbedUrl ?? "";
      setUrl(value);
      setSavedUrl(value);
      toast({ title: t("settings.map.saved") });
    } catch {
      toast({
        title: t("common.error"),
        description: t("settings.map.saveError"),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const previewUrl = savedUrl || url.trim();

  return (
    <div className="space-y-4">
      <section className="rounded-lg border bg-card p-6">
        <div className="mb-5 flex items-center gap-3 border-b pb-4">
          <MapIcon className="h-5 w-5 text-primary" />
          <div>
            <h2 className="text-lg font-semibold">{t("settings.map.title")}</h2>
            <p className="mt-1 text-xs text-muted-foreground">{t("settings.map.urlHint")}</p>
          </div>
        </div>

        {loading ? (
          <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
        ) : (
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="map-embed-url" className="text-xs">
                {t("settings.map.url")}
              </Label>
              <Input
                id="map-embed-url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://www.google.com/maps/embed?pb=..."
                className="h-9"
              />
            </div>
            <div className="flex items-center gap-3">
              <Button onClick={save} disabled={saving || url === savedUrl}>
                <Save className="mr-1.5 h-4 w-4" /> {saving ? t("common.saving") : t("common.save")}
              </Button>
            </div>
          </div>
        )}
      </section>

      <section className="rounded-lg border bg-card p-6">
        <h3 className="mb-4 text-base font-semibold">{t("settings.map.preview")}</h3>
        {previewUrl ? (
          <iframe
            key={previewUrl}
            src={previewUrl}
            title="Map preview"
            className="h-96 w-full rounded-lg border"
            loading="lazy"
            referrerPolicy="no-referrer-when-downgrade"
            allowFullScreen
          />
        ) : (
          <div className="flex h-96 w-full items-center justify-center rounded-lg border border-dashed bg-muted/40 text-sm text-muted-foreground">
            {t("settings.map.previewEmpty")}
          </div>
        )}
      </section>
    </div>
  );
};

/* ─── Settings Center ─── */
const SettingsCenter = () => {
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const currentTab = searchParams.get("tab");
  const initialTab = tabs.some((tab) => tab.key === currentTab)
    ? (currentTab as Tab)
    : "security";
  const [activeTab, setActiveTab] = useState<Tab>(initialTab);

  useEffect(() => {
    if (tabs.some((tab) => tab.key === currentTab)) {
      setActiveTab(currentTab as Tab);
    }
  }, [currentTab]);

  const onChangeTab = (tab: string) => {
    setActiveTab(tab as Tab);
    setSearchParams({ tab });
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header>
          <h1 className="text-2xl font-semibold">{t("settings.title")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t("settings.centerDesc")}</p>
        </header>

        <Tabs value={activeTab} onValueChange={onChangeTab} className="w-full">
          <TabsList className="w-full justify-start overflow-x-auto sm:w-auto">
            {tabs.map((tab) => (
              <TabsTrigger key={tab.key} value={tab.key} className="whitespace-nowrap">
                {t(tab.labelKey)}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>

        {activeTab === "security" && <SecurityTab />}
        {activeTab === "slideshow" && <SlideshowSettings />}
        {activeTab === "map" && <MapTab />}
      </div>
    </AdminLayout>
  );
};

export default SettingsCenter;
