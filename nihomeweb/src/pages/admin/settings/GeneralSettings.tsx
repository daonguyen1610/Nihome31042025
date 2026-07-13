import { useState } from "react";
import { Save } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  SettingSection,
  SettingRow,
  TextInput,
  SelectInput,
  Toggle,
  TextArea,
} from "@/components/admin/SettingsControls";
import {
  getGeneralSettings,
  saveGeneralSettings,
  type GeneralSettings,
} from "@/lib/settingsStore";
import { Button } from "@/components/ui/button";

const GeneralSettingsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [s, setS] = useState<GeneralSettings>(() => getGeneralSettings());
  const upd = <K extends keyof GeneralSettings>(k: K, v: GeneralSettings[K]) =>
    setS((prev) => ({ ...prev, [k]: v }));

  const save = () => {
    saveGeneralSettings(s);
    toast({ title: t("settings.saved") });
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">{t("set.general")}</h1>
          <Button onClick={save}>
            <Save className="mr-1.5 h-4 w-4" /> {t("common.save")}
          </Button>
        </header>

        <div className="space-y-4">
        <SettingSection title={t("set.section.social")}>
          <SettingRow label="Facebook page URL">
            <TextInput value={s.facebook} onChange={(e) => upd("facebook", e.target.value)} />
          </SettingRow>
          <SettingRow label="Twitter page URL">
            <TextInput value={s.twitter} onChange={(e) => upd("twitter", e.target.value)} />
          </SettingRow>
          <SettingRow label="YouTube channel URL">
            <TextInput value={s.youtube} onChange={(e) => upd("youtube", e.target.value)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.sitemap")}>
          <SettingRow label="Sitemap enabled">
            <Toggle on={s.sitemapEnabled} onChange={(v) => upd("sitemapEnabled", v)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.seo")}>
          <SettingRow label="Default page title">
            <TextInput value={s.defaultPageTitle} onChange={(e) => upd("defaultPageTitle", e.target.value)} />
          </SettingRow>
          <SettingRow label="Page title separator">
            <TextInput value={s.pageTitleSeparator} onChange={(e) => upd("pageTitleSeparator", e.target.value)} />
          </SettingRow>
          <SettingRow label="Default meta keywords">
            <TextInput value={s.defaultMetaKeywords} onChange={(e) => upd("defaultMetaKeywords", e.target.value)} />
          </SettingRow>
          <SettingRow label="Default meta description">
            <TextInput value={s.defaultMetaDescription} onChange={(e) => upd("defaultMetaDescription", e.target.value)} />
          </SettingRow>
          <SettingRow label="JavaScript bundling and minification">
            <Toggle on={s.jsBundling} onChange={(v) => upd("jsBundling", v)} />
          </SettingRow>
          <SettingRow label="CSS bundling and minification">
            <Toggle on={s.cssBundling} onChange={(v) => upd("cssBundling", v)} />
          </SettingRow>
          <SettingRow label="WWW prefix requirement">
            <SelectInput
              value={s.wwwPrefix}
              onChange={(e) => upd("wwwPrefix", e.target.value as GeneralSettings["wwwPrefix"])}
              options={[
                { value: "DoesntMatter", label: "Doesn't matter" },
                { value: "WithoutWww", label: "Without www" },
                { value: "WithWww", label: "With www" },
              ]}
            />
          </SettingRow>
          <SettingRow label="Convert non-western chars">
            <Toggle on={s.convertNonWestern} onChange={(v) => upd("convertNonWestern", v)} />
          </SettingRow>
          <SettingRow label="Enable canonical URLs">
            <Toggle on={s.enableCanonical} onChange={(v) => upd("enableCanonical", v)} />
          </SettingRow>
          <SettingRow label="Twitter META tags">
            <Toggle on={s.twitterMeta} onChange={(v) => upd("twitterMeta", v)} />
          </SettingRow>
          <SettingRow label="Open Graph META tags">
            <Toggle on={s.openGraphMeta} onChange={(v) => upd("openGraphMeta", v)} />
          </SettingRow>
          <SettingRow label="Custom <head> tag">
            <TextArea value={s.customHead} onChange={(e) => upd("customHead", e.target.value)} />
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.security")}>
          <SettingRow label="Admin area allowed IP">
            <TextInput value={s.adminAllowedIp} onChange={(e) => upd("adminAllowedIp", e.target.value)} />
          </SettingRow>
          <SettingRow label="Force SSL for all site pages">
            <Toggle on={s.forceSsl} onChange={(v) => upd("forceSsl", v)} />
          </SettingRow>
          <SettingRow label="Enable XSRF protection for admin area">
            <Toggle on={s.xsrfAdmin} onChange={(v) => upd("xsrfAdmin", v)} />
          </SettingRow>
          <SettingRow label="Enable XSRF protection for public store">
            <Toggle on={s.xsrfPublic} onChange={(v) => upd("xsrfPublic", v)} />
          </SettingRow>
          <SettingRow label="Enable honeypot">
            <Toggle on={s.honeypot} onChange={(v) => upd("honeypot", v)} />
          </SettingRow>
          <SettingRow label="Encryption private key">
            <div className="flex gap-2">
              <TextInput value={s.encryptionKey} onChange={(e) => upd("encryptionKey", e.target.value)} />
              <Button
                type="button"
                size="sm"
                className="shrink-0"
                onClick={() =>
                  upd("encryptionKey", Math.random().toString().slice(2, 18))
                }
              >
                Change
              </Button>
            </div>
          </SettingRow>
        </SettingSection>

        <SettingSection title={t("set.section.captcha")}>
          <SettingRow label="CAPTCHA enabled">
            <Toggle on={s.captchaEnabled} onChange={(v) => upd("captchaEnabled", v)} />
          </SettingRow>
        </SettingSection>
        </div>
      </div>
    </AdminLayout>
  );
};

export default GeneralSettingsPage;
