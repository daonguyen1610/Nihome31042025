import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ExternalLink, HardDrive, Map as MapIcon, RefreshCw, Save, ShieldCheck } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  type GoogleDriveAdminStatusResponse,
  type OtpSettingsResponse,
} from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import SlideshowSettings from "./settings/SlideshowSettings";
import GoogleDriveConfigurationForm from "./settings/GoogleDriveConfigurationForm";
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
type Tab = "security" | "slideshow" | "map" | "drive";
type OtpSettingsKey = keyof OtpSettingsResponse;

const tabs: { key: Tab; labelKey: string }[] = [
  { key: "security", labelKey: "settings.tab.security" },
  { key: "slideshow", labelKey: "set.slideshow" },
  { key: "map", labelKey: "settings.map.tab" },
  { key: "drive", labelKey: "settings.drive.tab" },
];

const DRIVE_STATUS_KEYS: Record<GoogleDriveAdminStatusResponse["status"], string> = {
  Disabled: "settings.drive.status.disabled",
  Connected: "settings.drive.status.connected",
  ReadOnly: "settings.drive.status.readOnly",
  InvalidRoot: "settings.drive.status.invalidRoot",
  ReconnectRequired: "settings.drive.status.reconnectRequired",
  Unavailable: "settings.drive.status.unavailable",
};

const DRIVE_ERROR_KEYS: Partial<Record<GoogleDriveAdminStatusResponse["status"], string>> = {
  InvalidRoot: "settings.drive.error.invalidRoot",
  ReconnectRequired: "settings.drive.error.reconnectRequired",
  Unavailable: "settings.drive.error.unavailable",
};

const DRIVE_OAUTH_RESULT_KEYS: Record<string, string> = {
  denied: "settings.drive.oauth.denied",
  invalid_state: "settings.drive.oauth.invalid_state",
  authorization_expired: "settings.drive.oauth.authorization_expired",
  missing_refresh_token: "settings.drive.oauth.missing_refresh_token",
  root_validation_failed: "settings.drive.oauth.root_validation_failed",
  configuration_changed: "settings.drive.oauth.configuration_changed",
  failed: "settings.drive.oauth.failed",
};

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

const DriveTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();
  const [status, setStatus] = useState<GoogleDriveAdminStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [connecting, setConnecting] = useState(false);
  const [configurationVersion, setConfigurationVersion] = useState(0);
  const canManage = has(ADMIN_PERMS.settingsManage);

  const loadStatus = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await adminApi.getGoogleDriveAdminStatus();
      setStatus(data);
    } catch {
      setStatus(null);
      toast({
        title: t("common.error"),
        description: t("settings.drive.loadError"),
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    void loadStatus();
  }, [loadStatus]);

  useEffect(() => {
    const result = searchParams.get("driveOAuth");
    if (!result) return;
    const successful = result === "success";
    toast({
      title: t(successful ? "settings.drive.oauth.success" : "settings.drive.oauth.error"),
      description: successful
        ? undefined
        : t(DRIVE_OAUTH_RESULT_KEYS[result] ?? "settings.drive.oauth.failed"),
      variant: successful ? "default" : "destructive",
    });
    const next = new URLSearchParams(searchParams);
    next.delete("driveOAuth");
    next.set("tab", "drive");
    setSearchParams(next, { replace: true });
    if (successful) void loadStatus();
  }, [loadStatus, searchParams, setSearchParams, t, toast]);

  const connect = async () => {
    setConnecting(true);
    try {
      const { data } = await adminApi.startGoogleDriveOAuth();
      const authorizationUrl = new URL(data.authorizationUrl);
      if (authorizationUrl.protocol !== "https:" || authorizationUrl.hostname !== "accounts.google.com")
        throw new Error("Unexpected OAuth authorization URL");
      window.location.assign(authorizationUrl.toString());
    } catch {
      setConnecting(false);
      toast({
        title: t("common.error"),
        description: t("settings.drive.startError"),
        variant: "destructive",
      });
    }
  };

  return (
    <div className="space-y-4">
      <GoogleDriveConfigurationForm
        canManage={canManage}
        reloadKey={configurationVersion}
        onSaved={() => {
          setConfigurationVersion((value) => value + 1);
          void loadStatus();
        }}
      />
      <section className="rounded-lg border bg-card p-4 sm:p-6">
      <div className="flex flex-col gap-4 border-b pb-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex items-start gap-3">
          <HardDrive className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <div>
            <h2 className="text-lg font-semibold">{t("settings.drive.title")}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{t("settings.drive.description")}</p>
          </div>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={loadStatus} disabled={loading}>
          <RefreshCw className={`mr-1.5 h-4 w-4 ${loading ? "animate-spin" : ""}`} />
          {t("settings.drive.check")}
        </Button>
      </div>

      {loading && !status ? (
        <p className="pt-5 text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : !status ? (
        <p className="pt-5 text-sm text-destructive">{t("settings.drive.loadError")}</p>
      ) : (
        <div className="space-y-5 pt-5">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-lg border bg-muted/30 p-4">
              <p className="text-xs text-muted-foreground">{t("settings.drive.connectionStatus")}</p>
              <p className="mt-1 font-medium">{t(DRIVE_STATUS_KEYS[status.status])}</p>
            </div>
            <div className="rounded-lg border bg-muted/30 p-4">
              <p className="text-xs text-muted-foreground">{t("settings.drive.account")}</p>
              <p className="mt-1 break-all font-medium">{status.accountEmail || t("settings.drive.notAvailable")}</p>
            </div>
          </div>

          {DRIVE_ERROR_KEYS[status.status] && (
            <div className="rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
              {t(DRIVE_ERROR_KEYS[status.status] as string)}
            </div>
          )}

          {status.rootFolderName && (
            <div className="flex flex-col gap-2 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-xs text-muted-foreground">{t("settings.drive.rootFolder")}</p>
                <p className="mt-1 font-medium">{status.rootFolderName}</p>
              </div>
              {status.rootFolderLink && (
                <Button asChild variant="ghost" size="sm">
                  <a href={status.rootFolderLink} target="_blank" rel="noreferrer">
                    <ExternalLink className="mr-1.5 h-4 w-4" />
                    {t("settings.drive.openFolder")}
                  </a>
                </Button>
              )}
            </div>
          )}

          {canManage ? (
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
              <Button type="button" onClick={connect} disabled={connecting || !status.oauthConfigured}>
                <HardDrive className="mr-1.5 h-4 w-4" />
                {connecting ? t("settings.drive.connecting") : t("settings.drive.reconnect")}
              </Button>
              {!status.oauthConfigured && (
                <p className="text-xs text-destructive">{t("settings.drive.oauthNotConfigured")}</p>
              )}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">{t("settings.drive.managePermissionRequired")}</p>
          )}
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
        {activeTab === "drive" && <DriveTab />}
      </div>
    </AdminLayout>
  );
};

export default SettingsCenter;
