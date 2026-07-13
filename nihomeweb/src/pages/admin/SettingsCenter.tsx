import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Save, Building2, Mail, Phone, MapPin, Globe, ShieldCheck, Map as MapIcon } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, type OtpSettingsResponse } from "@/services/adminApi";
import {
  SettingSection,
  SettingRow,
  TextInput,
  NumberInput,
  SelectInput,
  Toggle,
  TextArea,
} from "@/components/admin/SettingsControls";
import {
  getGeneralSettings,
  saveGeneralSettings,
  getMediaSettings,
  saveMediaSettings,
  type GeneralSettings,
  type MediaSettings,
} from "@/lib/settingsStore";
import SlideshowSettings from "./settings/SlideshowSettings";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

type Tab = "company" | "general" | "media" | "slideshow" | "map";
type OtpSettingsKey = keyof OtpSettingsResponse;

const tabs: { key: Tab; labelKey: string }[] = [
  { key: "company", labelKey: "settings.company" },
  { key: "general", labelKey: "set.general" },
  { key: "media", labelKey: "set.media" },
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
      <Toggle on={enabled} onChange={onToggle} disabled={disabled} ariaLabel={label} />
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

/* ─── Company Tab ─── */
const CompanyTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [company, setCompany] = useState({
    name: "Công ty NICON",
    email: "info@nicon.vn",
    phone: "+84 28 7300 1234",
    address: "Đường Mai Chí Thọ, Thủ Đức, TP.HCM",
    website: "https://nicon.vn",
  });
  const [features, setFeatures] = useState({
    recruitment: true,
    multilang: true,
    chat: false,
    analytics: true,
  });

  const save = () => toast({ title: t("settings.saved") });

  const fields = [
    { key: "name", label: t("settings.companyName"), icon: Building2 },
    { key: "email", label: "Email", icon: Mail as typeof Building2 },
    { key: "phone", label: t("settings.phone"), icon: Phone },
    { key: "address", label: t("settings.address"), icon: MapPin },
    { key: "website", label: "Website", icon: Globe },
  ] as const;

  const featureList = [
    { key: "recruitment", label: t("settings.feat.recruitment"), desc: t("settings.feat.recruitmentDesc") },
    { key: "multilang", label: t("settings.feat.multilang"), desc: t("settings.feat.multilangDesc") },
    { key: "chat", label: t("settings.feat.chat"), desc: t("settings.feat.chatDesc") },
    { key: "analytics", label: t("settings.feat.analytics"), desc: t("settings.feat.analyticsDesc") },
  ] as const;

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
      <section className="rounded-lg border bg-card p-6">
        <h2 className="text-lg font-semibold">{t("settings.company")}</h2>
        <p className="mb-6 mt-1 text-xs text-muted-foreground">{t("settings.companyDesc")}</p>
        <div className="space-y-4">
          {fields.map((f) => (
            <div key={f.key} className="space-y-1.5">
              <Label htmlFor={`company-${f.key}`} className="text-xs">
                {f.label}
              </Label>
              <div className="relative">
                <f.icon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id={`company-${f.key}`}
                  value={company[f.key]}
                  onChange={(e) => setCompany({ ...company, [f.key]: e.target.value })}
                  className="h-9 pl-9"
                />
              </div>
            </div>
          ))}
          <Button onClick={save} className="mt-2">
            <Save className="mr-1.5 h-4 w-4" /> {t("common.save")}
          </Button>
        </div>
      </section>

      <section className="rounded-lg border bg-card p-6">
        <h2 className="text-lg font-semibold">{t("settings.system")}</h2>
        <p className="mb-6 mt-1 text-xs text-muted-foreground">{t("settings.systemDesc")}</p>
        <div className="space-y-3">
          {featureList.map((f) => (
            <div key={f.key} className="flex items-center justify-between gap-4 rounded-lg border bg-muted/40 p-4">
              <div className="min-w-0">
                <p className="text-sm font-medium">{f.label}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">{f.desc}</p>
              </div>
              <Toggle on={features[f.key]} onChange={(v) => setFeatures({ ...features, [f.key]: v })} />
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};

/* ─── General Tab ─── */
const GeneralTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [s, setS] = useState<GeneralSettings>(() => getGeneralSettings());
  const [otpSettings, setOtpSettings] = useState<OtpSettingsResponse | null>(null);
  const [otpLoading, setOtpLoading] = useState(true);
  const [otpLoadFailed, setOtpLoadFailed] = useState(false);
  const [otpSavingKey, setOtpSavingKey] = useState<OtpSettingsKey | null>(null);
  const upd = <K extends keyof GeneralSettings>(k: K, v: GeneralSettings[K]) =>
    setS((prev) => ({ ...prev, [k]: v }));
  const save = () => { saveGeneralSettings(s); toast({ title: t("settings.saved") }); };

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
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button onClick={save}>
          <Save className="mr-1.5 h-4 w-4" /> {t("common.save")}
        </Button>
      </div>

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

      <SettingSection title={t("set.section.social")}>
        <SettingRow label="Facebook URL">
          <TextInput value={s.facebook} onChange={(e) => upd("facebook", e.target.value)} />
        </SettingRow>
        <SettingRow label="Twitter URL">
          <TextInput value={s.twitter} onChange={(e) => upd("twitter", e.target.value)} />
        </SettingRow>
        <SettingRow label="YouTube URL">
          <TextInput value={s.youtube} onChange={(e) => upd("youtube", e.target.value)} />
        </SettingRow>
      </SettingSection>

      <SettingSection title={t("set.section.seo")}>
        <SettingRow label={t("set.defaultPageTitle")}>
          <TextInput value={s.defaultPageTitle} onChange={(e) => upd("defaultPageTitle", e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.pageTitleSeparator")}>
          <TextInput value={s.pageTitleSeparator} onChange={(e) => upd("pageTitleSeparator", e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.metaKeywords")}>
          <TextInput value={s.defaultMetaKeywords} onChange={(e) => upd("defaultMetaKeywords", e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.metaDescription")}>
          <TextInput value={s.defaultMetaDescription} onChange={(e) => upd("defaultMetaDescription", e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.sitemapEnabled")}>
          <Toggle on={s.sitemapEnabled} onChange={(v) => upd("sitemapEnabled", v)} />
        </SettingRow>
        <SettingRow label={t("set.canonicalUrls")}>
          <Toggle on={s.enableCanonical} onChange={(v) => upd("enableCanonical", v)} />
        </SettingRow>
        <SettingRow label="Twitter META">
          <Toggle on={s.twitterMeta} onChange={(v) => upd("twitterMeta", v)} />
        </SettingRow>
        <SettingRow label="Open Graph META">
          <Toggle on={s.openGraphMeta} onChange={(v) => upd("openGraphMeta", v)} />
        </SettingRow>
        <SettingRow label={t("set.wwwPrefix")}>
          <SelectInput
            value={s.wwwPrefix}
            onChange={(e) => upd("wwwPrefix", e.target.value as GeneralSettings["wwwPrefix"])}
            options={[
              { value: "DoesntMatter", label: t("set.www.doesntMatter") },
              { value: "WithoutWww", label: t("set.www.without") },
              { value: "WithWww", label: t("set.www.with") },
            ]}
          />
        </SettingRow>
        <SettingRow label={t("set.customHead")}>
          <TextArea value={s.customHead} onChange={(e) => upd("customHead", e.target.value)} />
        </SettingRow>
      </SettingSection>

      <SettingSection title={t("set.section.security")}>
        <SettingRow label={t("set.adminAllowedIp")}>
          <TextInput value={s.adminAllowedIp} onChange={(e) => upd("adminAllowedIp", e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.forceSsl")}>
          <Toggle on={s.forceSsl} onChange={(v) => upd("forceSsl", v)} />
        </SettingRow>
        <SettingRow label={t("set.captchaEnabled")}>
          <Toggle on={s.captchaEnabled} onChange={(v) => upd("captchaEnabled", v)} />
        </SettingRow>
      </SettingSection>
    </div>
  );
};

/* ─── Media Tab ─── */
const MediaTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [s, setS] = useState<MediaSettings>(() => getMediaSettings());
  const upd = <K extends keyof MediaSettings>(k: K, v: MediaSettings[K]) =>
    setS((prev) => ({ ...prev, [k]: v }));
  const save = () => { saveMediaSettings(s); toast({ title: t("settings.saved") }); };

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button onClick={save}>
          <Save className="mr-1.5 h-4 w-4" /> {t("common.save")}
        </Button>
      </div>

      <SettingSection title={t("set.section.common")}>
        <SettingRow label={t("set.imageStorage")}>
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium">{s.storage}</span>
            <Button
              type="button"
              size="sm"
              onClick={() => upd("storage", s.storage === "database" ? "filesystem" : "database")}
            >
              {t("common.change")}
            </Button>
          </div>
        </SettingRow>
        <SettingRow label={t("set.maxImageSize")}>
          <NumberInput value={s.maxImageSize} onChange={(e) => upd("maxImageSize", +e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.imageQuality")}>
          <NumberInput value={s.defaultImageQuality} onChange={(e) => upd("defaultImageQuality", +e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.pictureZoom")}>
          <Toggle on={s.pictureZoom} onChange={(v) => upd("pictureZoom", v)} />
        </SettingRow>
      </SettingSection>

      <SettingSection title={t("set.section.thumbnails")}>
        <SettingRow label={t("set.projectThumb")}>
          <NumberInput value={s.projectThumbSize} onChange={(e) => upd("projectThumbSize", +e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.postThumb")}>
          <NumberInput value={s.postThumbSize} onChange={(e) => upd("postThumbSize", +e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.categoryThumb")}>
          <NumberInput value={s.categoryThumb} onChange={(e) => upd("categoryThumb", +e.target.value)} />
        </SettingRow>
        <SettingRow label={t("set.avatarSize")}>
          <NumberInput value={s.avatarSize} onChange={(e) => upd("avatarSize", +e.target.value)} />
        </SettingRow>
      </SettingSection>
    </div>
  );
};

/* ─── Map Tab ─── */
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
    : "company";
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

        {/* Tab content */}
        {activeTab === "company" && <CompanyTab />}
        {activeTab === "general" && <GeneralTab />}
        {activeTab === "media" && <MediaTab />}
        {activeTab === "slideshow" && <SlideshowSettings />}
        {activeTab === "map" && <MapTab />}
      </div>
    </AdminLayout>
  );
};

export default SettingsCenter;
