import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Save, Building2, Mail, Phone, MapPin, Globe } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
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

type Tab = "company" | "general" | "media" | "slideshow";

const tabs: { key: Tab; labelKey: string }[] = [
  { key: "company", labelKey: "settings.company" },
  { key: "general", labelKey: "set.general" },
  { key: "media", labelKey: "set.media" },
  { key: "slideshow", labelKey: "set.slideshow" },
];

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
    <div className="grid grid-cols-1 xl:grid-cols-2 gap-5">
      <div className="admin-card p-7">
        <h2 className="font-display text-lg font-extrabold mb-1">{t("settings.company")}</h2>
        <p className="text-xs mb-6" style={{ color: "hsl(var(--admin-muted))" }}>{t("settings.companyDesc")}</p>
        <div className="space-y-4">
          {fields.map((f) => (
            <div key={f.key}>
              <label className="text-xs uppercase tracking-wider font-bold mb-2 block" style={{ color: "hsl(var(--admin-muted))" }}>
                {f.label}
              </label>
              <div
                className="flex items-center gap-3 rounded-xl px-4 py-3 border"
                style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
              >
                <f.icon className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
                <input
                  value={company[f.key]}
                  onChange={(e) => setCompany({ ...company, [f.key]: e.target.value })}
                  className="bg-transparent text-sm outline-none flex-1 font-semibold"
                />
              </div>
            </div>
          ))}
          <button onClick={save} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm mt-3">
            <Save className="w-4 h-4" /> {t("common.save")}
          </button>
        </div>
      </div>

      <div className="admin-card p-7">
        <h2 className="font-display text-lg font-extrabold mb-1">{t("settings.system")}</h2>
        <p className="text-xs mb-6" style={{ color: "hsl(var(--admin-muted))" }}>{t("settings.systemDesc")}</p>
        <div className="space-y-3">
          {featureList.map((f) => (
            <div key={f.key} className="flex items-center justify-between gap-4 p-4 rounded-2xl" style={{ background: "hsl(var(--admin-bg))" }}>
              <div className="min-w-0">
                <p className="font-bold text-sm">{f.label}</p>
                <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>{f.desc}</p>
              </div>
              <Toggle on={features[f.key]} onChange={(v) => setFeatures({ ...features, [f.key]: v })} />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

/* ─── General Tab ─── */
const GeneralTab = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [s, setS] = useState<GeneralSettings>(() => getGeneralSettings());
  const upd = <K extends keyof GeneralSettings>(k: K, v: GeneralSettings[K]) =>
    setS((prev) => ({ ...prev, [k]: v }));
  const save = () => { saveGeneralSettings(s); toast({ title: t("settings.saved") }); };

  return (
    <div className="space-y-5">
      <div className="flex justify-end">
        <button onClick={save} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Save className="w-4 h-4" /> {t("common.save")}
        </button>
      </div>

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
    <div className="space-y-5">
      <div className="flex justify-end">
        <button onClick={save} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Save className="w-4 h-4" /> {t("common.save")}
        </button>
      </div>

      <SettingSection title={t("set.section.common")}>
        <SettingRow label={t("set.imageStorage")}>
          <div className="flex items-center gap-3">
            <span className="text-sm font-semibold">{s.storage}</span>
            <button
              type="button"
              onClick={() => upd("storage", s.storage === "database" ? "filesystem" : "database")}
              className="px-3 py-2 rounded-lg text-xs font-bold text-white"
              style={{ background: "hsl(var(--admin-primary))" }}
            >
              {t("common.change")}
            </button>
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

  const onChangeTab = (tab: Tab) => {
    setActiveTab(tab);
    setSearchParams({ tab });
  };

  return (
    <AdminLayout>
      <div className="mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("settings.title")}</h1>
        <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>{t("settings.centerDesc")}</p>
      </div>

      {/* Tabs */}
      <div
        className="flex gap-1 p-1 rounded-xl mb-6 overflow-x-auto"
        style={{ background: "hsl(var(--admin-bg))" }}
      >
        {tabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => onChangeTab(tab.key)}
            className="px-5 py-2.5 rounded-lg text-sm font-bold transition whitespace-nowrap"
            style={
              activeTab === tab.key
                ? { background: "hsl(var(--admin-primary))", color: "white" }
                : { color: "hsl(var(--admin-sidebar-text))" }
            }
          >
            {t(tab.labelKey)}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === "company" && <CompanyTab />}
      {activeTab === "general" && <GeneralTab />}
      {activeTab === "media" && <MediaTab />}
      {activeTab === "slideshow" && <SlideshowSettings />}
    </AdminLayout>
  );
};

export default SettingsCenter;
