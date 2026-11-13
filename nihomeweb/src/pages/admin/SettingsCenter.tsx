import { useConfirm } from "@/components/ui/confirmation";
import type { LucideIcon } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  ExternalLink,
  FileText,
  Gauge,
  Globe2,
  HardDrive,
  Images,
  LogOut,
  Map as MapIcon,
  RefreshCw,
  Save,
  ShieldCheck,
} from "lucide-react";
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
import CmSchedulePolicy from "./settings/CmSchedulePolicy";
import DisplaySettingsTab from "./settings/DisplaySettingsTab";
import SlideshowSettings from "./settings/SlideshowSettings";
import GoogleDriveConfigurationForm from "./settings/GoogleDriveConfigurationForm";
import SettingsSection from "./settings/SettingsSection";
import { settingsSectionId } from "./settings/settingsSectionIds";
import { PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { parseVnd } from "@/lib/numberFormat";
import { formatNumber } from "@/lib/displaySettings";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

// Every setting here persists through a real backend endpoint. Company info,
// feature-flag toggles and generic social/SEO toggles used to live here but
// were localStorage-only mocks; they were removed because they violated the
// "no dead code / no hardcode" rule.
type Tab = "general" | "drive";
type SectionKey = "display" | "cm-schedule" | "security" | "contracts" | "map" | "slideshow";
type OtpSettingsKey = keyof OtpSettingsResponse;

const tabs: { key: Tab; labelKey: string }[] = [
  { key: "general", labelKey: "settings.general.tab" },
  { key: "drive", labelKey: "settings.drive.tab" },
];

// Order follows how often each setting is touched, not the old tab order.
const sections: { key: SectionKey; labelKey: string; icon: LucideIcon }[] = [
  { key: "display", labelKey: "settings.display.tab", icon: Globe2 },
  { key: "cm-schedule", labelKey: "cmSchedule.policyTitle", icon: Gauge },
  { key: "security", labelKey: "settings.tab.security", icon: ShieldCheck },
  { key: "contracts", labelKey: "settings.contractUploads.tab", icon: FileText },
  { key: "map", labelKey: "settings.map.tab", icon: MapIcon },
  { key: "slideshow", labelKey: "set.slideshow", icon: Images },
];

const isTab = (value: string | null): value is Tab => value === "general" || value === "drive";

const isSectionKey = (value: string | null): value is SectionKey =>
  sections.some((section) => section.key === value);

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

const DRIVE_OAUTH_MESSAGE = "nicon.google-drive.oauth";

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
  <div className="flex items-start justify-between gap-6 py-4 first:pt-0 last:pb-0">
    <div className="min-w-0">
      <p className="text-sm font-medium">{label}</p>
      <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      {saving && (
        <p role="status" className="mt-1 text-xs font-medium text-primary">
          {savingLabel}
        </p>
      )}
    </div>
    <Switch
      checked={enabled}
      onCheckedChange={onToggle}
      disabled={disabled}
      aria-label={label}
      className="mt-0.5 shrink-0"
    />
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
    <SettingsSection
      sectionKey="security"
      icon={ShieldCheck}
      title={t("settings.tab.security")}
      description={t("settings.security.desc")}
    >
      {otpLoading ? (
        <PageLoading />
      ) : otpLoadFailed || !otpSettings ? (
        <PageError message={t("set.otp.loadError")} onRetry={loadOtpSettings} />
      ) : (
        <div className="max-w-2xl divide-y">
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
    </SettingsSection>
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
    <SettingsSection
      sectionKey="map"
      icon={MapIcon}
      title={t("settings.map.title")}
      description={t("settings.map.urlHint")}
    >
      {loading ? (
        <PageLoading />
      ) : (
        <div className="space-y-6">
          <div className="max-w-2xl space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="map-embed-url">{t("settings.map.url")}</Label>
              <Input
                id="map-embed-url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://www.google.com/maps/embed?pb=..."
                className="h-9"
              />
            </div>
            <Button onClick={save} disabled={saving || url === savedUrl}>
              <Save className="mr-1.5 h-4 w-4" /> {saving ? t("common.saving") : t("common.save")}
            </Button>
          </div>

          <div className="border-t pt-5">
            <h3 className="text-sm font-medium">{t("settings.map.preview")}</h3>
            <div className="mt-3">
              {previewUrl ? (
                <iframe
                  key={previewUrl}
                  src={previewUrl}
                  title={t("settings.map.preview")}
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
            </div>
          </div>
        </div>
      )}
    </SettingsSection>
  );
};

const DriveTab = () => {
  const confirmAction = useConfirm();
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [searchParams, setSearchParams] = useSearchParams();
  const [status, setStatus] = useState<GoogleDriveAdminStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [connecting, setConnecting] = useState(false);
  const [disconnecting, setDisconnecting] = useState(false);
  const [configurationVersion, setConfigurationVersion] = useState(0);
  const oauthPopup = useRef<Window | null>(null);
  const popupPoll = useRef<number | null>(null);
  const statusRequestId = useRef(0);
  const canManage = has(ADMIN_PERMS.settingsManage);

  const loadStatus = useCallback(async () => {
    const requestId = ++statusRequestId.current;
    setLoading(true);
    try {
      const { data } = await adminApi.getGoogleDriveAdminStatus();
      if (requestId === statusRequestId.current) setStatus(data);
    } catch {
      if (requestId === statusRequestId.current) {
        setStatus(null);
        toast({
          title: t("common.error"),
          description: t("settings.drive.loadError"),
          variant: "destructive",
        });
      }
    } finally {
      if (requestId === statusRequestId.current) setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    void loadStatus();
  }, [loadStatus]);

  const showOAuthResult = useCallback(
    (result: string) => {
      const successful = result === "success";
      toast({
        title: t(successful ? "settings.drive.oauth.success" : "settings.drive.oauth.error"),
        description: successful
          ? undefined
          : t(DRIVE_OAUTH_RESULT_KEYS[result] ?? "settings.drive.oauth.failed"),
        variant: successful ? "default" : "destructive",
      });
      setConnecting(false);
      if (successful) setConfigurationVersion((value) => value + 1);
      void loadStatus();
    },
    [loadStatus, t, toast],
  );

  useEffect(() => {
    const result = searchParams.get("driveOAuth");
    if (!result) return;
    if (window.opener && window.opener !== window) {
      window.opener.postMessage({ type: DRIVE_OAUTH_MESSAGE, result }, window.location.origin);
      window.close();
      return;
    }
    showOAuthResult(result);
    const next = new URLSearchParams(searchParams);
    next.delete("driveOAuth");
    next.set("tab", "drive");
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams, showOAuthResult]);

  useEffect(() => {
    const onOAuthMessage = (event: MessageEvent) => {
      if (event.origin !== window.location.origin || event.source !== oauthPopup.current) return;
      if (
        !event.data ||
        event.data.type !== DRIVE_OAUTH_MESSAGE ||
        typeof event.data.result !== "string"
      )
        return;
      if (popupPoll.current !== null) window.clearInterval(popupPoll.current);
      popupPoll.current = null;
      oauthPopup.current?.close();
      oauthPopup.current = null;
      showOAuthResult(event.data.result);
    };
    window.addEventListener("message", onOAuthMessage);
    return () => {
      window.removeEventListener("message", onOAuthMessage);
      if (popupPoll.current !== null) window.clearInterval(popupPoll.current);
      oauthPopup.current?.close();
    };
  }, [showOAuthResult]);

  const connect = async () => {
    const switchingAccount = status?.hasStoredCredential === true;
    const width = 560;
    const height = 720;
    const left = Math.max(0, window.screenX + (window.outerWidth - width) / 2);
    const top = Math.max(0, window.screenY + (window.outerHeight - height) / 2);
    const openPopup = () =>
      window.open(
        "about:blank",
        "nicon-google-drive-oauth",
        `popup=yes,width=${width},height=${height},left=${left},top=${top}`,
      );
    // Open in the confirmation button's click event so popup blockers retain
    // the user's activation even though the continuation awaits the dialog.
    const confirmedPopup: { value: Window | null } = { value: null };
    if (
      switchingAccount &&
      !(await confirmAction(t("settings.drive.switchConfirm"), {
        destructive: false,
        onConfirm: () => {
          confirmedPopup.value = openPopup();
        },
      }))
    )
      return;
    const popup = switchingAccount ? confirmedPopup.value : openPopup();
    if (!popup) {
      toast({
        title: t("common.error"),
        description: t("settings.drive.popupBlocked"),
        variant: "destructive",
      });
      return;
    }
    oauthPopup.current?.close();
    oauthPopup.current = popup;
    setConnecting(true);
    try {
      if (switchingAccount) {
        const { data: disconnectResult } = await adminApi.disconnectGoogleDriveOAuth();
        setConfigurationVersion((value) => value + 1);
        if (disconnectResult.hadStoredCredential && !disconnectResult.providerRevoked) {
          toast({
            title: t("settings.drive.disconnectSuccess"),
            description: t("settings.drive.disconnectRevokeWarning"),
          });
        }
      }
      const { data } = await adminApi.startGoogleDriveOAuth();
      const authorizationUrl = new URL(data.authorizationUrl);
      if (
        authorizationUrl.protocol !== "https:" ||
        authorizationUrl.hostname !== "accounts.google.com"
      )
        throw new Error("Unexpected OAuth authorization URL");
      popup.location.assign(authorizationUrl.toString());
      popup.focus();
      popupPoll.current = window.setInterval(() => {
        if (!popup.closed) return;
        if (popupPoll.current !== null) window.clearInterval(popupPoll.current);
        popupPoll.current = null;
        oauthPopup.current = null;
        setConnecting(false);
        void loadStatus();
        toast({ description: t("settings.drive.popupClosed") });
      }, 500);
    } catch {
      popup.close();
      oauthPopup.current = null;
      setConnecting(false);
      if (switchingAccount) void loadStatus();
      toast({
        title: t("common.error"),
        description: t("settings.drive.startError"),
        variant: "destructive",
      });
    }
  };

  const disconnect = async () => {
    if (!(await confirmAction(t("settings.drive.disconnectConfirm"), { destructive: false })))
      return;
    setDisconnecting(true);
    try {
      const { data } = await adminApi.disconnectGoogleDriveOAuth();
      toast({
        title: t("settings.drive.disconnectSuccess"),
        description:
          data.hadStoredCredential && !data.providerRevoked
            ? t("settings.drive.disconnectRevokeWarning")
            : undefined,
      });
      setConfigurationVersion((value) => value + 1);
      await loadStatus();
    } catch {
      await loadStatus();
      toast({
        title: t("common.error"),
        description: t("settings.drive.disconnectError"),
        variant: "destructive",
      });
    } finally {
      setDisconnecting(false);
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
              <p className="mt-1 text-sm text-muted-foreground">
                {t("settings.drive.description")}
              </p>
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
                <p className="text-xs text-muted-foreground">
                  {t("settings.drive.connectionStatus")}
                </p>
                <p className="mt-1 font-medium">{t(DRIVE_STATUS_KEYS[status.status])}</p>
              </div>
              <div className="rounded-lg border bg-muted/30 p-4">
                <p className="text-xs text-muted-foreground">{t("settings.drive.account")}</p>
                <p className="mt-1 break-all font-medium">
                  {status.accountEmail || t("settings.drive.notAvailable")}
                </p>
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
                    <a href={status.rootFolderLink} target="_blank" rel="noreferrer noopener">
                      <ExternalLink className="mr-1.5 h-4 w-4" />
                      {t("settings.drive.openFolder")}
                    </a>
                  </Button>
                )}
              </div>
            )}

            {canManage ? (
              <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
                <Button
                  type="button"
                  onClick={connect}
                  disabled={connecting || disconnecting || !status.oauthConfigured}
                >
                  <HardDrive className="mr-1.5 h-4 w-4" />
                  {connecting
                    ? t("settings.drive.connecting")
                    : t(
                        status.hasStoredCredential
                          ? "settings.drive.switchAccount"
                          : "settings.drive.connect",
                      )}
                </Button>
                {status.hasStoredCredential && (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={disconnect}
                    disabled={connecting || disconnecting}
                  >
                    <LogOut className="mr-1.5 h-4 w-4" />
                    {disconnecting
                      ? t("settings.drive.disconnecting")
                      : t("settings.drive.disconnect")}
                  </Button>
                )}
                {!status.oauthConfigured && (
                  <p className="text-xs text-destructive">
                    {t("settings.drive.oauthNotConfigured")}
                  </p>
                )}
              </div>
            ) : (
              <p className="text-xs text-muted-foreground">
                {t("settings.drive.managePermissionRequired")}
              </p>
            )}
          </div>
        )}
      </section>
    </div>
  );
};

/* ─── Settings Center ─── */
const ContractUploadTab = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [value, setValue] = useState("");
  const [savedValue, setSavedValue] = useState("");
  const [maxAllowed, setMaxAllowed] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const canManage = has(ADMIN_PERMS.settingsManage);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getContractUploadLimit();
      setValue(formatNumber(data.maxFileSizeMb, lang));
      setSavedValue(formatNumber(data.maxFileSizeMb, lang));
      setMaxAllowed(data.maxAllowedFileSizeMb);
    } catch {
      setError(t("settings.contractUploads.loadError"));
    } finally {
      setLoading(false);
    }
  }, [t, lang]);

  useEffect(() => {
    void load();
  }, [load]);

  const save = async () => {
    const parsed = /^(?:\d+|\d{1,3}(?:[.,]\d{3})+)$/.test(value.trim()) ? parseVnd(value) : NaN;
    if (maxAllowed === null || !Number.isSafeInteger(parsed) || parsed < 1 || parsed > maxAllowed) {
      setError(
        t("settings.contractUploads.invalidRange", { max: formatNumber(maxAllowed ?? 0, lang) }),
      );
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const { data } = await adminApi.updateContractUploadLimit(parsed);
      setValue(formatNumber(data.maxFileSizeMb, lang));
      setSavedValue(formatNumber(data.maxFileSizeMb, lang));
      setMaxAllowed(data.maxAllowedFileSizeMb);
      toast({ title: t("settings.saved") });
    } catch {
      setError(t("settings.contractUploads.saveError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <SettingsSection
      sectionKey="contracts"
      icon={FileText}
      title={t("settings.contractUploads.title")}
      description={t("settings.contractUploads.description")}
    >
      {loading ? (
        <PageLoading />
      ) : (
        <div className="max-w-2xl space-y-3">
          <Label htmlFor="contract-upload-limit">{t("settings.contractUploads.label")}</Label>
          <Input
            id="contract-upload-limit"
            inputMode="numeric"
            value={value}
            disabled={!canManage || saving || maxAllowed === null}
            onChange={(event) => setValue(event.target.value)}
            onBlur={() => {
              if (/^\d+$/.test(value) && Number.isSafeInteger(Number(value))) {
                setValue(formatNumber(Number(value), lang));
              }
            }}
            className="max-w-40"
          />
          {maxAllowed !== null ? (
            <p className="text-sm text-muted-foreground">
              {t("settings.contractUploads.maximum", { max: formatNumber(maxAllowed, lang) })}
            </p>
          ) : null}
          {error ? (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          ) : null}
          <div className="flex flex-wrap gap-2">
            {canManage ? (
              <Button disabled={saving || value === savedValue} onClick={() => void save()}>
                {t("common.save")}
              </Button>
            ) : null}
            {error ? (
              <Button variant="outline" onClick={() => void load()}>
                {t("common.retry")}
              </Button>
            ) : null}
          </div>
        </div>
      )}
    </SettingsSection>
  );
};

const SettingsCenter = () => {
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const rawTab = searchParams.get("tab");
  // Older links named the tab after the section they opened. Keep them working
  // by folding that tab into the general sheet and remembering the section.
  const legacySection = isSectionKey(rawTab) ? rawTab : null;
  const requestedSection = searchParams.get("section");
  const targetSection = isSectionKey(requestedSection) ? requestedSection : legacySection;
  const activeTab: Tab = rawTab === "drive" ? "drive" : "general";
  const [activeSection, setActiveSection] = useState<SectionKey>(targetSection ?? "display");

  useEffect(() => {
    if (!legacySection) return;
    const next = new URLSearchParams(searchParams);
    next.set("tab", "general");
    next.set("section", legacySection);
    setSearchParams(next, { replace: true });
  }, [legacySection, searchParams, setSearchParams]);

  useEffect(() => {
    if (activeTab !== "general" || !targetSection) return;
    const target = document.getElementById(settingsSectionId(targetSection));
    if (!target) return;
    setActiveSection(targetSection);

    // Sections above the target fill in as their settings load, pushing it
    // down; keep it anchored while the sheet changes size, and hand scrolling
    // back the moment the user takes over.
    let cancelled = false;
    let timer: number | undefined;
    const align = () => {
      if (!cancelled) target.scrollIntoView({ block: "start", behavior: "instant" });
    };
    const scheduleAlign = () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(align, 50);
    };
    const sheet = target.parentElement;
    const resizeObserver = sheet ? new ResizeObserver(scheduleAlign) : null;
    if (sheet) resizeObserver?.observe(sheet);
    const stop = () => {
      cancelled = true;
      resizeObserver?.disconnect();
      window.clearTimeout(timer);
    };
    for (const event of ["wheel", "touchstart", "keydown"] as const)
      window.addEventListener(event, stop, { passive: true, once: true });
    const giveUp = window.setTimeout(stop, 6000);
    align();
    return () => {
      cancelled = true;
      window.clearTimeout(giveUp);
      window.clearTimeout(timer);
      resizeObserver?.disconnect();
      for (const event of ["wheel", "touchstart", "keydown"] as const)
        window.removeEventListener(event, stop);
    };
  }, [activeTab, targetSection]);

  useEffect(() => {
    if (activeTab !== "general") return;
    let frame = 0;
    // The nav marks the last section whose heading has passed under the sticky
    // admin header, so the highlight always names what is being read.
    const update = () => {
      frame = 0;
      let current: SectionKey = sections[0].key;
      for (const { key } of sections) {
        const element = document.getElementById(settingsSectionId(key));
        if (element && element.getBoundingClientRect().top <= 104) current = key;
      }
      setActiveSection(current);
    };
    const requestUpdate = () => {
      if (frame) return;
      frame = window.requestAnimationFrame(update);
    };
    update();
    window.addEventListener("scroll", requestUpdate, { passive: true });
    window.addEventListener("resize", requestUpdate);
    return () => {
      window.removeEventListener("scroll", requestUpdate);
      window.removeEventListener("resize", requestUpdate);
      if (frame) window.cancelAnimationFrame(frame);
    };
  }, [activeTab]);

  const goToSection = (key: SectionKey) => {
    setActiveSection(key);
    document
      .getElementById(settingsSectionId(key))
      ?.scrollIntoView({ block: "start", behavior: "instant" });
    const next = new URLSearchParams(searchParams);
    next.set("tab", "general");
    next.set("section", key);
    setSearchParams(next, { replace: true });
  };

  const onChangeTab = (value: string) => {
    if (!isTab(value)) return;
    setSearchParams(value === "drive" ? { tab: "drive" } : {}, { replace: true });
    if (value === "general") setActiveSection("display");
    window.scrollTo({ top: 0 });
  };

  return (
    <AdminLayout>
      <div className="mx-auto w-full max-w-6xl space-y-5">
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

        {activeTab === "general" ? (
          <div className="grid gap-5 lg:grid-cols-[13rem_minmax(0,1fr)] lg:items-start lg:gap-8">
            <aside className="min-w-0 lg:self-start">
              <nav aria-label={t("settings.sections")}>
                <ul className="flex gap-1 overflow-x-auto pb-1 lg:flex-col lg:gap-0.5 lg:overflow-visible lg:pb-0">
                  {sections.map(({ key, labelKey, icon: Icon }) => {
                    const active = activeSection === key;
                    return (
                      <li key={key} className="shrink-0 lg:w-full">
                        <button
                          type="button"
                          onClick={() => goToSection(key)}
                          aria-current={active ? "location" : undefined}
                          className={cn(
                            "flex w-full items-center gap-2.5 whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                            active
                              ? "bg-primary/10 font-medium text-primary"
                              : "text-muted-foreground hover:bg-muted hover:text-foreground",
                          )}
                        >
                          <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                          {t(labelKey)}
                        </button>
                      </li>
                    );
                  })}
                </ul>
              </nav>
            </aside>

            <div className="min-w-0 rounded-xl border bg-card">
              <DisplaySettingsTab />
              <CmSchedulePolicy />
              <SecurityTab />
              <ContractUploadTab />
              <MapTab />
              <SlideshowSettings />
            </div>
          </div>
        ) : (
          <DriveTab />
        )}
      </div>
    </AdminLayout>
  );
};

export default SettingsCenter;
